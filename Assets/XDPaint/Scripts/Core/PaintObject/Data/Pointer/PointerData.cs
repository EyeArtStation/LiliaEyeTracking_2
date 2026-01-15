using UnityEngine;
using XDPaint.Controllers.InputData;
using XDPaint.Core.PaintObject.Data.Base;
using XDPaint.Tools.Raycast.Data;

namespace XDPaint.Core.PaintObject.Data
{
    public class PointerData : BasePointerData
    {
        public RaycastData RaycastData { get; private set; }
        public Vector2 TexturePosition { get; private set; }
        
        public PointerData(InputData inputData, RaycastData raycastData, Vector2 texturePosition) : base(inputData)
        {
            RaycastData = raycastData;
            TexturePosition = texturePosition;
        }
    }
}