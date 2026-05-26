namespace PRo3D.Viewer.Shared

open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Rendering.Effects
open FShade
open PRo3D.Viewer.Shared.RenderingConstants

/// Common shader functions shared between viewers
module SharedShaders =

    type PickBuffer = { [<Semantic("PickIds")>] id : int }

    let noPick (v : Aardvark.Rendering.Effects.Vertex) =
        fragment { return { id = -1 } }

    /// Vertex type for shader processing
    type Vertex = {
        [<Position>]      pos : V4d
        [<Normal>]        n   : V3d
        [<BiNormal>]      b   : V3d
        [<Tangent>]       t   : V3d
        [<Color>]         c   : V4d
        [<TexCoord>]      tc  : V2d
    }
    
    /// Common Level-of-Detail color shader
    /// Applies grayscale conversion when LoD visualization is enabled
    let LoDColor (v : Vertex) =
        fragment {
            if uniform?LodVisEnabled then
                let c : V4d = uniform?LoDColor
                let gamma = DEFAULT_GAMMA
                let grayscale =
                    RGB_TO_GRAYSCALE_R * v.c.X ** gamma +
                    RGB_TO_GRAYSCALE_G * v.c.Y ** gamma +
                    RGB_TO_GRAYSCALE_B * v.c.Z ** gamma
                return grayscale * c
            else
                return v.c
        }

    /// Precision-safe MVP transform.
    ///
    /// Positions in the vertex buffer must be stored relative to a reference
    /// point (small V3f offsets). The reference is passed as the V3d uniform
    /// "AlignmentTranslation" and added back in view space using double-precision
    /// arithmetic on the GPU (FShade emits dmat4/dvec4), avoiding catastrophic
    /// cancellation from large world-space coordinates.
    let stableTrafo (v : Vertex) =
        vertex {
            let vp = uniform.ModelViewTrafo * v.pos
            let translation : V3d = uniform?AlignmentTranslation
            // AlignmentTranslation is a world-space reference/offset, so add it back in
            // view space (ViewTrafo, not ModelViewTrafo) — otherwise a per-object model
            // trafo (e.g. the OPC patch trafo) would rotate it. Matches RibbonShaders.extrude.
            let tvp = uniform.ViewTrafo * V4d(translation, 0.0)
            return {
                pos = uniform.ProjTrafo * (vp + tvp)
                n   = uniform.ModelViewTrafoInv.TransposedTransformDir v.n |> Vec.normalize
                b   = uniform.ModelViewTrafo.TransformDir v.b |> Vec.normalize
                t   = uniform.ModelViewTrafo.TransformDir v.t |> Vec.normalize
                c   = v.c
                tc  = v.tc
            }
        }