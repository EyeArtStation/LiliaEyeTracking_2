using System;
using XDPaint.Tools.Image.Base;

namespace XDPaint.Tools.Image
{
    [Serializable]
    public class BrushSamplerToolSettings : BasePaintToolSettings
    {
        public BrushSamplerToolSettings(IPaintData paintData) : base(paintData)
        {
        }
    }
}