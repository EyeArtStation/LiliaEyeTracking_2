using XDPaint.Controllers.InputData;

namespace XDPaint.Core.PaintObject.Data.Base
{
    public abstract class BasePointerData
    {
        public InputData InputData { get; private set; }

        protected BasePointerData(InputData inputData)
        {
            InputData = inputData;
        }
    }
}