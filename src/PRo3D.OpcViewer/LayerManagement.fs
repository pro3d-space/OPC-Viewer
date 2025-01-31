namespace PRo3D.OpcViewer

open System.IO

type LayerInfo = {
    Path: DirectoryInfo
    PatchHierarchyFile: FileInfo
    }

module LayerManagement =

    let private (+/) path1 path2 = Path.Combine(path1, path2)

    /// Enumerates all (recursive) subdirs of given datadirs that contain layer data.
    /// Specifically, a directory is returned if it contains the file "patches/patchhierarchy.xml".
    let searchLayerDirs (datadirs : seq<string>) : seq<LayerInfo> =
        datadirs 
        |> Seq.map (fun s -> DirectoryInfo(s))
        |> Seq.filter (fun d -> d.Exists)
        |> Seq.collect (fun d -> 
            d.EnumerateDirectories("patches", SearchOption.AllDirectories)
            )
        |> Seq.filter (fun d -> File.Exists(d.FullName +/ "patchhierarchy.xml"))
        |> Seq.map (fun d -> { Path = d.Parent; PatchHierarchyFile = FileInfo(d.FullName +/ "patchhierarchy.xml") })

