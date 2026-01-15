using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using XDPaint.Core;
using XDPaint.Tools.Image.Base;
using XDPaint.Utils;
using Object = UnityEngine.Object;

namespace XDPaint.Tools.Image
{
    [Serializable]
    public sealed class GaussianBlurTool : BasePaintTool<GaussianBlurToolSettings>
    {
        [Preserve]
        public GaussianBlurTool(IPaintData paintData) : base(paintData)
        {
            Settings = new GaussianBlurToolSettings(paintData);
        }

        public override PaintTool Type => PaintTool.BlurGaussian;
        public override bool ShowPreview => false;
        public override bool RenderToLayer => false;
        public override bool RenderToInput => true;
        public override bool DrawPreProcess => true;
        public override bool BakeInputToLayer => false;

        private RenderTexture SourceTexture => Settings.UseAllActiveLayers
            ? Data.TextureHelper.GetTexture(RenderTarget.Combined)
            : Data.LayersController.ActiveLayer.RenderTexture;

        private BlurData blurData;
        private bool initialized;

        public override void Enter()
        {
            base.Enter();
            blurData = new BlurData();
            blurData.Enter(Data);
            UpdateRenderTextures();
            initialized = true;
        }

        public override void Exit()
        {
            Data.PaintMaterial.SetTexture(Constants.PaintShader.InputTexture, GetTexture(RenderTarget.Input));
            initialized = false;
            base.Exit();
            if (blurData != null)
            {
                blurData.Exit();
                blurData = null;
            }
        }

        #region Initialization

        private void UpdateRenderTextures()
        {
            if (blurData.BlurTexture != null)
                return;

            var renderTexture = GetTexture(RenderTarget.ActiveLayer);
            blurData.BlurTexture = RenderTextureFactory.CreateRenderTexture(renderTexture);
            blurData.BlurTarget = new RenderTargetIdentifier(blurData.BlurTexture);
            Data.CommandBuilder.LoadOrtho().Clear().SetRenderTarget(blurData.BlurTarget).ClearRenderTarget().Execute();
            blurData.PreBlurTexture = RenderTextureFactory.CreateRenderTexture(renderTexture);
            blurData.PreBlurTarget = new RenderTargetIdentifier(blurData.PreBlurTexture);
            Data.CommandBuilder.LoadOrtho().Clear().SetRenderTarget(blurData.PreBlurTarget).ClearRenderTarget().Execute();
            blurData.InitMaterials();
        }

        #endregion

        private void Blur(Material blurMaterial, RenderTexture source, RenderTexture destination)
        {
            if (blurMaterial != null)
            {
                blurMaterial.SetFloat(Constants.GaussianBlurShader.Size, Settings.KernelSize);
                blurMaterial.SetFloat(Constants.GaussianBlurShader.Spread, Settings.Spread);
                Graphics.Blit(source, destination, blurMaterial, 0);
            }
            else
            {
                Graphics.Blit(source, destination);
            }
        }

        public override void OnDrawPreProcess(RenderTargetIdentifier combined)
        {
            base.OnDrawPreProcess(combined);
            if (Data.IsPainted && initialized)
            {
                blurData.MaskMaterial.color = Data.Brush.Color;
                //clear render texture
                Data.CommandBuilder.Clear().LoadOrtho().SetRenderTarget(blurData.PreBlurTarget).ClearRenderTarget().Execute();
                //blur
                Blur(blurData.BlurMaterial, SourceTexture, blurData.PreBlurTexture);
                //render with mask
                Data.CommandBuilder.Clear().SetRenderTarget(blurData.BlurTarget).ClearRenderTarget().DrawMesh(Data.QuadMesh, blurData.MaskMaterial).Execute();
            }
        }

        public override void OnDrawProcess(RenderTargetIdentifier combined)
        {
            if (!initialized)
            {
                base.OnDrawProcess(combined);
                Data.CommandBuilder.Clear().SetRenderTarget(GetTarget(RenderTarget.Input)).ClearRenderTarget().Execute();
                return;
            }
            
            Data.PaintMaterial.SetTexture(Constants.PaintShader.InputTexture, blurData.BlurTexture);
            base.OnDrawProcess(combined);
            if (Data.IsPainted)
            {
                OnBakeInputToLayer(GetTarget(RenderTarget.ActiveLayer));
            }
        }
        
        protected override void DrawCurrentLayer()
        {
            if (!Data.IsPainting || !initialized)
            {
                base.DrawCurrentLayer();
                return;
            }
            
            Data.PaintMaterial.SetTexture(Constants.PaintShader.PaintTexture, GetTexture(RenderTarget.ActiveLayer));
            Data.PaintMaterial.SetTexture(Constants.PaintShader.InputTexture, blurData.BlurTexture);
            Data.CommandBuilder.Clear().LoadOrtho().SetRenderTarget(GetTarget(RenderTarget.ActiveLayerTemp)).DrawMesh(Data.QuadMesh, Data.PaintMaterial, PaintPass.Blend).Execute();
        }
        
        public override void OnBakeInputToLayer(RenderTargetIdentifier activeLayer)
        {
            base.OnBakeInputToLayer(activeLayer);
            Data.PaintMaterial.SetTexture(Constants.PaintShader.InputTexture, GetTexture(RenderTarget.Input));
            Data.CommandBuilder.Clear().SetRenderTarget(GetTarget(RenderTarget.Input)).ClearRenderTarget().Execute();
        }

        [Serializable]
        private class BlurData
        {
            public Material BlurMaterial;
            public Material MaskMaterial;
            public RenderTexture BlurTexture;
            public RenderTargetIdentifier BlurTarget;
            public RenderTexture PreBlurTexture;
            public RenderTargetIdentifier PreBlurTarget;
            
            private IPaintData data;

            public void Enter(IPaintData paintData)
            {
                data = paintData;
            }
        
            public void Exit()
            {
                if (PreBlurTexture != null)
                {
                    PreBlurTexture.ReleaseTexture();
                    PreBlurTexture = null;
                }
                
                if (BlurTexture != null)
                {
                    BlurTexture.ReleaseTexture();
                    BlurTexture = null;
                }
                
                if (BlurMaterial != null)
                {
                    Object.Destroy(BlurMaterial);
                    BlurMaterial = null;
                }
                
                if (MaskMaterial != null)
                {
                    Object.Destroy(MaskMaterial);
                    MaskMaterial = null;
                }
            }
        
            public void InitMaterials()
            {
                if (MaskMaterial == null)
                {
                    MaskMaterial = new Material(Tools.Settings.Instance.BrushBlurShader);
                }
                
                MaskMaterial.mainTexture = PreBlurTexture;
                MaskMaterial.SetTexture(Constants.GaussianBlurShader.MaskTexture, data.TextureHelper.GetTexture(RenderTarget.Input));
                MaskMaterial.color = data.Brush.Color;
                if (BlurMaterial == null)
                {
                    BlurMaterial = new Material(Tools.Settings.Instance.GaussianBlurShader);
                }
                
                BlurMaterial.mainTexture = BlurTexture;
            }
        }
    }
}