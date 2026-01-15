using System;
using UnityEngine;
using XDPaint.Tools.Raycast;
using XDPaint.Tools.Raycast.Data;

namespace XDPaint.Utils
{
    public static class MathHelper
    {
        public static Vector2 GetIntersectionUV(Triangle triangle, Ray ray)
        {
            var p1 = triangle.Position0;
            var p2 = triangle.Position1;
            var p3 = triangle.Position2;
            var e1 = p2 - p1;
            var e2 = p3 - p1;
            var p = Vector3.Cross(ray.direction, e2);
            var det = Vector3.Dot(e1, p);
            var invDet = 1.0f / det;
            var t = ray.origin - p1;
            var u = Vector3.Dot(t, p) * invDet;
            var q = Vector3.Cross(t, e1);
            var v = Vector3.Dot(ray.direction, q) * invDet;
            return triangle.UV0 + (triangle.UV1 - triangle.UV0) * u + (triangle.UV2 - triangle.UV0) * v;
        }
        
        public static bool GetIntersectionPosition(Triangle triangle, Ray ray, out Vector3 position)
        {
            var eps = Mathf.Epsilon;
            var p1 = triangle.Position0;
            var p2 = triangle.Position1;
            var p3 = triangle.Position2;
            var e1 = p2 - p1;
            var e2 = p3 - p1;
            var p = Vector3.Cross(ray.direction, e2);
            var det = Vector3.Dot(e1, p);
            var invDet = 1.0f / det;
            var t = ray.origin - p1;
            var u = Vector3.Dot(t, p) * invDet;
            var q = Vector3.Cross(t, e1);
            var v = Vector3.Dot(ray.direction, q) * invDet;
            if (Vector3.Dot(e2, q) * invDet > eps)
            {
                position = p1 + u * e1 + v * e2;
                return true;
            }
            
            position = default;
            return false;
        }

        public static Vector2 Interpolate(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;
            var x = 0.5f * (2 * p1.x + (-p0.x + p2.x) * t + (2 * p0.x - 5 * p1.x + 4 * p2.x - p3.x) * t2 +
                            (-p0.x + 3 * p1.x - 3 * p2.x + p3.x) * t3);
            var y = 0.5f * (2 * p1.y + (-p0.y + p2.y) * t + (2 * p0.y - 5 * p1.y + 4 * p2.y - p3.y) * t2 +
                            (-p0.y + 3 * p1.y - 3 * p2.y + p3.y) * t3);
            return new Vector2(x, y);
        }
        
        public static float GetTriangleArea(Vector3 p0, Vector3 p1 , Vector3 p2)
        {
            var res = Mathf.Pow(p1.x * p0.y - p2.x * p0.y - p0.x * p1.y + p2.x * p1.y + p0.x * p2.y - p1.x * p2.y, 2.0f);
            res += Mathf.Pow(p1.x * p0.z - p2.x * p0.z - p0.x * p1.z + p2.x * p1.z + p0.x * p2.z - p1.x * p2.z, 2.0f);
            res += Mathf.Pow(p1.y * p0.z - p2.y * p0.z - p0.y * p1.z + p2.y * p1.z + p0.y * p2.z - p1.y * p2.z, 2.0f);
            return Mathf.Sqrt(res) * 0.5f;
        }
        
        public static bool TryGetRaycastData(this Triangle triangle, Ray ray, out RaycastData raycastData)
        {
            raycastData = null;
            var p1 = triangle.Position0;
            var p2 = triangle.Position1;
            var p3 = triangle.Position2;
            var e1 = p2 - p1;
            var e2 = p3 - p1;
            var eps = float.Epsilon;
            var p = Vector3.Cross(ray.direction, e2);
            var det = Vector3.Dot(e1, p);
            if (det.IsNaNOrInfinity() || det > eps && det < -eps)
                return false;
            
            var invDet = 1.0f / det;
            var t = ray.origin - p1;
            var u = Vector3.Dot(t, p) * invDet;
            if (u.IsNaNOrInfinity() || u < 0f || u > 1f)
                return false;
            
            var q = Vector3.Cross(t, e1);
            var v = Vector3.Dot(ray.direction, q) * invDet;
            if (v.IsNaNOrInfinity() || v < 0f || u + v > 1f)
                return false;
            
            if (Vector3.Dot(e2, q) * invDet > eps)
            {
                raycastData = new RaycastData(triangle)
                {
                    Hit = p1 + u * e1 + v * e2,
                    UVHit = triangle.UV0 + (triangle.UV1 - triangle.UV0) * u + (triangle.UV2 - triangle.UV0) * v
                };
                return true;
            }
            
            return false;
        }

        private static bool IsPlaneIntersectLine(Vector3 n, Vector3 a, Vector3 w, Vector3 v, out Vector3 p)
        {
            p = Vector3.zero;
            var dotProduct = Vector3.Dot(n, w - v);
            if (Math.Abs(dotProduct) < float.Epsilon)
                return false;
            
            var dot1 = Vector3.Dot(n, a - v);
            var t = dot1 / dotProduct;
            if (t > 1f || t < 0f)
                return false;
            
            p = v + t * (w - v);
            return true;
        }
    }
}