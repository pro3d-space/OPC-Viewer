namespace PRo3D.Viewer.Ribbon

open System.IO
open System.Text.Json
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open PRo3D.Viewer.Shared

/// GeoJSON import.
module GeoJson =

    /// Parse coordinates from a LineString geometry element.
    let private extractPoints (geom : JsonElement) : V3d[] option =
        let mutable coordsEl = Unchecked.defaultof<JsonElement>
        if not (geom.TryGetProperty("coordinates", &coordsEl)) then None
        else
            let pts =
                coordsEl.EnumerateArray()
                |> Seq.map (fun c ->
                    let nums =
                        c.EnumerateArray()
                        |> Seq.map (fun v -> v.GetDouble())
                        |> Seq.toArray
                    V3d(nums.[0], nums.[1], if nums.Length > 2 then nums.[2] else 0.0))
                |> Seq.toArray
            if pts.Length < 2 then None else Some pts

    /// Parse all LineString features from a GeoJSON file.
    /// Returns an array of (name, points) pairs — one per LineString.
    let tryParseAllLineStrings (path : string) : Result<(string * V3d[]) array, string> =
        try
            if not (File.Exists path) then
                Result.Error (sprintf "File not found: %s" path)
            else
                let json    = File.ReadAllText path
                let doc     = JsonDocument.Parse json
                let root    = doc.RootElement
                let results = System.Collections.Generic.List<string * V3d[]>()
                let mutable idx = 0

                let mutable featuresEl = Unchecked.defaultof<JsonElement>
                let mutable typeEl     = Unchecked.defaultof<JsonElement>

                if root.TryGetProperty("features", &featuresEl) then
                    // FeatureCollection → iterate all features
                    for feat in featuresEl.EnumerateArray() do
                        let mutable geomEl = Unchecked.defaultof<JsonElement>
                        let mutable tEl    = Unchecked.defaultof<JsonElement>
                        if feat.TryGetProperty("geometry", &geomEl) &&
                           geomEl.TryGetProperty("type", &tEl) &&
                           tEl.GetString() = "LineString" then
                            // Try to get name from properties
                            let mutable propsEl = Unchecked.defaultof<JsonElement>
                            let mutable nameEl  = Unchecked.defaultof<JsonElement>
                            let name =
                                if feat.TryGetProperty("properties", &propsEl) &&
                                   propsEl.TryGetProperty("name", &nameEl) &&
                                   nameEl.ValueKind = JsonValueKind.String
                                then nameEl.GetString()
                                else sprintf "Feature %d" idx
                            match extractPoints geomEl with
                            | Some pts -> results.Add(name, pts); idx <- idx + 1
                            | None     -> ()
                elif root.TryGetProperty("type", &typeEl) && typeEl.GetString() = "LineString" then
                    // Bare LineString geometry
                    match extractPoints root with
                    | Some pts -> results.Add("Feature 0", pts)
                    | None     -> ()

                if results.Count = 0 then
                    Result.Error "No LineString geometries found."
                else
                    Result.Ok (results |> Seq.toArray)
        with ex ->
            Result.Error (sprintf "Parse error: %s" ex.Message)

    /// Convenience: parse only the first LineString (backward compat).
    let tryParseLineString (path : string) : Result<V3d[], string> =
        tryParseAllLineStrings path |> Result.map (fun arr -> snd arr.[0])


/// Scene graph builders.
module RibbonScene =

    // ── private helpers ───────────────────────────────────────────────────────

    /// Orange ribbon mesh, extruded on the GPU via RibbonShaders.extrude.
    let private ribbonSg
            (up         : V3d)
            (mode       : ExtrusionMode)
            (controlPts : (V3d * V3d)[])
            (halfWidth  : float)
            : ISg =
        let centers, dipVecsArr, sides, indices =
            RibbonAlgorithms.buildSurface up mode controlPts
        let n = controlPts.Length

        let tangents = RibbonAlgorithms.computeTangents (controlPts |> Array.map fst)
        let vertNormals =
            Array.init (2 * n) (fun i ->
                let d = dipVecsArr.[i / 2]
                let t = tangents.[i / 2]
                Vec.cross d t |> Vec.normalize |> V3f)

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
            do! DefaultSurfaces.constantColor (C4f(0.80f, 0.55f, 0.28f, 1.0f))
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

    // ── public API ────────────────────────────────────────────────────────────

    /// Build the scene graph for the currently selected polyline in RibbonState.
    let build (state : RibbonState) (transform : Trafo3d option) : ISg =
        let pts = RibbonState.currentPoints state
        if pts.Length < 2 then Sg.ofList []
        else
            let up      = V3d.ZAxis   // or pass from config/sky if needed
            let normals = RibbonAlgorithms.computeNormals up state.normalWindowSize pts
            let cps     = Array.zip pts normals

            let parts =
                [ yield ribbonSg up state.extrusionMode cps state.halfWidth
                  if state.showPolyline then yield polylineSg cps
                  if state.showNormals  then yield normalsSg  cps 2.0 ]

            let sg = Sg.ofList parts
            match transform with
            | Some t -> sg |> Sg.trafo' t
            | None   -> sg
