namespace PRo3D.Viewer.Ribbon

open System
open Aardvark.Base
open Aardvark.Rendering
open Aardvark.SceneGraph
open FSharp.Data.Adaptive
open PRo3D.Viewer.Shared

/// Static 2D rose diagram (top-left HUD) showing the distribution of dip directions.
/// One DnS plane is fitted per selected polyline; the dip-direction azimuths are binned
/// into a circular frequency histogram.
///
/// Local space: unit circle, x = east (right), y = north (up). A dip-direction azimuth θ
/// (clockwise from north) maps to (sin θ, cos θ), so north is up and azimuth grows clockwise.
module RoseDiagram =

    let private twoPi = 2.0 * Math.PI

    /// North/east basis in the horizontal plane perpendicular to `up`.
    /// North = horizontal projection of world +Z (fallback +X when +Z ∥ up).
    let private horizontalBasis (up : V3d) : V3d * V3d =
        let u    = Vec.normalize up
        let seed = if abs (Vec.dot u V3d.ZAxis) < 0.9 then V3d.ZAxis else V3d.XAxis
        let north = Vec.normalize (seed - Vec.dot seed u * u)
        let east  = Vec.normalize (Vec.cross u north)
        north, east

    /// Dip-direction azimuths in degrees [0,360): one DnS fit per selected polyline.
    let dipAzimuths (sky : V3d) (polylines : Polyline[]) : float[] =
        let up = Vec.normalize sky
        let north, east = horizontalBasis up
        polylines
        |> Array.filter (fun pl -> pl.isSelected && pl.points.Length >= 3)
        |> Array.choose (fun pl ->
            match DnsAlgorithms.computeDnSPlane up [| { pl with isSelected = true } |] with
            | None -> None
            | Some plane ->
                let d  = plane.dipDirection
                let dh = d - Vec.dot d up * up           // project dip dir onto horizontal plane
                if dh.Length < 1e-6 then None             // (near-)vertical dip ⇒ undefined azimuth
                else
                    let a   = atan2 (Vec.dot dh east) (Vec.dot dh north)
                    let deg = (a * 180.0 / Math.PI + 360.0) % 360.0
                    Some deg)

    // ── geometry helpers (all in unit-circle local space, z = 0) ──────────────

    let private filledSg (verts : V3f[]) (color : C4b) : ISg =
        IndexedGeometry(
            Mode = IndexedGeometryMode.TriangleList,
            IndexedAttributes = SymDict.ofList [ DefaultSemantic.Positions, verts :> System.Array ])
        |> Sg.ofIndexedGeometry
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.constantColor (C4f color)
            do! SharedShaders.noPick
        }
        |> Sg.cullMode' CullMode.None
        |> Sg.blendMode' BlendMode.Blend

    let private lineSg (verts : V3f[]) (color : C4b) : ISg =
        IndexedGeometry(
            Mode = IndexedGeometryMode.LineList,
            IndexedAttributes = SymDict.ofList [ DefaultSemantic.Positions, verts :> System.Array ])
        |> Sg.ofIndexedGeometry
        |> Sg.shader {
            do! DefaultSurfaces.trafo
            do! DefaultSurfaces.constantColor (C4f color)
            do! SharedShaders.noPick
        }

    let private dir (r : float) (angleRad : float) : V3f =
        V3f(r * sin angleRad, r * cos angleRad, 0.0)

    /// Closed line loop at radius r.
    let private ringSg (r : float) (segs : int) (color : C4b) : ISg =
        let pts   = Array.init segs (fun i -> dir r (float i / float segs * twoPi))
        let verts = Array.init (segs * 2) (fun k ->
                        let i = k / 2
                        if k % 2 = 0 then pts.[i] else pts.[(i + 1) % segs])
        lineSg verts color

    /// Wedge petals: one filled sector per non-empty azimuth bin, radius ∝ frequency.
    let private petalsSg (binDeg : float) (azimuths : float[]) (color : C4b) : ISg =
        let nBins  = int (round (360.0 / binDeg))
        let counts = Array.zeroCreate<int> nBins
        for az in azimuths do
            let b = (int (floor (az / binDeg))) % nBins
            counts.[b] <- counts.[b] + 1
        let maxCount = Array.fold max 0 counts
        if maxCount = 0 then Sg.ofList []
        else
            let binRad = binDeg * Math.PI / 180.0
            let sub    = 3
            let verts  = ResizeArray<V3f>()
            for b in 0 .. nBins - 1 do
                if counts.[b] > 0 then
                    let r  = float counts.[b] / float maxCount
                    let a0 = float b * binRad
                    for s in 0 .. sub - 1 do
                        let t0 = a0 + binRad * float s / float sub
                        let t1 = a0 + binRad * float (s + 1) / float sub
                        verts.Add V3f.Zero
                        verts.Add (dir r t0)
                        verts.Add (dir r t1)
            filledSg (verts.ToArray()) color

    /// Small filled triangle just outside the ring at north, pointing outward.
    /// A geometric north reference (text labels can't write the PickIds output that
    /// the view-mode framebuffer requires, so we avoid Sg.text here).
    let private northMarkerSg (color : C4b) : ISg =
        let tip = dir 1.20 0.0
        let bL  = V3f(-0.06f, 1.04f, 0.0f)
        let bR  = V3f( 0.06f, 1.04f, 0.0f)
        filledSg [| tip; bL; bR |] color

    // ── public API ────────────────────────────────────────────────────────────

    /// Build the static rose-diagram overlay, placed in the top-left corner.
    /// Returns an empty scene graph when no selected polyline yields a dip azimuth.
    let build (sizes : aval<V2i>) (sky : V3d) (polylines : Polyline[]) : ISg =
        let azimuths = dipAzimuths sky polylines
        if azimuths.Length = 0 then Sg.ofList []
        else
            let petals = petalsSg 10.0 azimuths (C4b(230uy, 120uy, 40uy, 180uy))
            let grid =
                Sg.ofList [
                    ringSg 1.0 64 (C4b(220uy, 220uy, 220uy, 255uy))
                    ringSg 0.5 64 (C4b(150uy, 150uy, 150uy, 255uy))
                ]
            let north = northMarkerSg (C4b(230uy, 60uy, 60uy, 255uy))

            // Place top-left; aspect-correct x so the circle stays round on resize.
            let placement =
                sizes |> AVal.map (fun s ->
                    let aspect = float s.X / float s.Y
                    let radius = 0.18
                    let margin = 0.08
                    let cx = -1.0 + margin + radius / aspect
                    let cy =  1.0 - margin - radius
                    Trafo3d.Scale(V3d(radius / aspect, radius, 1.0)) * Trafo3d.Translation(cx, cy, 0.0))

            Sg.ofList [ grid; petals; north ]
            |> Sg.trafo placement
            |> Sg.viewTrafo' Trafo3d.Identity
            |> Sg.projTrafo' Trafo3d.Identity
            |> Sg.depthTest' DepthTest.None
