using UnityEngine;
using XDPaint.Controllers.InputData;
using XDPaint.Tools.Raycast.Data;

namespace XDPaint.Core.PaintObject.Data
{
    public class DrawPointData
    {
        public InputData InputData { get; private set; }
        public RaycastData RaycastData { get; private set; }
        public Vector2 TexturePosition { get; private set; }

        public DrawPointData(InputData inputData, RaycastData raycastData, Vector2 texturePosition)
        {
            InputData = inputData;
            RaycastData = raycastData;
            TexturePosition = texturePosition;
        }
    }
}