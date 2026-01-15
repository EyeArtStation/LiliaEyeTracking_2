using System.Collections.Generic;
using UnityEngine;
using XDPaint.Core.PaintObject.Data;
using XDPaint.Core.PaintObject.LineProcessor.Base;
using XDPaint.Core.PaintObject.LineProcessor.Data;
using XDPaint.Core.PaintObject.LineProcessor.Data.Base;
using XDPaint.Tools.Raycast.Data;
using XDPaint.Utils;

namespace XDPaint.Core.PaintObject.LineProcessor
{
    public class LineWorldProcessor : ILineProcessor
    {
        public bool TryProcessLine(FrameDataBuffer<FrameData> frameData, IList<KeyValuePair<Ray, RaycastData>> raycasts, bool finishPainting, out ILineData[] result)
        {
            if (raycasts.Count > 0)
            {
                var arraysCount = Mathf.CeilToInt(raycasts.Count / (float)Constants.PaintWorldShader.PositionsCount);
                result = new ILineData[arraysCount];
                var raycastIndex = 0;
                for (var i = 0; i < result.Length; i++)
                {
                    var raycastsCount = Mathf.Min(Constants.PaintWorldShader.PositionsCount, raycasts.Count - i * Constants.PaintWorldShader.PositionsCount);
                    var positions = new Vector3[Constants.PaintWorldShader.PositionsCount];
                    var normals = new Vector3[Constants.PaintWorldShader.PositionsCount];
                    for (var j = 0; j < raycastsCount; j++)
                    {
                        positions[j] = raycasts[raycastIndex].Value.WorldHit;
                        normals[j] = raycasts[raycastIndex].Value.Triangle.WorldNormal;
                        raycastIndex++;
                    }
                    
                    result[i] = new WorldLineData
                    {
                        PointerPosition = frameData.GetFrameData(0).InputData.Ray.origin,
                        Positions = positions,
                        Normals = normals,
                        Count = raycastsCount
                    };
                }
                
                return true;
            }
            
            result = null;
            return false;
        }
    }
}