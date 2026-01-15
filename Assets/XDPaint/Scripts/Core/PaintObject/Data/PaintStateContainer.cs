using XDPaint.Utils;

namespace XDPaint.Core.PaintObject.Data
{
    public class PaintStateContainer
    {
        public FrameDataBuffer<FrameData> FrameBuffer { get; private set; }
        public PaintStateData PaintState { get; private set; }

        public PaintStateContainer(int length)
        {
            FrameBuffer = new FrameDataBuffer<FrameData>(length);
            PaintState = new PaintStateData();
        }
    }
}