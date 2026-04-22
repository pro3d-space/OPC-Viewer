namespace PRo3D.Viewer.Ribbon

open System.IO
open System.Text.Json
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open Aardvark.UI.Primitives
open PRo3D.Viewer.Shared

/// GeoJSON import: reads the first LineString geometry from a file.
module GeoJson =

    /// Try to read the first LineString geometry from a GeoJSON file.
    /// Handles FeatureCollection, Feature, or bare Geometry objects.
    /// Returns Ok(points) or Error(message).
    let tryParseLineString (path : string) : Result<V3d array, string> =
        try
            if not (File.Exists path) then
                Result.Error (sprintf "File not found: %s" path)
            else
                let json = File.ReadAllText path
                let doc  = JsonDocument.Parse json
                let root = doc.RootElement

                // Navigate to the first LineString geometry element.
                let findGeom (el : JsonElement) : JsonElement option =
                    let mutable featuresEl = Unchecked.defaultof<JsonElement>
                    let mutable geometryEl = Unchecked.defaultof<JsonElement>
                    let mutable typeEl     = Unchecked.defaultof<JsonElement>

                    if el.TryGetProperty("features", &featuresEl) then
                        // FeatureCollection → search features
                        featuresEl.EnumerateArray()
                        |> Seq.tryPick (fun feat ->
                            let mutable g = Unchecked.defaultof<JsonElement>
                            let mutable t = Unchecked.defaultof<JsonElement>
                            if feat.TryGetProperty("geometry", &g) then
                                if g.TryGetProperty("type", &t) && t.GetString() = "LineString"
                                then Some g
                                else None
                            else None)
                    elif el.TryGetProperty("geometry", &geometryEl) then
                        // Feature → check its geometry
                        let mutable t = Unchecked.defaultof<JsonElement>
                        if geometryEl.TryGetProperty("type", &t) && t.GetString() = "LineString"
                        then Some geometryEl
                        else None
                    elif el.TryGetProperty("type", &typeEl) && typeEl.GetString() = "LineString" then
                        // Bare geometry
                        Some el
                    else
                        None

                match findGeom root with
                | None ->
                    Result.Error "No LineString geometry found in the file."
                | Some geom ->
                    let mutable coordsEl = Unchecked.defaultof<JsonElement>
                    if not (geom.TryGetProperty("coordinates", &coordsEl)) then
                        Result.Error "LineString has no 'coordinates' property."
                    else
                        let pts =
                            coordsEl.EnumerateArray()
                            |> Seq.map (fun c ->
                                let nums =
                                    c.EnumerateArray()
                                    |> Seq.map (fun v -> v.GetDouble())
                                    |> Seq.toArray
                                let x = nums.[0]
                                let y = nums.[1]
                                let z = if nums.Length > 2 then nums.[2] else 0.0
                                V3d(x, y, z))
                            |> Seq.toArray
                        if pts.Length < 2 then
                            Result.Error (sprintf "LineString has only %d point(s); need at least 2." pts.Length)
                        else
                            Ok pts
        with ex ->
            Result.Error (sprintf "Parse error: %s" ex.Message)


/// Update function for RibbonState — pure, no side effects.
module RibbonUpdate =

    let update (model : RibbonState) (msg : RibbonMessage) =
        match msg with
        | Camera m           -> { model with cameraState   = FreeFlyController.update model.cameraState m }
        | IncreaseWidth      -> { model with halfWidth      = min 5.0 (model.halfWidth + 0.5) }
        | DecreaseWidth      -> { model with halfWidth      = max 0.5 (model.halfWidth - 0.5) }
        | TogglePolyline     -> { model with showPolyline   = not model.showPolyline }
        | ToggleNormals      -> { model with showNormals    = not model.showNormals  }
        | SetExtrusionMode m -> { model with extrusionMode  = m }
        | SetImportPath p    -> { model with importPath     = p; importError = "" }
        | ImportGeoJson      ->
            match GeoJson.tryParseLineString model.importPath with
            | Ok pts    ->
                // Compute centroid and a camera distance from the bounding extents.
                let centroid =
                    pts |> Array.fold (fun a p -> a + p) V3d.Zero
                        |> fun s -> s / float pts.Length
                let maxDist  =
                    pts |> Array.map (fun p -> (p - centroid).Length) |> Array.max
                let dist     = max 10.0 (maxDist * 3.0)
                let newView  = CameraView.lookAt (centroid + V3d(0.0, -dist, dist * 0.5)) centroid V3d.ZAxis
                { model with
                    polylinePoints = pts
                    importError    = ""
                    cameraState    = { model.cameraState with view = newView } }
            | Result.Error err -> { model with importError = err }


