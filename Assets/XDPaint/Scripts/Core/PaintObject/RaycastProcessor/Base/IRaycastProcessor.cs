using UnityEngine;
using XDPaint.Tools.Raycast.Data;

namespace XDPaint.Core.PaintObject.RaycastProcessor.Base
{
    public interface IRaycastProcessor
    {
        bool TryProcessRaycastPosition(Ray ray, RaycastData rayData, out Vector3 position);
    }
}