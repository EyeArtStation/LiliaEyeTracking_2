using UnityEngine;
using XDPaint.Core.PaintObject.Base;
using XDPaint.Utils;

namespace XDPaint.Core.PaintObject
{
#if XDP_DEBUG
    [System.Serializable]
#endif
    public sealed class SpriteRendererPaintObject : BasePaintObject
    {
        private SpriteRenderer renderer;
        private Sprite sprite;

        public override bool CanSmoothLines => true;
        
        public override Vector2 ConvertUVToTexturePosition(Vector2 uvPosition)
        {
            return new Vector2(
                Mathf.LerpUnclamped(sprite.rect.xMin, sprite.rect.xMax, uvPosition.x),
                Mathf.LerpUnclamped(sprite.rect.yMin, sprite.rect.yMax, uvPosition.y));
        }

        public override Vector2 ConvertTextureToUVPosition(Vector2 texturePosition)
        {
            return new Vector2(
                Mathf.InverseLerp(sprite.rect.xMin, sprite.rect.xMax, texturePosition.x),
                Mathf.InverseLerp(sprite.rect.yMin, sprite.rect.yMax, texturePosition.y));
        }

        protected override void Init()
        {
            ObjectTransform.TryGetComponent(out renderer);
            sprite = renderer.sprite;
        }

        protected override bool IsInBounds(Ray ray)
        {
            return renderer != null && renderer.bounds.IntersectRay(ray);
        }
    }
}