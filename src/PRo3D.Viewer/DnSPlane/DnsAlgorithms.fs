namespace PRo3D.Viewer.Ribbon

open Aardvark.Base
open Aardvark.Geometry

module DnsAlgorithms =

    // ── Shared geometry helpers (also used by RibbonAlgorithms) ──────────────

    /// Return -1 if the plane normal points away from `up`, +1 otherwise.
    let signedOrientation (up : V3d) (plane : Plane3d) : int =
        if Vec.dot plane.Normal up < 0.0 then -1 else 1

    let private computeStandardDeviation (avg : float) (xs : float[]) : float =
        if xs.Length = 0 then 0.0
        else
            let s = xs |> Array.sumBy (fun x -> let d = x - avg in d * d)
            sqrt (s / float xs.Length)

    let private fallbackPlane (up : V3d) (points : V3d[]) : Plane3d =
        let centroid =
            if points.Length = 0 then V3d.Zero
            else (points |> Array.fold (+) V3d.Zero) / float points.Length
        Plane3d(up.Normalized, centroid)

    /// Fit a plane through `points` using LinearRegression3d, falling back to
    /// an up-aligned plane on failure.  Logs residual statistics.
    let fitPlane (up : V3d) (points : V3d[]) : Plane3d =
        let linRegression =
            if points.Length >= 3 then
                LinearRegression3d(points).TryGetRegressionInfo()
            else
                None

        Log.line "[DnsAlgorithms] %A" linRegression

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
            Log.line "[dipandStrike]: avg %f; max %f; min %f; std: %f; sols: %f" avg mx mn std sos

        plane

    // ── DnS plane fitting ────────────────────────────────────────────────────

    /// Fit a plane through all points from selected polylines using
    /// LinearRegression3d.  Returns None when fewer than 3 points are selected.
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
