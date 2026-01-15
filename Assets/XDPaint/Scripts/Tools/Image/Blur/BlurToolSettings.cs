using XDPaint.Core.PaintObject.Base;
using XDPaint.Tools.Image.Base;

namespace XDPaint.Tools.Image
{
    public class BlurToolSettings : BasePaintToolSettings
    {
        [PaintToolSettings, PaintToolRange(1, 5)] public int Iterations { get; set; } = 3;
        [PaintToolSettings, PaintToolRange(0.01f, 5f)] public float BlurStrength { get; set; } = 1.5f;
        [PaintToolSettings, PaintToolRange(1, 16)] public int DownscaleRatio { get; set; } = 1;
        [PaintToolSettings] public bool UseAllActiveLayers { get; set; } = true;
        
        public BlurToolSettings(IPaintData paintData) : base(paintData)
        {
        }
    }
}
