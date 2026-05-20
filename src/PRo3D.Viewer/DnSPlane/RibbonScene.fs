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
    /// Returns an array of Polyline values — one per LineString.
    /// The `isSelected` flag is read from each feature's properties object.
    let tryParseAllLineStrings (path : string) : Result<Polyline array, string> =
        try
            if not (File.Exists path) then
                Result.Error (sprintf "File not found: %s" path)
            else
                let json    = File.ReadAllText path
                let doc     = JsonDocument.Parse json
                let root    = doc.RootElement
                let results = System.Collections.Generic.List<Polyline>()
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
                            let mutable propsEl = Unchecked.defaultof<JsonElement>
                            let mutable nameEl  = Unchecked.defaultof<JsonElement>
                            let mutable selEl   = Unchecked.defaultof<JsonElement>
                            let hasProps = feat.TryGetProperty("properties", &propsEl)
                            let name =
                                if hasProps &&
                                   propsEl.TryGetProperty("name", &nameEl) &&
                                   nameEl.ValueKind = JsonValueKind.String
                                then nameEl.GetString()
                                else sprintf "Feature %d" idx
                            let isSelected =
                                hasProps &&
                                propsEl.TryGetProperty("isSelected", &selEl) &&
                                selEl.ValueKind = JsonValueKind.True
                            match extractPoints geomEl with
                            | Some pts ->
                                results.Add({ name = name; points = pts; isSelected = isSelected })
                                idx <- idx + 1
                            | None -> ()
                elif root.TryGetProperty("type", &typeEl) && typeEl.GetString() = "LineString" then
                    // Bare LineString geometry
                    match extractPoints root with
                    | Some pts -> results.Add({ name = "Feature 0"; points = pts; isSelected = false })
                    | None     -> ()

                if results.Count = 0 then
                    Result.Error "No LineString geometries found."
                else
                    Result.Ok (results |> Seq.toArray)
        with ex ->
            Result.Error (sprintf "Parse error: %s" ex.Message)

    /// Convenience: parse only the first LineString (backward compat).
    let tryParseLineString (path : string) : Result<V3d[], string> =
        tryParseAllLineStrings path |> Result.map (fun arr -> arr.[0].points)


/// Scene graph builders.
module RibbonScene =

    /// Up vector used for the strike/dip computation. Y is up in this scene.
    let private up = V3d.YAxis

    // ── private helpers ───────────────────────────────────────────────────────

    /// Orange ribbon mesh, extruded on the GPU via RibbonShaders.extrude.
    /// One independent quad per polyline segment.
    let private ribbonSg
            (frames    : RibbonAlgorithms.SegmentFrame[])
            (halfWidth : float)
            : ISg =
        if frames.Length = 0 then Sg.ofList []
        else
            let centers, dipVecs, sides, indices =
                RibbonAlgorithms.buildRibbonMesh frames

            // Per-vertex shading normal: in the plane spanned by dip and the
            // segment's tangent, perpendicular to both — i.e. the geometric
            // normal of the extruded quad.
            let normals =
                Array.init centers.Length (fun v ->
                    let s   = v / 4         // segment index (4 verts per segment)
                    let f   = frames.[s]
                    let tan = (f.p1 - f.p0).Normalized
                    Vec.cross dipVecs.[v] tan |> Vec.normalize |> V3f)

            let ribbonFill =
                IndexedGeometry(
                    Mode       = IndexedGeometryMode.TriangleList,
                    IndexArray = (indices :> System.Array),
                    IndexedAttributes =
                        SymDict.ofList [
                            DefaultSemantic.Positions, centers |> Array.map V3f :> System.Array
                            DefaultSemantic.Normals,   normals                  :> System.Array
                            Sym.ofString "DipVec",     dipVecs |> Array.map V3f :> System.Array
                            Sym.ofString "Side",       sides                    :> System.Array
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

            let lineIndices =
                Array.init ((centers.Length - 1) * 8) (fun k ->
                    let seg = k / 8;
                    let li  = 2 * seg;
                    let ri  = 2 * seg + 1
                    let li1 = 2 * seg + 2;
                    let ri1 = 2 * seg + 3
                    match k % 8 with
                    | 0 -> li  | 1 -> li1   // left edge
                    | 2 -> ri  | 3 -> ri1   // right edge
                    | 4 -> li  | 5 -> ri    // start cap
                    | 6 -> li1 | _ -> ri1)  // end cap

            let ribbonOutlines =
                IndexedGeometry(
                    Mode       = IndexedGeometryMode.LineList,
                    IndexArray = (lineIndices :> System.Array),
                    IndexedAttributes =
                        SymDict.ofList [
                            DefaultSemantic.Positions, centers |> Array.map V3f :> System.Array
                            DefaultSemantic.Normals,   normals                  :> System.Array
                            Sym.ofString "DipVec",     dipVecs |> Array.map V3f :> System.Array
                            Sym.ofString "Side",       sides                    :> System.Array
                        ]
                )
                |> Sg.ofIndexedGeometry
                |> Sg.uniform "HalfWidth" (AVal.constant halfWidth)
                |> Sg.shader {
                    do! RibbonShaders.extrude
                    do! DefaultSurfaces.constantColor C4f.Black
                    do! SharedShaders.noPick
                }

            Sg.andAlso ribbonOutlines ribbonFill

    /// Red polyline along the control points.
    let private polylineSg (points : V3d[]) : ISg =
        if points.Length < 2 then Sg.ofList []
        else
            let lineVerts =
                Array.init ((points.Length - 1) * 2) (fun i ->
                    if i % 2 = 0 then V3f points.[i / 2]
                    else              V3f points.[i / 2 + 1])
            IndexedGeometry(
                Mode = IndexedGeometryMode.LineList,
                IndexedAttributes =
                    SymDict.ofList [ DefaultSemantic.Positions, lineVerts :> System.Array ]
            )
            |> Sg.ofIndexedGeometry
            |> Sg.shader {
                do! DefaultSurfaces.stableTrafo
                do! DefaultSurfaces.constantColor C4f.Red
                do! SharedShaders.noPick
            }


    // ── public API ────────────────────────────────────────────────────────────

    /// Build the scene graph for the currently selected polyline in RibbonState.
    let build (state : RibbonState) (transform : Trafo3d option) : ISg =
        let pts = RibbonState.currentPoints state

        let ribbonParts =
            if pts.Length < 2 then []
            else
                // Pre-apply the optional transform in world space so no child
                // Sg node ever sees a double-transform.
                let worldPts =
                    match transform with
                    | None   -> pts
                    | Some t -> pts |> Array.map (fun p -> t.Forward.TransformPos p)

                let frames =
                    RibbonAlgorithms.computeSegmentFrames
                        up state.useAllPoints state.neighborCount worldPts

                [   yield ribbonSg frames state.halfWidth
                    if state.showPolyline then yield polylineSg worldPts ]

        let dnsParts =
            match state.dnSPlane with
            | Some plane when plane.isVisible -> [DnsScene.planeSg plane]
            | _ -> []

        Sg.ofList (ribbonParts @ dnsParts)   // no Sg.trafo' — already baked in
