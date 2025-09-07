namespace Aardvark.Data.Remote

open System

// Standard .NET provides IProgress<T> but it's optimized for UI scenarios with 
// SynchronizationContext marshaling. Our implementation is better suited for 
// console applications with synchronous progress reporting and rich progress data.

/// Progress reporting utilities
module Progress =
    
    /// Progress information for download/resolution operations
    type ProgressInfo = {
        /// Current operation being performed
        Operation: string
        /// Progress percentage (0.0 to 100.0)
        Percentage: float
        /// Number of bytes processed (if applicable)
        BytesProcessed: int64 option
        /// Total bytes expected (if applicable)
        TotalBytes: int64 option
        /// Estimated time remaining (if applicable)
        TimeRemaining: TimeSpan option
        /// DataRef being processed
        DataRef: DataRef option
    }
    
    /// Create a basic progress info
    let create operation percentage =
        {
            Operation = operation
            Percentage = percentage
            BytesProcessed = None
            TotalBytes = None
            TimeRemaining = None
            DataRef = None
        }
    
    /// Create progress info with byte information
    let createWithBytes operation percentage bytesProcessed totalBytes =
        {
            Operation = operation
            Percentage = percentage
            BytesProcessed = Some bytesProcessed
            TotalBytes = Some totalBytes
            TimeRemaining = None
            DataRef = None
        }
    
    /// Create progress info for a specific DataRef
    let createForDataRef operation percentage dataRef =
        {
            Operation = operation
            Percentage = percentage
            BytesProcessed = None
            TotalBytes = None
            TimeRemaining = None
            DataRef = Some dataRef
        }
    
    /// Progress callback type that takes detailed progress information
    type DetailedProgressCallback = ProgressInfo -> unit
    
    /// Simple progress callback type that only takes percentage
    type SimpleProgressCallback = float -> unit
    
    /// Convert a simple callback to a detailed callback
    let toDetailedCallback (simpleCallback: SimpleProgressCallback) : DetailedProgressCallback =
        fun progressInfo -> simpleCallback progressInfo.Percentage
    
    /// Convert a detailed callback to a simple callback
    let toSimpleCallback (detailedCallback: DetailedProgressCallback) : SimpleProgressCallback =
        fun percentage -> 
            let progressInfo = create "Operation" percentage
            detailedCallback progressInfo
    
    /// No-op progress callback
    let noOp : DetailedProgressCallback = fun _ -> ()
    
    /// Console progress callback that prints progress to console
    let console : DetailedProgressCallback = 
        fun progress ->
            match progress.DataRef with
            | Some dataRef ->
                printfn "[%s] %s: %.1f%%" (Parser.describe dataRef) progress.Operation progress.Percentage
            | None ->
                printfn "%s: %.1f%%" progress.Operation progress.Percentage