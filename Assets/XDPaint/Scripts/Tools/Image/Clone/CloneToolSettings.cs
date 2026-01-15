using System;
using XDPaint.Core.PaintObject.Base;
using XDPaint.Tools.Image.Base;

namespace XDPaint.Tools.Image
{
    [Serializable]
    public class CloneToolSettings : BasePaintToolSettings
    {
        [PaintToolSettings] public bool CopyTextureOnPressDown { get; set; } = true;
        [PaintToolSettings] public bool UseAllActiveLayers { get; set; } = true;
        
        public CloneToolSettings(IPaintData paintData) : base(paintData)
        {
        }
    }
}