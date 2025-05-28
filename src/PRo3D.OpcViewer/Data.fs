namespace PRo3D.OpcViewer

open System
open System.IO
open System.IO.Compression
open System.Text.Json.Serialization
open System.Text.Json

[<AutoOpen>]
module Data =

    /// An existing directory.
    type DataDir = DataDir of string
    with
        static member tryOfString (path: string) =
            if Directory.Exists(path) then
                Some (DataDir path)
            else
                None

        static member ofString (path: string) =
            if Directory.Exists(path) then
                DataDir path
            else
                printfn "[ERROR] directory does not exist (%s)" path
                exit 1

    type DataRef =
        | AbsoluteDirRef of string * exists : bool 
        | RelativeDirRef of string
        | AbsoluteZipRef of string 
        | RelativeZipRef of string
        | HttpZipRef of Uri
        | SftpZipRef of Uri
        | InvalidDataRef of string

    let getDataRefFromString s : DataRef =

        let handleAbsolutePath (s : string) : DataRef =
            if Directory.Exists s then
                AbsoluteDirRef (s, true)
            else
                if Path.HasExtension s && Path.GetExtension(s).ToLowerInvariant() = ".zip" then
                    AbsoluteZipRef s
                else
                    AbsoluteDirRef (s, false)

        let handleRelativePath (s : string) : DataRef =
            if Path.HasExtension s && Path.GetExtension(s).ToLowerInvariant() = ".zip" then
                    RelativeZipRef s
                else
                    RelativeDirRef s

        let isZip (uri : Uri) : bool =
            let s = uri.AbsolutePath
            Path.HasExtension s && Path.GetExtension(s).ToLowerInvariant() = ".zip" 

        try
            let uri = Uri(s)
            match uri.Scheme.ToLowerInvariant() with
            | "http" -> HttpZipRef uri
            | "https" -> if isZip uri then HttpZipRef uri else InvalidDataRef s
            | "sftp" -> if isZip uri then SftpZipRef uri else InvalidDataRef s
            | "file" -> handleAbsolutePath s
            | _ -> InvalidDataRef s
        with
        | _ ->
            if Path.IsPathRooted(s) then
                handleAbsolutePath s
            elif Path.GetInvalidPathChars() |> Array.exists (fun c -> s.Contains(c)) then
                InvalidDataRef s
            else
                handleRelativePath s
      
    type ResolveDataPathResult =
        | Ok of DataDir
        | DownloadError of Uri * Exception
        | MissingSftpConfig of Uri
        | InvalidDataDir of string

    let resolveDataPath (basedir : string) (sftp : Sftp.SftpServerConfig option) (x : DataRef) : ResolveDataPathResult =
    
        let removeExtension (path : string) =
            if Path.HasExtension path then
                Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path))
            else path

        let handleZipFile (path : string) =
            let targetPath = path |> removeExtension
            if not (Directory.Exists(targetPath)) then
                Directory.CreateDirectory(targetPath) |> ignore
                ZipFile.ExtractToDirectory(path, targetPath)
            Ok (DataDir targetPath)

        match x with
        
        | AbsoluteDirRef (path, true) ->
            Ok (DataDir path)
        
        | AbsoluteDirRef (path, false) ->
            let info = Directory.CreateDirectory(path)
            Ok (DataDir info.FullName)
        
        | RelativeDirRef pathRel ->
            let path = Path.Combine(basedir, pathRel)
            let info = Directory.CreateDirectory(path)
            Ok (DataDir info.FullName)
        
        | AbsoluteZipRef path ->
            handleZipFile path
        
        | RelativeZipRef pathRel ->
            let path = Path.Combine(basedir, pathRel)
            handleZipFile path

        | HttpZipRef uri ->
            try
                let relPath = uri.AbsolutePath.Substring(1)
                let targetPath = Path.Combine(basedir, relPath)
                if not (File.Exists(targetPath)) then
                    let targetDir = Path.GetDirectoryName(targetPath)
                    if not (Directory.Exists(targetDir)) then
                        Directory.CreateDirectory(targetDir) |> ignore
                    Utils.downloadFileAsync uri targetPath |> Async.RunSynchronously
                handleZipFile targetPath
            with
            | e ->
                DownloadError (uri, e)

        | SftpZipRef uri ->
            match sftp with
            | Some sftp ->
                sftp.DownloadFile(uri, basedir, printfn "%s")
            
                let relPath = uri.AbsolutePath.Substring(1)
                let target = Path.Combine(basedir, relPath)
                handleZipFile target
            | None ->
                MissingSftpConfig uri
        
        | InvalidDataRef s ->
            InvalidDataDir s

    
    let private (+/) path1 path2 = Path.Combine(path1, path2)

    /// Enumerates all (recursive) subdirs of given dirs that contain layer data.
    /// Specifically, a directory is returned if it contains the file "patches/patchhierarchy.xml".
    let searchLayerDirs (dirs : seq<DataDir>) : LayerInfo list =
        dirs 
        |> Seq.map (fun (DataDir s) -> DirectoryInfo(s))
        |> Seq.filter (fun d -> d.Exists)
        |> Seq.collect (fun d -> 
            d.EnumerateDirectories("patches", SearchOption.AllDirectories)
            )
        |> Seq.filter (fun d -> File.Exists(d.FullName +/ "patchhierarchy.xml"))
        |> Seq.map (fun d -> { Path = d.Parent; PatchHierarchyFile = FileInfo(d.FullName +/ "patchhierarchy.xml") })
        |> List.ofSeq

    /// Enumerates all (recursive) subdirs of given dir that contain layer data.
    /// Specifically, a directory is returned if it contains the file "patches/patchhierarchy.xml".
    let searchLayerDir (dir : DataDir) : LayerInfo list =
        searchLayerDirs [dir]

    module Pro3DFile =
    
        type Surface = {
            Guid : Guid
            IsActive : bool
            IsVisible : bool
            Name : string
            ImportPath : string
            OpcPaths : string[]
            RelativePaths : bool
        }

        type SurfaceItem = {
            Surfaces : Surface
        }

        type Surfaces = {
            Flat: SurfaceItem array
        }

        type SurfaceModel = {
            Surfaces: Surfaces
        }

        type Pro3d = {
            Version: int
            SurfaceModel: SurfaceModel
        }
        
        let options =
            let o = JsonSerializerOptions()
            o.AllowTrailingCommas <- true
            o.NumberHandling <- JsonNumberHandling.AllowNamedFloatingPointLiterals ||| JsonNumberHandling.AllowReadingFromString
            o.PropertyNameCaseInsensitive <- true
            o.PropertyNamingPolicy <- JsonNamingPolicy.CamelCase
            o.WriteIndented <- true
            o

        let parse (s : string) = JsonSerializer.Deserialize<Pro3d>(s, options)

        ()
