namespace PRo3D.Viewer.Ribbon

open Aardvark.Base
open Aardvark.Rendering

/// GPU-side ribbon extrusion shader.
///
/// Each vertex stores the *center* position of the ribbon spine together
/// with a per-vertex dip vector and a ±1 side flag.  The vertex shader
/// computes the final world position as:
///
///   worldPos = centerPos + side * HalfWidth * dipVec
///
/// This keeps the CPU-side mesh data completely stable when the user
/// interactively changes the ribbon width — only the uniform needs updating.
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
            let hw       : float = uniform?HalfWidth
            let extruded = v.pos.XYZ + v.side * hw * v.dipVec
            return { v with pos = V4d(extruded, v.pos.W) }
        }
