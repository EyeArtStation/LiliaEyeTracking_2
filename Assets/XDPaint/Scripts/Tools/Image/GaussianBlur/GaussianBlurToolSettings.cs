using System;
using XDPaint.Core.PaintObject.Base;
using XDPaint.Tools.Image.Base;

namespace XDPaint.Tools.Image
{
    [Serializable]
    public class GaussianBlurToolSettings : BasePaintToolSettings
    {
        [PaintToolSettings, PaintToolRange(3, 7)] public int KernelSize { get; set; } = 3;
        [PaintToolSettings, PaintToolRange(0.01f, 5f)] public float Spread { get; set; } = 5f;
        [PaintToolSettings] public bool UseAllActiveLayers { get; set; } = true;

        public GaussianBlurToolSettings(IPaintData paintData) : base(paintData)
        {
        }
    }
}