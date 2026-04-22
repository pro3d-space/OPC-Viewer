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

    // ── Mode 1: Basic ─────────────────────────────────────────────────────────

    /// Build a ribbon mesh from (worldPos, dipStrikePlaneNormal) control points.
    /// dipVec = normalize(tangent × normal); falls back to tangent × Z or × Y.
    let buildDipStrikeSurface
            (controlPoints : (V3d * V3d)[])
            : V3d[] * V3d[] * float32[] * int[] =
        if controlPoints.Length < 2 then [||], [||], [||], [||]
        else
        let n         = controlPoints.Length
        let positions = controlPoints |> Array.map fst
        let normals   = controlPoints |> Array.map (snd >> Vec.normalize)
        let tangents  = computeTangents positions
        let eps       = 1e-6

        let dipVecs =
            Array.init n (fun i ->
                let t  = tangents.[i]
                let nn = normals.[i]
                let c  = Vec.cross t nn
                if c.Length < eps then
                    let fb =
                        if abs (Vec.dot t V3d.ZAxis) < 0.9
                        then Vec.cross t V3d.ZAxis
                        else Vec.cross t V3d.YAxis
                    fb.Normalized
                else c.Normalized)

        assembleMesh positions dipVecs

    // ── Mode 2: Stabilized ───────────────────────────────────────────────────

    /// Same as Basic but applies a sign-continuity pass to prevent the dipVec
    /// from flipping abruptly when the tangent rotates past the plane normal.
    let buildStabilizedSurface
            (controlPoints : (V3d * V3d)[])
            : V3d[] * V3d[] * float32[] * int[] =
        if controlPoints.Length < 2 then [||], [||], [||], [||]
        else
        let n         = controlPoints.Length
        let positions = controlPoints |> Array.map fst
        let normals   = controlPoints |> Array.map (snd >> Vec.normalize)
        let tangents  = computeTangents positions
        let eps       = 1e-6

        let dipVecs =
            Array.init n (fun i ->
                let t  = tangents.[i]
                let nn = normals.[i]
                let c  = Vec.cross t nn
                if c.Length < eps then
                    let fb =
                        if abs (Vec.dot t V3d.ZAxis) < 0.9
                        then Vec.cross t V3d.ZAxis
                        else Vec.cross t V3d.YAxis
                    fb.Normalized
                else c.Normalized)

        // Sign-continuity pass: flip if consecutive dipVecs point opposite ways.
        for i in 1 .. n-1 do
            if Vec.dot dipVecs.[i] dipVecs.[i-1] < 0.0 then
                dipVecs.[i] <- -dipVecs.[i]

        assembleMesh positions dipVecs

    // ── Mode 3: Frenet-Serret with torsion compensation ──────────────────────

    /// Uses the Frenet binormal B = normalize(T × dT/ds) as the extrusion direction.
    /// Sign continuity is enforced to prevent spinning at inflection points.
    /// Degenerate (zero-curvature) segments fall back to parallel-transporting
    /// the previous B.
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
                if i = 0 then   tangents.[1] - tangents.[0]
                elif i = n-1 then tangents.[n-1] - tangents.[n-2]
                else            tangents.[i+1] - tangents.[i-1]
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
    /// Never introduces torsion regardless of curve shape.
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
    /// with minimal rotation per step.  More accurate than Bishop for large step
    /// sizes; identical in the limit of small steps.
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

            // First reflection: across perpendicular bisector of chord P[i+1]−P[i]
            let v1   = positions.[i+1] - positions.[i]
            let v1sq = Vec.dot v1 v1
            let r_L, t_L =
                if v1sq < eps then r_i, t_i
                else
                    let c_r = 2.0 * Vec.dot v1 r_i / v1sq
                    let c_t = 2.0 * Vec.dot v1 t_i / v1sq
                    r_i - c_r * v1, t_i - c_t * v1

            // Second reflection: across bisector of t_L and t_n
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
            (mode          : ExtrusionMode)
            (controlPoints : (V3d * V3d)[])
            : V3d[] * V3d[] * float32[] * int[] =
        match mode with
        | Basic        -> buildDipStrikeSurface   controlPoints
        | Stabilized   -> buildStabilizedSurface  controlPoints
        | FrenetSerret -> buildFrenetSerretSurface controlPoints
        | Bishop       -> buildBishopSurface       controlPoints
        | RMF          -> buildRMFSurface          controlPoints

    // ── Normal estimation ─────────────────────────────────────────────────────

    /// For each vertex i, fit a plane through the sliding window [i-1, i, i+1, i+2]
    /// (clamped to array bounds, always at least 3 points).
    /// Uses LinearRegression3d; falls back to a zero plane on failure.
    /// The returned normal is oriented toward 'up'.
    let computeNormals (up : V3d) (points : V3d[]) : V3d[] =
        let n = points.Length
        Array.init n (fun i ->
            let hi     = min (n - 1) (i + 2)
            let lo     = max 0 (min (i - 1) (hi - 2))
            let window = points.[lo .. hi]

            let plane =
                if window.Length >= 3 then
                    match LinearRegression3d(window).TryGetRegressionInfo() with
                    | Some lr -> lr.Plane
                    | None    -> Plane3d()
                else
                    Plane3d()

            if Vec.dot plane.Normal up < 0.0 then -plane.Normal else plane.Normal)
