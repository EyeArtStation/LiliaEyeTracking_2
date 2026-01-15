using System.Collections.Generic;
using UnityEngine;
using XDPaint.Tools.Raycast.Data;

namespace XDPaint.Core.PaintObject.Data
{
    public class DrawLineData
    {
        public DrawPointData StartPointData { get; private set; }
        public DrawPointData EndPointData { get; private set; }
        public KeyValuePair<Ray, RaycastData>[] LineData { get; private set; }
        
        public DrawLineData(DrawPointData startPointData, DrawPointData endPointData, KeyValuePair<Ray, RaycastData>[] lineData)
        {
            StartPointData = startPointData;
            EndPointData = endPointData;
            LineData = lineData;
        }
    }
}