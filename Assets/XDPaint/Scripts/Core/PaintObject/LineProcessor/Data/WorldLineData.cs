using UnityEngine;
using XDPaint.Core.PaintObject.LineProcessor.Data.Base;

namespace XDPaint.Core.PaintObject.LineProcessor.Data
{
    public class WorldLineData : ILineData
    {
        public Vector3 PointerPosition;
        public Vector3[] Positions;
        public Vector3[] Normals;
        public int Count;
    }
}