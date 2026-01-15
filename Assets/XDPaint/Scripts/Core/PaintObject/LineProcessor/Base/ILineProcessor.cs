using System.Collections.Generic;
using UnityEngine;
using XDPaint.Core.PaintObject.Data;
using XDPaint.Core.PaintObject.LineProcessor.Data.Base;
using XDPaint.Tools.Raycast.Data;
using XDPaint.Utils;

namespace XDPaint.Core.PaintObject.LineProcessor.Base
{
    public interface ILineProcessor
    {
        bool TryProcessLine(FrameDataBuffer<FrameData> frameData, IList<KeyValuePair<Ray, RaycastData>> raycasts, bool finishPainting, out ILineData[] result);
    }
}