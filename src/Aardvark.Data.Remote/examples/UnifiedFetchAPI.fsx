// The unified Fetch API - demonstrating the new functional design
#r "../src/Aardvark.Data.Remote/bin/Debug/net8.0/Aardvark.Data.Remote.dll"
#r "nuget: SSH.NET"

open System
open System.IO
open System.Threading.Tasks
open Aardvark.Data.Remote

printfn "=== Unified Fetch API Demonstration ==="

// The new API is truly unified - both F# and C# use the same underlying functions
// but with appropriate type signatures and calling conventions for each language

printfn "\n1. F# Functional API:"

// F# - Simple and clean, uses default config
let fsharpResult1 = Fetch.resolve "fsharp-test-1"
printfn "F# resolve: %A" fsharpResult1

// F# - With custom config using record syntax
let fsharpConfig = {
    Fetch.defaultConfig with
        baseDirectory = Path.GetTempPath()
        maxRetries = 5
        progress = Some (fun percent -> printf "F# Progress: %.1f%% " percent)
}

let fsharpResult2 = Fetch.resolveWith fsharpConfig "fsharp-test-2"
printfn "\nF# resolveWith: %A" fsharpResult2

printfn "\n2. F# Async API:"

// F# - Async workflows (idiomatic F# asynchrony)
async {
    let! asyncResult = Fetch.resolveAsync "fsharp-async-test"
    printfn "F# async result: %A" asyncResult
    
    let! asyncWithConfigResult = Fetch.resolveAsyncWith fsharpConfig "fsharp-async-config-test"
    printfn "F# async with config: %A" asyncWithConfigResult
    
} |> Async.RunSynchronously

printfn "\n3. F# Batch Processing:"

// F# - Batch processing with List types
let fsharpUrls = ["batch-1"; "batch-2"; "batch-3"]

async {
    let! batchResults = Fetch.resolveMany fsharpUrls
    printfn "F# batch results:"
    for i, result in List.indexed batchResults do
        printfn "  [%d] %A" i result
        
    let! batchWithConfigResults = Fetch.resolveManyWith fsharpConfig fsharpUrls
    printfn "F# batch with config:"
    for i, result in List.indexed batchWithConfigResults do
        printfn "  [%d] %A" i result
        
} |> Async.RunSynchronously

printfn "\n4. C# Interop API:"

// C# - Simple usage (same function names, but PascalCase)
let csharpResult1 = Fetch.Resolve("csharp-test-1")
printfn "C# Resolve: %A" csharpResult1

// C# - With configuration using init-only class
let csharpConfig = FetchConfiguration()
csharpConfig.BaseDirectory <- Path.GetTempPath()
csharpConfig.MaxRetries <- 5
csharpConfig.Progress <- fun percent -> printf "C# Progress: %.1f%% " percent

let csharpResult2 = Fetch.ResolveWith "csharp-test-2" csharpConfig  
printfn "\nC# ResolveWith: %A" csharpResult2

printfn "\n5. C# Task API:"

// C# - Task-based async (idiomatic C# asynchrony)
let csharpTaskResult = Fetch.ResolveAsync("csharp-task-test") |> Async.AwaitTask |> Async.RunSynchronously
printfn "C# Task result: %A" csharpTaskResult

let csharpTaskWithConfigResult = Fetch.ResolveAsyncWith "csharp-task-config-test" csharpConfig |> Async.AwaitTask |> Async.RunSynchronously
printfn "C# Task with config: %A" csharpTaskWithConfigResult

printfn "\n6. C# Batch Processing:"

// C# - Task-based batch processing
let csharpUrls = ["csharp-batch-1"; "csharp-batch-2"] // F# list works in C# API too
let csharpBatchResult = Fetch.ResolveMany(csharpUrls) |> Async.AwaitTask |> Async.RunSynchronously
printfn "C# batch results:"
for i, result in List.indexed csharpBatchResult do
    printfn "  [%d] %A" i result

let csharpBatchWithConfigResult = Fetch.ResolveManyWith csharpUrls csharpConfig |> Async.AwaitTask |> Async.RunSynchronously
printfn "C# batch with config:"
for i, result in List.indexed csharpBatchWithConfigResult do
    printfn "  [%d] %A" i result

printfn "\n7. Config Interoperability:"

// Show how F# and C# configs can interoperate
let fsharpToCs = FetchConfiguration.FromFSharp(fsharpConfig)
printfn "F# config -> C#: BaseDirectory = %s, MaxRetries = %d" fsharpToCs.BaseDirectory fsharpToCs.MaxRetries

let csToFsharp = csharpConfig.ToFSharp()
printfn "C# config -> F#: baseDirectory = %s, maxRetries = %d" csToFsharp.baseDirectory csToFsharp.maxRetries

// Both configs work with both APIs!
let mixedResult1 = Fetch.resolveWith csToFsharp "mixed-test-1"  // C# config with F# API
let mixedResult2 = Fetch.ResolveWith "mixed-test-2" fsharpToCs  // F# config with C# API
printfn "Mixed usage works: %A, %A" mixedResult1 mixedResult2

printfn "\n8. Advanced Unified Patterns:"

// Pattern 1: Mixed async processing
let mixedAsyncTasks = [
    Fetch.resolveAsync "mixed-async-1"                    // F# async
    Fetch.ResolveAsync("mixed-async-2") |> Async.AwaitTask  // C# Task -> F# Async
]

async {
    let! mixedResults = mixedAsyncTasks |> Async.Parallel
    printfn "Mixed async results: %A" mixedResults
} |> Async.RunSynchronously

// Pattern 2: Configuration chain (F# style with C# interop)
let chainedConfig = 
    FetchConfiguration(BaseDirectory = "./temp", MaxRetries = 3)
    |> fun c -> c.ToFSharp()
    |> fun config -> { config with progress = Some (fun p -> printf "Chain: %.1f%% " p) }

let chainedResult = Fetch.resolveWith chainedConfig "chained-test"
printfn "\nChained config result: %A" chainedResult

printfn "\n9. Functional Architecture Benefits:"

printfn "✓ No initialization required - pure functions"
printfn "✓ Immutable configuration - thread-safe by design"
printfn "✓ Type-safe error handling - discriminated unions"
printfn "✓ Language-appropriate idioms - Async for F#, Task for C#"
printfn "✓ Discoverable API - IntelliSense works perfectly"
printfn "✓ Testable - no hidden state or global configuration"

printfn "\n10. Performance Comparison:"

// Demonstrate that both APIs have identical performance (same underlying implementation)
let performanceUrls = [for i in 1..5 -> $"perf-test-{i}"]

let stopwatch = System.Diagnostics.Stopwatch.StartNew()
async {
    let! fsharpPerfResults = Fetch.resolveMany performanceUrls
    let fsharpTime = stopwatch.ElapsedMilliseconds
    stopwatch.Restart()
    
    let! csharpPerfResults = Fetch.ResolveMany(performanceUrls) |> Async.AwaitTask
    let csharpTime = stopwatch.ElapsedMilliseconds
    
    printfn "Performance test:"
    printfn "  F# API: %dms, C# API: %dms" fsharpTime csharpTime
    printfn "  Results identical: %b" (fsharpPerfResults = csharpPerfResults)
    
} |> Async.RunSynchronously

printfn "\n=== Unified API Demonstration Complete ==="
printfn "\nThe unified design provides:"
printfn "- One implementation, two idiomatic APIs"
printfn "- Perfect F#/C# interoperability"
printfn "- Consistent behavior across both languages"
printfn "- Type safety and discoverability"
printfn "- Pure functional architecture"