using UnityEngine;
using XDPaint.Controllers;
using XDPaint.Core.PaintObject.RaycastProcessor.Base;
using XDPaint.Tools.Raycast.Data;

namespace XDPaint.Core.PaintObject.RaycastProcessor
{
    public class PenRaycastProcessor : BaseRaycastProcessor
    {
        public override bool TryProcessRaycastPosition(Ray ray, RaycastData rayData, out Vector3 position)
        {
            position = rayData.WorldHit;
            return Vector3.Distance(ray.origin, rayData.WorldHit) <= InputController.Instance.MinimalDistanceToPaint;
        }
    }
}