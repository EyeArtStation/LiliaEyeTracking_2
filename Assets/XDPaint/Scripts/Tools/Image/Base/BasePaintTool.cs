using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using UnityEngine;
using UnityEngine.Rendering;
using XDPaint.Core;
using XDPaint.Core.Layers;
using XDPaint.Core.Materials;
using XDPaint.Core.PaintModes;
using XDPaint.Core.PaintObject.Data;

namespace XDPaint.Tools.Image.Base
{
    [Serializable]
    public abstract class BasePaintTool<T> : IPaintTool where T : BasePaintToolSettings
    {
        /// <summary>
        /// Type of the tool
        /// </summary>
        public abstract PaintTool Type { get; }

        protected IPaintData Data;

        public virtual bool AllowRender => RenderToLayer || RenderToInput || ShowPreview;
        public virtual bool CanDrawLines { get; protected set; } = true;
        public virtual bool ConsiderPreviousPosition => Settings.DrawOnBrushMove;
        public virtual bool ShowPreview => Data.Brush.Preview;
        public virtual int Smoothing => Settings.Smoothing;
        public virtual bool RandomizePointsQuadsAngle => Settings.RandomizePointsQuadsAngle;
        public virtual bool RandomizeLinesQuadsAngle => Settings.RandomizeLinesQuadsAngle;
        public virtual bool RenderToLayer { get; protected set; } = true;
        public virtual bool RenderToInput { get; protected set; } = true;
        public virtual bool RenderToTextures => RenderToLayer || RenderToInput;
        public virtual bool DrawPreProcess { get; protected set; }
        public virtual bool DrawProcess => true;
        public virtual bool DrawPostProcess { get; private set; }
        public virtual bool BakeInputToLayer => true;
        public virtual bool ProcessingFinished { get; protected set; } = true;
        public virtual bool RequiredCombinedTempTexture => false;

        protected virtual PaintPass InputToPaintPass => PaintPass.Blend;
        
        private bool IsLayersProcessing => (Data.StatesController != null && (Data.StatesController.IsRedoProcessing || Data.StatesController.IsUndoProcessing)) || Data.LayersController.IsMerging;

        #region Settings

        public BasePaintToolSettings BaseSettings => toolSettings;
        
        [SerializeField] private T toolSettings;
        public virtual T Settings
        {
            get => toolSettings;
            protected set => toolSettings = value;
        }

        #endregion

        protected Action<RenderTargetIdentifier> OnPaintWorldRender;
        private string keyword;
        private bool hasPluralLayersEnabled;
        private bool shouldUpdateCombinedTemp;

        protected BasePaintTool(IPaintData paintData)
        {
            Data = paintData;
            if (Data.PaintSpace == PaintSpace.World)
            {
                if (Data.PlaneMesh != null)
                {
                    OnPaintWorldRender = identifier =>
                    {
                        Data.CommandBuilder.Clear().SetRenderTarget(identifier).DrawMesh(Data.PlaneMesh,
                            Data.RenderComponents.RendererComponent.transform.localToWorldMatrix,
                            Data.PaintWorldMaterial, Data.SubMesh, PaintWorldPass.Draw).Execute();
                    };
                }
                else
                {
                    OnPaintWorldRender = identifier =>
                    {
                        Data.CommandBuilder.Clear().SetRenderTarget(identifier)
                            .DrawRenderer(Data.RenderComponents.RendererComponent as Renderer, Data.PaintWorldMaterial,
                                Data.SubMesh, PaintWorldPass.Draw).Execute();
                    };
                }
            }

            DrawPostProcess = Data.PaintSpace == PaintSpace.World && Data.RenderComponents.IsMesh();
        }

