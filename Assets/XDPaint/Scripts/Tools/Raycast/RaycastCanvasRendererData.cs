using UnityEngine;
using UnityEngine.UI;
using XDPaint.Core;
using XDPaint.Tools.Raycast.Data;
using XDPaint.Tools.Raycast.Base;
using XDPaint.Utils;

namespace XDPaint.Tools.Raycast
{
    public class RaycastCanvasRendererData : BaseRaycastMeshData
    {
        private RawImage rawImage;
        private PlaneData planeData;
        
        public override void Init(Component paintComponent, Component rendererComponent)
        {
            base.Init(paintComponent, rendererComponent);
            rawImage = rendererComponent as RawImage;
        }

        public override void AddPaintManager(IPaintManager paintManager)
        {
            base.AddPaintManager(paintManager);
            UpdatePlaneData();
            SetUVs(paintManager, planeData.UVs);
            SetTriangles(paintManager, planeData.Vertices, planeData.Normals);
        }

        protected override void UpdateMeshBounds(IPaintManager paintManager)
        {
            UpdatePlaneData();
            SetTriangles(paintManager, planeData.Vertices, planeData.Normals);
            SetUVs(paintManager, planeData.UVs);
            Mesh.RecalculateBounds();
            MeshWorldBounds = rawImage.rectTransform.GetMaxBounds();
        }

        private void UpdatePlaneData()
        {
            var corners = rawImage.rectTransform.GetLocalCorners();
            var edge1 = corners[1] - corners[0];
            var edge2 = corners[2] - corners[0];
            var normal =  Vector3.Cross(edge1, edge2).normalized;
            planeData = new PlaneData
            {
                Vertices = new[] { corners[0], corners[1], corners[2], corners[3] },
                Normals = new [] { normal, normal, normal, normal },
                UVs = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right }
            };
        }
    }
}