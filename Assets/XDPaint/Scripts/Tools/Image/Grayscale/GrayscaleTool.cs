using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using XDPaint.Core;
using XDPaint.Core.Layers;
using XDPaint.Core.PaintObject.Data;
using XDPaint.Tools.Image.Base;
using XDPaint.Utils;
using Object = UnityEngine.Object;

namespace XDPaint.Tools.Image
{
    [Serializable]
    public sealed class GrayscaleTool : BasePaintTool<GrayscaleToolSettings>
    {
        [Preserve]
        public GrayscaleTool(IPaintData paintData) : base(paintData)
        {
            Settings = new GrayscaleToolSettings(paintData);
        }
        
        public override PaintTool Type => PaintTool.Grayscale;
        public override bool ShowPreview => preview && base.ShowPreview;
        public override bool RenderToLayer => false;
        public override bool RenderToInput => true;
        public override bool DrawPreProcess => true;
        public override bool BakeInputToLayer => bakeInputToPaint && Data.PaintMode.UsePaintInput;

        private GrayscaleData grayscaleData;
        private Material alphaMaskMaterial;
        private bool bakeInputToPaint;
        private bool preview;
        private bool initialized;

        public override void Enter()
        {
            preview = Data.Brush.Preview;
            base.Enter();
            bakeInputToPaint = false;
            Data.Render();
            grayscaleData = new GrayscaleData();
            grayscaleData.Enter(Data);
            UpdateRenderTextures();
            InitBrushMaterial();
            preview = true;
            Data.LayersController.OnActiveLayerSwitched += OnActiveLayerSwitched;
            initialized = true;
        }

        public override void Exit()
        {
            Data.LayersController.OnActiveLayerSwitched -= OnActiveLayerSwitched;
            Data.PaintMaterial.SetTexture(Constants.PaintShader.InputTexture, GetTexture(RenderTarget.Input));
            initialized = false;
            base.Exit();
            if (grayscaleData != null)
            {
                grayscaleData.Exit();
                grayscaleData = null;
            }

            if (alphaMaskMaterial != null)
            {
                Object.Destroy(alphaMaskMaterial);
                alphaMaskMaterial = null;
            }
        }

        public override void UpdateHover(PointerData pointerData)
        {
            base.UpdateHover(pointerData);
            if (ShowPreview && (grayscaleData.PrevPaintPosition != pointerData.TexturePosition || grayscaleData.PrevUV != pointerData.RaycastData.UVHit || grayscaleData.PrevPressure != pointerData.InputData.Pressure))
            {
                grayscaleData.PrevUV = pointerData.RaycastData.UVHit;
                grayscaleData.PrevPaintPosition = pointerData.TexturePosition;
                grayscaleData.PrevPressure = pointerData.InputData.Pressure;
            }
        }

        public override void UpdateUp(PointerUpData pointerUpData)
        {
            base.UpdateUp(pointerUpData);
            RenderGrayscaleMaterial();
            preview = true;
            Data.Render();
            bakeInputToPaint = false;
        }

        private void RenderGrayscaleMaterial()
        {
            var previousTexture = grayscaleData.GrayscaleMaterial.GetTexture(Constants.GrayscaleShader.MaskTexture);
            grayscaleData.GrayscaleMaterial.SetTexture(Constants.GrayscaleShader.MaskTexture, null);
            Graphics.Blit(GetTexture(RenderTarget.ActiveLayer), grayscaleData.GrayscaleTexture, grayscaleData.GrayscaleMaterial);
            grayscaleData.GrayscaleMaterial.SetTexture(Constants.GrayscaleShader.MaskTexture, previousTexture);
        }

        #region Initialization

        private void OnActiveLayerSwitched(ILayer layer)
        {
            UpdateRenderTextures();
        }
        
