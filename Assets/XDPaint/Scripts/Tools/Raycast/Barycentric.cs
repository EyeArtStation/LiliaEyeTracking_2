using UnityEngine;

namespace XDPaint.Tools.Raycast
{
    public readonly struct Barycentric
    {
        private readonly float u;
        private readonly float v;
        private readonly float w;

        public Barycentric(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 hit)
        {
            Vector3 a = v2 - v3, b = v1 - v3, c = hit - v3;
            var aLen = a.x * a.x + a.y * a.y + a.z * a.z;
            var bLen = b.x * b.x + b.y * b.y + b.z * b.z;
            var ab = a.x * b.x + a.y * b.y + a.z * b.z;
            var ac = a.x * c.x + a.y * c.y + a.z * c.z;
            var bc = b.x * c.x + b.y * c.y + b.z * c.z;
            var d = aLen * bLen - ab * ab;
            u = (aLen * bc - ab * ac) / d;
            v = (bLen * ac - ab * bc) / d;
            w = 1.0f - u - v;
        }

        public Vector3 Interpolate(Vector3 v1, Vector3 v2, Vector3 v3)
        {
            return v1 * u + v2 * v + v3 * w;
        }
    }
}