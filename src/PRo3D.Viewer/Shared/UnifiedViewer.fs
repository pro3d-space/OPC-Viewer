namespace PRo3D.Viewer.Shared

open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.Base
open Aardvark.Data.Opc
open Aardvark.GeoSpatial.Opc
open Aardvark.GeoSpatial.Opc.Load
open Aardvark.Rendering
open Aardvark.SceneGraph
open Aardvark.Rendering.Text
open FSharp.Data.Adaptive
open MBrace.FsPickler
open PRo3D.Viewer
open PRo3D.Viewer.Diff
open PRo3D.Viewer.Shared
open PRo3D.Viewer.Shared.RenderingConstants
open System
open FShade
open Aardvark.FontProvider

// Font type for text overlays
type DiffFont = GoogleFontProvider<"Roboto Mono">

/// Unified viewer mode - determines which features are enabled
type ViewerMode =
    | ViewMode of ViewModeConfig
    | DiffMode of DiffModeConfig

/// Configuration for View mode
and ViewModeConfig = {
    /// Optional OBJ scene graphs to render
    objSceneGraphs : ISg list
    /// Enable object picking and cursor
    enablePicking : bool
}

/// Configuration for Diff mode  
and DiffModeConfig = {
    /// Environment for diff computation
    env : Diff.DiffTypes.DiffEnv
    /// Initial toggle mode
    initialToggleMode : DiffToggleMode
}

/// Configuration for the unified viewer
type ViewerConfig = {
    /// Viewer mode (View or Diff)
    mode : ViewerMode
    /// OPC scene to render
    scene : OpcScene
    /// Initial camera view
    initialCameraView : CameraView
    /// Custom keyboard handlers (key -> handler)
    customKeyHandlers : Map<Keys, unit -> unit>
    /// Custom mouse move handler
    customMouseHandler : Option<V2i -> V2d -> unit>
    /// Enable text overlay
    enableTextOverlay : bool
    /// Text overlay function (if enabled)
    textOverlayFunc : Option<unit -> string>
    /// Background color for rendering
    backgroundColor : C4f
    /// Screenshots directory (None = default ./screenshots)
    screenshotDirectory : string option
    /// Application version for window title
    version : string
}

