using UnityEngine;
using XDPaint.Tools.Raycast.Data;

namespace XDPaint.Core.PaintObject.RaycastProcessor.Base
{
    public class BaseRaycastProcessor : IRaycastProcessor
    {
        public virtual bool TryProcessRaycastPosition(Ray ray, RaycastData rayData, out Vector3 position)
        {
            position = rayData.WorldHit;
            return true;
        }
    }
}