        /// <summary>
        /// Enter the tool
        /// </summary>
        public virtual void Enter()
        {
            CanDrawLines = true;
            RenderToLayer = true;
            RenderToInput = true;
            Data.PaintMaterial.SetTexture(Constants.PaintShader.PaintTexture, GetTexture(RenderTarget.ActiveLayer));
            Data.PaintMaterial.SetTexture(Constants.PaintShader.InputTexture, GetTexture(RenderTarget.Input));
            Data.LayersController.OnLayersCollectionChanged += OnLayersCollectionChanged;
            Data.LayersController.OnLayerChanged += OnLayerChanged;
            Data.Brush.OnPreviewChanged += OnBrushPreviewChanged;
            UpdateCombinedTempTexture();
        }

        /// <summary>
        /// Exit from the tool
        /// </summary>
        public virtual void Exit()
        {
            Data.CommandBuilder.Clear().LoadOrtho().SetRenderTarget(GetTarget(RenderTarget.Input)).ClearRenderTarget().Execute();
            UpdateCombinedTempTexture();
            Data.LayersController.OnLayersCollectionChanged -= OnLayersCollectionChanged;
            Data.LayersController.OnLayerChanged -= OnLayerChanged;
            Data.Brush.OnPreviewChanged -= OnBrushPreviewChanged;
        }
        
        private void OnLayersCollectionChanged(ObservableCollection<ILayer> collection, NotifyCollectionChangedEventArgs notify)
        {
            if (IsLayersProcessing)
            {
                shouldUpdateCombinedTemp = true; 
                return;
            }
            
            UpdateCombinedTempTexture();
        }

        private void OnLayerChanged(ILayer layer)
        {
            if (IsLayersProcessing)
            {
                shouldUpdateCombinedTemp = true; 
                return;
            }

            UpdateCombinedTempTexture();
        }
        
        private void OnBrushPreviewChanged(bool previewEnabled)
        {
            UpdateCombinedTempTexture();
        }
        
        protected void UpdateCombinedTempTexture(bool render = true)
        {
            shouldUpdateCombinedTemp = false;
            hasPluralLayersEnabled = GetActiveSubLayersCount() > 1;
            if (hasPluralLayersEnabled || ShowPreview || RequiredCombinedTempTexture || Data.PaintSpace == PaintSpace.World)
            {
                if (GetTexture(RenderTarget.CombinedTemp) == null)
                {
                    Data.TextureHelper.CreateRenderTexture(RenderTarget.CombinedTemp);
                }
            }
            else
            {
                Data.TextureHelper.ReleaseRenderTexture(RenderTarget.CombinedTemp);
            }

            if (render)
            {
                Data.Render();
            }
        }

        private int GetActiveSubLayersCount()
        {
            var activeSubLayerCount = 0;
            foreach (var layer in Data.LayersController.Layers)
            {
                if (layer.Enabled && layer.Opacity > 0)
                {
                    activeSubLayerCount++;
                }

                if (layer.MaskRenderTexture != null && layer.MaskEnabled)
                {
                    activeSubLayerCount++;
                }

                if (activeSubLayerCount > 1)
                {
                    break;
                }
            }
            return activeSubLayerCount;
        }

        public virtual void DoDispose()
        {
        }

        /// <summary>
        /// On Mouse Hover handler
        /// </summary>
        public virtual void UpdateHover(PointerData pointerData)
        {
        }

        /// <summary>
        /// On Mouse Down handler
        /// </summary>
        public virtual void UpdateDown(PointerData pointerData)
        {
        }

        /// <summary>
        /// On Mouse Press handler
        /// </summary>
        public virtual void UpdatePress(PointerData pointerData)
        {
        }

        /// <summary>
        /// On Mouse Up handler
        /// </summary>
        public virtual void UpdateUp(PointerUpData pointerUpData)
        {
        }
        
        public void FillWithColor(Color color)
        {
            Data.CommandBuilder.Clear().LoadOrtho().SetRenderTarget(GetTarget(RenderTarget.ActiveLayer)).ClearRenderTarget(color).Execute();
            Data.LayersController.ActiveLayer.SaveState();
            Data.Render();
        }
                
