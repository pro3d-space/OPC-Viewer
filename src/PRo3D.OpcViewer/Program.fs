open System

open System.IO
open Aardvark.Base
open Aardvark.GeoSpatial.Opc
open Aardvark.Opc

[<EntryPoint>]
let main argv =

    // do proper arg parsing here...
    let pathToVictoricaCrater = 
        let env = Environment.GetEnvironmentVariable("pro3d-data")
        if env.IsNullOrEmpty() then
            failwith "set pro3d-data environment variable"
        else
            let path = Path.Combine(env, "VictoriaCrater")
            if Directory.Exists path then
                path
            else 
                failwith "victory crater dataset not found"

    let scene =
        { 
            useCompressedTextures = true
            preTransform     = Trafo3d.Identity
            patchHierarchies = 
                    Seq.delay (fun _ -> 
                        System.IO.Directory.GetDirectories(pathToVictoricaCrater) 
                        |> Seq.collect System.IO.Directory.GetDirectories
                    )
            boundingBox      = Box3d.Parse("[[3376119.134144473, -327481.437907507, -122246.526319265], [3376413.794832183, -325447.648562717, -119523.964585745]]") 
            near             = 0.1
            far              = 10000.0
            speed            = 5.0
            lodDecider       = DefaultMetrics.mars2 
        }


    OpcViewer.run scene