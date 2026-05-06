namespace PRo3D.Viewer.Shared

open Aardvark.Base
open Aardvark.Rendering
open FShade
open PRo3D.Viewer.Shared.RenderingConstants

/// Common shader functions shared between viewers
module SharedShaders =
    
    // FShade 5.7 (Aardvark.Rendering 5.6) requires single-precision vector
    // types in vertex/fragment records — `V4f`/`V3f`/`V2f`/`float32`.

    /// Vertex type for shader processing
    type Vertex = {
        [<Position>]      pos : V4f
        [<WorldPosition>] wp  : V4f
        [<Normal>]        n   : V3f
        [<BiNormal>]      b   : V3f
        [<Tangent>]       t   : V3f
        [<Color>]         c   : V4f
        [<TexCoord>]      tc  : V2f
    }

    /// Common Level-of-Detail color shader
    /// Applies grayscale conversion when LoD visualization is enabled
    let LoDColor (v : Vertex) =
        fragment {
            if uniform?LodVisEnabled then
                let c : V4f = uniform?LoDColor
                let gamma = float32 DEFAULT_GAMMA
                let grayscale =
                    float32 RGB_TO_GRAYSCALE_R * v.c.X ** gamma +
                    float32 RGB_TO_GRAYSCALE_G * v.c.Y ** gamma +
                    float32 RGB_TO_GRAYSCALE_B * v.c.Z ** gamma
                return grayscale * c
            else
                return v.c
        }