        private void InitBrushMaterial()
        {
            if (alphaMaskMaterial == null)
            {
                alphaMaskMaterial = new Material(Tools.Settings.Instance.SpriteMaskShader);
                alphaMaskMaterial.SetInt(Constants.AlphaMaskShader.SrcColorBlend, (int)BlendMode.One);
                alphaMaskMaterial.SetInt(Constants.AlphaMaskShader.DstColorBlend, (int)BlendMode.Zero);
            }
            
            alphaMaskMaterial.mainTexture = GetTexture(RenderTarget.ActiveLayer);
        }
        
        private void UpdateRenderTextures()
        {
            var renderTexture = GetTexture(RenderTarget.ActiveLayer);
            if (grayscaleData.GrayscaleTexture != null && grayscaleData.GrayscaleTexture.IsCreated() && 
                (grayscaleData.GrayscaleTexture.width != renderTexture.width || grayscaleData.GrayscaleTexture.height != renderTexture.height))
            {
                var grayscaleRenderTexture = grayscaleData.GrayscaleTexture;
                grayscaleRenderTexture.Release();
                grayscaleRenderTexture.width = renderTexture.width;
                grayscaleRenderTexture.height = renderTexture.height;
                grayscaleRenderTexture.Create();
            }
            else if (grayscaleData.GrayscaleTexture == null)
            {
                grayscaleData.GrayscaleTexture = RenderTextureFactory.CreateRenderTexture(renderTexture);
                grayscaleData.GrayscaleTarget = new RenderTargetIdentifier(grayscaleData.GrayscaleTexture);
            }
            
            grayscaleData.InitMaterial();
            RenderGrayscaleMaterial();
        }

        #endregion

        public override void OnDrawPreProcess(RenderTargetIdentifier combined)
        {
            base.OnDrawPreProcess(combined);
            if (Data.IsPainted && initialized)
            {
                grayscaleData.GrayscaleMaterial.SetTexture(Constants.GrayscaleShader.MaskTexture, GetTexture(RenderTarget.Input));
                Graphics.Blit(GetTexture(RenderTarget.ActiveLayer), grayscaleData.GrayscaleTexture, grayscaleData.GrayscaleMaterial);
                bakeInputToPaint = true;
            }
        }

        public override void OnDrawProcess(RenderTargetIdentifier combined)
        {
            if (!initialized)
            {
                base.OnDrawProcess(combined);
                return;
            }

            base.OnDrawProcess(combined);
            if (!Data.PaintMode.UsePaintInput && Data.IsPainted)
            {
                OnBakeInputToLayer(GetTarget(RenderTarget.ActiveLayer));
            }
        }

        protected override void DrawCurrentLayer()
        {
            if ((!Data.IsPainting || !initialized) && !bakeInputToPaint)
            {
                base.DrawCurrentLayer();
                return;
            }

            Data.PaintMaterial.SetTexture(Constants.PaintShader.PaintTexture, GetTexture(RenderTarget.ActiveLayer));
            Data.PaintMaterial.SetTexture(Constants.PaintShader.InputTexture, grayscaleData.GrayscaleTexture);
            Data.CommandBuilder.Clear().LoadOrtho().SetRenderTarget(GetTarget(RenderTarget.ActiveLayerTemp)).DrawMesh(Data.QuadMesh, Data.PaintMaterial, InputToPaintPass).Execute();
        }

