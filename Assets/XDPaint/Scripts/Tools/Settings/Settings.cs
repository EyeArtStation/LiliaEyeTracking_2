using UnityEngine;
using UnityEngine.Serialization;
using XDPaint.Core;
using XDPaint.Utils;

namespace XDPaint.Tools
{
    [CreateAssetMenu(fileName = "XDPaintSettings", menuName = "XDPaint/Settings", order = 102)]
    public class Settings : SingletonScriptableObject<Settings>
    {
        #region Shaders

        [SerializeField] private Shader brushShader;
        [SerializeField] private Shader brushRenderShader;
        [SerializeField] private Shader eyedropperShader;
        [SerializeField] private Shader brushSamplerShader;
        [SerializeField] private Shader brushCloneShader;
        [SerializeField] private Shader blurShader;
        [SerializeField] private Shader gaussianBlurShader;
        [SerializeField] private Shader grayscaleShader;
        [SerializeField] private Shader brushBlurShader;
        [SerializeField] private Shader paintShader;
        [SerializeField] private Shader averageColorShader;
        [SerializeField] private Shader spriteMaskShader;
        [SerializeField] private Shader depthToWorldPositionShader;
        [SerializeField] private Shader paintWorldShader;
        
        public Shader BrushShader => brushShader;
        public Shader BrushRenderShader => brushRenderShader;
        public Shader EyedropperShader => eyedropperShader;
        public Shader BrushSamplerShader => brushSamplerShader;
        public Shader BrushCloneShader => brushCloneShader;
        public Shader BlurShader => blurShader;
        public Shader GaussianBlurShader => gaussianBlurShader;
        public Shader GrayscaleShader => grayscaleShader;
        public Shader BrushBlurShader => brushBlurShader;
        public Shader PaintShader => paintShader;
        public Shader AverageColorShader => averageColorShader;
        public Shader SpriteMaskShader => spriteMaskShader;
        public Shader DepthToWorldPositionShader => depthToWorldPositionShader;
        public Shader PaintWorldShader => paintWorldShader;

        #endregion

        public Texture DefaultBrush;
        public Texture DefaultCircleBrush;
        public Texture DefaultPatternTexture;
        [FormerlySerializedAs("IsVRMode")] public bool VRModeEnabled;
        public bool PressureEnabled = true;
        public bool CheckCanvasRaycasts = true;
        public RaycastSystemType RaycastsMethod = RaycastSystemType.JobSystem;
        public float BrushDuplicatePartWidth = 16;
        public float RaycastInterval = 0.5f;
        public float PixelPerUnit = 100f;
        public string ContainerGameObjectName = "[XDPaintContainer]";
    }
}