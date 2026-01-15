using UnityEngine;
using UnityEngine.UI;
using XDPaint.Core.PaintObject.Base;
using XDPaint.Utils;

namespace XDPaint.Core.PaintObject
{
#if XDP_DEBUG
    [System.Serializable]
#endif
    public sealed class CanvasRendererPaintObject : BasePaintObject
    {
        private RawImage rawImage;
        
        public override bool CanSmoothLines => true;

        public override Vector2 ConvertUVToTexturePosition(Vector2 uvPosition)
        {
            var texture = rawImage.texture;
            var uvRect = rawImage.uvRect;
            return new Vector2(
                Mathf.Lerp(uvRect.xMin, uvRect.xMax, uvPosition.x) * texture.width,
                Mathf.Lerp(uvRect.yMin, uvRect.yMax, uvPosition.y) * texture.height
            );
        }
        
        public override Vector2 ConvertTextureToUVPosition(Vector2 texturePosition)
        {
            var texture = rawImage.texture;
            var uvRect = rawImage.uvRect;
            return new Vector2(
                Mathf.InverseLerp(uvRect.xMin * texture.width, uvRect.xMax * texture.width, texturePosition.x),
                Mathf.InverseLerp(uvRect.yMin * texture.height, uvRect.yMax * texture.height, texturePosition.y)
            );
        }
        
        protected override void Init()
        {
            ObjectTransform.TryGetComponent(out rawImage);
        }

        protected override bool IsInBounds(Ray ray)
        {
            return rawImage.rectTransform != null && rawImage.rectTransform.GetMaxBounds().IntersectRay(ray);
        }
    }
}