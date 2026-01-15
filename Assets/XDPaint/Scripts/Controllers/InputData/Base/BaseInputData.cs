using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XDPaint.Tools;
using XDPaint.Tools.Raycast;
using XDPaint.Tools.Raycast.Data;
using XDPaint.Utils;
using IDisposable = XDPaint.Core.IDisposable;

namespace XDPaint.Controllers.InputData.Base
{
    public abstract class BaseInputData : IDisposable
    {
        public event Action<InputData, RaycastData> OnHoverSuccessHandler;
        public event Action<InputData> OnHoverFailedHandler;
        public event Action<InputData, RaycastData> OnDownHandler;
        public event Action<InputData> OnDownFailedHandler;
        public event Action<InputData, RaycastData> OnPressHandler;
        public event Action<InputData> OnPressFailedHandler;
        public event Action<InputData> OnUpHandler;
        
        protected Camera Camera;
        protected PaintManager PaintManager;
        public FrameDataBuffer<InputData>[] InputDataHistory;
        
        private Canvas canvas;
        private List<CanvasGraphicRaycaster> raycasters;
        private Dictionary<int, Dictionary<CanvasGraphicRaycaster, List<RaycastResult>>> raycastResults;
        private bool canHover = true;
        private bool isOnDownSuccess;
        public bool isDown;

        protected virtual bool IsScreenSpace => true;

        private const int FramesHistoryLength = 2;
        
        public virtual void Init(PaintManager paintManagerInstance, Camera camera)
        {
            Camera = camera;
            PaintManager = paintManagerInstance;
            InputDataHistory = new FrameDataBuffer<InputData>[InputController.Instance.MaxTouchesCount];
            for (var i = 0; i < InputDataHistory.Length; i++)
            {
                InputDataHistory[i] = new FrameDataBuffer<InputData>(FramesHistoryLength);
                for (var j = 0; j < FramesHistoryLength; j++)
                {
                    InputDataHistory[i].AddFrameData(new InputData(i));
                }
            }

            raycasters = new List<CanvasGraphicRaycaster>();
            raycastResults = new Dictionary<int, Dictionary<CanvasGraphicRaycaster, List<RaycastResult>>>();
            for (var i = 0; i < InputController.Instance.MaxTouchesCount; i++)
            {
                raycastResults.Add(i, new Dictionary<CanvasGraphicRaycaster, List<RaycastResult>>());
            }

            if (PaintManager.ObjectForPainting.TryGetComponent<RawImage>(out var rawImage))
            {
                canvas = rawImage.canvas;
            }

            if (Settings.Instance.CheckCanvasRaycasts)
            {
                if (canvas != null)
                {
                    if (!canvas.TryGetComponent<CanvasGraphicRaycaster>(out var graphicRaycaster))
                    {
                        graphicRaycaster = canvas.gameObject.AddComponent<CanvasGraphicRaycaster>();
                    }
                    
                    if (!raycasters.Contains(graphicRaycaster))
                    {
                        raycasters.Add(graphicRaycaster);
                    }
                }

                foreach (var canvasInstance in InputController.Instance.Canvases)
                {
                    if (canvasInstance == null)
                        continue;

                    if (canvasInstance != null)
                    {
                        if (!canvasInstance.TryGetComponent<CanvasGraphicRaycaster>(out var graphicRaycaster))
                        {
                            graphicRaycaster = canvasInstance.gameObject.AddComponent<CanvasGraphicRaycaster>();
                        }
                        
                        if (!raycasters.Contains(graphicRaycaster))
                        {
                            raycasters.Add(graphicRaycaster);
                        }
                    }
                }
            }
        }
        
        public virtual void DoDispose()
        {
            raycasters.Clear();
            raycastResults.Clear();
        }

        public virtual void OnUpdate()
        {
            foreach (var frameDataBuffer in InputDataHistory)
            {
                frameDataBuffer.AddFrameData(frameDataBuffer.GetFrameData(1));
            }
        }

        public void OnHover(int fingerId, Vector3 position)
        {
            if (!CanProcess(fingerId))
            {
                OnHoverFailed(InputDataHistory[fingerId].UpdateFrameData(data => data.Update(GetRay(position), position)));
                return;
            }
            
            if (Settings.Instance.CheckCanvasRaycasts && raycasters.Count > 0)
            {
                raycastResults[fingerId].Clear();
                foreach (var raycaster in raycasters)
                {
                    var result = raycaster.GetRaycasts(position);
                    if (result != null)
                    {
                        raycastResults[fingerId].Add(raycaster, result);
                    }
                }

                if (canHover && (raycastResults[fingerId].Count == 0 || CheckRaycasts(fingerId)))
                {
                    OnHoverSuccess(InputDataHistory[fingerId].UpdateFrameData(data => data.Update(GetRay(position), position)), null);
                }
                else
                {
                    OnHoverFailed(InputDataHistory[fingerId].UpdateFrameData(data => data.Update(GetRay(position), position)));
                }
            }
            else
            {
                OnHoverSuccess(InputDataHistory[fingerId].UpdateFrameData(data => data.Update(GetRay(position), position)), null);
            }
        }