        /// <summary>
        /// Returns Vector4 for brush preview
        /// </summary>
        /// <returns></returns>
        protected virtual Vector4 GetPreviewVector(Texture texture, Vector2 paintPosition, float pressure)
        {
            var brushScale = Data.Brush.Size * pressure;
            var sourceTextureScaled = new Vector2(Data.Brush.SourceTexture.width, Data.Brush.SourceTexture.height) * brushScale;
            var texel = texture.texelSize;
            var offset = new Vector2(paintPosition.x / texture.width, paintPosition.y / texture.height);
            offset -= sourceTextureScaled * texel / 2f;
            
            if (Data.Brush.RenderOffset.x > 0)
            {
                offset.x += texel.x / 2f;
                if (Data.Brush.SourceTexture.width > 1)
                {
                    offset.x -= offset.x % texel.x;
                }
            }
            
            if (Data.Brush.RenderOffset.y > 0)
            {
                offset.y += texel.y / 2f;
                if (Data.Brush.SourceTexture.height > 1)
                {
                    offset.y -= offset.y % texel.y;
                }
            }

            var scale = new Vector2(sourceTextureScaled.x / texture.width, sourceTextureScaled.y / texture.height);
            return new Vector4(offset.x, offset.y, scale.x, scale.y);
        }

        public virtual void SetPaintMode(IPaintMode mode)
        {
            SetBrushBlending(mode.UsePaintInput);
        }

        public virtual void OnBrushChanged(IBrush brush)
        {
            UpdateCombinedTempTexture();
        }

        /// <summary>
        /// Pre Draw Process handler
        /// </summary>
        /// <param name="combined"></param>
        public virtual void OnDrawPreProcess(RenderTargetIdentifier combined)
        {
            DrawPreProcess = false;
        }

        /// <summary>
        /// Draw Process handler
        /// </summary>
        /// <param name="combined"></param>
        public virtual void OnDrawProcess(RenderTargetIdentifier combined)
        {
            RenderLayers(combined);
            if (!Data.IsPainting)
            {
                RenderPreview(combined);
            }
        }

        public virtual void OnDrawPostProcess(RenderTargetIdentifier combined)
        {
            Graphics.Blit(Data.TextureHelper.GetTexture(RenderTarget.Combined), Data.TextureHelper.GetTexture(RenderTarget.CombinedTemp));
            var mainTexture = Data.PaintWorldMaterial.mainTexture;
            Data.PaintWorldMaterial.SetTexture(Constants.PaintWorldShader.MainTexture, Data.TextureHelper.GetTexture(RenderTarget.CombinedTemp));
            Graphics.Blit(Data.TextureHelper.GetTexture(RenderTarget.CombinedTemp), Data.TextureHelper.GetTexture(RenderTarget.Combined), Data.PaintWorldMaterial, (int)PaintWorldPass.IslandEdges);
            Data.PaintWorldMaterial.mainTexture = mainTexture;
        }

