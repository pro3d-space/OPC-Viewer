namespace PRo3D.Viewer.Shared

/// Constants used throughout the rendering system
module RenderingConstants =
    
    /// Default field of view for camera frustum
    [<Literal>]
    let DEFAULT_FOV = 60.0
    
    /// Speed multiplier for PageUp/PageDown keys
    [<Literal>]
    let SPEED_MULTIPLIER = 1.5
    
    /// Default speed calculation divisor (speed = far / divisor)
    [<Literal>]
    let DEFAULT_SPEED_DIVISOR = 64.0
    
    /// Default gamma value for color correction
    [<Literal>]
    let DEFAULT_GAMMA = 1.0
    
    /// RGB to grayscale conversion coefficients (ITU-R BT.709)
    let RGB_TO_GRAYSCALE_R = 0.2126
    let RGB_TO_GRAYSCALE_G = 0.7152
    let RGB_TO_GRAYSCALE_B = 0.0722
    
    /// Default cursor sizes
    [<Literal>]
    let DEFAULT_CURSOR_SIZE = 0.5
    
    [<Literal>]
    let DIFF_CURSOR_SIZE = 0.002
    
    /// UI text positioning and scaling
    [<Literal>]
    let TEXT_SCALE = 0.04