using System;
using UnityEngine;
using XDPaint.Tools;
using Object = UnityEngine.Object;

namespace XDPaint.Core.Materials
{
    [Serializable]
    public class Paint : IDisposable
    {
        #region Properties and variables
        
        private Material paintMaterial;
        public Material PaintMaterial => paintMaterial;
        
        private Material paintWorldMaterial;
        public Material PaintWorldMaterial => paintWorldMaterial;

        [SerializeField] private string shaderTextureName = "_MainTex";
        public string ShaderTextureName
        {
            get => shaderTextureName;
            set => shaderTextureName = value;
        }
        
        [SerializeField] private int defaultTextureWidth = 2048;
        public int DefaultTextureWidth
        {
            get => defaultTextureWidth;
            set => defaultTextureWidth = value;
        }
        
        [SerializeField] private int defaultTextureHeight = 2048;
        public int DefaultTextureHeight
        {
            get => defaultTextureHeight;
            set => defaultTextureHeight = value;
        }
        
        [SerializeField] private Color defaultTextureColor = Color.clear;
        public Color DefaultTextureColor
        {
            get => defaultTextureColor;
            set => defaultTextureColor = value;
        }

        [SerializeField] private ProjectionMethod projectionMethod = ProjectionMethod.Planar;
        public ProjectionMethod ProjectionMethod
        {
            get => projectionMethod;
            set
            {
                projectionMethod = value;
                if (initialized && Application.isPlaying && paintWorldMaterial != null && renderComponentsHelper.IsMesh())
                {
                    paintWorldMaterial.SetFloat(Constants.PaintWorldShader.UsePlanarProjection, projectionMethod == ProjectionMethod.Planar ? 1 : 0);
                }
            }
        }
        
        [SerializeField] private ProjectionDirection projectionDirection = ProjectionDirection.Normal;
        public ProjectionDirection ProjectionDirection
        {
            get => projectionDirection;
            set
            {
                projectionDirection = value;
                if (initialized && Application.isPlaying && paintWorldMaterial != null && renderComponentsHelper.IsMesh())
                {
                    paintWorldMaterial.SetFloat(Constants.PaintWorldShader.UseNormalDirection, projectionDirection == ProjectionDirection.Normal ? 1 : 0);
                }
            }
        }
        
        #region Normal Cutout
        
        [SerializeField] private float normalCutout;
        public float NormalCutout
        {
            get => normalCutout;
            set
            {
                normalCutout = value;
                if (initialized && Application.isPlaying && paintWorldMaterial != null && renderComponentsHelper.IsMesh())
                {
                    paintWorldMaterial.SetFloat(Constants.PaintWorldShader.NormalCutout, normalCutout);
                }
            }
        }

        #endregion
        
        #region Angle

        [SerializeField] private float projectionMaxAngle = 90f;
        public float ProjectionMaxAngle
        {
            get => projectionMaxAngle;
            set
            {
                projectionMaxAngle = value;
                if (initialized && Application.isPlaying && paintWorldMaterial != null && renderComponentsHelper.IsMesh())
                {
                    paintWorldMaterial.SetFloat(Constants.PaintWorldShader.MaxAngle, projectionMaxAngle);
                }
            }
        }
        
        [SerializeField] private AngleAttenuationMode projectionAngleAttenuationMode;
        public AngleAttenuationMode ProjectionAngleAttenuationMode
        {
            get => projectionAngleAttenuationMode;
            set
            {
                projectionAngleAttenuationMode = value;
                if (initialized && Application.isPlaying && paintWorldMaterial != null && renderComponentsHelper.IsMesh())
                {
                    paintWorldMaterial.SetFloat(Constants.PaintWorldShader.AngleAttenuationMode, projectionAngleAttenuationMode == AngleAttenuationMode.Soft ? 0 : 1);
                }
            }
        }
        
        #endregion

        #region Brush Depth

        [SerializeField] private float projectionBrushDepth;
        public float ProjectionBrushDepth
        {
            get => projectionBrushDepth;
            set
            {
                projectionBrushDepth = value;
                if (initialized && Application.isPlaying && paintWorldMaterial != null && renderComponentsHelper.IsMesh())
                {
                    paintWorldMaterial.SetFloat(Constants.PaintWorldShader.BrushDepth, projectionBrushDepth);
                }
            }
        }
        
        [SerializeField] private float projectionDepthFadeRange;
        public float ProjectionDepthFadeRange
        {
            get => projectionDepthFadeRange;
            set
            {
                projectionDepthFadeRange = value;
                if (initialized && Application.isPlaying && paintWorldMaterial != null && renderComponentsHelper.IsMesh())
                {
                    paintWorldMaterial.SetFloat(Constants.PaintWorldShader.DepthFadeRange, projectionDepthFadeRange);
                }
            }
        }
        
        #endregion

        #region Edge
        
        [SerializeField] private float projectionEdgeSoftness;
        public float ProjectionEdgeSoftness
        {
            get => projectionEdgeSoftness;
            set
            {
                projectionEdgeSoftness = value;
                if (initialized && Application.isPlaying && paintWorldMaterial != null && renderComponentsHelper.IsMesh())
                {
                    paintWorldMaterial.SetFloat(Constants.PaintWorldShader.EdgeSoftness, projectionEdgeSoftness);
                }
            }
        }
        
