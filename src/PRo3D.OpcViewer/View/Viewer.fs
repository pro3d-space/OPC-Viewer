namespace Aardvark.Opc

open Aardvark.Application
open Aardvark.Application.Slim
open Aardvark.Base
open Aardvark.Data.Opc
open Aardvark.GeoSpatial.Opc
open Aardvark.Rendering
open Aardvark.SceneGraph

open FSharp.Data.Adaptive 
open MBrace.FsPickler

open Aardvark.GeoSpatial.Opc.Load
open OpcRendering

[<AutoOpen>]
module Shader =

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

    type PickBuffer = 
        {
            [<Semantic("PickIds")>] id : int
        }

    type UniformScope with  
        member x.PatchId : int = uniform?PatchId

    let encodePickIds (v : Vertex) = 
        fragment {
            return { id = uniform.PatchId }
        }

    let noPick (v : Vertex) = 
        fragment {
            return { id = -1 }
        }

    type VertexWithDistance = 
        {
            [<Position>] pos : V4d
            [<Semantic(OpcRendering.DefaultSemantic.Distances)>] distance : V3d
        }

    let showDistances (v : VertexWithDistance) = 
        fragment {
            return V4d(v.distance, 1.0)
        }


module OpcViewer = 
    

    let run (scene : OpcScene) (initialCameraView : CameraView) =

        Aardvark.Init()

        use app = new OpenGlApplication()
        let win = app.CreateGameWindow()
        let runtime = win.Runtime

        let runner = runtime.CreateLoadRunner 2 

        let serializer = FsPickler.CreateBinarySerializer()


        // for identifying pixel picks
        let infoTable = PatchInfoTable()
        let pickIdSym = Sym.ofString "PickIds"
        let framebufferSignature = 
            runtime.CreateFramebufferSignature [
                DefaultSemantic.Colors, TextureFormat.Rgba8
                DefaultSemantic.DepthStencil, TextureFormat.Depth24Stencil8
                pickIdSym, TextureFormat.R32i
            ]


        let hierarchies = 
            scene.patchHierarchies |> Seq.toList |> List.map (fun basePath -> 
                let h = PatchHierarchy.load serializer.Pickle serializer.UnPickle (OpcPaths.OpcPaths basePath)
                OpcRendering.createSceneGraphCustom framebufferSignature runner infoTable basePath h
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

        let cursorPos = AVal.init V3d.Zero
        let cursor = 
            Box3d.FromCenterAndSize(V3d.Zero, V3d.III * 0.5)
            |> Sg.box' C4b.White 
            |> Sg.trafo (cursorPos |> AVal.map Trafo3d.Translation)
            |> Sg.shader {
                do! DefaultSurfaces.stableTrafo
                do! Shader.noPick
            }

        let scene = 
            Sg.ofList hierarchies
            |> Sg.andAlso cursor
            |> Sg.viewTrafo (view |> AVal.map CameraView.viewTrafo)
            |> Sg.projTrafo (frustum |> AVal.map Frustum.projTrafo)
            |> Sg.shader {
                    do! Shader.stableTrafo
                    do! DefaultSurfaces.constantColor C4f.White 
                    do! DefaultSurfaces.diffuseTexture 
                    do! Shader.LoDColor
                    do! Shader.encodePickIds
                }
            |> Sg.uniform "LodVisEnabled" lodVisEnabled
            |> Sg.fillMode fillMode

       

        let offscreenBuffer = 
            let c = clear { colors [DefaultSemantic.Colors, C4f.DarkGray;]; depth 1.0;  }
            let output = Set.ofList [ DefaultSemantic.Colors; pickIdSym; DefaultSemantic.DepthStencil ]
            scene
            |> Sg.compile win.Runtime framebufferSignature
            |> RenderTask.renderSemanticsWithClear output win.Sizes c
          

        win.Mouse.Move.Values.Add(fun (_, p) -> 

            let pos = V2i(p.Position.X, p.Position.Y)
            let region = Box2i.FromMinAndSize(pos, V2i.II)

            if win.FramebufferSize.AllGreater(pos) then

                // get current view / proj
                let view = view.GetValue()
                let frustum = frustum.GetValue()

                // get depth values
                let renderedDepth = offscreenBuffer.[DefaultSemantic.DepthStencil].GetValue()
                let depth = renderedDepth.DownloadDepth(region = region)

                // get picked object
                let pickIds = runtime.Download(offscreenBuffer.[pickIdSym].GetValue(), region = region) |> unbox<PixImage<int32>>
                let pickId = pickIds.GetChannel(0L)[0,0]
                let object = infoTable.LookupLinear(pickId) 

                match object with
                | None -> 
                    ()
                | Some object -> 
                    Log.line "hit: %A" object.Name
                    // unproject
                    let d = depth[0,0] |> float
                    let viewProj = CameraView.viewTrafo view *  Frustum.projTrafo frustum
                    // window coordinates and ndc flip in Y
                    let ndc = V3d(V2d(p.NormalizedPosition.X, 1.0 - p.NormalizedPosition.Y) * 2.0 - V2d.II, d * 2.0 - 1.0)
                    // compute world space position
                    let wp = viewProj.Backward.TransformPosProj(ndc)
                    Log.line $"viewpos: {p}" 
                    let localPos = object.Local2Global.Backward.TransformPos(wp)
                    Log.line $"localpos: {localPos}"

                    transact (fun _ -> 
                        cursorPos.Value <- wp
                    )
                
        )
        
        let fullScreenPass = 
            Sg.fullScreenQuad
            |> Sg.diffuseTexture offscreenBuffer.[DefaultSemantic.Colors]
            |> Sg.shader {
                do! DefaultSurfaces.diffuseTexture
            }

        win.RenderTask <- runtime.CompileRender(win.FramebufferSignature, fullScreenPass)
        win.Run()
        0