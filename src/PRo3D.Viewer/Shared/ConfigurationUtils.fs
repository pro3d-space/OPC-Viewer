namespace PRo3D.Viewer.Shared

open Argu
open PRo3D.Viewer.Configuration

/// Common utility functions for configuration building to eliminate DRY violations
module ConfigurationUtils =

    /// Apply CLI overrides to ViewConfig (for project command)
    let applyViewConfigOverrides (baseConfig: ViewConfig) (backgroundColorOverride: string option) (screenshotsOverride: string option) (forceDownloadOverride: bool option) : ViewConfig =
        let configWithBg = 
            match backgroundColorOverride with
            | Some cliColor -> { baseConfig with BackgroundColor = Some cliColor }
            | None -> baseConfig
        
        let configWithScreenshots = 
            match screenshotsOverride with
            | Some cliScreenshots -> { configWithBg with Screenshots = Some cliScreenshots }
            | None -> configWithBg
        
        let configWithForceDownload =
            match forceDownloadOverride with
            | Some true -> { configWithScreenshots with ForceDownload = Some true }
            | _ -> configWithScreenshots
        
        configWithForceDownload

    /// Apply CLI overrides to DiffConfig (for project command)
    let applyDiffConfigOverrides (baseConfig: DiffConfig) (backgroundColorOverride: string option) (screenshotsOverride: string option) (forceDownloadOverride: bool option) : DiffConfig =
        let configWithBg = 
            match backgroundColorOverride with
            | Some cliColor -> { baseConfig with BackgroundColor = Some cliColor }
            | None -> baseConfig
        
        let configWithScreenshots = 
            match screenshotsOverride with
            | Some cliScreenshots -> { configWithBg with Screenshots = Some cliScreenshots }
            | None -> configWithBg
        
        let configWithForceDownload =
            match forceDownloadOverride with
            | Some true -> { configWithScreenshots with ForceDownload = Some true }
            | _ -> configWithScreenshots
        
        configWithForceDownload
    
    /// Apply CLI overrides to ExportConfig (for project command)
    let applyExportConfigOverrides (baseConfig: ExportConfig) (backgroundColorOverride: string option) (screenshotsOverride: string option) (forceDownloadOverride: bool option) : ExportConfig =
        // ExportConfig doesn't have BackgroundColor or Screenshots fields, only ForceDownload
        let configWithForceDownload =
            match forceDownloadOverride with
            | Some true -> { baseConfig with ForceDownload = Some true }
            | _ -> baseConfig
        
        configWithForceDownload