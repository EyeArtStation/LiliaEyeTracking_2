using UnityEngine;
using UnityEngine.Rendering;

namespace XDPaint.Core
{
    public interface IRenderTextureHelper : IDisposable
    {
        void Init(Vector2Int dimensions, FilterMode filterMode);
        void CreateRenderTexture(RenderTarget renderTarget, RenderTextureFormat format = RenderTextureFormat.ARGB32);
        void ReleaseRenderTexture(RenderTarget renderTarget);
        RenderTargetIdentifier GetTarget(RenderTarget target);
        RenderTexture GetTexture(RenderTarget target);
    }
}