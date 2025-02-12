namespace PRo3D.OpcViewer

open Aardvark.Base
open Aardvark.Data.Opc
open Aardvark.Rendering
open MBrace.FsPickler
open System.IO

type LayerInfo = {
    Path: DirectoryInfo
    PatchHierarchyFile: FileInfo
}

type PatchHierarchyStats = {
    CountLeafNodes : int
    CountInnerNodes : int
    CountVertices : int
    CountFaces : int
    IndexedAttributes : Symbol[]
    SingleAttributes : Symbol[]
}

module LayerUtils =

    let private (+/) path1 path2 = Path.Combine(path1, path2)
    let private serializer = FsPickler.CreateBinarySerializer()

    let loadPatchHierarchy (info : LayerInfo) : PatchHierarchy =
        PatchHierarchy.load serializer.Pickle serializer.UnPickle (OpcPaths.OpcPaths info.Path.FullName)

    /// Enumerates all (recursive) subdirs of given dirs that contain layer data.
    /// Specifically, a directory is returned if it contains the file "patches/patchhierarchy.xml".
    let searchLayerDirs (dirs : seq<string>) : LayerInfo list =
        dirs 
        |> Seq.map (fun s -> DirectoryInfo(s))
        |> Seq.filter (fun d -> d.Exists)
        |> Seq.collect (fun d -> 
            d.EnumerateDirectories("patches", SearchOption.AllDirectories)
            )
        |> Seq.filter (fun d -> File.Exists(d.FullName +/ "patchhierarchy.xml"))
        |> Seq.map (fun d -> { Path = d.Parent; PatchHierarchyFile = FileInfo(d.FullName +/ "patchhierarchy.xml") })
        |> List.ofSeq

    /// Enumerates all (recursive) subdirs of given dir that contain layer data.
    /// Specifically, a directory is returned if it contains the file "patches/patchhierarchy.xml".
    let searchLayerDir (dir : string) : LayerInfo list =
        searchLayerDirs [dir]

    let rec traverse root includeInner = seq {
        match root with
        | QTree.Node (n, xs) ->
            if includeInner then yield n
            for x in xs do yield! traverse x includeInner
        | QTree.Leaf n -> yield n
        }
       
    let getPatchHierarchyStats (patchHierarchy : PatchHierarchy) : PatchHierarchyStats =

        let mutable countLeafNodes = 0
        let mutable countInnerNodes = 0
        let mutable countVertices = 0
        let mutable countFaces = 0

        let opcPaths = patchHierarchy.opcPaths
        let rec traverse (n : QTree<Patch>) : unit =
            match n with
            | QTree.Node (n, xs) ->
                countInnerNodes <- countInnerNodes + 1
                for x in xs do traverse x
            | QTree.Leaf n ->
                countLeafNodes <- countLeafNodes + 1
                let ig, _ = Patch.load opcPaths  ViewerModality.XYZ n.info
                countVertices <- countVertices + ig.VertexCount
                countFaces <- countFaces + ig.FaceCount
            

        traverse patchHierarchy.tree
        
        let patch = match patchHierarchy.tree with | QTree.Node (n, _) -> n | QTree.Leaf n -> n
        let ig, _ = Patch.load patchHierarchy.opcPaths ViewerModality.XYZ patch.info

        {
            CountLeafNodes = countLeafNodes
            CountInnerNodes = countInnerNodes
            CountVertices = countVertices
            CountFaces = countFaces
            IndexedAttributes = if ig.IndexedAttributes <> null then ig.IndexedAttributes.Keys |> Array.ofSeq else Array.empty
            SingleAttributes = if ig.SingleAttributes <> null then ig.SingleAttributes.Keys |> Array.ofSeq else Array.empty
        }

    let printPatchInfo (info : LayerInfo) =
        let root = loadPatchHierarchy info
        let patch = match root.tree with | QTree.Node (n, _) -> n | QTree.Leaf n -> n
        let ig, _ = Patch.load root.opcPaths ViewerModality.XYZ patch.info
      
        let mutable totalLeafNodes  = 0
        let mutable totalPoints = 0
        let mutable totalFaces  = 0
        for x in traverse root.tree false do
            let ig, _ = Patch.load root.opcPaths ViewerModality.XYZ patch.info
            let positions = 
                match ig.IndexedAttributes[DefaultSemantic.Positions] with
                | (:? array<V3f> as v) when not (isNull v) -> v
                | _ -> failwith "[Queries] Patch has no V3f[] positions"
            //printfn "%A %d %d" x.info.Name x.level positions.Length
            totalLeafNodes <- totalLeafNodes + 1
            totalPoints    <- totalPoints + positions.Length
            totalFaces     <- totalFaces + ig.FaceCount
             
        if ig.IndexedAttributes <> null then
            printfn "indexed attributes"
            for a in ig.IndexedAttributes.Keys do printfn "    %A" a
        if ig.SingleAttributes  <> null then
            printfn "single attributes"
            for a in ig.SingleAttributes.Keys do printfn "    %A" a
        printfn "leaf nodes  %16d" totalLeafNodes
        printfn "     points %16d" totalPoints
        printfn "     faces  %16d" totalFaces


type LayerInfo with

    member this.LoadPatchHierarchy () = LayerUtils.loadPatchHierarchy this

    member this.PrintInfo () = LayerUtils.printPatchInfo this