/// Unified viewer implementation
module UnifiedViewer =

    [<AutoOpen>]
    module private Shaders =

        open Aardvark.Rendering.Effects

        // Reuse shared shaders
        let LoDColor = SharedShaders.LoDColor

        let stableTrafo (v : Vertex) =
            vertex {
                let vp = uniform.ModelViewTrafo * v.pos
                let wp = uniform.ModelTrafo * v.pos
                return {
                    pos = uniform.ProjTrafo * vp
                    wp = wp
                    n = uniform.NormalMatrix * v.n
                    b = uniform.NormalMatrix * v.b
                    t = uniform.NormalMatrix * v.t
                    c = v.c
                    tc = v.tc
                }
            }

        type PickBuffer = {
            [<Semantic("PickIds")>] id : int
        }

        type UniformScope with  
            member x.PatchId : int = uniform?PatchId
            member x.ShowDistances : bool = uniform?ShowDistances
            member x.LodVisEnabled : bool = uniform?LodVisEnabled

        let encodePickIds (v : Vertex) = 
            fragment {
                return { id = uniform.PatchId }
            }

        let noPick (v : Vertex) = 
            fragment {
                return { id = -1 }
            }

        // Diff-specific vertex type
        type VertexWithDistance = {
            [<Position>] pos : V4d
            [<Normal>] n : V3d
            [<Color>] c : V4d
            [<Semantic("LightDir")>] ldir : V3d
            [<Semantic("ViewPosition")>] vp : V4d
            [<Semantic(Diff.DiffRendering.DefaultSemantic.Distances)>] distance : V3d
        }

        let stableTrafoWithDistance (v : VertexWithDistance) =
            vertex {
                let vp = uniform.ModelViewTrafo * v.pos
                return { 
                    v with
                        pos = uniform.ProjTrafo * vp
                        vp = vp
                        n = (uniform.ModelViewTrafo * V4d(v.n, 0.0)).XYZ
                        c = v.c
                }
            }

        type NormalVertex = {
            [<Position>] pos : V4d
            [<SourceVertexIndex>] i : int
            [<Normal>] n : V3d
        }

        let generateNormal (t : Triangle<NormalVertex>) =
            triangle {
                let p0 = t.P0.pos.XYZ
                let p1 = t.P1.pos.XYZ
                let p2 = t.P2.pos.XYZ

                let edge1 = p1 - p0
                let edge2 = p2 - p0

                let normal = Vec.cross edge2 edge1 |> Vec.normalize

                yield { t.P0 with n = normal; i = 0 }
                yield { t.P1 with n = normal; i = 1 }
                yield { t.P2 with n = normal; i = 2 }
            }

        let showDistances (v : VertexWithDistance) = 
            fragment {
                if uniform.ShowDistances then
                    let n = v.n |> Vec.normalize
                    let ld = v.vp.XYZ |> Vec.normalize

                    let ambient = 0.0
                    let diffuse = Vec.dot ld n |> abs

                    let l = ambient + (1.0 - ambient) * diffuse
                    return V4d(v.distance * l, 1.0)
                else
                    return v.c
            }

    /// Run the unified viewer with the given configuration
    let run (config : ViewerConfig) =

        Aardvark.Init()

        use app = new OpenGlApplication()
        let win = app.CreateGameWindow()
        win.Title <- sprintf "PRo3D.Viewer v%s" config.version
        let runtime = win.Runtime

        let runner = 
            match config.mode with
            | ViewMode _ -> runtime.CreateLoadRunner 2
            | DiffMode _ -> runtime.CreateLoadRunner 1

        let serializer = FsPickler.CreateBinarySerializer()

        // Common viewer state
        let speed = AVal.init config.scene.speed
        let view = ViewerCommon.createCameraController config.initialCameraView speed win
        let frustum = ViewerCommon.createFrustum win.Sizes config.scene.near config.scene.far DEFAULT_FOV
        let lodVisEnabled = cval true
        let fillMode = cval FillMode.Fill

        // Setup common keyboard handlers
        ViewerCommon.setupCommonKeyboardHandlers win speed fillMode
        
        // Add L-key handler only for View mode
        match config.mode with
        | ViewMode _ ->
            win.Keyboard.KeyDown(Keys.L).Values.Add(fun _ -> 
                transact (fun _ -> lodVisEnabled.Value <- not lodVisEnabled.Value)
            )
        | DiffMode _ -> () // L-key doesn't apply to diff mode (no LoD structure)

        // Setup custom keyboard handlers
        config.customKeyHandlers |> Map.iter (fun key handler ->
            win.Keyboard.KeyDown(key).Values.Add(fun _ -> handler())
        )

        // Create scene based on mode
        let (scene, offscreenBuffer) =
            match config.mode with
            | ViewMode viewConfig ->
                // View mode implementation
                let infoTable = View.OpcRendering.PatchInfoTable()
                let pickIdSym = Sym.ofString "PickIds"
                let framebufferSignature = 
                    runtime.CreateFramebufferSignature [
                        DefaultSemantic.Colors, TextureFormat.Rgba8
                        DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
                        pickIdSym, TextureFormat.R32i
                    ]

                let hierarchies = 
                    config.scene.patchHierarchies |> Seq.toList |> List.map (fun basePath -> 
                        let h = PatchHierarchy.load serializer.Pickle serializer.UnPickle (OpcPaths.OpcPaths basePath)
                        View.OpcRendering.createSceneGraphCustom framebufferSignature runner infoTable basePath h
                    )

                let cursorPos = AVal.init V3d.Zero
                let cursor = 
                    if viewConfig.enablePicking then
                        Box3d.FromCenterAndSize(V3d.Zero, V3d.III * DEFAULT_CURSOR_SIZE)
                        |> Sg.box' C4b.White 
                        |> Sg.trafo (cursorPos |> AVal.map Trafo3d.Translation)
                        |> Sg.shader {
                            do! DefaultSurfaces.stableTrafo
                            do! noPick
                        }
                    else
                        Sg.empty

                // Apply shaders to OPC scene
                let opcSceneWithShaders = 
                    Sg.ofList hierarchies
                    |> Sg.shader {
                        do! stableTrafo
                        do! DefaultSurfaces.constantColor C4f.White 
                        do! DefaultSurfaces.diffuseTexture 
                        do! LoDColor
                        do! encodePickIds
                    }
                    |> Sg.uniform "LodVisEnabled" lodVisEnabled

                // Apply shaders to OBJ scene
                let objSceneWithShaders = 
                    Sg.ofList viewConfig.objSceneGraphs
                    |> Sg.shader {
                        do! stableTrafo
                        do! DefaultSurfaces.constantColor C4f.White 
                        do! DefaultSurfaces.diffuseTexture 
                        do! LoDColor
                        do! noPick
                    }
                    |> Sg.uniform "LodVisEnabled" lodVisEnabled
                
                // Separate geometry (affected by wireframe) from overlays (always solid)
                let geometryScene = 
                    opcSceneWithShaders
                    |> Sg.andAlso objSceneWithShaders
                    |> Sg.andAlso cursor
                    |> Sg.viewTrafo (view |> AVal.map CameraView.viewTrafo)
                    |> Sg.projTrafo (frustum |> AVal.map Frustum.projTrafo)
                    |> Sg.fillMode fillMode
                
                // Combine geometry with overlays (no fillMode applied to full scene)
                let combinedScene = geometryScene

                // Create offscreen buffer for view mode
                let buffer = 
                    let c = clear { colors [DefaultSemantic.Colors, config.backgroundColor;]; depth 1.0; }
                    let output = Set.ofList [ DefaultSemantic.Colors; pickIdSym; DefaultSemantic.DepthStencil ]
                    combinedScene
                    |> Sg.compile runtime framebufferSignature
                    |> RenderTask.renderSemanticsWithClear output win.Sizes c

                // Setup mouse handling after buffer is created
                if viewConfig.enablePicking then
                    win.Mouse.Move.Values.Add(fun (_, p) -> 
                        let pos = V2i(p.Position.X, p.Position.Y)
                        let region = Box2i.FromMinAndSize(pos, V2i.II)

                        if win.FramebufferSize.AllGreater(pos) then
                            let view = view.GetValue()
                            let frustum = frustum.GetValue()

                            let renderedDepth = buffer.[DefaultSemantic.DepthStencil].GetValue()
                            let depth = renderedDepth.DownloadDepth(region = region)

                            let pickIds = runtime.Download(buffer.[pickIdSym].GetValue(), region = region) |> unbox<PixImage<int32>>
                            let pickId = pickIds.GetChannel(0L)[0,0]
                            let object = infoTable.LookupLinear(pickId)

                            match object with
                            | None -> ()
                            | Some object ->
                                let d = depth[0,0] |> float
                                let viewProj = CameraView.viewTrafo view * Frustum.projTrafo frustum
                                let ndc = V3d(V2d(p.NormalizedPosition.X, 1.0 - p.NormalizedPosition.Y) * 2.0 - V2d.II, d * 2.0 - 1.0)
                                let wp = viewProj.Backward.TransformPosProj(ndc)
                                transact (fun _ -> cursorPos.Value <- wp)
                    )

                (combinedScene, buffer)

            | DiffMode diffConfig ->
                // Diff mode implementation
                let distMode = cval DistanceComputationMode.Sky
                let toggleMode = cval diffConfig.initialToggleMode
                let showDistancesEnabled = cval true

                let hierarchies = 
                    config.scene.patchHierarchies |> Seq.toList |> List.map (fun basePath -> 
                        let h = PatchHierarchy.load serializer.Pickle serializer.UnPickle (OpcPaths.OpcPaths basePath)
                        Diff.DiffRendering.createSceneGraphCustom win.FramebufferSignature runner h distMode toggleMode diffConfig.env.GetColor
                    )

                let pickPos = AVal.init V3d.NaN
                let cursorPos = AVal.init V3d.NaN
                let layerDistAtCursor = AVal.init nan

                let cursor = 
                    Box3d.FromCenterAndSize(V3d.Zero, V3d.III * DIFF_CURSOR_SIZE)
                    |> Sg.box' C4b.Red 
                    |> Sg.trafo (pickPos |> AVal.map Trafo3d.Translation)
                    |> Sg.shader {
                        do! DefaultSurfaces.stableTrafo
                    }

                // Diff-specific keyboard handlers
                win.Keyboard.KeyDown(Keys.C).Values.Add(fun _ -> 
                    transact (fun _ -> showDistancesEnabled.Value <- not showDistancesEnabled.Value)
                )

                win.Keyboard.KeyDown(Keys.M).Values.Add(fun _ -> 
                    transact (fun _ -> 
                        distMode.Value <-
                            match distMode.Value with
                            | DistanceComputationMode.Sky -> DistanceComputationMode.Nearest
                            | DistanceComputationMode.Nearest -> DistanceComputationMode.Sky
                    )
                )

                win.Keyboard.KeyDown(Keys.T).Values.Add(fun _ -> 
                    transact (fun _ -> 
                        toggleMode.Value <-
                            match toggleMode.Value with
                            | First -> Second
                            | Second -> First
                    )
                )

                // Mouse handling for diff mode
                win.Mouse.Move.Values.Add(fun (_, p) -> 
                    let pos = V2i(p.Position.X, p.Position.Y)

                    if win.FramebufferSize.AllGreater(pos) then
                        let view = view.GetValue()
                        let frustum = frustum.GetValue()
                        let viewProj = CameraView.viewTrafo view * Frustum.projTrafo frustum
                        let ndc = V3d(V2d(p.NormalizedPosition.X, 1.0 - p.NormalizedPosition.Y) * 2.0 - V2d.II, 0.0)
                        let pp = viewProj.Backward.TransformPosProj(ndc)
                        let ray = Ray3d(view.Location, (pp - view.Location).Normalized)
                        
                        let hit0Result = diffConfig.env.Tree0.IntersectRay(&ray)
                        let hit0 = if hit0Result.HasIntersection then Some(abs hit0Result.T, hit0Result.T) else None
                        let hit1Result = diffConfig.env.Tree1.IntersectRay(&ray)
                        let hit1 = if hit1Result.HasIntersection then Some(abs hit1Result.T, hit1Result.T) else None

                        let (maybeT, isFirst) =
                            match hit0, hit1 with
                            | None, None -> (None, false)
                            | Some (_, t0), None -> if t0 > 0.0 then (Some t0, true) else (None, false)
                            | None, Some (_, t1) -> if t1 > 0.0 then (Some t1, false) else (None, false)
                            | Some (_, t0), Some (_, t1) ->
                                match t0 > 0, t1 > 0 with
                                | false, false -> (None, false)
                                | false, true -> (Some t1, false)
                                | true, false -> (Some t0, true)
                                | true, true -> if t0 < t1 then (Some t0, true) else (Some t1, false)
                        
                        match maybeT with
                        | None ->
                            transact (fun _ ->
                                pickPos.Value <- V3d.NaN
                                cursorPos.Value <- V3d.NaN
                                layerDistAtCursor.Value <- nan
                            )
                        | Some t ->
                            let hitPosGlobal = ray.GetPointOnRay(t)
                            let skyRay = Ray3d(hitPosGlobal, diffConfig.env.Sky)

                            let otherHitPos =
                                if isFirst then
                                    let hit = diffConfig.env.Tree1.IntersectRay(&skyRay)
                                    if hit.HasIntersection then Some(abs hit.T, hit.T) else None
                                else
                                    let hit = diffConfig.env.Tree0.IntersectRay(&skyRay)
                                    if hit.HasIntersection then Some(abs hit.T, hit.T) else None

                            let distFromFirstToSecondLayer =
                                match otherHitPos with
                                | None -> nan
                                | Some (_, otherT) -> if isFirst then otherT else -otherT

                            transact (fun _ ->
                                pickPos.Value <- pp
                                cursorPos.Value <- hitPosGlobal
                                layerDistAtCursor.Value <- distFromFirstToSecondLayer
                            )
                )

                // Text overlay for diff mode
                let aspect = win.Sizes |> AVal.map (fun s -> float s.X / float s.Y)
                let scale = aspect |> AVal.map (fun aspect -> Trafo3d.Scale(V3d(1.0, aspect, 1.0)))
                
                let font = DiffFont.Font

                let text = (toggleMode, cursorPos) ||> AVal.map2 (fun tgl p ->
                    String.concat Environment.NewLine [
                        sprintf "%s %s" (match tgl with | First -> "*" | Second -> " ") diffConfig.env.Label0
                        sprintf "%s %s" (match tgl with | First -> " " | Second -> "*") diffConfig.env.Label1
                        ""
                        match p.IsNaN with
                        | true -> ""
                        | false -> sprintf "pos = %0.3f %0.3f %0.3f" p.X p.Y p.Z
                        match isNaN layerDistAtCursor.Value with
                        | true -> ""
                        | false -> sprintf "Δ   = %+0.3f" layerDistAtCursor.Value
                    ]
                )

                let info =
                    Sg.ofList [
                        Sg.text font C4b.Black text
                        |> Sg.trafo (scale |> AVal.map (fun s -> Trafo3d.Scale(0.04) * s * Trafo3d.Translation(-0.95, 0.90, 0.0)))

                        Sg.text font C4b.Yellow text
                        |> Sg.trafo (scale |> AVal.map (fun s -> Trafo3d.Scale(0.04) * s * Trafo3d.Translation(-0.954, 0.904, 0.0)))
                    ]
                    |> Sg.viewTrafo' Trafo3d.Identity
                    |> Sg.projTrafo' Trafo3d.Identity

                // Layer visibility control
                let showFirst = toggleMode |> AVal.map (fun x -> match x with | First -> true | Second -> false)
                let showSecond = toggleMode |> AVal.map (fun x -> match x with | First -> false | Second -> true)

                // Separate geometry from text overlay for diff mode
                let geometryScene =
                    Sg.ofList [
                        Sg.onOff showFirst hierarchies[0]
                        Sg.onOff showSecond hierarchies[1]
                    ]
                    |> Sg.andAlso cursor
                
                // Combine but keep info separate for now
                let combinedScene = 
                    geometryScene
                    |> Sg.andAlso info

                // Apply wireframe only to geometry, not text
                let geometryWithWireframe = 
                    geometryScene
                    |> Sg.viewTrafo (view |> AVal.map CameraView.viewTrafo)
                    |> Sg.projTrafo (frustum |> AVal.map Frustum.projTrafo)
                    |> Sg.shader {
                        do! generateNormal
                        do! stableTrafoWithDistance
                        do! DefaultSurfaces.constantColor C4f.White 
                        do! DefaultSurfaces.diffuseTexture
                        do! showDistances
                    }
                    |> Sg.uniform "ShowDistances" showDistancesEnabled
                    |> Sg.fillMode fillMode
                
                // Combine geometry with text overlay (text stays solid)
                let sg = 
                    geometryWithWireframe
                    |> Sg.andAlso info

                // Create offscreen buffer for diff mode
                let buffer = 
                    let c = clear { colors [DefaultSemantic.Colors, config.backgroundColor;]; depth 1.0; }
                    let output = Set.ofList [ DefaultSemantic.Colors; DefaultSemantic.DepthStencil ]
                    sg
                    |> Sg.compile runtime win.FramebufferSignature
                    |> RenderTask.renderSemanticsWithClear output win.Sizes c

                (sg, buffer)

        // Add screenshot handler (F12 key) - common for all modes
        win.Keyboard.KeyDown(Keys.F12).Values.Add(fun _ ->
            let colorTexture = offscreenBuffer.[DefaultSemantic.Colors].GetValue()
            ViewerCommon.saveScreenshot runtime colorTexture config.screenshotDirectory
        )
        
        // Create fullscreen pass to display the offscreen buffer
        let fullScreenPass = 
            Sg.fullScreenQuad
            |> Sg.diffuseTexture offscreenBuffer.[DefaultSemantic.Colors]
            |> Sg.shader {
                do! DefaultSurfaces.diffuseTexture
            }
        
        win.RenderTask <- runtime.CompileRender(win.FramebufferSignature, fullScreenPass)
        win.Run()
        0