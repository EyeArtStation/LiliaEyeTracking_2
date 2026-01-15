using System.Collections;
using UnityEngine;
using XDPaint.Core;
using XDPaint.Core.Layers;
using XDPaint.Core.Materials;
using XDPaint.Core.PaintModes;
using XDPaint.States;
using XDPaint.Utils;

namespace XDPaint.Tools.Image.Base
{
    public interface IPaintData : IDisposable
    {
        IStatesController StatesController { get; }
        ILayersController LayersController { get; }
        IRenderTextureHelper TextureHelper { get; }
        IRenderComponentsHelper RenderComponents { get; }
        IBrush Brush { get; }
        IPaintMode PaintMode { get; }
        PaintSpace PaintSpace { get; }
        int SubMesh { get; }
        
        Material PaintMaterial { get; }
        Material PaintWorldMaterial { get; }
        CommandBufferBuilder CommandBuilder { get; }
        Mesh QuadMesh { get; }
        Mesh PlaneMesh { get; }
        bool InBounds { get; }
        bool IsPainting { get; }
        bool IsPainted { get; }
        bool CanSmoothLines { get; }

        void Render();
        void SaveState();
        Coroutine StartCoroutine(IEnumerator coroutine);
        void StopCoroutine(IEnumerator coroutine);
        void StopCoroutine(Coroutine coroutine);
    }
}