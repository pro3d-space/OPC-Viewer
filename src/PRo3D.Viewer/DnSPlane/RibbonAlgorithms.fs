namespace PRo3D.Viewer.Ribbon

open Aardvark.Base
open Aardvark.Geometry

/// Pure geometry algorithms for building the ribbon mesh data.
///
/// One algorithm only: per-segment dip-and-strike extrusion.
///
/// For each segment of the polyline (the line between two consecutive
/// vertices P_i and P_{i+1}) we
///   1. collect a window of nearby polyline points,
///   2. fit a plane through that window with LinearRegression3d,
///   3. derive the geological strike and dip vectors using up = Y:
///         strike = up × planeNormal
///         dip    = strike × planeNormal
///   4. emit a quad (P_i, P_{i+1}) extruded ±halfWidth along the dip vector.
///
/// To keep adjacent quads connected the dip vector at every interior
/// polyline vertex is the average of the two incident segment dips, so
/// neighbouring segments meet along a shared edge instead of producing
/// disjoint floating ribbons.
///
/// The vertex shader does the actual extrusion:
///   worldPos = centerPos + side * halfWidth * dipVec
/// so changing halfWidth at runtime only requires updating the uniform.
module RibbonAlgorithms =

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// Return -1 if the plane normal points away from `up`, +1 otherwise.
    /// Used to flip the plane normal so it always points roughly "upward",
    /// which makes the strike/dip cross products produce a stable orientation.
    let private signedOrientation (up : V3d) (plane : Plane3d) : int =
        if Vec.dot plane.Normal up < 0.0 then -1 else 1

    /// Standard deviation given a precomputed average. Two-pass; matches the
    /// helper referenced in the original PRo3D snippet.
    let private computeStandardDeviation (avg : float) (xs : float[]) : float =
        if xs.Length = 0 then 0.0
        else
            let s =
                xs
                |> Array.sumBy (fun x -> let d = x - avg in d * d)
            sqrt (s / float xs.Length)

    /// EVD-style plane fallback used when LinearRegression3d cannot fit a plane
    /// (e.g. fewer than 3 distinct points or co-linear input). We just return
    /// a plane passing through the centroid with the supplied up vector as its
    /// normal — good enough to keep rendering until enough points are available.
    let private fallbackPlane (up : V3d) (points : V3d[]) : Plane3d =
        let centroid =
            if points.Length = 0 then V3d.Zero
            else (points |> Array.fold (+) V3d.Zero) / float points.Length
        Plane3d(up.Normalized, centroid)

    /// Fit a plane through `points` using LinearRegression3d, fall back to an
    /// up-aligned plane on failure. Logs the residuals (avg / max / min / std /
    /// sum-of-squares), exactly matching the original PRo3D snippet.
    let private fitPlane (up : V3d) (points : V3d[]) : Plane3d =
        let linRegression =
            if points.Length >= 3 then
                LinearRegression3d(points).TryGetRegressionInfo()
            else
                None

        Log.line "[RibbonAlgorithms] %A" linRegression

        let plane =
            match linRegression with
            | Some lr -> lr.Plane
            | None ->
                Log.line "[dns computation] linear regression failed, fallback to evd"
                fallbackPlane up points

        if points.Length > 0 then
            let distances = points |> Array.map (fun x -> (plane.Height x) |> abs)
            let sos = distances |> Array.map (fun x -> x * x) |> Array.sum
            let avg = distances |> Array.average
            let mx  = distances |> Array.max
            let mn  = distances |> Array.min
            let std = distances |> computeStandardDeviation avg
            Log.line
                "[dipandStrike]: avg %f; max %f; min %f; std: %f; sols: %f"
                avg mx mn std sos

        plane

    // ── Per-segment dip / strike ─────────────────────────────────────────────

    /// Result of running the dip/strike fit for one polyline segment.
    /// To stitch adjacent quads together we let each endpoint use the
    /// average of its two incident segment dips: `dipP0` is averaged with
    /// the previous segment, `dipP1` with the next. End segments fall back
    /// to the segment's own dip on the open side.
    type SegmentFrame =
        {
            /// First polyline endpoint of the segment.
            p0     : V3d
            /// Second polyline endpoint of the segment.
            p1     : V3d
            /// Extrusion direction at p0  (averaged with the previous segment,
            /// or equal to this segment's dip for the first segment).
            dipP0  : V3d
            /// Extrusion direction at p1  (averaged with the next segment,
            /// or equal to this segment's dip for the last segment).
            dipP1  : V3d
            /// In-plane horizontal direction. Kept on the frame because the
            /// scene-graph builder uses it (together with the per-vertex dip)
            /// to derive the shading normal of the extruded quad.
            strike : V3d
        }

    /// Internal: per-segment fit result before neighbour averaging.
    type private RawSegment =
        {
            p0     : V3d
            p1     : V3d
            dip    : V3d
            strike : V3d
        }

    /// Pick the points that feed the regression for segment `i`.
    /// `i` is the index of the segment, i.e. the segment goes from
    /// `points.[i]` to `points.[i+1]`.
    let private regressionWindow
            (useAllPoints : bool)
            (neighborCount : int)
            (i : int)
            (points : V3d[])
            : V3d[] =
        if useAllPoints then
            points
        else
            let n  = points.Length
            let lo = max 0       (i     - neighborCount)
            let hi = min (n - 1) (i + 1 + neighborCount)
            points.[lo .. hi]

    /// Compute the raw (unsmoothed) dip/strike for a single segment.
    let private computeRawSegment
            (up            : V3d)
            (useAllPoints  : bool)
            (neighborCount : int)
            (points        : V3d[])
            (i             : int)
            : RawSegment =
        let p0     = points.[i]
        let p1     = points.[i + 1]
        let window = regressionWindow useAllPoints neighborCount i points

        let plane = fitPlane up window

        // Orient the plane normal so it points in the same direction as `up`.
        let planeNormal =
            match signedOrientation up plane with
            | -1 -> -plane.Normal
            | _  ->  plane.Normal

        let eps = 1e-6
        let strikeRaw = Vec.cross up planeNormal
        let strike =
            if strikeRaw.Length < eps then
                // Plane is horizontal — strike is undefined; pick any
                // horizontal axis perpendicular to `up`.
                let fallback = Vec.cross up V3d.XAxis
                if fallback.Length < eps then V3d.ZAxis.Normalized
                else fallback.Normalized
            else
                strikeRaw.Normalized

        let dipRaw = Vec.cross strike planeNormal
        let dip    =
            if dipRaw.Length < eps then V3d.XAxis
            else dipRaw.Normalized

        { p0 = p0; p1 = p1; dip = dip; strike = strike }

    /// Average two dip vectors (both unit length) and re-normalise.
    /// Falls back to `a` if the sum collapses to zero (anti-parallel dips).
    let private averageDip (a : V3d) (b : V3d) : V3d =
        let s = a + b
        if s.Length < 1e-6 then a else s.Normalized

    /// Build per-segment frames for every segment of the polyline.
    /// Adjacent segments share their endpoint dip vectors so that the
    /// extruded quads connect along their common edge: at each interior
    /// polyline vertex the two incident segments use the same averaged
    /// dip, which makes the corresponding quad corners coincide in space.
    let computeSegmentFrames
            (up            : V3d)
            (useAllPoints  : bool)
            (neighborCount : int)
            (points        : V3d[])
            : SegmentFrame[] =
        if points.Length < 2 then [||]
        else
            let segCount = points.Length - 1

            // Pass 1: compute the raw dip/strike for each segment independently.
            let raw =
                Array.init segCount
                    (computeRawSegment up useAllPoints neighborCount points)

            // Pass 2: blend dips at shared polyline vertices so adjacent
            // quads connect. End segments keep their own dip on the open side.
            Array.init segCount (fun i ->
                let r     = raw.[i]
                let dipP0 =
                    if i = 0 then r.dip
                    else averageDip raw.[i - 1].dip r.dip
                let dipP1 =
                    if i = segCount - 1 then r.dip
                    else averageDip r.dip raw.[i + 1].dip
                {
                    p0     = r.p0
                    p1     = r.p1
                    dipP0  = dipP0
                    dipP1  = dipP1
                    strike = r.strike
                })

    // ── Ribbon mesh assembly ─────────────────────────────────────────────────

    /// Build the ribbon mesh as a list of independent quads — one quad per
    /// polyline segment. Each segment carries its own dip vector, so adjacent
    /// segments do NOT share vertices.
    ///
    /// Vertex layout per segment s = 4*s + {0,1,2,3}:
    ///   4s+0 : P_s   on the left  (side = -1)
    ///   4s+1 : P_s   on the right (side = +1)
    ///   4s+2 : P_s+1 on the left  (side = -1)
    ///   4s+3 : P_s+1 on the right (side = +1)
    ///
    /// The vertex shader extrudes:
    ///   worldPos = centerPos + side * halfWidth * dipVec
    let buildRibbonMesh
            (frames : SegmentFrame[])
            : V3d[] * V3d[] * float32[] * int[] =
        let segCount = frames.Length
        let centers  = Array.zeroCreate<V3d>     (4 * segCount)
        let dips     = Array.zeroCreate<V3d>     (4 * segCount)
        let sides    = Array.zeroCreate<float32> (4 * segCount)
        let indices  = Array.zeroCreate<int>     (6 * segCount)

        for s in 0 .. segCount - 1 do
            let f = frames.[s]
            let v = 4 * s

            // p0 endpoints use the dip averaged with the previous segment;
            // p1 endpoints use the dip averaged with the next segment.
            // Adjacent segments therefore share identical extruded-corner
            // positions and the ribbon is watertight along every interior
            // polyline vertex.
            centers.[v + 0] <- f.p0;  dips.[v + 0] <- f.dipP0;  sides.[v + 0] <- -1.0f
            centers.[v + 1] <- f.p0;  dips.[v + 1] <- f.dipP0;  sides.[v + 1] <-  1.0f
            centers.[v + 2] <- f.p1;  dips.[v + 2] <- f.dipP1;  sides.[v + 2] <- -1.0f
            centers.[v + 3] <- f.p1;  dips.[v + 3] <- f.dipP1;  sides.[v + 3] <-  1.0f

            let k = 6 * s
            // Triangle 1: L0, R1, L1
            indices.[k + 0] <- v + 0
            indices.[k + 1] <- v + 3
            indices.[k + 2] <- v + 2
            // Triangle 2: L0, R0, R1
            indices.[k + 3] <- v + 0
            indices.[k + 4] <- v + 1
            indices.[k + 5] <- v + 3

        centers, dips, sides, indices

    // ── DnS plane fitting ────────────────────────────────────────────────────

    /// Fit a plane through all points from selected polylines using
    /// LinearRegression3d.  Returns None when fewer than 3 points are selected.
    /// The returned DnSPlane is immediately visible with a size equal to the
    /// maximum in-plane distance from the centre of mass — just enough to cover
    /// the selected point cloud.
    let computeDnSPlane (up : V3d) (polylines : Polyline[]) : DnSPlane option =
        let selectedPoints =
            polylines
            |> Array.filter  (fun p -> p.isSelected)
            |> Array.collect (fun p -> p.points)

        if selectedPoints.Length < 3 then None
        else
            let plane = fitPlane up selectedPoints

            let centerOfMass =
                (selectedPoints |> Array.fold (+) V3d.Zero) / float selectedPoints.Length

            let planeNormal =
                match signedOrientation up plane with
                | -1 -> -plane.Normal
                | _  ->  plane.Normal

            let eps = 1e-6
            let strikeRaw = Vec.cross up planeNormal
            let strike =
                if strikeRaw.Length < eps then
                    let fallback = Vec.cross up V3d.XAxis
                    if fallback.Length < eps then V3d.ZAxis.Normalized
                    else fallback.Normalized
                else
                    strikeRaw.Normalized

            let dipRaw = Vec.cross strike planeNormal
            let dip =
                if dipRaw.Length < eps then V3d.XAxis
                else dipRaw.Normalized

            let defaultSize =
                selectedPoints
                |> Array.map (fun p ->
                    let v = p - centerOfMass
                    (v - Vec.dot v planeNormal * planeNormal).Length)
                |> Array.max

            Some {
                isVisible       = true
                size            = defaultSize
                dipDirection    = dip
                strikeDirection = strike
                plane           = Plane3d(planeNormal, centerOfMass)
                centerOfMass    = centerOfMass
            }