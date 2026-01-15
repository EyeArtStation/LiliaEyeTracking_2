using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using XDPaint.Core.Materials;
using XDPaint.Tools.Image.Base;
using XDPaint.Utils;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace XDPaint.Core.PaintObject.Base
{
    public class BasePaintObjectRenderer : IDisposable
    {
        protected IPaintData PaintData;
        protected Paint Paint;
        protected BaseLineDrawer LineDrawer;
        private Renderer rendererComponent;
        private Mesh lineMesh;
        private Mesh quadMesh;
        private CommandBufferBuilder commandBuffer;
        private Action<RenderTarget> onPaintWorldRender;

        public IPaintTool Tool { get; set; }

        public void InitRenderer(Renderer rendererComponentInstance)
        {
            rendererComponent = rendererComponentInstance;
            if (PaintData.PaintSpace == PaintSpace.World)
            {
                if (PaintData.PlaneMesh != null)
                {
                    onPaintWorldRender = target =>
                    {
                        commandBuffer.Clear().SetRenderTarget(GetRenderTarget(target)).DrawMesh(PaintData.PlaneMesh,
                            PaintData.RenderComponents.RendererComponent.transform.localToWorldMatrix,
                            PaintData.PaintWorldMaterial, PaintData.SubMesh, PaintWorldPass.Draw).Execute();
                    };
                }
                else
                {
                    onPaintWorldRender = target =>
                    {
                        commandBuffer.Clear().SetRenderTarget(GetRenderTarget(target)).DrawRenderer(rendererComponent,
                            PaintData.PaintWorldMaterial, PaintData.SubMesh, PaintWorldPass.Draw).Execute();
                    };
                }
                
                if (PaintData.RenderComponents.IsMesh())
                {
                    commandBuffer.Clear().SetRenderTarget(GetRenderTarget(RenderTarget.Unwrapped)).ClearRenderTarget(true, true, Color.black).
                        DrawRenderer(rendererComponent, PaintData.PaintWorldMaterial, PaintData.SubMesh, PaintWorldPass.Unwrap).Execute();
                    PaintData.PaintWorldMaterial.SetTexture(Constants.PaintWorldShader.IslandMapTexture, PaintData.TextureHelper.GetTexture(RenderTarget.Unwrapped));
                }
            }
        }

        public virtual void DoDispose()
        {
            commandBuffer?.Release();
            
            if (lineMesh != null)
            {
                Object.Destroy(lineMesh);
                lineMesh = null;
            }
            
            if (quadMesh != null)
            {
                Object.Destroy(quadMesh);
                quadMesh = null;
            }
        }
        
        protected void InitRenderer(IPaintManager paintManager, Paint paint)
        {
            lineMesh = new Mesh();
            Paint = paint;
            LineDrawer = new BaseLineDrawer(paintManager);
            LineDrawer.Init(Paint.SourceTexture.GetDimensions(), RenderLineUV, RenderMesh);
            commandBuffer = new CommandBufferBuilder($"XDPaintObject_{GetType().Name}");
            InitQuadMesh();
        }

        private void InitQuadMesh()
        {
            if (quadMesh == null)
            {
                quadMesh = MeshGenerator.GenerateQuad(Vector3.one, Vector3.zero);
            }
        }

        protected void ClearTexture(RenderTarget target)
        {
            commandBuffer.Clear().SetRenderTarget(PaintData.TextureHelper.GetTarget(target)).ClearRenderTarget().Execute();
        }
        
        protected void ClearTexture(RenderTexture renderTexture, Color color)
        {
            commandBuffer.Clear().SetRenderTarget(renderTexture).ClearRenderTarget(color).Execute();
        }
        
        protected void DrawPreProcess()
        {
            if (Tool.DrawPreProcess)
            {
                Tool.OnDrawPreProcess(PaintData.TextureHelper.GetTarget(RenderTarget.Combined));
            }
        }

        protected void DrawProcess()
        {
            if (Tool.DrawProcess)
            {
                Tool.OnDrawProcess(PaintData.TextureHelper.GetTarget(RenderTarget.Combined));
            }

            if (Tool.DrawPostProcess)
            {
                Tool.OnDrawPostProcess(PaintData.TextureHelper.GetTarget(RenderTarget.Combined));
            }
        }

        protected void BakeInputToPaint()
        {
            if (Tool.BakeInputToLayer)
            {
                Tool.OnBakeInputToLayer(PaintData.LayersController.ActiveLayer.RenderTarget);
            }
        }
        
        protected void SetPaintWorldProperties(BaseWorldData worldData, Vector3 pointerPosition, float[] brushSizes)
        {
            PaintData.PaintWorldMaterial.SetTexture(Constants.PaintWorldShader.MainTexture, PaintData.TextureHelper.GetTexture(RenderTarget.Input));
            PaintData.PaintWorldMaterial.SetTexture(Constants.PaintWorldShader.BrushTexture, PaintData.Brush.RenderTexture);
            PaintData.PaintWorldMaterial.SetVectorArray(Constants.PaintWorldShader.PositionsArray, worldData.Positions);
            PaintData.PaintWorldMaterial.SetVectorArray(Constants.PaintWorldShader.NormalsArray, worldData.Normals);
            PaintData.PaintWorldMaterial.SetFloatArray(Constants.PaintWorldShader.RotationsArray, worldData.Rotations);
            PaintData.PaintWorldMaterial.SetInt(Constants.PaintWorldShader.PositionsArrayCount, worldData.Count);
            PaintData.PaintWorldMaterial.SetVector(Constants.PaintWorldShader.PointerPosition, pointerPosition);
            PaintData.PaintWorldMaterial.SetFloatArray(Constants.PaintWorldShader.BrushSizes, brushSizes);
            PaintData.PaintWorldMaterial.color = PaintData.Brush.Color;
        }

        protected void SetPaintWorldCount(int count)
        {
            PaintData.PaintWorldMaterial.SetInt(Constants.PaintWorldShader.PositionsArrayCount, count);
        }

        protected void UpdateQuadMesh(Vector2 paintPosition, Vector2 renderOffset, float quadScale, bool randomizeAngle = false)
        {
            var center = (Vector3)paintPosition;
            var v1 = center + new Vector3(-PaintData.Brush.RenderTexture.width, PaintData.Brush.RenderTexture.height) * quadScale / 2f;
            var v2 = center + new Vector3(PaintData.Brush.RenderTexture.width, PaintData.Brush.RenderTexture.height) * quadScale / 2f;
            var v3 = center + new Vector3(PaintData.Brush.RenderTexture.width, -PaintData.Brush.RenderTexture.height) * quadScale / 2f;
            var v4 = center + new Vector3(-PaintData.Brush.RenderTexture.width, -PaintData.Brush.RenderTexture.height) * quadScale / 2f;
            if (randomizeAngle)
            {
                var quaternion = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward);
                v1 = quaternion * (v1 - center) + center;
                v2 = quaternion * (v2 - center) + center;
                v3 = quaternion * (v3 - center) + center;
                v4 = quaternion * (v4 - center) + center;
            }

            var scale = new Vector2(Paint.SourceTexture.width, Paint.SourceTexture.height);
            v1 = v1 / scale + renderOffset;
            v2 = v2 / scale + renderOffset;
            v3 = v3 / scale + renderOffset;
            v4 = v4 / scale + renderOffset;

            quadMesh.SetVertices(new[] { v1, v2, v3, v4 });
            quadMesh.SetUVs(0, new[] {Vector2.up, Vector2.one, Vector2.right, Vector2.zero});
            GL.LoadOrtho();
        }
        
        private RenderTargetIdentifier GetRenderTarget(RenderTarget target)
        {
            return target == RenderTarget.ActiveLayer ? PaintData.LayersController.ActiveLayer.RenderTexture : PaintData.TextureHelper.GetTarget(target);
        }

        protected void RenderMesh(Mesh mesh = null)
        {
            if (mesh == null)
            {
                mesh = quadMesh;
            }
                
            if (Tool.RenderToLayer && !Tool.RenderToInput)
            {
                if (PaintData.PaintSpace == PaintSpace.UV)
                {
                    RenderToTarget(PaintData.PaintMode.RenderTarget, mesh);
                }
                else if (PaintData.PaintSpace == PaintSpace.World)
                {
                    RenderToTargetWorld(PaintData.PaintMode.RenderTarget);
                }
            }
            
            RenderToInput(mesh);
        }

        private void RenderToInput(Mesh mesh)
        {
            if (!Tool.RenderToInput) 
                return;
            
            var clear = !PaintData.PaintMode.UsePaintInput;
            if (PaintData.PaintSpace == PaintSpace.UV)
            {
                RenderToTarget(RenderTarget.Input, mesh, clear);
            }
            else if (PaintData.PaintSpace == PaintSpace.World)
            {
                RenderToTargetWorld(RenderTarget.Input, clear);
            }

            if (clear)
            {
                BakeInputToPaint();
            }
        }
  
        private void RenderToTarget(RenderTarget target, Mesh mesh, bool clear = false)
        {
            if (!Tool.RenderToLayer && target == RenderTarget.ActiveLayer)
                return;

            if (!Tool.RenderToInput && target == RenderTarget.Input)
                return;
            
            //render brush
            if (clear)
            {
                commandBuffer.Clear().SetRenderTarget(GetRenderTarget(target)).ClearRenderTarget().DrawMesh(mesh, PaintData.Brush.Material).Execute();
            }
            else
            {
                commandBuffer.Clear().SetRenderTarget(GetRenderTarget(target)).DrawMesh(mesh, PaintData.Brush.Material).Execute();
            }
            
            //colorize
            if (target == RenderTarget.Input)
            {
                commandBuffer.Clear().LoadOrtho().SetRenderTarget(GetRenderTarget(RenderTarget.Input)).DrawMesh(mesh, PaintData.Brush.Material, PaintData.Brush.Material.passCount - 1).Execute();
            }
        }

        private void RenderToTargetWorld(RenderTarget target, bool clear = false)
        {
            if (!Tool.RenderToLayer && target == RenderTarget.ActiveLayer)
                return;

            if (!Tool.RenderToInput && target == RenderTarget.Input)
                return;

            if (clear)
            {
                ClearTexture(target);
            }
            
            var mainTexture = PaintData.PaintWorldMaterial.mainTexture;
            Graphics.Blit(PaintData.TextureHelper.GetTexture(RenderTarget.Input), PaintData.TextureHelper.GetTexture(RenderTarget.ActiveLayerTemp));
            PaintData.PaintWorldMaterial.mainTexture = PaintData.TextureHelper.GetTexture(RenderTarget.ActiveLayerTemp);

            //render brush
            onPaintWorldRender.Invoke(target);

            //colorize
            if (target == RenderTarget.Input)
            {
                Graphics.Blit(PaintData.TextureHelper.GetTexture(RenderTarget.Input), PaintData.TextureHelper.GetTexture(RenderTarget.ActiveLayerTemp));
                Graphics.Blit(PaintData.TextureHelper.GetTexture(RenderTarget.ActiveLayerTemp), PaintData.TextureHelper.GetTexture(RenderTarget.Input), PaintData.Brush.Material, PaintData.Brush.Material.passCount - 1);
            }
            
            PaintData.PaintWorldMaterial.mainTexture = mainTexture;
        }

        private void RenderLineUV(List<Vector3> positions, List<Vector2> uv, List<int> indices, List<Color> colors)
        {
            if (lineMesh != null)
            {
                lineMesh.Clear(false);
            }
            
            lineMesh.SetVertices(positions);
            lineMesh.SetUVs(0, uv);
            lineMesh.SetTriangles(indices, 0);
            lineMesh.SetColors(colors);
            
            GL.LoadOrtho();
            RenderToTarget(PaintData.PaintMode.RenderTarget, lineMesh, false);
            RenderToInput(lineMesh);
        }
    }
}