        protected virtual void OnHoverSuccess(InputData inputData, RaycastData raycastData)
        {
            OnHoverSuccessHandlerInvoke(inputData, raycastData);
        }
        
        protected void OnHoverSuccessHandlerInvoke(InputData inputData, RaycastData raycastData)
        {
            OnHoverSuccessHandler?.Invoke(inputData, raycastData);
        }
        
        protected virtual void OnHoverFailed(InputData inputData)
        {
            OnHoverFailedHandler?.Invoke(inputData);
        }

        public void OnDown(int fingerId, Vector3 position, float pressure = 1.0f)
        {
            if (!CanProcess(fingerId, true))
            {
                OnDownFailed(InputDataHistory[fingerId].UpdateFrameData(data => data.Update(GetRay(position), position, pressure)));
                return;
            }

            if (Settings.Instance.CheckCanvasRaycasts && raycasters.Count > 0)
            {
                raycastResults[fingerId].Clear();
                foreach (var raycaster in raycasters)
                {
                    var result = raycaster.GetRaycasts(position);
                    if (result != null)
                    {
                        raycastResults[fingerId].Add(raycaster, result);
                    }
                }
                
                if (raycastResults[fingerId].Count == 0 || CheckRaycasts(fingerId))
                {
                    isOnDownSuccess = true;
                    OnDownSuccess(InputDataHistory[fingerId].UpdateFrameData(data => data.Update(GetRay(position), position, pressure)), null);
                }
                else
                {
                    canHover = false;
                    isOnDownSuccess = false;
                    OnDownFailed(InputDataHistory[fingerId].UpdateFrameData(data => data.Update(GetRay(position), position, pressure)));
                }
            }
            else
            {
                isOnDownSuccess = true;
                OnDownSuccess(InputDataHistory[fingerId].UpdateFrameData(data => data.Update(GetRay(position), position, pressure)), null);
            }
            Debug.Log("BaseInputData.OnDown called");
        }

        public void OnEyeTrackingDown(int fingerId, Vector3 position, float pressure = 1.0f)
        {
            isDown = true;
            // Debug.Log("isDown is TRUE");

            isOnDownSuccess = true;
            OnDownSuccess(InputDataHistory[fingerId].UpdateFrameData(data => data.Update(GetRay(position), position, pressure)), null);

            /* if (!CanProcess(fingerId, true))
             {
                 OnDownFailed(InputDataHistory[fingerId].UpdateFrameData(data => data.Update(GetRay(position), position, pressure)));
                 return;
             }

             if (Settings.Instance.CheckCanvasRaycasts && raycasters.Count > 0)
             {
                 raycastResults[fingerId].Clear();
                 foreach (var raycaster in raycasters)
                 {
                     var result = raycaster.GetRaycasts(position);
                     if (result != null)
                     {
                         raycastResults[fingerId].Add(raycaster, result);
                     }
                 }

                 if (raycastResults[fingerId].Count == 0 || CheckRaycasts(fingerId))
                 {
                     isOnDownSuccess = true;
                     OnDownSuccess(InputDataHistory[fingerId].UpdateFrameData(data => data.Update(GetRay(position), position, pressure)), null);
                 }
                 else
                 {
                     canHover = false;
                     isOnDownSuccess = false;
                     OnDownFailed(InputDataHistory[fingerId].UpdateFrameData(data => data.Update(GetRay(position), position, pressure)));
                 }
             }
             else
             {
                 isOnDownSuccess = true;
                 OnDownSuccess(InputDataHistory[fingerId].UpdateFrameData(data => data.Update(GetRay(position), position, pressure)), null);
             }*/
        }

        protected virtual void OnDownSuccess(InputData inputData, RaycastData raycastData)
        {
            OnDownSuccessInvoke(inputData, raycastData);
        }

        protected void OnDownSuccessInvoke(InputData inputData, RaycastData raycastData)
        {
            OnDownHandler?.Invoke(inputData, raycastData);
        }
        
        protected void OnDownFailed(InputData inputData)
        {
            OnDownFailedHandler?.Invoke(inputData);
        }

        public void OnPress(int fingerId, Vector3 position, float pressure = 1.0f)
        {
            if (!CanProcess(fingerId))
            {
                OnPressFailed(InputDataHistory[fingerId].UpdateFrameData(data => data.Update(position, pressure)));
                return;
            }

            if (Settings.Instance.CheckCanvasRaycasts && InputController.Instance.BlockRaycastsOnPress && raycasters.Count > 0)
            {
                raycastResults[fingerId].Clear();
                foreach (var raycaster in raycasters)
                {
                    var result = raycaster.GetRaycasts(position);
                    if (result != null)
                    {
                        raycastResults[fingerId].Add(raycaster, result);
                    }
                }

                if (raycastResults[fingerId].Count == 0 || CheckRaycasts(fingerId))
                {
                    OnPressSuccess(InputDataHistory[fingerId].UpdateFrameData(data => data.Update(GetRay(position), position, pressure)), null);
                }
                else
                {
                    OnPressFailed(InputDataHistory[fingerId].UpdateFrameData(data => data.Update(GetRay(position), position, pressure)));
                }
            }
            else if (isOnDownSuccess)
            {
                OnPressSuccess(InputDataHistory[fingerId].UpdateFrameData(data => data.Update(GetRay(position), position, pressure)), null);
            }

            Debug.Log("BaseInputData.OnPress called");
        }

