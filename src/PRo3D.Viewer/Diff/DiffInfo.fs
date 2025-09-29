namespace PRo3D.Viewer.Diff

open Aardvark.Base
open PRo3D.Viewer.Shared
open Uncodium.Geometry.TriangleSet

[<AutoOpen>]
module DiffTypes =

    type ComputeDistanceColor = DistanceComputationMode -> V3d -> C3b
    
    type DiffEnv = {
        Label0 : string
        Label1 : string
        Tree0: ITriangleSet
        Tree1: ITriangleSet
        GetColor : ComputeDistanceColor
        Sky : V3d
    }