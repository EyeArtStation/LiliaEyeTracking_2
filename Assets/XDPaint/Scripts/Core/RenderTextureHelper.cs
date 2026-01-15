using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using XDPaint.Utils;

namespace XDPaint.Core
{
#if XDP_DEBUG
    [Serializable]
#endif
    public class RenderTextureHelper : IRenderTextureHelper
    {
        private Dictionary<RenderTarget, KeyValuePair<RenderTexture, RenderTargetIdentifier>> renderTexturesData;
        private Vector2Int textureDimensions;
        private FilterMode filterMode;

        /// <summary>
        /// Creates RenderTextures:
        /// Input - for paint between using Input down and up events (AdditivePaintMode) or for current frame paint result storing (DefaultPaintMode);
        /// Combined - for combining source texture with paint textures and for brush preview;
        /// CombinedTemp - for combining layers (optional);
        /// ActiveLayerTemp - temporary texture of the active layer (optional);
        /// Unwrapped - texture that contains unwrapped mesh (optional).
        /// </summary>
        /// <param name="dimensions"></param>
        /// <param name="filter"></param>
        public void Init(Vector2Int dimensions, FilterMode filter)
        {
            textureDimensions = dimensions;
            filterMode = filter;
            renderTexturesData = new Dictionary<RenderTarget, KeyValuePair<RenderTexture, RenderTargetIdentifier>>();
            CreateRenderTexture(RenderTarget.Input);
            CreateRenderTexture(RenderTarget.Combined);
            CreateRenderTexture(RenderTarget.ActiveLayerTemp);
        }

        public void CreateRenderTexture(RenderTarget renderTarget, RenderTextureFormat format = RenderTextureFormat.ARGB32)
        {
            if (!renderTexturesData.ContainsKey(renderTarget) || renderTexturesData[renderTarget].Equals(default(KeyValuePair<RenderTexture, RenderTargetIdentifier>)))
            {
                var renderTexture = RenderTextureFactory.CreateRenderTexture(textureDimensions.x, textureDimensions.y, 0, format, filterMode);
                renderTexture.name = Enum.GetName(typeof(RenderTarget), renderTarget) ?? string.Empty;
                renderTexturesData[renderTarget] = new KeyValuePair<RenderTexture, RenderTargetIdentifier>(renderTexture, new RenderTargetIdentifier(renderTexture));
            }
        }

        public void ReleaseRenderTexture(RenderTarget renderTarget)
        {
            if (!renderTexturesData.ContainsKey(renderTarget)) 
                return;
            
            renderTexturesData[renderTarget].Key.ReleaseTexture();
            renderTexturesData[renderTarget] = default;
        }

        public void DoDispose()
        {
            ReleaseRT(RenderTarget.Input);
            ReleaseRT(RenderTarget.Combined);
            ReleaseRT(RenderTarget.CombinedTemp);
            ReleaseRT(RenderTarget.ActiveLayerTemp);
            ReleaseRT(RenderTarget.Unwrapped);
        }

        public RenderTargetIdentifier GetTarget(RenderTarget target)
        {
            return renderTexturesData[target].Value;
        }

        public RenderTexture GetTexture(RenderTarget target)
        {
            return renderTexturesData.TryGetValue(target, out var result) ? result.Key : null;
        }

        private void ReleaseRT(RenderTarget target)
        {
            if (renderTexturesData != null && renderTexturesData.ContainsKey(target))
            {
                renderTexturesData[target].Key.ReleaseTexture();
                renderTexturesData.Remove(target);
            }
        }
    }
}