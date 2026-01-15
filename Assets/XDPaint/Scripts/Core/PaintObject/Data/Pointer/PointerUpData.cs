using XDPaint.Controllers.InputData;
using XDPaint.Core.PaintObject.Data.Base;

namespace XDPaint.Core.PaintObject.Data
{
    public class PointerUpData : BasePointerData
    {
        public readonly bool IsInBounds;

        public PointerUpData(InputData inputData, bool isInBounds) : base(inputData)
        {
            IsInBounds = isInBounds;
        }
    }
}