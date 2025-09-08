namespace PRo3D.Viewer.Shared

open Aardvark.Base
open Aardvark.Data.Remote
open PRo3D.Viewer

/// Common utility functions for command processing to eliminate DRY violations
module CommandUtils =

    /// Parse SFTP configuration with consistent error handling
    let parseSftpConfig (sftpPath: string option) : SftpConfig option =
        sftpPath 
        |> Option.bind (fun path ->
            match FileZillaConfig.parseFile path with
            | Result.Ok sftpConfig -> Some sftpConfig
            | Result.Error msg -> 
                printfn "[ERROR] Failed to parse SFTP config: %s" msg
                None)

    /// Resolve base directory with consistent default behavior
    let resolveBaseDirectory (baseDir: string option) : string =
        match baseDir with
        | Some s -> s
        | None -> System.IO.Path.Combine(System.Environment.CurrentDirectory, "tmp/data")

    /// Parse background color with consistent error handling  
    let parseBackgroundColor (colorStr: string option) : C4f =
        match colorStr with
        | Some colorStr ->
            match Utils.parseBackgroundColor colorStr with
            | Result.Ok color -> color
            | Result.Error msg ->
                printfn "[WARNING] Invalid background color '%s': %s. Using default black." colorStr msg
                C4f.Black
        | None -> C4f.Black

    /// Resolve data paths with consistent workflow
    let resolveDataPaths (basedir: string) (sftpConfig: SftpConfig option) (forceDownload: bool) (logger: Logger.LogCallback option) (dataRefs: DataRef list) : ResolveDataPathResult list =
        let resolve = Data.resolveDataPath basedir sftpConfig forceDownload logger
        dataRefs |> List.map resolve

    /// Handle resolve results with consistent error reporting
    let handleResolveResults (resolvedResults: ResolveDataPathResult list) : DataDir list option =
        let datadirResults = 
            resolvedResults |> List.map (fun x ->
                match x with
                | ResolveDataPathResult.Ok ok -> Some ok
                | ResolveDataPathResult.MissingSftpConfig uri ->
                    printfn "Use --sftp|-s do specify SFTP config for %A" uri
                    None
                | ResolveDataPathResult.DownloadError (uri, e) ->
                    printfn "%A: %A" uri e
                    None
                | ResolveDataPathResult.InvalidDataDir s ->
                    printfn "invalid data dir: %A" s
                    None
            )
        
        if datadirResults |> List.exists Option.isNone then
            None
        else
            Some (datadirResults |> List.choose id)