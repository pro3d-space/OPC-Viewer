namespace PRo3D.Viewer.Ribbon

open Aardvark.Base
open Aardvark.Geometry

/// Pure geometry algorithms for building ribbon mesh data.
/// All functions return (centerPositions, dipVecs, sides, indices) ready
/// for upload to the GPU.  The vertex shader does the actual extrusion:
///   worldPos = centerPos + side * halfWidth * dipVec
module RibbonAlgorithms =

    // ── Shared helpers ────────────────────────────────────────────────────────

    /// Central-difference tangents (forward/backward at the ends).
    let computeTangents (positions : V3d[]) : V3d[] =
        let n = positions.Length
        Array.init n (fun i ->
            let raw =
                if   i = 0   then positions.[1]     - positions.[0]
                elif i = n-1 then positions.[n-1]   - positions.[n-2]
                else              positions.[i+1]   - positions.[i-1]
            raw.Normalized)

    /// Build the GPU-ready mesh arrays from per-vertex center positions and dip vectors.
    /// Vertex layout interleaves left/right: [L₀, R₀, L₁, R₁, …]
    /// The shader extrudes: worldPos = centerPos + side * halfWidth * dipVec.
    let assembleMesh
            (positions : V3d[])
            (dipVecs   : V3d[])
            : V3d[] * V3d[] * float32[] * int[] =
        let n            = positions.Length
        let centerVerts  = Array.init (2 * n) (fun i -> positions.[i / 2])
        let expandedDips = Array.init (2 * n) (fun i -> dipVecs.[i / 2])
        let sides        = Array.init (2 * n) (fun i -> if i % 2 = 0 then -1.0f else 1.0f)
        let indices =
            Array.init ((n - 1) * 6) (fun k ->
                let seg = k / 6
                let li  = 2 * seg
                let ri  = 2 * seg + 1
                let li1 = 2 * seg + 2
                let ri1 = 2 * seg + 3
                match k % 6 with
                | 0 -> li  | 1 -> ri1 | 2 -> li1
                | 3 -> li  | 4 -> ri  | _ -> ri1)
        centerVerts, expandedDips, sides, indices

    // ── Normal estimation ─────────────────────────────────────────────────────

    /// For each vertex i, fit a plane through a sliding window of 'windowSize' points.
    /// Returns the plane normal oriented toward 'up'.
    let computeNormals (up : V3d) (slidingWindow : int) (points : V3d[]) : V3d[] =
        let n = points.Length
        Array.init n (fun i ->
            let hi     = min (n - 1) (i + slidingWindow)
            let lo     = max 0 (min (i - 1) (hi - slidingWindow))
            let window = points.[lo .. hi]

            let normal =
                if window.Length >= 3 then
                    match LinearRegression3d(window).TryGetRegressionInfo() with
                    | Some lr when lr.Plane.Normal.Length > 1e-6 -> lr.Plane.Normal
                    | _ -> up
                else
                    up

            if Vec.dot normal up < 0.0 then -normal else normal
        )

    /// Compute the geological dip vector from a plane normal and up vector.
    /// This matches the PRo3D definition:
    ///   strike = up × planeNormal
    ///   dip    = strike × planeNormal
    /// The dip vector lies within the geological plane, perpendicular to strike,
    /// pointing in the direction of steepest descent.
    let computeDipVec (up : V3d) (planeNormal : V3d) : V3d =
        let eps = 1e-6
        // Orient plane normal toward up
        let nn = if Vec.dot planeNormal up < 0.0 then -planeNormal else planeNormal
        // Strike: horizontal intersection of geological plane with horizontal plane
        let strike = Vec.cross up nn
        if strike.Length < eps then
            // Plane is horizontal — dip is undefined, fall back to world X
            V3d.XAxis
        else
            let strike = strike.Normalized
            // Dip: steepest descent direction within the plane
            Vec.cross strike nn |> Vec.normalize

    // ── Mode 1: Basic ─────────────────────────────────────────────────────────

    /// Geological dip/strike surface.
    /// dipVec = strike × planeNormal, where strike = up × planeNormal.
    /// The tangent along the polyline is NOT used — dip is purely geometric.
    let buildDipStrikeSurface
            (up            : V3d)
            (controlPoints : (V3d * V3d)[])
            : V3d[] * V3d[] * float32[] * int[] =
        if controlPoints.Length < 2 then [||], [||], [||], [||]
        else
        let positions = controlPoints |> Array.map fst
        let normals   = controlPoints |> Array.map (snd >> Vec.normalize)

        let dipVecs =
            Array.init controlPoints.Length (fun i ->
                computeDipVec up normals.[i])

        assembleMesh positions dipVecs

    // ── Mode 2: Stabilized ───────────────────────────────────────────────────

    /// Same as Basic but applies a sign-continuity pass to prevent the dipVec
    /// from flipping when the plane normal varies along the polyline.
    let buildStabilizedSurface
            (up            : V3d)
            (controlPoints : (V3d * V3d)[])
            : V3d[] * V3d[] * float32[] * int[] =
        if controlPoints.Length < 2 then [||], [||], [||], [||]
        else
        let positions = controlPoints |> Array.map fst
        let normals   = controlPoints |> Array.map (snd >> Vec.normalize)

        let dipVecs =
            Array.init controlPoints.Length (fun i ->
                computeDipVec up normals.[i])

        // Sign-continuity pass: flip if consecutive dipVecs point opposite ways
        for i in 1 .. dipVecs.Length - 1 do
            if Vec.dot dipVecs.[i] dipVecs.[i-1] < 0.0 then
                dipVecs.[i] <- -dipVecs.[i]

        assembleMesh positions dipVecs

    // ── Mode 3: Frenet-Serret with torsion compensation ──────────────────────

    /// Uses the Frenet binormal B = normalize(T × dT/ds) as the extrusion direction.
    /// Sign continuity is enforced to prevent spinning at inflection points.
    let buildFrenetSerretSurface
            (controlPoints : (V3d * V3d)[])
            : V3d[] * V3d[] * float32[] * int[] =
        if controlPoints.Length < 2 then [||], [||], [||], [||]
        else
        let n         = controlPoints.Length
        let positions = controlPoints |> Array.map fst
        let tangents  = computeTangents positions
        let eps       = 1e-6

        let t0    = tangents.[0]
        let seed0 =
            let seed = if abs (Vec.dot t0 V3d.ZAxis) < 0.9 then V3d.ZAxis else V3d.YAxis
            let proj = seed - t0 * Vec.dot seed t0
            if proj.Length < eps then V3d.YAxis else proj.Normalized

        let frenetB (i : int) (fallback : V3d) : V3d =
            let t  = tangents.[i]
            let dT =
                if i = 0     then tangents.[1]     - tangents.[0]
                elif i = n-1 then tangents.[n-1]   - tangents.[n-2]
                else              tangents.[i+1]   - tangents.[i-1]
            if dT.Length < eps then
                let proj = fallback - t * Vec.dot fallback t
                if proj.Length < eps then fallback else proj.Normalized
            else
                let cross = Vec.cross t dT.Normalized
                if cross.Length < eps then
                    let proj = fallback - t * Vec.dot fallback t
                    if proj.Length < eps then fallback else proj.Normalized
                else cross.Normalized

        let dipVecs   = Array.zeroCreate n
        let mutable lastB = frenetB 0 seed0
        dipVecs.[0] <- lastB

        for i in 1 .. n-1 do
            let b  = frenetB i lastB
            let bc = if Vec.dot b lastB < 0.0 then -b else b
            dipVecs.[i] <- bc
            lastB       <- bc

        assembleMesh positions dipVecs

    // ── Mode 4: Bishop Frame (parallel transport) ────────────────────────────

    /// Initialises one perpendicular frame vector and parallel-transports it
    /// along the curve by projecting out the new tangent component at each step.
    let buildBishopSurface
            (controlPoints : (V3d * V3d)[])
            : V3d[] * V3d[] * float32[] * int[] =
        if controlPoints.Length < 2 then [||], [||], [||], [||]
        else
        let n         = controlPoints.Length
        let positions = controlPoints |> Array.map fst
        let tangents  = computeTangents positions
        let eps       = 1e-6

        let t0   = tangents.[0]
        let seed = if abs (Vec.dot t0 V3d.ZAxis) < 0.9 then V3d.ZAxis else V3d.YAxis
        let u0   =
            let proj = seed - t0 * Vec.dot seed t0
            if proj.Length < eps then
                let seed2 = if abs (Vec.dot t0 V3d.YAxis) < 0.9 then V3d.YAxis else V3d.XAxis
                let proj2 = seed2 - t0 * Vec.dot seed2 t0
                if proj2.Length < eps then seed2 else proj2.Normalized
            else proj.Normalized

        let dipVecs = Array.zeroCreate n
        dipVecs.[0] <- u0

        for i in 1 .. n-1 do
            let t    = tangents.[i]
            let prev = dipVecs.[i-1]
            let proj = prev - t * Vec.dot prev t
            dipVecs.[i] <- if proj.Length < eps then prev else proj.Normalized

        assembleMesh positions dipVecs

    // ── Mode 5: RMF – Rotation Minimizing Frame (double-reflection) ──────────

    /// Wang et al. 2008: uses two successive reflections to propagate the frame
    /// with minimal rotation per step.
    let buildRMFSurface
            (controlPoints : (V3d * V3d)[])
            : V3d[] * V3d[] * float32[] * int[] =
        if controlPoints.Length < 2 then [||], [||], [||], [||]
        else
        let n         = controlPoints.Length
        let positions = controlPoints |> Array.map fst
        let tangents  = computeTangents positions
        let eps       = 1e-6

        let t0   = tangents.[0]
        let seed = if abs (Vec.dot t0 V3d.ZAxis) < 0.9 then V3d.ZAxis else V3d.YAxis
        let u0   =
            let proj = seed - t0 * Vec.dot seed t0
            if proj.Length < eps then
                let seed2 = if abs (Vec.dot t0 V3d.YAxis) < 0.9 then V3d.YAxis else V3d.XAxis
                let proj2 = seed2 - t0 * Vec.dot seed2 t0
                if proj2.Length < eps then seed2 else proj2.Normalized
            else proj.Normalized

        let dipVecs = Array.zeroCreate n
        dipVecs.[0] <- u0

        for i in 0 .. n-2 do
            let r_i = dipVecs.[i]
            let t_i = tangents.[i]
            let t_n = tangents.[i+1]

            let v1   = positions.[i+1] - positions.[i]
            let v1sq = Vec.dot v1 v1
            let r_L, t_L =
                if v1sq < eps then r_i, t_i
                else
                    let c_r = 2.0 * Vec.dot v1 r_i / v1sq
                    let c_t = 2.0 * Vec.dot v1 t_i / v1sq
                    r_i - c_r * v1, t_i - c_t * v1

            let v2   = t_n - t_L
            let v2sq = Vec.dot v2 v2
            let r_n  =
                if v2sq < eps then r_L
                else
                    let c2 = 2.0 * Vec.dot v2 r_L / v2sq
                    r_L - c2 * v2

            dipVecs.[i+1] <- if r_n.Length < eps then r_L else r_n.Normalized

        assembleMesh positions dipVecs

    // ── Dispatch ──────────────────────────────────────────────────────────────

    let buildSurface
            (up            : V3d)
            (mode          : ExtrusionMode)
            (controlPoints : (V3d * V3d)[])
            : V3d[] * V3d[] * float32[] * int[] =
        match mode with
        | Basic        -> buildDipStrikeSurface   up controlPoints
        | Stabilized   -> buildStabilizedSurface  up controlPoints
        | FrenetSerret -> buildFrenetSerretSurface    controlPoints
        | Bishop       -> buildBishopSurface          controlPoints
        | RMF          -> buildRMFSurface              controlPoints
