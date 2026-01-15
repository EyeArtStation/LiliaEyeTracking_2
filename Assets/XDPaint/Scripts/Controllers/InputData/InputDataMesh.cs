using UnityEngine;
using XDPaint.Controllers.InputData.Base;
using XDPaint.Tools.Raycast.Base;
using XDPaint.Tools.Raycast.Data;

namespace XDPaint.Controllers.InputData
{
    public class InputDataMesh : BaseInputData
    {
        protected RaycastData[] RaycastData;

        public override void Init(PaintManager paintManagerInstance, Camera camera)
        {
            base.Init(paintManagerInstance, camera);
            RaycastData = new RaycastData[InputDataHistory.Length];
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            for (var i = 0; i < InputDataHistory.Length; i++)
            {
                RaycastData[i] = null;
            }
        }
        
        protected override void OnHoverSuccess(InputData inputData, RaycastData raycastData)
        {
            inputData.Ray = GetRay(inputData.Position);
            RaycastController.Instance.RequestRaycast(PaintManager, inputData, InputDataHistory[inputData.FingerId].GetFrameData(1), container =>
            {
                OnHoverSuccessEnd(container, inputData);
            });
        }

        protected virtual void OnHoverSuccessEnd(RaycastRequestContainer request, InputData inputData)
        {
            if (RaycastData[inputData.FingerId] == null)
            {
                RaycastData[inputData.FingerId] = RaycastController.Instance.TryGetRaycast(request, inputData.FingerId, inputData.Ray.origin);
            }
            
            if (RaycastData[inputData.FingerId] != null)
            {
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
                inputData.Ray = GetRay(inputData.Position);
            }
            
            if (RaycastData[inputData.FingerId] == null)
            {
                RaycastController.Instance.RequestRaycast(PaintManager, inputData, InputDataHistory[inputData.FingerId].GetFrameData(1), container =>
                {
                    OnDownSuccessCallback(container, inputData);
                });
            }
        }

        protected virtual void OnDownSuccessCallback(RaycastRequestContainer request, InputData inputData)
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
                OnDownSuccessInvoke(inputData, RaycastData[inputData.FingerId]);
            }
        }

        protected override void OnPressSuccess(InputData inputData, RaycastData raycastData)
        {
            if (inputData.Ray.Equals(default(Ray)))
            {
                inputData.Ray = GetRay(inputData.Position);
            }
                
            if (RaycastData[inputData.FingerId] == null)
            {
                RaycastController.Instance.RequestRaycast(PaintManager, inputData, InputDataHistory[inputData.FingerId].GetFrameData(1), container =>
                {
                    OnPressSuccessCallback(container, inputData);
                });
            }
        }

        protected virtual void OnPressSuccessCallback(RaycastRequestContainer request, InputData inputData)
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
                OnPressSuccessInvoke(inputData, RaycastData[inputData.FingerId]);
            }
        }

        protected override void OnUpSuccessInvoke(InputData inputData)
        {
            RaycastController.Instance.AddCallbackToRequest(PaintManager, inputData.FingerId, () => base.OnUpSuccessInvoke(inputData));
        }
    }
}