        protected virtual void SetBrushBlending(bool usePaintInput)
        {
            var material = Data.Brush.Material;
            material.SetInt(Constants.BrushShader.BlendOpColor, (int)BlendOp.Add);
            material.SetInt(Constants.BrushShader.BlendOpAlpha, (int)BlendOp.Add);
            material.SetInt(Constants.BrushShader.SrcColorBlend, (int)BlendMode.SrcAlpha);
            material.SetInt(Constants.BrushShader.DstColorBlend, (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt(Constants.BrushShader.SrcAlphaBlend, (int)BlendMode.SrcAlpha);
            material.SetInt(Constants.BrushShader.DstAlphaBlend, (int)BlendMode.One);
        }
        
        /// <summary>
        /// Render Layers in the Combined Render Texture
        /// </summary>
        /// <param name="combined"></param>
        protected virtual void RenderLayers(RenderTargetIdentifier combined)
        {
            var rendered = false;
            var paintTexture = Data.PaintMaterial.GetTexture(Constants.PaintShader.PaintTexture);
            var inputTexture = Data.PaintMaterial.GetTexture(Constants.PaintShader.InputTexture);
            var color = Data.PaintMaterial.color;
            Data.PaintMaterial.SetFloat(Constants.PaintShader.Opacity, 1);
            if (shouldUpdateCombinedTemp)
            {
                UpdateCombinedTempTexture(false);
            }
            
            if (hasPluralLayersEnabled || ShowPreview || RequiredCombinedTempTexture)
            {
                Data.CommandBuilder.Clear().LoadOrtho().SetRenderTarget(GetTarget(RenderTarget.CombinedTemp)).ClearRenderTarget().Execute();
            }
            else
            {
                Data.CommandBuilder.Clear().LoadOrtho().SetRenderTarget(GetTarget(RenderTarget.Combined)).ClearRenderTarget().Execute();
            }

            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < Data.LayersController.Layers.Count; i++)
            {
                var currentLayer = Data.LayersController.Layers[i];
                if (!currentLayer.Enabled || currentLayer.Opacity == 0)
                    continue;

                if (currentLayer == Data.LayersController.ActiveLayer)
                {
                    DrawCurrentLayer();
                }
                
                if (!string.IsNullOrEmpty(keyword))
                {
                    Data.PaintMaterial.DisableKeyword(keyword);
                }

                if (rendered)
                {
                    keyword = Constants.PaintShader.LayerBlendFormat[currentLayer.BlendingMode];
                    Data.PaintMaterial.EnableKeyword(keyword);
                }
                else
                {
                    keyword = Constants.PaintShader.LayerBlendFormat[BlendingMode.Normal];
                    Data.PaintMaterial.EnableKeyword(keyword);
                }

                Data.PaintMaterial.SetFloat(Constants.PaintShader.Opacity, currentLayer.Opacity);
                if (currentLayer == Data.LayersController.ActiveLayer)
                {
                    Data.PaintMaterial.SetTexture(Constants.PaintShader.PaintTexture, Data.TextureHelper.GetTexture(RenderTarget.ActiveLayerTemp));
                }
                else
                {
                    Data.PaintMaterial.SetTexture(Constants.PaintShader.PaintTexture, currentLayer.RenderTexture);
                }

                if (hasPluralLayersEnabled || ShowPreview || RequiredCombinedTempTexture)
                {
                    Graphics.Blit(GetTexture(RenderTarget.Combined), GetTexture(RenderTarget.CombinedTemp));
                    Data.PaintMaterial.SetTexture(Constants.PaintShader.InputTexture, GetTexture(RenderTarget.CombinedTemp));
                }
                else
                {
                    Data.PaintMaterial.SetTexture(Constants.PaintShader.InputTexture, null);
                }

                if (currentLayer.MaskEnabled && currentLayer.MaskRenderTexture != null)
                {
                    Data.PaintMaterial.SetTexture(Constants.PaintShader.MaskTexture, currentLayer.MaskRenderTexture);
                }
                else
                {
                    Data.PaintMaterial.SetTexture(Constants.PaintShader.MaskTexture, null);
                }

                Data.PaintMaterial.color = new Color(color.r, color.g, color.b, currentLayer.Opacity);
                Data.CommandBuilder.Clear().LoadOrtho().SetRenderTarget(combined).DrawMesh(Data.QuadMesh, Data.PaintMaterial).Execute();
                rendered = true;
            }

            Data.PaintMaterial.SetTexture(Constants.PaintShader.PaintTexture, paintTexture);
            Data.PaintMaterial.SetTexture(Constants.PaintShader.InputTexture, inputTexture);
            Data.PaintMaterial.color = color;
        }

        /// <summary>
        /// Render Active Layer
        /// </summary>
        protected virtual void DrawCurrentLayer()
        {
            Data.PaintMaterial.SetTexture(Constants.PaintShader.PaintTexture, GetTexture(RenderTarget.ActiveLayer));
            Data.PaintMaterial.SetTexture(Constants.PaintShader.InputTexture, GetTexture(RenderTarget.Input));
            Data.CommandBuilder.Clear().LoadOrtho().SetRenderTarget(GetTarget(RenderTarget.ActiveLayerTemp)).DrawMesh(Data.QuadMesh, Data.PaintMaterial, InputToPaintPass).Execute();
        }

        /// <summary>
        /// Render brush preview
        /// </summary>
        /// <param name="combined"></param>
        protected void RenderPreview(RenderTargetIdentifier combined)
        {
            if (ShowPreview && Data.InBounds)
            {
                if (Data.PaintSpace == PaintSpace.UV)
                {
                    RenderPreviewUV(combined);
                }
                else if (Data.PaintSpace == PaintSpace.World)
                {
                    RenderPreviewWorld(combined);
                }
            }
        }

        protected virtual void RenderPreviewUV(RenderTargetIdentifier combined)
        {
            var previousPaintTexture = Data.PaintMaterial.GetTexture(Constants.PaintShader.PaintTexture);
            Graphics.Blit(GetTexture(RenderTarget.Combined), GetTexture(RenderTarget.CombinedTemp));
            Data.PaintMaterial.SetTexture(Constants.PaintShader.PaintTexture, GetTexture(RenderTarget.CombinedTemp));
            Data.CommandBuilder.Clear().LoadOrtho().SetRenderTarget(combined).DrawMesh(Data.QuadMesh, Data.PaintMaterial, PaintPass.Preview).Execute();
            Data.PaintMaterial.SetTexture(Constants.PaintShader.PaintTexture, previousPaintTexture);
        }

        protected virtual void RenderPreviewWorld(RenderTargetIdentifier combined)
        {
            var previousMainTexture = Data.PaintWorldMaterial.GetTexture(Constants.PaintWorldShader.MainTexture);
            Graphics.Blit(GetTexture(RenderTarget.Combined), GetTexture(RenderTarget.CombinedTemp));
            Data.PaintWorldMaterial.SetTexture(Constants.PaintWorldShader.MainTexture, GetTexture(RenderTarget.CombinedTemp));
            Data.PaintWorldMaterial.color = Data.Brush.Color;
            OnPaintWorldRender.Invoke(combined);
            Data.PaintWorldMaterial.SetTexture(Constants.PaintWorldShader.MainTexture, previousMainTexture);
        }

        /// <summary>
        /// Bake Input RenderTexture into Paint Render Texture (Active Layer)
        /// </summary>
        /// <param name="activeLayer"></param>
        public virtual void OnBakeInputToLayer(RenderTargetIdentifier activeLayer)
        {
            Graphics.Blit(GetTexture(RenderTarget.ActiveLayer), GetTexture(RenderTarget.ActiveLayerTemp));
            Data.PaintMaterial.SetTexture(Constants.PaintShader.PaintTexture, GetTexture(RenderTarget.ActiveLayerTemp));
            Data.CommandBuilder.LoadOrtho().Clear().SetRenderTarget(activeLayer).DrawMesh(Data.QuadMesh, Data.PaintMaterial, InputToPaintPass).Execute();
            Data.PaintMaterial.SetTexture(Constants.PaintShader.PaintTexture, GetTexture(RenderTarget.ActiveLayer));
        }

        protected RenderTexture GetTexture(RenderTarget target)
        {
            if (target == RenderTarget.ActiveLayer)
                return Data.LayersController.ActiveLayer.RenderTexture;
            
            return Data.TextureHelper.GetTexture(target);
        }

        protected RenderTargetIdentifier GetTarget(RenderTarget target)
        {
            if (target == RenderTarget.ActiveLayer)
                return Data.LayersController.ActiveLayer.RenderTarget;
            
            return Data.TextureHelper.GetTarget(target);
        }
    }
}