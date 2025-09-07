// The unified Fetch API - both styles in one clean module!
#r "../src/Aardvark.Data.Remote/bin/Debug/net8.0/Aardvark.Data.Remote.dll"

open Aardvark.Data.Remote

printfn "=== Unified 'Fetch' API ==="

// ============================================================
// Style 1: F# Pipeline (camelCase) - Functional & Composable
// ============================================================
printfn "\nF# Pipeline Style:"

// Simple
"http://example.com/data.zip" |> Fetch.from |> Fetch.resolveDefault

// Configured
"sftp://server.com/data.zip"
|> Fetch.from
|> Fetch.configure
|> Fetch.withBaseDirectory "/cache"
|> Fetch.withSftpConfigFile "~/.filezilla.xml"
|> Fetch.withMaxRetries 5
|> Fetch.resolve

// Quick functions
Fetch.quickResolve "/local/data"
Fetch.resolveMany ["path1"; "path2"; "path3"]

// ============================================================
// Style 2: C# Builder (PascalCase) - Fluent & Familiar
// ============================================================
printfn "\nC# Builder Style:"

// Simple
Fetch.Resolve "/local/data"

// Configured
Fetch
    .From("http://example.com/data.zip")
    .WithBaseDirectory("/cache")
    .WithProgress(fun p -> printfn "%.0f%%" p)
    .WithMaxRetries(5)
    .Resolve()

// Async
Fetch.ResolveAsync "/data" |> Async.AwaitTask

printfn "\n✨ One module, two styles, zero confusion!"