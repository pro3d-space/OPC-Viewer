namespace Aardvark.Opc

open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.Base
open Aardvark.Data.Opc
open Aardvark.GeoSpatial.Opc
open Aardvark.GeoSpatial.Opc.Load
open Aardvark.Rendering
open Aardvark.SceneGraph
open DiffRendering
open FSharp.Data.Adaptive 
open MBrace.FsPickler
open PRo3D.OpcViewer
open System
open Aardvark.Rendering.Text
open FShade
open Aardvark.Rendering.Effects

[<AutoOpen>]
module DiffShader =

    open Aardvark.Rendering.Effects
    
    open FShade

    let LoDColor  (v : Vertex) =
        fragment {
            if uniform?LodVisEnabled then
                let c : V4d = uniform?LoDColor
                let gamma = 1.0
                let grayscale = 0.2126 * v.c.X ** gamma + 0.7152 * v.c.Y ** gamma  + 0.0722 * v.c.Z ** gamma 
                return grayscale * c 
            else return v.c
        }

    type VertexWithDistance = {
            [<Position>]                pos     : V4d
            [<Normal>]                  n       : V3d
            [<Color>]                   c       : V4d
            [<Semantic("LightDir")>]    ldir    : V3d
            [<Semantic("ViewPosition")>] vp : V4d
            [<Semantic(DiffRendering.DefaultSemantic.Distances)>] distance : V3d
        }

    let stableTrafo (v : VertexWithDistance) =
        vertex {
            let vp = uniform.ModelViewTrafo * v.pos
            return 
                { v with
                    pos = uniform.ProjTrafo * vp
                    vp = vp
                    n = (uniform.ModelViewTrafo * V4d(v.n, 0.0)).XYZ
                    c = v.c
                }
        }


    type NormalVertex = 
        {
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

  

    type UniformScope with
        member x.ShowDistances : bool = uniform?ShowDistances


   

    //type VertexWithDistance = 
    //    {
    //        [<Position>] pos : V4d
    //        [<Color>] c: V4d
    //        [<Semantic(DiffRendering.DefaultSemantic.Distances)>] distance : V3d
    //    }

    let showDistances (v : VertexWithDistance) = 
        //fragment {
        //    if uniform.ShowDistances then
        //        return V4d(v.distance, 1.0)
        //    else
        //        return v.c
        //}
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


module DiffViewer = 

    open Aardvark.FontProvider
    type Font = GoogleFontProvider<"Roboto Mono">

    type ToggleMode = 
        | First
        | Second

    let run (scene : OpcScene) (initialCameraView : CameraView) (env : DiffEnv) =

        Aardvark.Init()

        use app = new OpenGlApplication()
        let win = app.CreateGameWindow()
        let runtime = win.Runtime

        let runner = runtime.CreateLoadRunner 1 

        let serializer = FsPickler.CreateBinarySerializer()

        let mode = cval DistanceComputationMode.Sky
        let toggleMode = cval ToggleMode.First

        let hierarchies = 
            scene.patchHierarchies |> Seq.toList |> List.map (fun basePath -> 
                let h = PatchHierarchy.load serializer.Pickle serializer.UnPickle (OpcPaths.OpcPaths basePath)
                DiffRendering.createSceneGraphCustom win.FramebufferSignature runner basePath h mode env.GetColor
            )

        let speed = AVal.init scene.speed

        let view = initialCameraView |> DefaultCameraController.controlWithSpeed speed win.Mouse win.Keyboard win.Time
        let frustum = win.Sizes |> AVal.map (fun s -> Frustum.perspective 60.0 scene.near scene.far (float s.X / float s.Y))
        let aspect = win.Sizes |> AVal.map (fun s -> float s.X / float s.Y)

        let lodVisEnabled = cval true
        let fillMode = cval FillMode.Fill

        let pickPos = AVal.init V3d.NaN
        let cursorPos = AVal.init V3d.NaN
        let layerDistAtCursor = AVal.init nan

        let cursor = 
            Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.002)
            |> Sg.box' C4b.Red 
            |> Sg.trafo (pickPos |> AVal.map Trafo3d.Translation)
            |> Sg.shader {
                do! DefaultSurfaces.stableTrafo
            }

        win.Keyboard.KeyDown(Keys.PageUp).Values.Add(fun _ -> 
            transact (fun _ -> speed.Value <- speed.Value * 1.5)
        )

        win.Keyboard.KeyDown(Keys.PageDown).Values.Add(fun _ -> 
            transact (fun _ -> speed.Value <- speed.Value / 1.5)
        )

        win.Keyboard.KeyDown(Keys.L).Values.Add(fun _ -> 
            transact (fun _ -> lodVisEnabled.Value <- not lodVisEnabled.Value)
        )

        win.Keyboard.KeyDown(Keys.F).Values.Add(fun _ -> 
            transact (fun _ -> 
                fillMode.Value <-
                    match fillMode.Value with
                    | FillMode.Fill -> FillMode.Line
                    | _-> FillMode.Fill
            )
        )

        let showDistances = cval true
        win.Keyboard.KeyDown(Keys.C).Values.Add(fun _ -> 
            transact (fun _ -> 
                showDistances.Value <- not showDistances.Value
            )
        )

        win.Keyboard.KeyDown(Keys.M).Values.Add(fun _ -> 
            transact (fun _ -> 
                mode.Value <-
                    match mode.Value with
                    | DistanceComputationMode.Sky -> DistanceComputationMode.Nearest
                    | DistanceComputationMode.Nearest -> DistanceComputationMode.Sky
            )
        )

        win.Keyboard.KeyDown(Keys.T).Values.Add(fun _ -> 
            transact (fun _ -> 
                toggleMode.Value <-
                    match toggleMode.Value with
                    | ToggleMode.First -> ToggleMode.Second
                    | ToggleMode.Second -> ToggleMode.First
            )
        )

        win.Mouse.Move.Values.Add(fun (_, p) -> 

            let pos = V2i(p.Position.X, p.Position.Y)
            let region = Box2i.FromMinAndSize(pos, V2i.II)

            if win.FramebufferSize.AllGreater(pos) then

                // get current view / proj
                let view = view.GetValue()
                let frustum = frustum.GetValue()
                let viewProj = CameraView.viewTrafo view * Frustum.projTrafo frustum
                let ndc = V3d(V2d(p.NormalizedPosition.X, 1.0 - p.NormalizedPosition.Y) * 2.0 - V2d.II, 0.0)
                let pp = viewProj.Backward.TransformPosProj(ndc) // pick position
                let ray = Ray3d(view.Location, (pp - view.Location).Normalized)
                
                //let ray = Ray3d(pGlobal, sky)
                let hit0 = TriangleTree.getNearestIntersection env.Tree0 ray
                let hit1 = TriangleTree.getNearestIntersection env.Tree1 ray

                let (maybeT, isFirst) =
                    match hit0, hit1 with
                    | None        , None         -> (None, false)
                    | Some (_, t0), None         -> if t0 > 0.0 then (Some t0, true) else (None, false)
                    | None        , Some (_, t1) -> if t1 > 0.0 then (Some t1, false) else (None, false)
                    | Some (_, t0), Some (_, t1) ->
                        match t0 > 0, t1 > 0 with
                        | false, false -> (None, false)
                        | false, true -> (Some t1, false)
                        | true, false -> (Some t0, true)
                        | true, true ->
                            if t0 < t1 then
                                (Some t0, true)
                            else
                                (Some t1, false)
                
                match maybeT with
                | None ->  // no hit (at mouse position)
                    transact (fun _ ->
                        pickPos.Value <- V3d.NaN
                        cursorPos.Value <- V3d.NaN
                        layerDistAtCursor.Value <- nan
                    )
                | Some t -> // hit (at mouse position)
                    let hitPosGlobal = ray.GetPointOnRay(t)
                    let skyRay = Ray3d(hitPosGlobal, env.Sky)

                    let otherHitPos =
                        if isFirst then
                            TriangleTree.getNearestIntersection env.Tree1 skyRay
                        else
                            TriangleTree.getNearestIntersection env.Tree0 skyRay

                    let distFromFirstToSecondLayer =
                        match otherHitPos with
                        | None -> nan
                        | Some (_, otherT) -> if isFirst then otherT else -otherT
                        

                    //printfn "t = %f     hitPosGlobal = %A" t hitPosGlobal
                    //printfn "isFirst = %A     dist = %0.4f" isFirst distFromFirstToSecondLayer

                    transact (fun _ ->
                        pickPos.Value <- pp
                        cursorPos.Value <- hitPosGlobal
                        layerDistAtCursor.Value <- distFromFirstToSecondLayer
                    )

                //printfn "mouse move: p = %A     wp = %A          cam = %A      ray = %A" p.Position wp view.Location ray
                //printfn "mouse move: ray = %A     %A    %A" ray hit0 hit1

        )

        // --------- info text -----------------
        let font = Font.Font
        let scale = aspect |> AVal.map (fun aspect -> Trafo3d.Scale(V3d(1.0, aspect, 1.0)))
        
        let text = (toggleMode, cursorPos) ||> AVal.map2 (fun tgl p ->

            let offset = layerDistAtCursor.Value * env.Sky

            String.concat Environment.NewLine [
                
                sprintf "%s %s" (match tgl with | First -> "*" | Second -> " ") env.Label0

                sprintf "%s %s" (match tgl with | First -> " " | Second -> "*") env.Label1

                ""

                //sprintf "sky %0.3f %0.3f %0.3f" env.Sky.X env.Sky.Y env.Sky.Z

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
                |> Sg.trafo (scale |> AVal.map (fun s -> Trafo3d.Scale(0.04) * s * Trafo3d.Translation(-0.95, 0.90,0.0)))

                Sg.text font C4b.Yellow text
                |> Sg.trafo (scale |> AVal.map (fun s -> Trafo3d.Scale(0.04) * s * Trafo3d.Translation(-0.954, 0.904,0.0)))
            ]
            |> Sg.viewTrafo' Trafo3d.Identity
            |> Sg.projTrafo' Trafo3d.Identity


        // ------------ layers ----------------------
        let showFirst  = toggleMode |> AVal.map (fun x -> match x with | First -> true  | Second -> false)
        let showSecond = toggleMode |> AVal.map (fun x -> match x with | First -> false | Second -> true )

        // ------------ final scene ------------------
        let scene =
            Sg.ofList [
                Sg.onOff showFirst hierarchies[0]
                Sg.onOff showSecond hierarchies[1]
                info
                ]
            |> Sg.andAlso cursor

        let sg = 
            scene //Sg.ofList hierarchies
            |> Sg.viewTrafo (view |> AVal.map CameraView.viewTrafo)
            |> Sg.projTrafo (frustum |> AVal.map Frustum.projTrafo)
            |> Sg.shader {
                do! DiffShader.generateNormal
                do! DiffShader.stableTrafo 
                do! DefaultSurfaces.constantColor C4f.White 
                do! DefaultSurfaces.diffuseTexture 
                //do! //DiffShader.LoDColor |> toEffect
                do! DiffShader.showDistances 
              
            }
            |> Sg.uniform "LodVisEnabled" lodVisEnabled
            |> Sg.uniform "ShowDistances" showDistances
            |> Sg.fillMode fillMode

        win.RenderTask <- runtime.CompileRender(win.FramebufferSignature, sg)
        win.Run()
        0