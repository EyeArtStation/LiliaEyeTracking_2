using UnityEngine;
using XDPaint.Core.Layers;
using XDPaint.Core.Materials;
using XDPaint.Core.PaintModes;
using XDPaint.Core.PaintObject.Base;
using XDPaint.States;
using XDPaint.Tools.Raycast;

namespace XDPaint.Core
{
    public interface IPaintManager : IDisposable
    {
        GameObject ObjectForPainting { get; }
        Paint Material { get; }
        BasePaintObject PaintObject { get; }
        ILayersController LayersController { get; }
        IStatesController StatesController { get; }
        IBrush Brush { get; }
        IPaintMode PaintMode { get; }
        PaintSpace PaintSpace { get; }
        Triangle[] Triangles { get; }
        int SubMesh { get; }
        int UVChannel { get; }
        bool Initialized { get; }
   
        void Init();
        bool IsActive();
        void Render();
    }
}