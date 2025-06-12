namespace Aardvark.Opc

open PRo3D.OpcViewer
open Aardvark.Base

[<AutoOpen>]
module DiffCommand =

    type DistanceComputationMode = 
        | Sky
        | Nearest

    type ComputeDistanceColor = DistanceComputationMode -> V3d -> C3b
    
    type DiffEnv = {
        Label0 : string
        Label1 : string
        Tree0: TriangleTree
        Tree1: TriangleTree
        GetColor : ComputeDistanceColor
        Sky : V3d
    }