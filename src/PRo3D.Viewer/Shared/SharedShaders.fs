namespace PRo3D.Viewer.Shared

open Aardvark.Base
open Aardvark.Rendering
open FShade
open PRo3D.Viewer.Shared.RenderingConstants

/// Common shader functions shared between viewers
module SharedShaders =
    
    /// Vertex type for shader processing
    type Vertex = {
        [<Position>]      pos : V4d
        [<WorldPosition>] wp  : V4d
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