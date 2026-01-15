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
    public class LineSmoothUVProcessor : ILineProcessor
    {
        private readonly Func<Vector2, Vector2> convertUVToTexturePosition;
        private int smoothing;
        
        private const int LineElements = 3;

        public LineSmoothUVProcessor(Func<Vector2, Vector2> uvToTexturePosition)
        {
            convertUVToTexturePosition = uvToTexturePosition;
        }

        public void SetSmoothing(int value)
        {
            smoothing = value;
        }

        public bool TryProcessLine(FrameDataBuffer<FrameData> frameData, IList<KeyValuePair<Ray, RaycastData>> raycasts, bool finishPainting, out ILineData[] result)
        {
            var texturePositions = new Vector2[raycasts.Count];
            for (var i = 0; i < raycasts.Count; i++)
            {
                texturePositions[i] = convertUVToTexturePosition(raycasts[i].Value.UVHit);
            }

            var pressures = new float[texturePositions.Length];
            var brushIndex = 0;
            for (var i = 0; i < frameData.Count; i++)
            {
                var data = frameData.GetFrameData(i);
                if (data.RaycastData == null)
                    break;

                pressures[brushIndex] = data.BrushSize * data.InputData.Pressure;
                brushIndex++;
            }

            var startPressure = texturePositions.Length <= LineElements ? pressures[0] : pressures[1];
            var endPressure = texturePositions.Length <= LineElements ? pressures[1] : pressures[0];
            var prevPressure = startPressure;
            float length;
            if (texturePositions.Length == LineElements)
            {
                length = finishPainting
                    ? Vector2.Distance(texturePositions[1], texturePositions[2])
                    : Vector2.Distance(texturePositions[0], texturePositions[1]);
            }
            else
            {
                length = finishPainting
                    ? Vector2.Distance(texturePositions[2], texturePositions[3])
                    : Vector2.Distance(texturePositions[1], texturePositions[2]);
            }

            var numSegments = (int)Mathf.Clamp(length / 10f, 1, smoothing);
            var previous = -Vector2.one;
            var linesData = new List<TextureLineData>();
            for (var i = 0; i <= numSegments; i++)
            {
                var t = (float)i / numSegments;
                Vector2 interpolatedPosition;
                if (texturePositions.Length == LineElements)
                {
                    interpolatedPosition = finishPainting
                        ? MathHelper.Interpolate(texturePositions[2], texturePositions[1], texturePositions[0], texturePositions[0], t)
                        : MathHelper.Interpolate(texturePositions[2], texturePositions[2], texturePositions[1], texturePositions[0], t);
                }
                else
                {
                    interpolatedPosition = finishPainting
                        ? MathHelper.Interpolate(texturePositions[2], texturePositions[1], texturePositions[0], texturePositions[0], t)
                        : MathHelper.Interpolate(texturePositions[3], texturePositions[2], texturePositions[1], texturePositions[0], t);
                }

                if (previous != -Vector2.one)
                {
                    var positions = new[] { previous, interpolatedPosition };
                    var pressureStart = prevPressure;
                    var pressureEnd = pressureStart + (endPressure - pressureStart) * t;
                    if (positions.Length > 0)
                    {
                        linesData.Add(new TextureLineData
                        {
                            TexturePositions = positions,
                            Pressures = new [] { pressureStart, pressureEnd }
                        });
                    }

                    prevPressure = pressureEnd;
                }

                previous = interpolatedPosition;
            }

            result = new ILineData[]
            {
                new TextureSmoothLinesData
                {
                    Data = linesData.ToArray()   
                }
            };
            
            return linesData.Count > 0;
        }
    }
}