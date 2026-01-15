using System;

namespace XDPaint.Core
{
    public enum ObjectComponentType
    {
        Unknown,
        RawImage,
        MeshFilter,
        SkinnedMeshRenderer,
        SpriteRenderer
    }

    public enum PaintPass
    {
        Paint = 0,
        Blend = 1,
        Erase = 2,
        Preview = 3
    }
    
    public enum PaintWorldPass
    {
        Unwrap = 0,
        Draw = 1,
        IslandEdges = 2
    }
    
    public enum PaintTool
    {
        Brush,
        Erase,
        Eyedropper,
        BrushSampler,
        Clone,
        Blur,
        BlurGaussian,
        Grayscale,
        Bucket
    }

    public enum PaintRenderTexture
    {
        PaintTexture,
        CombinedTexture
    }

    public enum PaintSpace
    {
        UV,
        World
    }

    public enum ProjectionMethod
    {
        Planar,
        Triplanar
    }
    
    public enum ProjectionDirection
    {
        Normal,
        Ray
    }

    public enum AngleAttenuationMode
    {
        Soft,
        Hard
    }

    public enum PaintMode
    {
        Default = 0x0,
        Additive = 0x100
    }
    
    public enum RenderTarget
    {
        ActiveLayer = 0x100,
        ActiveLayerTemp = 0x150,
        Input = 0x200,
        Combined = 0x300,
        CombinedTemp = 0x350,
        Unwrapped = 0x400
    }

    public enum BlendingMode
    {
        Normal,
        
        Darken,
        Multiply,
        ColorBurn,
        LinearBurn,
        DarkerColor,
        
        Lighten,
        Screen,
        ColorDodge,
        LinearDodge,
        LighterColor,
        
        Overlay,
        SoftLight,
        HardLight,
        VividLight,
        LinearLight,
        PinLight,
        HardMix,
        
        Difference,
        Exclusion,
        Subtract,
        Divide,
        
        Hue,
        Saturation,
        Color,
        Luminosity
    }
    
    [Flags]
    public enum InputMethods
    {
    
        None = 0,
        VRMode = 1 << 0,
        Pen = 1 << 1,
        Touch = 1 << 2,
        Mouse = 1 << 3,
    }

    public enum InputSource
    {
        Screen,
        World,
    }
    
    public enum RaycastSystemType
    {
        CPU,
        JobSystem,
    }
}