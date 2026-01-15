using XDPaint.Controllers;
using XDPaint.Utils;

namespace XDPaint.Core.PaintObject.Data
{
    public class FrameDataContainer : IDisposable
    {
        private FrameDataBuffer<FrameData>[] paintDataHistory;

        public FrameDataBuffer<FrameData>[] Data => paintDataHistory;

        public FrameDataContainer(int historySize)
        {
            paintDataHistory = new FrameDataBuffer<FrameData>[InputController.Instance.MaxTouchesCount + 1];
            for (var i = 0; i < paintDataHistory.Length; i++)
            {
                paintDataHistory[i] = new FrameDataBuffer<FrameData>(historySize);
            }
        }
        
        public void DoDispose()
        {
            if (paintDataHistory != null)
            {
                foreach (var frameDataBuffer in paintDataHistory)
                {
                    frameDataBuffer?.DoDispose();
                }
                
                paintDataHistory = null;
            }
        }
    }
}