namespace PRo3D.Viewer

open System
open System.IO
open System.Text.Json.Serialization
open System.Text.Json
open Aardvark.Data.Remote

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

    // Compatibility layer - use Aardvark.Data.Remote types but maintain old naming
    type DataRef = Aardvark.Data.Remote.DataRef

    // Use the new library's parser
    let getDataRefFromString s : DataRef = Parser.parse s
      
    // Compatibility result type
    type ResolveDataPathResult =
        | Ok of DataDir
        | DownloadError of Uri * Exception
        | MissingSftpConfig of Uri
        | InvalidDataDir of string

    // Use the new library with compatibility wrapper
    let resolveDataPath (basedir : string) (sftp : Aardvark.Data.Remote.SftpConfig option) (forceDownload : bool) (logger : Aardvark.Data.Remote.Logger.LogCallback option) (x : DataRef) : ResolveDataPathResult =
        
        // Initialize providers
        Resolver.initializeDefaultProviders()
        
        // Create configuration - sftp is already in correct format
        
        let config = { 
            ResolverConfig.Default with 
                BaseDirectory = basedir
                SftpConfig = sftp
                ForceDownload = forceDownload
                Logger = logger
                ProgressCallback = Some (fun percent -> 
                    printf "\r%.2f%%" percent
                    if percent >= 100.0 then printfn "" else System.Console.Out.Flush()
                )
        }
        
        // Resolve using new library
        let result = Resolver.resolve config x
        
        // Convert result to old format
        match result with
        | Aardvark.Data.Remote.Resolved path -> ResolveDataPathResult.Ok (DataDir path)
        | Aardvark.Data.Remote.InvalidPath reason -> ResolveDataPathResult.InvalidDataDir reason
        | Aardvark.Data.Remote.SftpConfigMissing uri -> ResolveDataPathResult.MissingSftpConfig uri
        | Aardvark.Data.Remote.DownloadError (uri, ex) -> ResolveDataPathResult.DownloadError (uri, ex)

    
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