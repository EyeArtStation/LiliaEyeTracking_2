using UnityEngine;
using XDPaint.Tools.Raycast.Base;
using XDPaint.Tools.Raycast.Data;

namespace XDPaint.Controllers.InputData
{
    public class InputDataVR : InputDataMesh
    {
        private Transform penTransform;

        protected override bool IsScreenSpace => false;

        public override void Init(PaintManager paintManagerInstance, Camera camera)
        {
            base.Init(paintManagerInstance, camera);
            penTransform = InputController.Instance.PenTransform;
        }

        protected override void OnHoverSuccess(InputData inputData, RaycastData raycastData)
        {
            inputData.Ray = GetRay(inputData.Position);
            RaycastController.Instance.RequestRaycast(PaintManager, inputData, InputDataHistory[inputData.FingerId].GetFrameData(1), container =>
            {
                OnHoverSuccessEnd(container, inputData);
            });
        }

        protected override void OnHoverSuccessEnd(RaycastRequestContainer request, InputData inputData)
        {
            if (RaycastData[inputData.FingerId] == null)
            {
                RaycastData[inputData.FingerId] = RaycastController.Instance.TryGetRaycast(request, inputData.FingerId, inputData.Ray.origin);
            }
            
            if (RaycastData[inputData.FingerId] != null)
            {
                inputData.Position = Camera.WorldToScreenPoint(RaycastData[inputData.FingerId].WorldHit);
                OnHoverSuccessHandlerInvoke(inputData, RaycastData[inputData.FingerId]);
            }
            else
            {
                base.OnHoverFailed(inputData);
            }
        }
        
        protected override void OnDownSuccess(InputData inputData, RaycastData raycastData)
        {
            if (inputData.Ray.Equals(default(Ray)))
            {
                inputData.Ray = GetRay(default);
            }
            
            if (RaycastData[inputData.FingerId] == null)
            {
                RaycastController.Instance.RequestRaycast(PaintManager, inputData, InputDataHistory[inputData.FingerId].GetFrameData(1), container =>
                {
                    OnDownSuccessCallback(container, inputData);
                });
            }
        }
        
        protected override void OnDownSuccessCallback(RaycastRequestContainer request, InputData inputData)
        {
            if (RaycastData[inputData.FingerId] == null)
            {
                RaycastData[inputData.FingerId] = RaycastController.Instance.TryGetRaycast(request, inputData.FingerId, inputData.Ray.origin);
            }

            if (RaycastData[inputData.FingerId] == null)
            {
                OnDownFailed(inputData);
            }
            else
            {
                inputData.Position = Camera.WorldToScreenPoint(RaycastData[inputData.FingerId].WorldHit);
                OnDownSuccessInvoke(inputData, RaycastData[inputData.FingerId]);
            }
        }

        protected override void OnPressSuccess(InputData inputData, RaycastData raycastData)
        {
            if (inputData.Ray.Equals(default(Ray)))
            {
                inputData.Ray = GetRay(default);
            }

            if (RaycastData[inputData.FingerId] == null)
            {
                RaycastController.Instance.RequestRaycast(PaintManager, inputData, InputDataHistory[inputData.FingerId].GetFrameData(1), container =>
                {
                    OnPressSuccessCallback(container, inputData);
                });
            }
        }

        protected override void OnPressSuccessCallback(RaycastRequestContainer request, InputData inputData)
        {
            if (RaycastData[inputData.FingerId] == null)
            {
                RaycastData[inputData.FingerId] = RaycastController.Instance.TryGetRaycast(request, inputData.FingerId, inputData.Ray.origin);
            }

            if (RaycastData[inputData.FingerId] == null)
            {
                OnPressFailed(inputData);
            }
            else
            {
                inputData.Position = Camera.WorldToScreenPoint(RaycastData[inputData.FingerId].WorldHit);
                OnPressSuccessInvoke(inputData, RaycastData[inputData.FingerId]);
            }
        }
        
        public override Ray GetRay(Vector3 screenPosition)
        {
            return new Ray(penTransform.position, penTransform.forward);
        }
    }
}