/// Scene graph builders — all functions return an ISg that can be composed
/// into any larger scene graph with Sg.ofList.
module RibbonScene =

    // ── private helpers ───────────────────────────────────────────────────────

    /// Orange ribbon mesh, extruded on the GPU via RibbonShaders.extrude.
    let private ribbonSg
            (mode        : ExtrusionMode)
            (controlPts  : (V3d * V3d)[])
            (halfWidth   : float)
            : ISg =
        let centers, dipVecsArr, sides, indices =
            RibbonAlgorithms.buildSurface mode controlPts
        let n = controlPts.Length

        let vertNormals =
            Array.init (2 * n) (fun i ->
                (controlPts.[i / 2] |> snd).Normalized |> V3f)

        IndexedGeometry(
            Mode       = IndexedGeometryMode.TriangleList,
            IndexArray = (indices :> System.Array),
            IndexedAttributes =
                SymDict.ofList [
                    DefaultSemantic.Positions, centers    |> Array.map V3f :> System.Array
                    DefaultSemantic.Normals,   vertNormals                  :> System.Array
                    Sym.ofString "DipVec",     dipVecsArr |> Array.map V3f :> System.Array
                    Sym.ofString "Side",       sides                        :> System.Array
                ]
        )
        |> Sg.ofIndexedGeometry
        |> Sg.uniform "HalfWidth" (AVal.constant halfWidth)
        |> Sg.shader {
            do! RibbonShaders.extrude
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.constantColor (C4f(0.80f, 0.55f, 0.28f, 1.0f))
            do! DefaultSurfaces.simpleLighting
            do! SharedShaders.noPick
        }
        |> Sg.cullMode' CullMode.None

    /// Red polyline along the control points.
    let private polylineSg (controlPts : (V3d * V3d)[]) : ISg =
        let pts = controlPts |> Array.map (fst >> V3f)
        let lineVerts =
            Array.init ((controlPts.Length - 1) * 2) (fun i ->
                if i % 2 = 0 then pts.[i / 2] else pts.[i / 2 + 1])
        IndexedGeometry(
            Mode = IndexedGeometryMode.LineList,
            IndexedAttributes =
                SymDict.ofList [ DefaultSemantic.Positions, lineVerts :> System.Array ]
        )
        |> Sg.ofIndexedGeometry
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.constantColor C4f.Red
            do! SharedShaders.noPick
        }

    /// Blue normal stubs at each control point.
    let private normalsSg (controlPts : (V3d * V3d)[]) (scale : float) : ISg =
        let lineVerts =
            controlPts
            |> Array.collect (fun (pos, n) ->
                [| V3f pos; V3f(pos + n.Normalized * scale) |])
        IndexedGeometry(
            Mode = IndexedGeometryMode.LineList,
            IndexedAttributes =
                SymDict.ofList [ DefaultSemantic.Positions, lineVerts :> System.Array ]
        )
        |> Sg.ofIndexedGeometry
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.constantColor C4f.Blue
            do! SharedShaders.noPick
        }

    /// Build the complete ribbon scene graph from the current RibbonState.
    ///
    /// The returned ISg can be composed directly with the OPC scene graph:
    ///
    ///   let scene = Sg.ofList [ opcSg; RibbonScene.build ribbonState ]
    ///
    /// Pass an optional world-space transform (e.g. to align local ribbon
    /// coordinates with planet-centred OPC coordinates).
    let build
            (state     : RibbonState)
            (transform : Trafo3d option)
            : ISg =
        if state.polylinePoints.Length < 2 then Sg.ofList []
        else
            let normals = RibbonAlgorithms.computeNormals V3d.ZAxis state.normalWindowSize state.polylinePoints
            let cps      = Array.zip state.polylinePoints normals

            let parts =
                [ yield ribbonSg state.extrusionMode cps state.halfWidth
                  if state.showPolyline then yield polylineSg cps
                  if state.showNormals  then yield normalsSg  cps 2.0 ]

            let sg = Sg.ofList parts

            match transform with
            | Some t -> sg |> Sg.trafo' t
            | None   -> sg

    /// Adaptive version: rebuilds the scene graph whenever any reactive value
    /// changes. 
    let buildAdaptive
            (mode        : aval<ExtrusionMode>)
            (points      : aval<V3d[]>)
            (halfWidth   : aval<float>)
            (showPolyline: aval<bool>)
            (showNormals : aval<bool>)
            (transform   : aval<Trafo3d option>)
            : aval<ISg> =
        adaptive {
            let! m   = mode
            let! pts = points
            let! hw  = halfWidth
            let! sp  = showPolyline
            let! sn  = showNormals
            let! t   = transform
            return
                build
                    { RibbonState.defaultState with
                        extrusionMode  = m
                        polylinePoints = pts
                        halfWidth      = hw
                        showPolyline   = sp
                        showNormals    = sn }
                    t
        }
