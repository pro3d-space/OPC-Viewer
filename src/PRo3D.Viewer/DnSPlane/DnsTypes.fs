namespace PRo3D.Viewer.Ribbon

open Aardvark.Base
open Aardvark.Rendering

type DnSPlane =
    {
        isVisible       : bool
        size            : float
        dipDirection    : V3d
        strikeDirection : V3d
        plane           : Plane3d
        centerOfMass    : V3d
    }
