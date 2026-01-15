using System;
using XDPaint.Tools.Image.Base;

namespace XDPaint.Tools.Image
{
    [Serializable]
    public class GrayscaleToolSettings : BasePaintToolSettings
    {
        public GrayscaleToolSettings(IPaintData paintData) : base(paintData)
        {
        }
    }
}