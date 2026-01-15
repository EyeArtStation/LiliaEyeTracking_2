using UnityEngine;
using XDPaint.Core.PaintObject.LineProcessor.Data.Base;

namespace XDPaint.Core.PaintObject.LineProcessor.Data
{
    public class TextureLineData : ILineData
    {
        public Vector2[] TexturePositions;
        public float[] Pressures;
    }
}