namespace PRo3D.Viewer.Shared

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.Application
open Aardvark.Application.Slim
open FSharp.Data.Adaptive
open Aardvark.SceneGraph
open PRo3D.Viewer.Shared.RenderingConstants

/// Common viewer functionality shared between different viewer implementations
module ViewerCommon =
    
    /// Setup common keyboard handlers for viewer controls
    let inline setupCommonKeyboardHandlers (win : ^a when ^a : (member Keyboard : IKeyboard)) (speed : cval<float>) (fillMode : cval<FillMode>) =
        let keyboard = (^a : (member Keyboard : IKeyboard) win)
        
        // Speed controls (PageUp/PageDown)
        keyboard.KeyDown(Keys.PageUp).Values.Add(fun _ -> 
            transact (fun _ -> speed.Value <- speed.Value * SPEED_MULTIPLIER)
        )
        
        keyboard.KeyDown(Keys.PageDown).Values.Add(fun _ -> 
            transact (fun _ -> speed.Value <- speed.Value / SPEED_MULTIPLIER)
        )
        
        // Fill mode toggle (F key)
        keyboard.KeyDown(Keys.F).Values.Add(fun _ -> 
            transact (fun _ -> 
                fillMode.Value <-
                    match fillMode.Value with
                    | FillMode.Fill -> FillMode.Line
                    | _ -> FillMode.Fill
            )
        )
    
    /// Create camera controller with initial view and speed
    let inline createCameraController (initialView : CameraView) (speed : aval<float>) (win : ^a when ^a : (member Mouse : IMouse) and ^a : (member Keyboard : IKeyboard) and ^a : (member Time : aval<DateTime>)) =
        let mouse = (^a : (member Mouse : IMouse) win)
        let keyboard = (^a : (member Keyboard : IKeyboard) win)
        let time = (^a : (member Time : aval<DateTime>) win)
        // DefaultCameraController.controlWithSpeed expects cval, so convert if needed
        let speedCval = 
            match speed with
            | :? cval<float> as c -> c
            | _ -> cval(speed.GetValue())
        initialView |> DefaultCameraController.controlWithSpeed speedCval mouse keyboard time
    
    /// Create perspective frustum for the viewer
    let createFrustum (sizes : aval<V2i>) (near : float) (far : float) (fov : float) =
        sizes |> AVal.map (fun s -> 
            Frustum.perspective fov near far (float s.X / float s.Y)
        )
    
    /// Calculate default camera speed based on scene bounds
    let calculateDefaultSpeed (far : float) =
        far / DEFAULT_SPEED_DIVISOR
    
    /// Save screenshot from framebuffer to file with basic error handling
    let saveScreenshot (runtime : IRuntime) (framebuffer : IBackendTexture) (screenshotDir : string option) : unit =
        try
            // Determine screenshot directory with fallback to default
            let targetDir = 
                match screenshotDir with
                | None -> "./screenshots"
                | Some path when System.String.IsNullOrWhiteSpace(path) -> "./screenshots"
                | Some path -> path
            
            // Ensure directory exists
            if not (System.IO.Directory.Exists targetDir) then
                try
                    System.IO.Directory.CreateDirectory(targetDir) |> ignore
                    printfn "[INFO] Created screenshot directory: %s" targetDir
                with
                | ex ->
                    printfn "[WARNING] Could not create directory %s: %s" targetDir ex.Message
                    printfn "[INFO] Falling back to default directory"
                    let fallbackDir = "./screenshots"
                    if not (System.IO.Directory.Exists fallbackDir) then
                        System.IO.Directory.CreateDirectory(fallbackDir) |> ignore
            
            let finalDir = if System.IO.Directory.Exists targetDir then targetDir else "./screenshots"
            
            // Generate timestamp-based filename
            let timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")
            let filename = System.IO.Path.Combine(finalDir, sprintf "screenshot_%s.png" timestamp)
            
            // Download the framebuffer content as PixImage
            let image = runtime.Download(framebuffer) |> unbox<PixImage>
            
            // Save the image as PNG
            image.SaveAsPng(filename)
            
            // Get and print the full absolute path
            let fullPath = System.IO.Path.GetFullPath(filename)
            printfn "Screenshot saved: %s" fullPath
        with
        | ex ->
            printfn "Failed to save screenshot: %s" ex.Message
    
    /// Apply common camera transforms to scene graph
    let applyCameraTransforms (view : aval<CameraView>) (frustum : aval<Frustum>) (sg : ISg) =
        sg
        |> Sg.viewTrafo (view |> AVal.map CameraView.viewTrafo)
        |> Sg.projTrafo (frustum |> AVal.map Frustum.projTrafo)