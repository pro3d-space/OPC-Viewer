namespace PRo3D.Viewer

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
                // Validate path to prevent directory traversal
                let relPath = uri.AbsolutePath.Substring(1).Replace("..", "").Replace("~", "")
                let targetPath = Path.GetFullPath(Path.Combine(basedir, relPath))
                
                // Ensure target path is within basedir
                if not (targetPath.StartsWith(Path.GetFullPath(basedir))) then
                    InvalidDataDir (sprintf "Path traversal detected: %s" targetPath)
                else
                let targetPath = targetPath  // Continue with validated path
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

    module Wavefront =

        open Aardvark.Base
        open Aardvark.Data.Wavefront
        open Aardvark.Rendering
        open Aardvark.SceneGraph

        let loadObjFileWithTransform (filename : string) (transform : M44d option) : ISg =

            let wobj = ObjParser.Load(filename, useDoublePrecision = true)

            let textureFilename =
                match wobj.Materials |> Seq.tryFind (fun x -> x.Contains(WavefrontMaterial.Property.DiffuseColorMap)) with
                | Some v -> v[WavefrontMaterial.Property.DiffuseColorMap] :?> string |> Some
                | None -> None

            let verts = wobj.Vertices.ToArrayOfT<V4d>() |> Array.map _.XYZ
            let bounds = Box3d verts
            let centerTrafo = Trafo3d.Translation(-bounds.Center) * Trafo3d.Scale(2.0/bounds.Size.NormMax)
    
            let positions = verts |> Array.map (fun v -> centerTrafo.Forward.TransformPos v) |> Array.map V3f
            let texCoords = wobj.TextureCoordinates |> CSharpList.toArray |> Array.map (fun v -> V2f(v.X,1.0f-v.Y))
            let normals = wobj.Normals |> CSharpList.toArray
    
            let ps,ns,tcs =
                let ps = ResizeArray()
                let ns = ResizeArray()
                let tcs = ResizeArray()
                for set in wobj.FaceSets do
                    let iPos = set.VertexIndices
                    let iNormals =
                        if isNull set.NormalIndices then iPos
                        else set.NormalIndices
                    let iTcs = 
                        if isNull set.TexCoordIndices then iPos
                        else set.TexCoordIndices
                    for ti in 0 .. set.ElementCount - 1 do
                        let fi = set.FirstIndices.[ti]
                        let cnt = set.FirstIndices.[ti+1] - fi

                        if cnt = 3 then
                            tcs.Add texCoords.[iTcs.[fi + 0]]
                            tcs.Add texCoords.[iTcs.[fi + 1]]
                            tcs.Add texCoords.[iTcs.[fi + 2]]

                            ps.Add positions.[iPos.[fi + 0]]
                            ps.Add positions.[iPos.[fi + 1]]
                            ps.Add positions.[iPos.[fi + 2]]

                            // ns.Add normals.[iNormals.[fi + 0]]
                            // ns.Add normals.[iNormals.[fi + 1]]
                            // ns.Add normals.[iNormals.[fi + 2]]
                ps.ToArray(), ns.ToArray(), tcs.ToArray()
    
            let model0 =
                Sg.draw IndexedGeometryMode.TriangleList
                |> Sg.vertexAttribute' DefaultSemantic.Positions ps
                |> Sg.vertexAttribute' DefaultSemantic.DiffuseColorCoordinates tcs
                //|> Sg.vertexAttribute' DefaultSemantic.Normals ns
                |> Sg.transform centerTrafo.Inverse

            let model1 = 
                match textureFilename with
                | Some s -> model0 |> Sg.fileTexture DefaultSemantic.DiffuseColorTexture s true
                | None -> model0
            
            // Apply user transformation if provided
            match transform with
            | Some t -> model1 |> Sg.transform (Trafo3d(t, t.Inverse))
            | None -> model1

        let loadObjFile (filename : string) : ISg =
            loadObjFileWithTransform filename None

        let getObjFileBounds (filename : string) : Box3d option =
            try
                let wobj = ObjParser.Load(filename, useDoublePrecision = true)
                let verts = wobj.Vertices.ToArrayOfT<V4d>() |> Array.map _.XYZ
                if verts.Length > 0 then
                    Some (Box3d verts)
                else
                    printfn "[OBJ WARNING] No vertices found in %s" filename
                    None
            with
            | ex -> 
                printfn "[OBJ WARNING] Could not load bounds from %s: %s" filename ex.Message
                None

        ()