namespace Aardvark.Opc

open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.Base
open Aardvark.Data.Opc
open Aardvark.GeoSpatial.Opc
open Aardvark.GeoSpatial.Opc.Load
open Aardvark.Rendering
open Aardvark.SceneGraph

open FSharp.Data.Adaptive 
open MBrace.FsPickler
open PRo3D.OpcViewer

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

    type VertexWithDistance = 
        {
            [<Position>] pos : V4d
            [<Color>] c: V4d
            [<Semantic(DiffRendering.DefaultSemantic.Distances)>] distance : V3d
        }

    let showDistances (v : VertexWithDistance) = 
        fragment {
            if uniform.ShowDistances then
                return V4d(v.distance, 1.0)
            else
                return v.c
        }


module DiffViewer = 

    let run (scene : OpcScene) (initialCameraView : CameraView) (getColor : ComputeDistance) =

        Aardvark.Init()

        use app = new OpenGlApplication()
        let win = app.CreateGameWindow()
        let runtime = win.Runtime

        let runner = runtime.CreateLoadRunner 1 

        let serializer = FsPickler.CreateBinarySerializer()

        let mode = cval DistanceComputationMode.Sky

        let hierarchies = 
            scene.patchHierarchies |> Seq.toList |> List.map (fun basePath -> 
                let h = PatchHierarchy.load serializer.Pickle serializer.UnPickle (OpcPaths.OpcPaths basePath)
                DiffRendering.createSceneGraphCustom win.FramebufferSignature runner basePath h mode getColor
            )

        let speed = AVal.init scene.speed

        //let bb = scene.boundingBox
        //let initialView = CameraView.lookAt bb.Max bb.Center bb.Center.Normalized
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

        let sg = 
            Sg.ofList hierarchies
            |> Sg.viewTrafo (view |> AVal.map CameraView.viewTrafo)
            |> Sg.projTrafo (frustum |> AVal.map Frustum.projTrafo)
            |> Sg.shader {
                    do! DiffShader.generateNormal
                    do! DiffShader.stableTrafo 
                    do! DefaultSurfaces.constantColor C4f.White 
                    do! DefaultSurfaces.diffuseTexture 
                    //do! //DiffShader.LoDColor |> toEffect
                    do! DiffShader.showDistances 
                    do! DefaultSurfaces.simpleLighting
               }
            |> Sg.uniform "LodVisEnabled" lodVisEnabled
            |> Sg.uniform "ShowDistances" showDistances
            |> Sg.fillMode fillMode

        win.RenderTask <- runtime.CompileRender(win.FramebufferSignature, sg)
        win.Run()
        0