namespace Aardvark.Opc

open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.Base
open Aardvark.Data.Opc
open Aardvark.GeoSpatial.Opc
open Aardvark.GeoSpatial.Opc.Load
open Aardvark.Glfw
open Aardvark.Rendering
open Aardvark.SceneGraph
open DiffRendering
open FSharp.Data.Adaptive 
open MBrace.FsPickler

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

    type ToggleMode = 
        | First
        | Second

    let run (scene : OpcScene) (initialCameraView : CameraView) (getColor : ComputeDistance) =

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
                DiffRendering.createSceneGraphCustom win.FramebufferSignature runner basePath h mode getColor
            )

        let speed = AVal.init scene.speed

        let view = initialCameraView |> DefaultCameraController.controlWithSpeed speed win.Mouse win.Keyboard win.Time
        let frustum = win.Sizes |> AVal.map (fun s -> Frustum.perspective 60.0 scene.near scene.far (float s.X / float s.Y))

        let lodVisEnabled = cval true
        let fillMode = cval FillMode.Fill

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
        
        let showFirst  = toggleMode |> AVal.map (fun x -> match x with | First -> true  | Second -> false)
        let showSecond = toggleMode |> AVal.map (fun x -> match x with | First -> false | Second -> true )

        let scene = Sg.ofList [
            Sg.onOff showFirst hierarchies[0]
            Sg.onOff showSecond hierarchies[1]
            ]

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