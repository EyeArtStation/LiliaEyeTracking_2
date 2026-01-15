using XDPaint.Controllers.InputData;
using XDPaint.Tools.Raycast.Data;

namespace XDPaint.Core.PaintObject.Data
{
    public class FrameData : IDisposable
    {
        public InputData InputData { get; private set; }
        public RaycastData RaycastData { get; private set; }
        public float BrushSize { get; internal set; }
        public PaintStateData State { get; internal set; } = new PaintStateData();
        
        public FrameData(InputData inputData, RaycastData raycastData, float brushSize)
        {
            InputData = inputData;
            RaycastData = raycastData;
            BrushSize = brushSize;
        }

        public void DoDispose()
        {
            InputData = default;
            RaycastData = null;
            BrushSize = 1f;
            State.DoDispose();
        }
    }
}