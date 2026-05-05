namespace PRo3D.Viewer.Ribbon

open Aardvark.Base
open Aardvark.UI.Primitives
open Aardvark.Rendering


/// How the lateral (dip) direction of the extruded ribbon is computed.
type ExtrusionMode =
    | Basic         // tangent × geological-normal  (original, may twist)
    | Stabilized    // same but with sign-continuity to prevent flips
    | FrenetSerret  // curvature binormal B = T × dT/ds, with sign-continuity
    | Bishop        // parallel transport via project-and-normalize
    | RMF           // parallel transport via double-reflection (Wang et al. 2008)

/// Runtime state for a single ribbon overlay on the OPC viewer.
type RibbonState =
    {
        allPolylines     : V3d[][] 
        selectedIndex    : int
        cameraState    : CameraControllerState
        halfWidth      : float
        showPolyline   : bool
        showNormals    : bool
        extrusionMode  : ExtrusionMode
        importPath     : string
        importError    : string
        normalWindowSize : int
    }

module RibbonState =
    let private initialView =
        CameraView.lookAt (V3d(0.0, -22.0, 10.0)) V3d.Zero V3d.ZAxis

    let defaultState =
        {
            allPolylines     = [||]
            selectedIndex    = 0
            cameraState    = { FreeFlyController.initial with view = initialView }
            halfWidth      = 2.0
            extrusionMode  = Basic
            showPolyline   = true
            showNormals    = true
            importPath     = ""
            importError    = ""
            normalWindowSize = 4
        }

    let currentPoints (s : RibbonState) =
        if s.allPolylines.Length = 0 then [||]
        else s.allPolylines.[s.selectedIndex % s.allPolylines.Length]

type RibbonMessage =
    | Camera           of FreeFlyController.Message
    | IncreaseWidth
    | DecreaseWidth
    | TogglePolyline
    | ToggleNormals
    | SetExtrusionMode of ExtrusionMode
    | SetImportPath    of string
    | ImportGeoJson