        #endregion

        private int materialIndex;
        public int MaterialIndex => materialIndex;

        private Texture sourceTexture;
        public Texture SourceTexture => sourceTexture;

        public Material SourceMaterial;
        private IRenderComponentsHelper renderComponentsHelper;
        private Material objectMaterial;
        private bool isSourceTextureCreated;
        private bool initialized;
        
        #endregion

        public void Init(IRenderComponentsHelper renderComponents, Texture source)
        {
            DoDispose();
            renderComponentsHelper = renderComponents;
            materialIndex = renderComponents.GetMaterialIndex(SourceMaterial);
            if (SourceMaterial != null || SourceMaterial != null && objectMaterial == null)
            {
                objectMaterial = Object.Instantiate(SourceMaterial);
            }
            else if (renderComponentsHelper.Material != null)
            {
                objectMaterial = Object.Instantiate(renderComponentsHelper.Material);
            }
            
            sourceTexture = renderComponentsHelper.GetSourceTexture(objectMaterial, shaderTextureName);
            if (sourceTexture == null && source == null)
            {
                sourceTexture = renderComponentsHelper.CreateSourceTexture(objectMaterial, shaderTextureName, defaultTextureWidth, defaultTextureHeight, defaultTextureColor);
                isSourceTextureCreated = true;
            }
            else if (source != null)
            {
                sourceTexture = source;
            }
            
            paintMaterial = new Material(Settings.Instance.PaintShader)
            {
                mainTexture = sourceTexture
            };
            
            initialized = true;
        }

        public void CreateWorldMaterial()
        {
            DisposeWorldMaterial();
            paintWorldMaterial = new Material(Settings.Instance.PaintWorldShader);
            if (renderComponentsHelper.IsMesh())
            {
                paintWorldMaterial.SetFloat(Constants.PaintWorldShader.UseNormalDirection, ProjectionDirection == ProjectionDirection.Normal ? 1 : 0);
                paintWorldMaterial.SetFloat(Constants.PaintWorldShader.UsePlanarProjection, ProjectionMethod == ProjectionMethod.Planar ? 1 : 0);
                paintWorldMaterial.SetFloat(Constants.PaintWorldShader.NormalCutout, NormalCutout);
                paintWorldMaterial.SetFloat(Constants.PaintWorldShader.MaxAngle, ProjectionMaxAngle);
                paintWorldMaterial.SetFloat(Constants.PaintWorldShader.AngleAttenuationMode, ProjectionAngleAttenuationMode == AngleAttenuationMode.Soft ? 0 : 1);
                paintWorldMaterial.SetFloat(Constants.PaintWorldShader.BrushDepth, ProjectionBrushDepth);
                paintWorldMaterial.SetFloat(Constants.PaintWorldShader.DepthFadeRange, ProjectionDepthFadeRange);
                paintWorldMaterial.SetFloat(Constants.PaintWorldShader.EdgeSoftness, ProjectionEdgeSoftness);
            }
        }

        public void DoDispose()
        {
            if (isSourceTextureCreated)
            {
                Object.Destroy(sourceTexture);
                isSourceTextureCreated = false;
                renderComponentsHelper.DestroySourceTexture(objectMaterial, shaderTextureName);
            }
            
            if (objectMaterial != null)
            {
                Object.Destroy(objectMaterial);
                objectMaterial = null;
            }
            
            if (paintMaterial != null)
            {
                Object.Destroy(paintMaterial);
                paintMaterial = null;
            }

            DisposeWorldMaterial();
            
            initialized = false;
        }

        public void RestoreTexture()
        {
            if (!initialized)
                return;
            if (SourceTexture != null)
            {
                objectMaterial.SetTexture(shaderTextureName, SourceTexture);
            }
            else
            {
                renderComponentsHelper.Material = SourceMaterial;
            }
        }

        public void SetObjectMaterialTexture(Texture texture)
        {
            if (!initialized)
                return;
            objectMaterial.SetTexture(shaderTextureName, texture);
            renderComponentsHelper.SetSourceMaterial(objectMaterial, materialIndex);
        }

        public void SetPreviewTexture(Texture texture)
        {
            if (!initialized)
                return;
            
            paintMaterial.SetTexture(Constants.PaintShader.BrushTexture, texture);
        }

        public void SetPaintTexture(Texture texture)
        {
            if (!initialized)
                return;
            
            paintMaterial.SetTexture(Constants.PaintShader.PaintTexture, texture);
        }
        
        public void SetInputTexture(Texture texture)
        {
            if (!initialized)
                return;
            
            paintMaterial.SetTexture(Constants.PaintShader.InputTexture, texture);
        }

        public void SetPreviewVector(Vector4 brushOffset)
        {
            if (!initialized)
                return;
            
            paintMaterial.SetVector(Constants.PaintShader.BrushOffset, brushOffset);
        }
        
        private void DisposeWorldMaterial()
        {
            if (paintWorldMaterial != null)
            {
                Object.Destroy(paintWorldMaterial);
                paintWorldMaterial = null;
            }
        }
    }
}