        public void OnEyeTrackingPress(int fingerId, Vector3 position, float pressure = 1.0f)
        {
            if (isDown)
            {
                if (!CanProcess(fingerId))
                {
                    OnPressFailed(InputDataHistory[fingerId].UpdateFrameData(data => data.Update(position, pressure)));
                    return;
                }

                if (Settings.Instance.CheckCanvasRaycasts && InputController.Instance.BlockRaycastsOnPress && raycasters.Count > 0)
                {
                    raycastResults[fingerId].Clear();
                    foreach (var raycaster in raycasters)
                    {
                        var result = raycaster.GetRaycasts(position);
                        if (result != null)
                        {
                            raycastResults[fingerId].Add(raycaster, result);
                        }
                    }

                    if (raycastResults[fingerId].Count == 0 || CheckRaycasts(fingerId))
                    {
                        OnPressSuccess(InputDataHistory[fingerId].UpdateFrameData(data => data.Update(GetRay(position), position, pressure)), null);
                    }
                    else
                    {
                        OnPressFailed(InputDataHistory[fingerId].UpdateFrameData(data => data.Update(GetRay(position), position, pressure)));
                    }
                }
                else if (isOnDownSuccess)
                {
                    OnPressSuccess(InputDataHistory[fingerId].UpdateFrameData(data => data.Update(GetRay(position), position, pressure)), null);
                }
            }
        }

        protected virtual void OnPressSuccess(InputData inputData, RaycastData raycastData)
        {
            OnPressSuccessInvoke(inputData, raycastData);
        }

        protected void OnPressSuccessInvoke(InputData inputData, RaycastData raycastData)
        {
            OnPressHandler?.Invoke(inputData, raycastData);
        }
        
        protected void OnPressFailed(InputData inputData)
        {
            OnPressFailedHandler?.Invoke(inputData);
        }

        public void OnUp(int fingerId, Vector3 position)
        {
            if (!CanProcess(fingerId))
                return;

            OnUpSuccessInvoke(InputDataHistory[fingerId].UpdateFrameData(data => data.Update(position)));
            canHover = true;

            Debug.Log("BaseInputData.OnUp called");
        }

        public void OnEyeTrackingUp(int fingerId, Vector3 position, bool triggerDown)
        {
            isDown = false;
            /*if (!CanProcess(fingerId))
                return;*/

            OnUpSuccessInvoke(InputDataHistory[fingerId].UpdateFrameData(data => data.Update(position)));
            canHover = true;

            if (triggerDown) { OnEyeTrackingDown(0, Input.mousePosition); }
        }

        protected virtual void OnUpSuccessInvoke(InputData inputData)
        {
            OnUpHandler?.Invoke(inputData);
        }

        public virtual Ray GetRay(Vector3 screenPosition)
        {
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                var origin = new Vector3(screenPosition.x, screenPosition.y, -canvas.transform.forward.z);
                var direction = canvas.transform.forward;
                return new Ray(origin, direction);
            }

            return Camera.ScreenPointToRay(screenPosition);
        }

        private bool CheckRaycasts(int fingerId)
        {
            var result = true;
            if (fingerId < raycastResults.Count)
            {
                var ignoreRaycasts = InputController.Instance.IgnoreForRaycasts;
                foreach (var raycaster in raycastResults[fingerId].Keys)
                {
                    if (raycastResults[fingerId][raycaster].Count > 0)
                    {
                        var raycast = raycastResults[fingerId][raycaster][0];
                        if (raycast.gameObject == PaintManager.ObjectForPainting && PaintManager.Initialized)
                        {
                            continue;
                        }

                        if (!ignoreRaycasts.Contains(raycast.gameObject))
                        {
                            result = false;
                            break;
                        }
                    }
                }
            }
            
            return result;
        }
        
        private bool CanProcess(int fingerId, bool printWarnings = false)
        {
            if (fingerId >= InputDataHistory.Length)
                return false;
            
            if (!PaintManager.IsActive() || !PaintManager.LayersController.ActiveLayer.Enabled || PaintManager.Brush.Color.a == 0f)
            {
                if (printWarnings)
                {
                    if (!PaintManager.LayersController.ActiveLayer.Enabled)
                    {
                        Debug.LogWarning("Active layer is disabled!");
                    }
                    else if (PaintManager.Brush.Color.a == 0f)
                    {
                        Debug.LogWarning("Brush has zero alpha value!");
                    }
                }
                
                return false;
            }
            
            return true;
        }
    }
}