namespace XDPaint.Core.PaintObject.Data
{
    public class PaintStateData : IDisposable
    {
        public bool InBounds { get; internal set; }
        public bool IsPainting { get; internal set; }
        public bool IsPaintingPerformed { get; internal set; }

        public void DoDispose()
        {
            InBounds = false;
            IsPainting = false;
            IsPaintingPerformed = false;
        }
        
        public void CopyFrom(PaintStateData other)
        {
            if (other == null)
                return;
        
            InBounds = other.InBounds;
            IsPainting = other.IsPainting;
            IsPaintingPerformed = other.IsPaintingPerformed;
        }
    }
}