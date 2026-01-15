using UnityEngine;
using XDPaint.Core;
using XDPaint.Tools.Raycast.Base;
using XDPaint.Tools.Raycast.Data;
using XDPaint.Utils;

namespace XDPaint.Tools.Raycast
{
    public class RaycastSpriteRendererData : BaseRaycastMeshData
    {
        private SpriteRenderer spriteRenderer;
        
        public override void Init(Component paintComponent, Component rendererComponent)
        {
            base.Init(paintComponent, rendererComponent);
            spriteRenderer = rendererComponent as SpriteRenderer;
        }

        public override void AddPaintManager(IPaintManager paintManager)
        {
            base.AddPaintManager(paintManager);
            var plane = GetPlaneData(spriteRenderer);
            SetUVs(paintManager, plane.UVs);
            SetTriangles(paintManager, plane.Vertices, plane.Normals);
            Mesh.RecalculateBounds();
        }

        protected override void UpdateMeshBounds(IPaintManager paintManager)
        {
            MeshWorldBounds = spriteRenderer.bounds;
        }

        private PlaneData GetPlaneData(SpriteRenderer renderer)
        {
            var corners = renderer.GetLocalCorners();
            var edge1 = corners[1] - corners[0];
            var edge2 = corners[2] - corners[0];
            var normal =  Vector3.Cross(edge1, edge2).normalized;
            var planeData = new PlaneData
            {
                Vertices = new[] { corners[0], corners[1], corners[2], corners[3] },
                Normals = new [] { normal, normal, normal, normal },
                UVs = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right },
            };
            return planeData;
        }
    }
}