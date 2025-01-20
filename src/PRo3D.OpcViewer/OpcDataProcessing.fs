namespace Aardvark.Opc

open System
open System.IO
open System.Collections.Generic
open Aardvark.Base
open Aardvark.Data.Opc
open Aardvark.GeoSpatial.Opc.Configurations
open Aardvark.Data.Opc.Aara
open Aardvark.Rendering


module QTree =

    open Aardvark.Data.Opc

    let rec foldCulled 
        (consider : Box3d -> bool) 
        (f        : Patch -> 's -> 's) 
        (seed     : 's) 
        (tree     : QTree<Patch>) =
        match tree with
        | QTree.Node(p, children) -> 
            if consider p.info.GlobalBoundingBox then
                Seq.fold (foldCulled consider f) seed children
            else
                seed
        | QTree.Leaf(p) -> 
            if consider p.info.GlobalBoundingBox then
                f p seed
            else
                seed

module OpcDataProcessing =
//    let loadAttibuteLayer =

    type QueryAttribute = { channels : int; array : System.Array }
    type QueryResult = 
        {
            attributes        : Map<string, QueryAttribute>
            globalPositions   : IReadOnlyList<V3d>
            localPositions    : IReadOnlyList<V3f>
            patchFileInfoPath : string
            indices           : IReadOnlyList<int>
        }

    type QueryFunctions =
        {
            boxIntersectsQuery          : Box3d -> bool
            globalWorldPointWithinQuery : V3d -> bool
        }

    let handlePatch 
        (q                   : QueryFunctions) 
        (paths               : OpcPaths) 
        (attributeLayerNames : list<string>) 
        (p                   : Patch) 
        (o                   : List<QueryResult>) 
        : List<QueryResult> =

        let ig, _ = Patch.load paths ViewerModality.XYZ p.info
        let pfi = paths.Patches_DirAbsPath +/ p.info.Name
        let attributes = 
            let available = Set.ofList p.info.Attributes
            let attributes = 
                attributeLayerNames 
                |> List.choose (fun layerName -> 
                    if Set.contains layerName available then
                        let path = paths.Patches_DirAbsPath +/ p.info.Name +/ layerName
                        if File.Exists path then
                            Some (layerName, path)
                        else
                            None
                    else 
                        Log.warn $"[Queries] requested attribute {layerName} but patch {p.info.Name} does not provide it." 
                        Log.line "[Queries] available attributes: %s" (available |> Set.toSeq |> String.concat ",")
                        None
                )

            attributes
            |> List.map (fun (attributeName, filePath) -> 
                let arr = filePath |> fromFile<float> // change this to allow different attribute types
                attributeName, arr
            )
            |> Map.ofList

        //let positions = paths.Patches_DirAbsPath +/ p.info.Name +/ p.info.Positions |> fromFile<V3f>
        let positions = 
            match ig.IndexedAttributes[DefaultSemantic.Positions] with
            | (:? array<V3f> as v) when not (isNull v) -> v
            | _ -> failwith "[Queries] Patch has no V3f[] positions"

        let idxArray = 
            if ig.IsIndexed then
                match ig.IndexArray with
                | :? array<int> as idx -> idx
                | _ -> failwith "[Queries] Patch index geometry has no int[] index"
            else
                failwith "[Queries] Patch index geometry is not indexed."

        let attributesInputOutput =
            attributes 
            |> Map.map (fun name inputArray -> 
                inputArray, List<float>()
            )
        let globalOutputPositions = List<V3d>()
        let localOutputPositions = List<V3f>()
        let indices = List<int>()
                
        for startIndex in 0 .. 3 ..  idxArray.Length - 3 do
            let tri = [| idxArray[startIndex]; idxArray[startIndex + 1]; idxArray[startIndex + 2] |] 
            let localVertices = tri |> Array.map (fun idx -> positions[idx])
            if localVertices |> Array.exists (fun v -> v.IsNaN) then
                ()
            else
                let globalVertices = 
                    localVertices |> Array.map (fun local -> 
                        let v = V3d local
                        p.info.Local2Global.TransformPos v
                    )
                let validInside (v : V3d) = q.globalWorldPointWithinQuery v
                let triWithinQuery = globalVertices |> Array.forall validInside
                if triWithinQuery then
                    indices.Add(localOutputPositions.Count)
                    indices.Add(localOutputPositions.Count + 1)
                    indices.Add(localOutputPositions.Count + 2)
                    attributesInputOutput |> Map.iter (fun name (inputArray, output) -> 
                        tri |> Array.iter (fun idx -> 
                            output.Add(inputArray[idx])
                        )
                    )
                    globalOutputPositions.AddRange(globalVertices)
                    localOutputPositions.AddRange(localVertices)

        o.Add {
            attributes          = attributesInputOutput |> Map.map (fun p (i,o) -> { channels = 1; array = o.ToArray() :> System.Array})
            globalPositions     = globalOutputPositions :> IReadOnlyList<V3d>
            patchFileInfoPath   = pfi
            localPositions      = localOutputPositions :> IReadOnlyList<V3f>
            indices             = indices :> IReadOnlyList<int>
        }
        o

    let clip 
        (hierarchies         : list<PatchHierarchy * FileName>) 
        (requestedAttributes : list<string>) 
        (q                   : QueryFunctions) 
        (handlePatch         : OpcPaths -> list<string> -> Patch -> List<QueryResult> -> List<QueryResult>) 
        (hit                 : List<QueryResult> -> unit) =

        let queryResults =      
            hierarchies 
            |> List.fold (fun points (h, basePath) -> 
                let paths = OpcPaths basePath
                
                QTree.foldCulled 
                    q.boxIntersectsQuery 
                    (handlePatch paths requestedAttributes) 
                    points 
                    h.tree //|> ignore                
            ) (List<QueryResult>())


        Log.line("[AnnotationQuery:] found following patches") 
        queryResults |> List.ofSeq |> List.iter(fun x -> Log.line $"{x.patchFileInfoPath |> Path.GetFileName}")
            

        hit queryResults
        queryResults
