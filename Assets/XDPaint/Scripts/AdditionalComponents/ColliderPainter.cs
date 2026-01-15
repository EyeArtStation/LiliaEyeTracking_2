using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XDPaint.Controllers;
using XDPaint.Controllers.InputData;
using XDPaint.Core;
using XDPaint.Tools.Raycast.Data;
using XDPaint.Utils;

namespace XDPaint.AdditionalComponents
{
    public class ColliderPainter : MonoBehaviour
    {
        public event Action<PaintManager, Collision> OnCollide;
        public Color Color = Color.white;
        public float Pressure = 1f;
        public int FingerId;
        
        private readonly Dictionary<PaintManager, FrameDataBuffer<PaintState>> paintStates = new Dictionary<PaintManager, FrameDataBuffer<PaintState>>();

        private void OnCollisionEnter(Collision collision)
        {
            OnCollision(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            OnCollision(collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            for (var i = paintStates.Keys.Count - 1; i >= 0; i--)
            {
                var key = paintStates.Keys.ElementAt(i);
                var paintState = paintStates[key];
                for (var j = 0; j < paintState.Count; j++)
                {
                    if (paintState.GetFrameData(j).CollisionTransform == collision.transform)
                    {
                        paintState.DoDispose();
                        paintStates.Remove(key);
                        return;
                    }
                }
            }
        }

        private void OnCollision(Collision collision)
        {
            foreach (var paintManager in PaintController.Instance.GetAllPaintManagersAsEnumerable())
            {
                if (paintManager.ObjectForPainting == collision.gameObject)
                {
                    var position = collision.contacts[0].point + collision.contacts[0].normal * 0.01f;
                    var ray = new Ray(position, -collision.contacts[0].normal);
                    var inputData = new InputData(ray, position, Pressure, InputSource.World, FingerId);
                    InputData previousInputData = default;
                    if (paintStates.ContainsKey(paintManager) && paintStates[paintManager].Count > 0)
                    {
                        var previousPaintState = paintStates[paintManager].GetFrameData(0);
                        previousInputData = previousPaintState.InputData;
                    }

                    RaycastController.Instance.RequestRaycast(paintManager, inputData, previousInputData, container =>
                    {
                        var raycastData = RaycastController.Instance.TryGetRaycast(container, inputData.FingerId, inputData.Ray.origin);
                        if (raycastData != null)
                        {
                            if (!paintStates.ContainsKey(paintManager))
                            {
                                paintStates.Add(paintManager, new FrameDataBuffer<PaintState>(2));
                            }

                            paintStates[paintManager].AddFrameData(new PaintState
                            {
                                InputData = inputData,
                                RaycastData = raycastData,
                                CollisionTransform = collision.transform
                            });

                            var previousBrushColor = paintManager.Brush.Color;
                            paintManager.Brush.SetColor(Color, true, false);
                            OnCollide?.Invoke(paintManager, collision);

                            if (paintStates[paintManager].Count > 1)
                            {
                                var previous = paintStates[paintManager].GetFrameData(1);
                                paintManager.PaintObject.DrawLine(inputData, previousInputData, raycastData, previous.RaycastData);
                            }
                            else
                            {
                                paintManager.PaintObject.DrawPoint(inputData, raycastData);
                            }

                            paintManager.Brush.SetColor(previousBrushColor, true, false);
                        }
                        else
                        {
                            OnCollisionExit(collision);
                        }
                    });
                }
            }
        }

        private class PaintState
        {
            public InputData InputData;
            public RaycastData RaycastData;
            public Transform CollisionTransform;
        }
    }
}