        protected override void RenderPreviewUV(RenderTargetIdentifier combined)
        {
            var previousPaintTexture = Data.PaintMaterial.GetTexture(Constants.PaintShader.PaintTexture);
            Data.PaintMaterial.SetTexture(Constants.PaintShader.PaintTexture, Texture2D.blackTexture);
            Data.CommandBuilder.Clear().LoadOrtho().SetRenderTarget(GetTarget(RenderTarget.ActiveLayerTemp)).DrawMesh(Data.QuadMesh, Data.PaintMaterial, PaintPass.Preview).Execute();
            Data.PaintMaterial.SetTexture(Constants.PaintShader.PaintTexture, previousPaintTexture);
            
            grayscaleData.GrayscaleMaterial.SetTexture(Constants.GrayscaleShader.MaskTexture, GetTexture(RenderTarget.ActiveLayerTemp));
            Graphics.Blit(GetTexture(RenderTarget.ActiveLayer), grayscaleData.GrayscaleTexture, grayscaleData.GrayscaleMaterial);

            alphaMaskMaterial.mainTexture = grayscaleData.GrayscaleTexture;
            alphaMaskMaterial.SetTexture(Constants.AlphaMaskShader.MaskTexture, GetTexture(RenderTarget.ActiveLayerTemp));
            Graphics.Blit(alphaMaskMaterial.mainTexture, GetTexture(RenderTarget.Input), alphaMaskMaterial);
        }

        protected override void RenderPreviewWorld(RenderTargetIdentifier combined)
        {
            var previousMainTexture = Data.PaintWorldMaterial.GetTexture(Constants.PaintWorldShader.MainTexture);
            Data.PaintWorldMaterial.SetTexture(Constants.PaintWorldShader.MainTexture, Texture2D.blackTexture);
            Data.PaintWorldMaterial.color = Data.Brush.Color;
            OnPaintWorldRender(GetTarget(RenderTarget.ActiveLayerTemp));
            Data.PaintWorldMaterial.SetTexture(Constants.PaintWorldShader.MainTexture, previousMainTexture);
            
            grayscaleData.GrayscaleMaterial.SetTexture(Constants.GrayscaleShader.MaskTexture, GetTexture(RenderTarget.ActiveLayerTemp));
            Graphics.Blit(GetTexture(RenderTarget.ActiveLayer), grayscaleData.GrayscaleTexture, grayscaleData.GrayscaleMaterial);

            alphaMaskMaterial.mainTexture = grayscaleData.GrayscaleTexture;
            alphaMaskMaterial.SetTexture(Constants.AlphaMaskShader.MaskTexture, GetTexture(RenderTarget.ActiveLayerTemp));
            Graphics.Blit(alphaMaskMaterial.mainTexture, GetTexture(RenderTarget.Input), alphaMaskMaterial);
        }
        
        public override void OnBakeInputToLayer(RenderTargetIdentifier activeLayer)
        {
            if (bakeInputToPaint)
            {
                var inputTexture = GetTexture(RenderTarget.Input);
                Data.PaintMaterial.SetTexture(Constants.PaintShader.InputTexture, grayscaleData.GrayscaleTexture);
                base.OnBakeInputToLayer(activeLayer);
                Data.PaintMaterial.SetTexture(Constants.PaintShader.InputTexture, inputTexture);
            }
        }
        
        [Serializable]
        private class GrayscaleData
        {
            public Material GrayscaleMaterial;
            public RenderTexture GrayscaleTexture;
            public RenderTargetIdentifier GrayscaleTarget;
            public Vector2 PrevUV = -Vector2.one;
            public Vector2 PrevPaintPosition = -Vector2.one;
            public float PrevPressure = -1f;
            private IPaintData data;

            public void Enter(IPaintData paintData)
            {
                data = paintData;
            }
        
            public void Exit()
            {
                if (GrayscaleMaterial != null)
                {
                    Object.Destroy(GrayscaleMaterial);
                    GrayscaleMaterial = null;
                }
                
                if (GrayscaleTexture != null)
                {
                    GrayscaleTexture.ReleaseTexture();
                    GrayscaleTexture = null;
                }
            }
        
            public void InitMaterial()
            {
                if (GrayscaleMaterial == null)
                {
                    GrayscaleMaterial = new Material(Tools.Settings.Instance.GrayscaleShader);
                }
                
                GrayscaleMaterial.mainTexture = data.LayersController.ActiveLayer.RenderTexture;
                GrayscaleMaterial.SetTexture(Constants.GrayscaleShader.MaskTexture, data.TextureHelper.GetTexture(RenderTarget.Input));
            }
        }
    }
}