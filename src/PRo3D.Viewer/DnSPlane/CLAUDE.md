New feature request:

in the file ribbonScene.fs i am processing geojson files. 
the features in my geojson files have a isSelected tag:
  "properties": {
  "isSelected": true
  }

i wanna save this tag. i need a feature which creates a plane, using LinearRegression, analogues to this code snippet:

"""
            let linRegression = LinearRegression3d(points).TryGetRegressionInfo()

            Log.line "[AnnotationHelpers.fs] %A" linRegression

            let plane = 
                match linRegression with
                | Some lr -> lr.Plane
                | None ->
                    Log.line "[dns computation] linear regression failed, fallback to evd"
                    PlaneFitting.planeFit(points)
    
            let distances = 
                points 
                |> Array.map(fun x -> (plane.Height x).Abs())
    
            let sos = distances |> Array.map (fun x -> x * x) |> Array.sum /// (float distances.Length)
            
            let avg = distances |> Array.average
            let max = distances |> Array.max
            let min = distances |> Array.min
         
            let std = distances |> computeStandardDeviation avg
    
            Log.line("[dipandStrike]: avg %f; max %f; min %f; std: %f; sols: %f") avg max min std sos
            
            //correct plane orientation - check if normals point in same direction           
            let planeNormal = 
                match signedOrientation up plane with
                | -1 -> -plane.Normal
                | _  -> plane.Normal
            
            //strike
            let strike = up.Cross(planeNormal).Normalized
    
            //dip vector 
            let dip = strike.Cross(planeNormal).Normalized
"""

the points used for the linear regression should be the points belonging to all the polylines that are flagged with "isSelected". the plane should be turned on and off using the key "P". and it should be possible to increase / decrease the plane size (it should be rendered as a disk) using shift+ or shift-.