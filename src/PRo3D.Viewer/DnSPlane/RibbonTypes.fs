namespace PRo3D.Viewer.Ribbon

open Aardvark.Base
open Aardvark.UI.Primitives
open Aardvark.Rendering


type Polyline =
    {
        name       : string
        points     : V3d[]
        isSelected : bool
    }

type DnSPlane =
    {
        isVisible       : bool
        size            : float
        dipDirection    : V3d
        strikeDirection : V3d
        plane           : Plane3d
        centerOfMass    : V3d
    }

/// Runtime state for a single ribbon overlay on the OPC viewer.
///
/// The ribbon is built one segment at a time: for each pair of consecutive
/// polyline points (P_i, P_{i+1}) we fit a plane through a window of
/// neighbouring polyline points using LinearRegression3d, derive the
/// geological dip and strike vectors from that plane (with up = Y), and
/// extrude the segment laterally along the dip vector in the vertex shader.
type RibbonState =
    {
        allPolylines     : Polyline[]
        selectedIndex    : int
        cameraState      : CameraControllerState
        /// Half of the lateral extrusion length (along the dip vector).
        /// Controlled at runtime with +/- keys.
        halfWidth        : float
        showPolyline     : bool
        showStrikeArrows : bool
        importPath       : string
        importError      : string
        /// If true, every segment fits its plane through ALL polyline points.
        /// If false, every segment fits its plane through the segment's two
        /// endpoints plus `neighborCount` vertices on each side.
        /// Toggled at runtime with the Left arrow key.
        useAllPoints     : bool
        /// Number of polyline vertices on each side of the segment that are
        /// included in the local linear-regression window when
        /// `useAllPoints = false`. Adjusted at runtime with Up/Down arrows.
        neighborCount    : int
        dnSPlane         : DnSPlane option
    }

module RibbonState =
    let private initialView =
        // Y is up: place the camera at (0, 10, 22) looking back at the origin.
        CameraView.lookAt (V3d(0.0, 10.0, 22.0)) V3d.Zero V3d.YAxis

    let defaultState =
        {
            allPolylines     = [||]
            selectedIndex    = 0
            cameraState      = { FreeFlyController.initial with view = initialView }
            halfWidth        = 2.0
            showPolyline     = true
            showStrikeArrows = true
            importPath       = ""
            importError      = ""
            useAllPoints     = false
            neighborCount    = 4
            dnSPlane         = None
        }

    let currentPoints (s : RibbonState) =
        if s.allPolylines.Length = 0 then [||]
        else s.allPolylines.[s.selectedIndex % s.allPolylines.Length].points

type RibbonMessage =
    | Camera               of FreeFlyController.Message
    /// Increase the lateral extrusion length (bound to '+').
    | IncreaseWidth
    /// Decrease the lateral extrusion length (bound to '-').
    | DecreaseWidth
    | TogglePolyline
    | ToggleStrikeArrows
    /// Increase the per-segment regression window (bound to ArrowUp).
    | IncreaseNeighborCount
    /// Decrease the per-segment regression window (bound to ArrowDown).
    | DecreaseNeighborCount
    /// Toggle between "all polyline points" and "local window" regression
    /// (bound to ArrowLeft).
    | ToggleUseAllPoints
    | SetImportPath        of string
    | ImportGeoJson
    /// Toggle the DnS plane on/off (bound to P). First press fits the plane.
    | ToggleDnSPlane
    /// Increase the disk radius (bound to Shift++).
    | IncreasePlaneSize
    /// Decrease the disk radius (bound to Shift+-).
    | DecreasePlaneSize
