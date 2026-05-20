namespace PRo3D.Viewer.Ribbon

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
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
            do! SharedShaders.stableTrafo
            do! DefaultSurfaces.constantColor (C4f color)
            do! SharedShaders.noPick
        }

    /// Semitransparent blue disk + dip arrow + strike lines for a fitted DnS plane.
    /// Disk radius is controlled externally via DnSPlane.size (Shift+/-).
    ///
    /// All geometry is built in center-local space (small V3f offsets from center).
    /// The center is passed as AlignmentTranslation so the custom stableTrafo can
    /// add it back in double precision on the GPU, avoiding float32 precision loss.
    let planeSg (plane : DnSPlane) : ISg =
        let segs   = 64
        let center = plane.centerOfMass   // V3d — goes into AlignmentTranslation uniform
        let radius = plane.size
        let dip    = plane.dipDirection.Normalized
        let strike = plane.strikeDirection.Normalized
        let twoPi  = 2.0 * System.Math.PI

        // ── disk: positions relative to center (small V3f) ───────────────────
        let positions = Array.zeroCreate<V3f> (segs + 1)
        positions.[0] <- V3f.Zero   // center is the local origin
        for i in 0 .. segs - 1 do
            let theta = float i / float segs * twoPi
            positions.[i + 1] <- V3f (radius * (cos theta * strike + sin theta * dip))

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
                do! SharedShaders.stableTrafo
                do! DefaultSurfaces.constantColor (C4f(0.3f, 0.6f, 0.9f, 0.5f))
                do! SharedShaders.noPick
            }
            |> Sg.cullMode' CullMode.None
            |> Sg.blendMode' BlendMode.Blend

        // ── dip arrow: shaft + cone, all in center-local space ───────────────
        let lineLen       = radius
        let coneH         = lineLen * 0.2
        let coneR         = coneH  * 0.3
        let shaftEndLocal = dip * (lineLen - coneH)   // relative to center

        let dipLineSg = lineSg V3d.Zero (dip * lineLen - dip * coneH) C4b.Blue

        let dipConeSg =
            Sg.cone' 16 C4b.Blue coneR coneH
            |> Sg.trafo' (Trafo3d.Translation(shaftEndLocal) * Trafo3d.RotateInto(V3d.ZAxis, dip))
            |> Sg.shader {
                do! SharedShaders.stableTrafo
                do! DefaultSurfaces.constantColor C4f.Blue
                do! SharedShaders.noPick
            }

        // ── strike lines: local coords ────────────────────────────────────────
        let strikeLineSg = lineSg (-strike * lineLen) (strike * lineLen) C4b.Red

        // ── apply center as AlignmentTranslation for the whole group ──────────
        Sg.ofList [ diskSg; dipLineSg; dipConeSg; strikeLineSg ]
        |> Sg.uniform "AlignmentTranslation" (AVal.constant center)
