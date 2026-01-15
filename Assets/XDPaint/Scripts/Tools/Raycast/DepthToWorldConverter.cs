using UnityEngine;
using XDPaint.Controllers;
using XDPaint.Core;
using XDPaint.Utils;
using IDisposable = XDPaint.Core.IDisposable;
using Object = UnityEngine.Object;

namespace XDPaint.Tools.Raycast
{
    public class DepthToWorldConverter : IDisposable
    {
        private CommandBufferBuilder commandBuffer;
        private RenderTexture renderTexture;
        private Mesh quadMesh;
        private Material material;
        private Texture2D texture;
        private int frameId;
        private bool initialized;

        public void Init()
        {
            DoDispose();
            commandBuffer = new CommandBufferBuilder();
            quadMesh = MeshGenerator.GenerateQuad(Vector3.one, Vector3.zero);
            material = new Material(Settings.Instance.DepthToWorldPositionShader);
            renderTexture = RenderTextureFactory.CreateRenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
            texture = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
            initialized = true;
        }
        
        public void DoDispose()
        {
            commandBuffer?.Release();
            if (quadMesh != null)
            {
                Object.Destroy(quadMesh);
            }
            if (material != null)
            {
                Object.Destroy(material);
            }
            if (renderTexture != null)
            {
                renderTexture.ReleaseTexture();
            }
            if (texture != null)
            {
                Object.Destroy(texture);
            }

            initialized = false;
        }

        public bool IsInTextureBounds(Vector2Int screenPosition)
        {
            return screenPosition.x >= 0 && screenPosition.y >= 0 && screenPosition.x < renderTexture.width && screenPosition.y < renderTexture.height;
        }
        
        public bool TryGetPosition(Vector2Int screenPosition, out Vector4 worldPosition)
        {
            if (!initialized)
            {
                worldPosition = default;
                return false;
            }

            if (frameId == Time.frameCount)
            {
                worldPosition = GetPixelColor(screenPosition);
                return true;
            }

            var mainCamera = PaintController.Instance.Camera;
            var projectionMatrix = GL.GetGPUProjectionMatrix(mainCamera.projectionMatrix, false);
            var inverseViewProjectionMatrix = (projectionMatrix * mainCamera.worldToCameraMatrix).inverse;
            material.SetMatrix(Constants.DepthToWorldPositionShader.InverseViewProjectionMatrix, inverseViewProjectionMatrix);
            commandBuffer.LoadOrtho().Clear().SetRenderTarget(renderTexture).DrawMesh(quadMesh, material).Execute();
            worldPosition = GetPixelColor(screenPosition);
            return true;
        }

        private Color GetPixelColor(Vector2Int screenPosition)
        {
            var prevRenderTexture = RenderTexture.active;
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(screenPosition.x, screenPosition.y, 1, 1), 0, 0);
            texture.Apply();
            RenderTexture.active = prevRenderTexture;
            frameId = Time.frameCount;
            return texture.GetPixel(screenPosition.x, screenPosition.y);
        }
    }
}