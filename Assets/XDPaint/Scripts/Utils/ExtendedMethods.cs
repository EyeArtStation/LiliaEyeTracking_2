using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace XDPaint.Utils
{
    public static class ExtendedMethods
    {
        public static Vector2 Clamp(this Vector2 value, Vector2 from, Vector2 to)
        {
            if (value.x < from.x)
            {
                value.x = from.x;
            }
            if (value.y < from.y)
            {
                value.y = from.y;
            }
            if (value.x > to.x)
            {
                value.x = to.x;
            }
            if (value.y > to.y)
            {
                value.y = to.y;
            }
            return value;
        }
        
        public static bool IsNaNOrInfinity(this float value)
        {
            return float.IsInfinity(value) || float.IsNaN(value);
        }
        
        public static void ReleaseTexture(this RenderTexture renderTexture)
        {
            if (renderTexture != null && renderTexture.IsCreated())
            {
                if (RenderTexture.active == renderTexture)
                {
                    RenderTexture.active = null;
                }
                renderTexture.Release();
                Object.Destroy(renderTexture);
            }
        }
        
        public static Texture2D GetTexture2D(this RenderTexture renderTexture)
        {
            var format = TextureFormat.ARGB32;
            if (renderTexture.format == RenderTextureFormat.RFloat)
            {
                format = TextureFormat.RFloat;
            }
            var texture2D = new Texture2D(renderTexture.width, renderTexture.height, format, false);
            var previousRenderTexture = RenderTexture.active;
            RenderTexture.active = renderTexture;
            texture2D.ReadPixels(new Rect(0, 0, texture2D.width, texture2D.height), 0, 0, false);
            texture2D.Apply();
            RenderTexture.active = previousRenderTexture;
            return texture2D;
        }

        public static string CapitalizeFirstLetter(this string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            if (text.Length == 1)
                return text.ToUpper();
            return text.Remove(1).ToUpper() + text.Substring(1);
        }
        
        public static string ToPascalCase(this string text)
        {
            return Regex.Replace(CapitalizeFirstLetter(text), @"\b\p{Ll}", match => match.Value.ToUpper());
        }

        public static string ToCamelCaseWithSpace(this string text)
        {
            return Regex.Replace(CapitalizeFirstLetter(text), @"(\B[A-Z]+?(?=[A-Z][^A-Z])|\B[A-Z]+?(?=[^A-Z]))", " $1").Trim();
        }
        
        public static bool AreColorsSimilar(this Color32 c1, Color32 c2, int tolerance)
        {
            return Mathf.Abs(c1.r * c1.a - c2.r * c2.a) <= tolerance && Mathf.Abs(c1.g * c1.a - c2.g * c2.a) <= tolerance && 
                   Mathf.Abs(c1.b & c1.a - c2.b & c2.a) <= tolerance && Mathf.Abs(c1.a - c2.a) <= tolerance;
        }
        
        public static bool AreAlphasSimilar(this Color32 c1, Color32 c2, int tolerance)
        {
            return Mathf.Abs(c1.a - c2.a) <= tolerance;
        }
        
        public static Bounds TransformBounds(this Bounds localBounds, Transform transform)
        {
            var centerWorld = transform.TransformPoint(localBounds.center);
            var extentsWorld = transform.TransformVector(localBounds.extents);
            extentsWorld.x = Mathf.Abs(extentsWorld.x);
            extentsWorld.y = Mathf.Abs(extentsWorld.y);
            extentsWorld.z = Mathf.Abs(extentsWorld.z);
            return new Bounds(centerWorld, extentsWorld * 2f);
        }

        public static int GetVertexAttributeFormatSize(this VertexAttributeFormat format)
        {
            switch (format)
            {
                case VertexAttributeFormat.Float32:
                    return 4;
                case VertexAttributeFormat.Float16:
                    return 2;
                case VertexAttributeFormat.UNorm8:
                    return 1;
                case VertexAttributeFormat.SNorm8:
                    return 1;
                case VertexAttributeFormat.UNorm16:
                    return 2;
                case VertexAttributeFormat.SNorm16:
                    return 2;
                case VertexAttributeFormat.UInt8:
                    return 1;
                case VertexAttributeFormat.SInt8:
                    return 1;
                case VertexAttributeFormat.UInt16:
                    return 2;
                case VertexAttributeFormat.SInt16:
                    return 2;
                case VertexAttributeFormat.UInt32:
                    return 4;
                case VertexAttributeFormat.SInt32:
                    return 4;
            }
            return 0;
        }

        public static bool IsCorrectFilename(this string filename, bool printIncorrectCharacters)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                if (printIncorrectCharacters)
                {
                    Debug.LogWarning($"Invalid filename! Filename cannot be null or consists only of white-space characters.");
                }
                return false;
            }
            
            if (filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                if (printIncorrectCharacters)
                {
                    var invalidChars = new string(filename.Where(c => Path.GetInvalidFileNameChars().Contains(c)).ToArray());
                    Debug.LogWarning($"Invalid filename! Filename contains characters: {invalidChars}");
                }
                return false;
            }
            return true;
        }
        
        public static Vector3[] GetLocalCorners(this RectTransform rectTransform)
        {
            var rect = rectTransform.rect;
            return new[]
            {
                new Vector3(rect.x, rect.y, 0f),
                new Vector3(rect.x, rect.yMax, 0f),
                new Vector3(rect.xMax, rect.yMax, 0f),
                new Vector3(rect.xMax, rect.y, 0f)
            };
        }

        public static Vector3[] GetLocalCornersScaled(this RectTransform rectTransform)
        {
            var corners = GetLocalCorners(rectTransform);
            for (var i = 0; i < corners.Length; i++)
            {
                corners[i] = Vector3.Scale(corners[i], rectTransform.transform.GetCorrectedLossyScale());
            }
            return corners;
        }
        
        public static Mesh CreatePlaneMesh(this RectTransform rectTransform)
        {
            var corners = rectTransform.GetLocalCorners();
            var quadMesh = new Mesh();
            var vertices = new Vector3[4];
            vertices[0] = corners[0];
            vertices[1] = corners[1];
            vertices[2] = corners[2];
            vertices[3] = corners[3];

            var uvs = new Vector2[4];
            uvs[0] = new Vector2(0f, 0f);
            uvs[1] = new Vector2(0f, 1f);
            uvs[2] = new Vector2(1f, 1f);
            uvs[3] = new Vector2(1f, 0f);

            var triangles = new int[]
            {
                0, 1, 2,
                0, 2, 3
            };

            quadMesh.vertices = vertices;
            quadMesh.uv = uvs;
            quadMesh.triangles = triangles;
            quadMesh.RecalculateNormals();
            quadMesh.RecalculateBounds();
            return quadMesh;
        }

        public static Vector3[] GetLocalCorners(this SpriteRenderer spriteRenderer)
        {
            var bottomLeft = new Vector3(spriteRenderer.sprite.bounds.min.x, spriteRenderer.sprite.bounds.min.y);
            var topLeft = new Vector3(spriteRenderer.sprite.bounds.min.x, spriteRenderer.sprite.bounds.max.y);
            var topRight = new Vector3(spriteRenderer.sprite.bounds.max.x, spriteRenderer.sprite.bounds.max.y);
            var bottomRight = new Vector3(spriteRenderer.sprite.bounds.max.x, spriteRenderer.sprite.bounds.min.y);
            return new []{ bottomLeft, topLeft, topRight, bottomRight };
        }
        
        public static Vector3[] GetLocalCornersScaled(this SpriteRenderer spriteRenderer)
        {
            var corners = GetLocalCorners(spriteRenderer);
            for (var i = 0; i < corners.Length; i++)
            {
                corners[i] = Vector3.Scale(corners[i], spriteRenderer.transform.GetCorrectedLossyScale());
            }
            return corners;
        }
        
        public static Vector3[] GetWorldCorners(this SpriteRenderer spriteRenderer)
        {
            var corners = GetLocalCorners(spriteRenderer);
            for (var i = 0; i < corners.Length; i++)
            {
                corners[i] = spriteRenderer.transform.TransformPoint(corners[i]);
            }
            return corners;
        }
        
        public static Mesh CreatePlaneMesh(this SpriteRenderer spriteRenderer)
        {
            var quadMesh = new Mesh();
            var vertices = new Vector3[spriteRenderer.sprite.vertices.Length];
            for (var i = 0; i < spriteRenderer.sprite.vertices.Length; i++)
            {
                vertices[i] = spriteRenderer.sprite.vertices[i];
            }
            
            quadMesh.SetVertices(vertices);
            quadMesh.SetUVs(0, spriteRenderer.sprite.uv);
            quadMesh.SetTriangles(spriteRenderer.sprite.triangles, 0, false);
            quadMesh.RecalculateNormals();
            quadMesh.RecalculateBounds();
            return quadMesh;
        }

        public static Bounds GetMaxBounds(this RectTransform rectTransform)
        {
            var worldCorners = new Vector3[4];
            rectTransform.GetWorldCorners(worldCorners);
            var min = worldCorners[0];
            var max = worldCorners[0];
            for (var i = 1; i < worldCorners.Length; i++)
            {
                min = Vector3.Min(min, worldCorners[i]);
                max = Vector3.Max(max, worldCorners[i]);
            }
            
            Bounds maxBounds = default;
            maxBounds.SetMinMax(min, max);
            return maxBounds;
        }
        
        public static Vector2Int GetDimensions(this Texture texture)
        {
            if (texture == null)
            {
                Debug.LogError("Texture is null.");
                return Vector2Int.zero;
            }

            return new Vector2Int(texture.width, texture.height);
        }
        
        private static Vector3 GetCorrectedLossyScale(this Transform transform)
        {
            var correctedScale = transform.localScale;
            var currentTransform = transform.parent;

            while (currentTransform != null)
            {
                correctedScale = new Vector3(
                    correctedScale.x * currentTransform.localScale.x,
                    correctedScale.y * currentTransform.localScale.y,
                    correctedScale.z * currentTransform.localScale.z
                );

                currentTransform = currentTransform.parent;
            }

            return correctedScale;
        }
    }
}