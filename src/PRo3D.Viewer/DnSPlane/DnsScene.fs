namespace PRo3D.Viewer.Ribbon

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open PRo3D.Viewer.Shared

module DnsScene =

    /// Build a line-list ISg between two world-space points.
    let private lineSg (a : V3d) (b : V3d) (color : C4b) : ISg =
        IndexedGeometry(
            Mode = IndexedGeometryMode.LineList,
            IndexedAttributes =
                SymDict.ofList [ DefaultSemantic.Positions, [| V3f a; V3f b |] :> System.Array ]
        )
        |> Sg.ofIndexedGeometry
        |> Sg.shader {
            do! DefaultSurfaces.stableTrafo
            do! DefaultSurfaces.constantColor (C4f color)
            do! SharedShaders.noPick
        }

    /// Semitransparent blue disk + dip arrow + strike lines for a fitted DnS plane.
    /// Disk radius is controlled externally via DnSPlane.size (Shift+/-).
    let planeSg (plane : DnSPlane) : ISg =
        let segs   = 64
        let center = plane.centerOfMass
        let radius = plane.size
        let dip    = plane.dipDirection.Normalized
        let strike = plane.strikeDirection.Normalized
        let twoPi  = 2.0 * System.Math.PI

        // ── disk (triangle fan in the strike/dip plane) ───────────────────────
        let positions = Array.zeroCreate<V3f> (segs + 1)
        positions.[0] <- V3f center
        for i in 0 .. segs - 1 do
            let theta = float i / float segs * twoPi
            positions.[i + 1] <- V3f (center + radius * (cos theta * strike + sin theta * dip))

        let indices = Array.zeroCreate<int> (segs * 3)
        for i in 0 .. segs - 1 do
            indices.[i * 3 + 0] <- 0
            indices.[i * 3 + 1] <- i + 1
            indices.[i * 3 + 2] <- (i + 1) % segs + 1

        let diskSg =
            IndexedGeometry(
                Mode       = IndexedGeometryMode.TriangleList,
                IndexArray = (indices :> System.Array),
                IndexedAttributes =
                    SymDict.ofList [ DefaultSemantic.Positions, positions :> System.Array ]
            )
            |> Sg.ofIndexedGeometry
            |> Sg.shader {
                do! DefaultSurfaces.stableTrafo
                do! DefaultSurfaces.constantColor (C4f(0.3f, 0.6f, 0.9f, 0.5f))
                do! SharedShaders.noPick
            }
            |> Sg.cullMode' CullMode.None
            |> Sg.blendMode' BlendMode.Blend

        // ── dip arrow: shaft line + cone arrowhead ────────────────────────────
        let lineLen  = radius
        let coneH    = lineLen * 0.2
        let coneR    = coneH  * 0.3
        let tipPos   = center + dip * lineLen
        let shaftEnd = tipPos - dip * coneH

        let dipLineSg = lineSg center shaftEnd C4b.Blue

        let dipConeSg =
            Sg.cone' 16 C4b.Blue coneR coneH
            |> Sg.trafo' (Trafo3d.Translation(shaftEnd) * Trafo3d.RotateInto(V3d.ZAxis, dip))
            |> Sg.shader {
                do! DefaultSurfaces.stableTrafo
                do! DefaultSurfaces.constantColor C4f.Blue
                do! SharedShaders.noPick
            }

        // ── strike lines ──────────────────────────────────────────────────────
        let strikeLineSg = lineSg (center - strike * lineLen) (center + strike * lineLen) C4b.Red

        Sg.ofList [ diskSg; dipLineSg; dipConeSg; strikeLineSg ]
