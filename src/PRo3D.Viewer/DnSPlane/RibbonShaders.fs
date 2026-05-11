namespace PRo3D.Viewer.Ribbon

open Aardvark.Base
open Aardvark.Rendering

/// GPU-side ribbon extrusion shader.
///
/// Each vertex stores the *center* position of a polyline endpoint together
/// with its segment's per-vertex dip vector and a ±1 side flag. The vertex
/// shader computes the final world position as:
///
///   worldPos = centerPos + side * HalfWidth * dipVec
///
/// This keeps the CPU-side mesh data completely stable when the user
/// interactively changes the extrusion length — only the HalfWidth uniform
/// has to be refreshed on the GPU.
module RibbonShaders =
    open FShade

    [<RequireQualifiedAccess>]
    type RibbonVertex =
        {
            [<Position>]            pos    : V4d
            [<Normal>]              n      : V3d
            [<Semantic("DipVec")>]  dipVec : V3d
            [<Semantic("Side")>]    side   : float
        }

    let extrude (v : RibbonVertex) =
        vertex {
            let hw : float = uniform?HalfWidth

            // Use the stable model trafo — works in eye-relative space
            // to avoid float precision loss at large coordinates
            let centerView  = uniform.ModelViewTrafo * v.pos
            let dipView     = (uniform.ModelViewTrafo * V4d(v.dipVec, 0.0)).XYZ

            let extrudedView = V4d(centerView.XYZ + v.side * hw * dipView, 1.0)
            let nView        = (uniform.ModelViewTrafo * V4d(v.n, 0.0)).XYZ |> Vec.normalize

            return { v with
                        pos = uniform.ProjTrafo * extrudedView
                        n   = nView }
        }

    let simpleLight (v : RibbonVertex) =
        fragment {
            let n        = Vec.normalize v.n
            let lightDir = Vec.normalize (V3d(1.0, 2.0, 3.0))
            let diffuse  = abs (Vec.dot n lightDir)
            let color    : V4d = uniform?Color
            return V4d(color.XYZ * (0.4 + 0.6 * diffuse), color.W)
        }
