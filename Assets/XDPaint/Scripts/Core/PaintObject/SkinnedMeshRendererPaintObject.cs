using UnityEngine;
using XDPaint.Controllers;
using XDPaint.Core.PaintObject.Base;
using XDPaint.Utils;

namespace XDPaint.Core.PaintObject
{
#if XDP_DEBUG
    [System.Serializable]
#endif
    public sealed class SkinnedMeshRendererPaintObject : BasePaintObject
    {
        private SkinnedMeshRenderer skinnedMeshRenderer;
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
            if (ObjectTransform.TryGetComponent(out skinnedMeshRenderer))
            {
				mesh = RaycastController.Instance.GetMesh(PaintManager);
            }

            if (mesh == null)
            {
                Debug.LogError("Can't find SkinnedMeshRenderer component!");
            }
        }

        protected override bool IsInBounds(Ray ray)
        {
            if (skinnedMeshRenderer != null)
            {
                bounds = mesh.GetSubMesh(PaintManager.SubMesh).bounds;
                bounds = bounds.TransformBounds(skinnedMeshRenderer.transform);
            }
            
            return bounds.IntersectRay(ray);
        }
    }
}