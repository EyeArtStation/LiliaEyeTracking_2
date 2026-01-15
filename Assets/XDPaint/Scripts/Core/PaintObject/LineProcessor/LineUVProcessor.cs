using System;
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
    public class LineUVProcessor : ILineProcessor
    {
        private readonly Func<Vector2, Vector2> convertUVToTexturePosition;

        public LineUVProcessor(Func<Vector2, Vector2> uvToTexturePosition)
        {
            convertUVToTexturePosition = uvToTexturePosition;
        }

        public bool TryProcessLine(FrameDataBuffer<FrameData> frameData, 
            IList<KeyValuePair<Ray, RaycastData>> raycasts, bool finishPainting, out ILineData[] result)
        {
            var texturePositions = new Vector2[raycasts.Count];
            for (var i = 0; i < raycasts.Count; i++)
            {
                texturePositions[i] = convertUVToTexturePosition(raycasts[i].Value.UVHit);
            }

            var brushes = new float[texturePositions.Length];
            var brushIndex = 0;
            for (var i = 0; i < frameData.Count; i++)
            {
                var data = frameData.GetFrameData(i);
                if (data.RaycastData == null)
                    break;

                if (brushIndex >= brushes.Length)
                    break;

                brushes[brushIndex] = data.BrushSize * data.InputData.Pressure;
                brushIndex++;
            }

            if (texturePositions.Length > 0)
            {
                result = new ILineData[]
                {
                    new TextureLineData
                    {
                        TexturePositions = texturePositions,
                        Pressures = brushes
                    }
                };
                return true;
            }

            result = null;
            return false;
        }
    }
}