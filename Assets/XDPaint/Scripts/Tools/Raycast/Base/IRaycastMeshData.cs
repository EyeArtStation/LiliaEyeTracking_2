using System.Collections.Generic;
using UnityEngine;
using XDPaint.Controllers.InputData;
using XDPaint.Core;
using XDPaint.Tools.Raycast.Data;

namespace XDPaint.Tools.Raycast.Base
{
    public interface IRaycastMeshData : IDisposable
    {
        Transform Transform { get; }
        List<Vector3> Vertices { get; }
        List<Vector3> Normals { get; }
        Mesh Mesh { get; }
        IReadOnlyCollection<IPaintManager> PaintManagers { get; }

        void AddPaintManager(IPaintManager paintManager);
        void RemovePaintManager(IPaintManager paintManager);

        void Init(Component paintComponent, Component rendererComponent);
        Vector2 GetUV(int channel, int index);
        IRaycastRequest RequestRaycast(ulong requestId, IPaintManager sender, InputData inputData, InputData previousInputData, bool useWorld = true, bool useCache = true, bool raycastAll = true);
        RaycastData TryGetRaycastResponse(RaycastRequestContainer request, out IList<Triangle> triangles);
        RaycastData GetRaycast(IPaintManager sender, Ray ray, Vector3 pointerPosition, int fingerId, bool useWorld = true, bool raycastAll = true);
    }
}