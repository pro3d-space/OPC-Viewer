namespace PRo3D.Viewer.Diff

open PRo3D.Viewer
open Aardvark.Base
open Uncodium.Geometry.TriangleSet

[<AutoOpen>]
module DiffTypes =

    type DistanceComputationMode = 
        | Sky
        | Nearest

    type ComputeDistanceColor = DistanceComputationMode -> V3d -> C3b
    
    type DiffEnv = {
        Label0 : string
        Label1 : string
        Tree0: TriangleSet3d
        Tree1: TriangleSet3d
        GetColor : ComputeDistanceColor
        Sky : V3d
    }