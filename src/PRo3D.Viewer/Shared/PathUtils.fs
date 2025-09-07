namespace PRo3D.Viewer.Shared

open System.IO

/// Common utility functions for path resolution to eliminate DRY violations
module PathUtils =

    /// Check if path is absolute or remote URL
    let isAbsoluteOrRemotePath (path: string) : bool =
        path.StartsWith("http://") || path.StartsWith("https://") || path.StartsWith("sftp://") || Path.IsPathRooted(path)

    /// Resolve a single path relative to a project directory
    let resolveProjectPath (projectDir: string) (path: string) : string =
        match path with
        | p when p.StartsWith("http://") || p.StartsWith("https://") || p.StartsWith("sftp://") -> p
        | p when Path.IsPathRooted(p) -> p
        | p -> Path.GetFullPath(Path.Combine(projectDir, p))

    /// Resolve an optional path relative to project directory (for BaseDir, SFTP, Screenshots)
    let resolveOptionalProjectPath (projectDir: string) (path: string option) : string option =
        path
        |> Option.map (fun p ->
            if Path.IsPathRooted(p) then p
            else Path.GetFullPath(Path.Combine(projectDir, p))
        )

    /// Resolve multiple configuration paths at once
    let resolveConfigPaths (projectDir: string) (baseDir: string option) (sftp: string option) (screenshots: string option) : (string option * string option * string option) =
        let resolvedBaseDir = resolveOptionalProjectPath projectDir baseDir
        let resolvedSftp = resolveOptionalProjectPath projectDir sftp
        let resolvedScreenshots = resolveOptionalProjectPath projectDir screenshots
        (resolvedBaseDir, resolvedSftp, resolvedScreenshots)