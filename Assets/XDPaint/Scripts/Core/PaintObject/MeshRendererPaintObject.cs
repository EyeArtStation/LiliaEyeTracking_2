using UnityEngine;
using XDPaint.Core.PaintObject.Base;

namespace XDPaint.Core.PaintObject
{
#if XDP_DEBUG
    [System.Serializable]
#endif
    public sealed class MeshRendererPaintObject : BasePaintObject
    {
        private Renderer renderer;
        private Mesh mesh;
        private Bounds bounds;

        public override bool CanSmoothLines => false;
        
        public override Vector2 ConvertUVToTexturePosition(Vector2 uvPosition)
        {
            return new Vector2(Paint.SourceTexture.width * uvPosition.x, Paint.SourceTexture.height * uvPosition.y);
        }
        
        public override Vector2 ConvertTextureToUVPosition(Vector2 texturePosition)
        {
            return new Vector2(texturePosition.x / Paint.SourceTexture.width, texturePosition.y / Paint.SourceTexture.height);
        }

        protected override void Init()
        {
            ObjectTransform.TryGetComponent(out renderer);
            
            if (ObjectTransform.TryGetComponent<MeshFilter>(out var meshFilter))
            {
                mesh = meshFilter.sharedMesh;
            }
            
            if (mesh == null)
            {
                Debug.LogError("Can't find MeshFilter component!");
            }
        }

        protected override bool IsInBounds(Ray ray)
        {
            return renderer != null && renderer.bounds.IntersectRay(ray);
        }
    }
}