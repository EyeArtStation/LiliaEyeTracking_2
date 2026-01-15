using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using XDPaint.Core;
using XDPaint.Core.PaintObject.Data;
using XDPaint.Tools.Image.Base;

namespace XDPaint.Tools.Image
{
    [Serializable]
    public sealed class EraseTool : BasePaintTool<EraseToolSettings>
    {
        [Preserve]
        public EraseTool(IPaintData paintData) : base(paintData)
        {
            Settings = new EraseToolSettings(paintData);
        }

        public override PaintTool Type => PaintTool.Erase;
        public override bool DrawPreProcess => true;
        public override bool RenderToLayer => !Data.PaintMode.UsePaintInput;
        protected override PaintPass InputToPaintPass => PaintPass.Erase;
        
        public override void Enter()
        {
            base.Enter();
            SetBrushBlending(Data.PaintMode.UsePaintInput);
        }

        public override void Exit()
        {
            base.Exit();
            base.SetBrushBlending(Data.PaintMode.UsePaintInput);
        }
        
        public override void UpdateDown(PointerData pointerData)
        {
            base.UpdateDown(pointerData);
            SetBrushBlending(true);
        }
        
        public override void UpdatePress(PointerData pointerData)
        {
            base.UpdatePress(pointerData);
            SetBrushBlending(Data.PaintMode.UsePaintInput);
        }

        public override void OnDrawPreProcess(RenderTargetIdentifier combined)
        {
            base.OnDrawPreProcess(combined);
            if (Data.IsPainted)
            {
                Data.PaintMaterial.SetTexture(Constants.PaintShader.PaintTexture, GetTexture(RenderTarget.ActiveLayer));
                Data.CommandBuilder.Clear().SetRenderTarget(GetTarget(RenderTarget.ActiveLayerTemp)).ClearRenderTarget().DrawMesh(Data.QuadMesh, Data.PaintMaterial, PaintPass.Paint, PaintPass.Erase).Execute();
                Data.PaintMaterial.SetTexture(Constants.PaintShader.PaintTexture, GetTexture(RenderTarget.ActiveLayerTemp));
            }
        }
        
        protected override void SetBrushBlending(bool usePaintInput)
        {
            if (usePaintInput)
            {
                base.SetBrushBlending(true);
            }
            else
            {
                var material = Data.Brush.Material;
                material.SetInt(Constants.BrushShader.BlendOpColor, (int)BlendOp.Add);
                material.SetInt(Constants.BrushShader.BlendOpAlpha, (int)BlendOp.ReverseSubtract);
                material.SetInt(Constants.BrushShader.SrcColorBlend, (int)BlendMode.Zero);
                material.SetInt(Constants.BrushShader.DstColorBlend, (int)BlendMode.One);
                material.SetInt(Constants.BrushShader.SrcAlphaBlend, (int)BlendMode.SrcAlpha);
                material.SetInt(Constants.BrushShader.DstAlphaBlend, (int)BlendMode.OneMinusSrcAlpha);
            }
        }
        
        protected override void RenderPreviewUV(RenderTargetIdentifier combined)
        {
            var previousPaintTexture = Data.PaintMaterial.GetTexture(Constants.PaintShader.PaintTexture);
            Data.PaintMaterial.SetTexture(Constants.PaintShader.PaintTexture, Texture2D.blackTexture);
            Data.CommandBuilder.Clear().LoadOrtho().SetRenderTarget(GetTarget(RenderTarget.Input)).DrawMesh(Data.QuadMesh, Data.PaintMaterial, PaintPass.Preview).Execute();
            Data.PaintMaterial.SetTexture(Constants.PaintShader.PaintTexture, previousPaintTexture);
        }
        
        protected override void RenderPreviewWorld(RenderTargetIdentifier combined)
        {
            var previousMainTexture = Data.PaintWorldMaterial.GetTexture(Constants.PaintWorldShader.MainTexture);
            Data.PaintWorldMaterial.SetTexture(Constants.PaintWorldShader.MainTexture, Texture2D.blackTexture);
            Data.PaintWorldMaterial.color = Data.Brush.Color;
            OnPaintWorldRender(GetTarget(RenderTarget.Input));
            Data.PaintWorldMaterial.SetTexture(Constants.PaintWorldShader.MainTexture, previousMainTexture);
        }
    }
}