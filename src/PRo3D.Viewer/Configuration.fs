namespace PRo3D.Viewer

open Aardvark.Base

/// Type-safe configuration structures for OPC Viewer commands
module Configuration =
    
    /// Re-export DataEntry and DataType from ProjectFile for consistency
    type DataEntry = Project.DataEntry
    type DataType = Project.DataType
    
    /// Configuration for the View command
    type ViewConfig = {
        Data: DataEntry array  // Unified array for both OPC and OBJ
        Speed: float option
        Sftp: string option
        BaseDir: string option
        BackgroundColor: string option
        Screenshots: string option
        ForceDownload: bool option
        Verbose: bool option
    }
    
    /// Configuration for the Diff command
    type DiffConfig = {
        Data: string array  // Diff doesn't support transforms yet
        NoValue: float option
        Speed: float option
        Verbose: bool option
        Sftp: string option
        BaseDir: string option
        BackgroundColor: string option
        Screenshots: string option
        ForceDownload: bool option
    }
    
    /// Export format options
    type ExportFormat = 
        | Pts
        | Ply
    
    /// Configuration for the Export command
    type ExportConfig = {
        Data: DataEntry array
        Format: ExportFormat
        OutFile: string option
        Sftp: string option
        BaseDir: string option
        ForceDownload: bool option
        Verbose: bool option
    }
    
    /// Configuration for the List command
    type ListConfig = {
        DataDir: string
        Sftp: string option
    }