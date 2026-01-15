using System.Collections.Generic;
using UnityEngine;
using XDPaint.Core;
using XDPaint.Tools.Raycast.Base;

namespace XDPaint.Tools.Raycast
{
    public class RaycastMeshRendererData : BaseRaycastMeshData
    {
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        
        public override void Init(Component paintComponent, Component rendererComponent)
        {
            base.Init(paintComponent, rendererComponent);
            meshRenderer = rendererComponent as MeshRenderer;
            meshFilter = paintComponent as MeshFilter;
        }

        public override void AddPaintManager(IPaintManager paintManager)
        {
            base.AddPaintManager(paintManager);
            var mesh = meshFilter.sharedMesh;
            var uvs = new List<Vector2>();
            mesh.GetUVs(paintManager.UVChannel, uvs);
            SetUVs(paintManager, uvs);
            var vertices = new List<Vector3>();
            mesh.GetVertices(vertices);
            var normals = new List<Vector3>();
            mesh.GetNormals(normals);
            SetTriangles(paintManager, vertices, normals);
        }

        protected override void UpdateMeshBounds(IPaintManager paintManager)
        {
            MeshWorldBounds = meshRenderer.bounds;
        }
    }
}