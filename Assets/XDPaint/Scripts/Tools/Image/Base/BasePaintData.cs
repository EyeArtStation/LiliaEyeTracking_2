using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using XDPaint.Core;
using XDPaint.Core.Layers;
using XDPaint.Core.Materials;
using XDPaint.Core.PaintModes;
using XDPaint.Core.PaintObject.Base;
using XDPaint.States;
using XDPaint.Utils;

namespace XDPaint.Tools.Image.Base
{
    public class BasePaintData : IPaintData
    {
        public IStatesController StatesController => paintManager.StatesController;
        public ILayersController LayersController => paintManager.LayersController;
        public IRenderTextureHelper TextureHelper { get; }
        public IRenderComponentsHelper RenderComponents { get; }
        public IBrush Brush => paintManager.Brush;
        public IPaintMode PaintMode => paintManager.PaintMode;
        public PaintSpace PaintSpace { get; }
        public int SubMesh => paintManager.SubMesh;
        public Material PaintMaterial => paintManager.Material.PaintMaterial;
        public Material PaintWorldMaterial => paintManager.Material.PaintWorldMaterial;
        public CommandBufferBuilder CommandBuilder => commandBufferBuilder;
        public Mesh QuadMesh => quadMesh;
        public Mesh PlaneMesh => planeMesh;
        public bool IsPainted => paintObject.IsPainted;
        public bool IsPainting => paintObject.IsPainting;
        public bool InBounds => paintObject.InBounds;
        public bool CanSmoothLines => paintObject.CanSmoothLines;

        private readonly IPaintManager paintManager;
        private readonly BasePaintObject paintObject;
        private CommandBufferBuilder commandBufferBuilder;
        private Mesh quadMesh;
        private Mesh planeMesh;

        public BasePaintData(IPaintManager currentPaintManager, IRenderTextureHelper currentRenderTextureHelper, IRenderComponentsHelper componentsHelper)
        {
            paintManager = currentPaintManager;
            paintObject = paintManager.PaintObject;
            PaintSpace = paintManager.PaintSpace;
            TextureHelper = currentRenderTextureHelper;
            RenderComponents = componentsHelper;
            commandBufferBuilder = new CommandBufferBuilder();
            quadMesh = MeshGenerator.GenerateQuad(Vector3.one, Vector3.zero);
            if (componentsHelper.RendererComponent is RawImage rawImage)
            {
                planeMesh = rawImage.rectTransform.CreatePlaneMesh();
            }
            else if (componentsHelper.RendererComponent is SpriteRenderer spriteRenderer)
            {
                planeMesh = spriteRenderer.CreatePlaneMesh();
            }
        }

        public virtual void Render()
        {
            paintManager.Render();
        }

        public virtual void SaveState()
        {
            paintObject.SaveUndoTexture();
        }

        public Coroutine StartCoroutine(IEnumerator coroutine)
        {
            return ((PaintManager)paintManager).StartCoroutine(coroutine);
        }

        public void StopCoroutine(IEnumerator coroutine)
        {
            ((PaintManager)paintManager).StopCoroutine(coroutine);
        }

        public void StopCoroutine(Coroutine coroutine)
        {
            ((PaintManager)paintManager).StopCoroutine(coroutine);
        }

        public void DoDispose()
        {
            if (commandBufferBuilder != null)
            {
                commandBufferBuilder.Release();
                commandBufferBuilder = null;
            }
            
            if (quadMesh != null)
            {
                Object.Destroy(quadMesh);
                quadMesh = null;
            }
            
            if (planeMesh != null)
            {
                Object.Destroy(planeMesh);
                planeMesh = null;
            }
        }
    }
}