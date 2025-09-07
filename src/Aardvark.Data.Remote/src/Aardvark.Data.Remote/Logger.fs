namespace Aardvark.Data.Remote

/// Simple callback-based logging for zero-dependency diagnostic output
module Logger =
    
    /// Log levels in ascending order of severity
    type LogLevel = 
        | Debug 
        | Info 
        | Warning 
        | Error
    
    /// Helper module for LogLevel comparisons
    module LogLevel =
        let private getValue = function
            | Debug -> 0 | Info -> 1 | Warning -> 2 | Error -> 3
            
        let isAtLeast (minLevel: LogLevel) (level: LogLevel) =
            getValue level >= getValue minLevel
    
    /// Simple logging callback: level -> message -> unit
    type LogCallback = LogLevel -> string -> unit
    
    /// No-op logger (default)
    let silent : LogCallback = fun _ _ -> ()
    
    /// Console logger with level filtering
    let console (minLevel: LogLevel) : LogCallback =
        fun level msg ->
            if LogLevel.isAtLeast minLevel level then
                let prefix = 
                    match level with
                    | Debug -> "[DEBUG]"
                    | Info -> "[INFO]"
                    | Warning -> "[WARN]"
                    | Error -> "[ERROR]"
                printfn "%s %s" prefix msg
    
    /// Helper to log only if callback exists
    let log (logger: LogCallback option) level msg =
        logger |> Option.iter (fun l -> l level msg)