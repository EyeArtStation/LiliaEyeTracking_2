using UnityEngine;

namespace XDPaint.Tools.Raycast.Data
{
    public class RaycastData
    {
        private Barycentric barycentric;

        public Vector3 Hit
        {
            get => barycentric.Interpolate(Triangle.Position0, Triangle.Position1, Triangle.Position2);
            internal set => barycentric = new Barycentric(Triangle.Position0, Triangle.Position1, Triangle.Position2, value);
        }
        
        public Vector3 WorldHit => Triangle.Transform.localToWorldMatrix.MultiplyPoint(Hit);

        public Vector2 UVHit { get; set; }

        public Triangle Triangle { get; }

        public RaycastData(Triangle triangle)
        {
            Triangle = triangle;
            barycentric = new Barycentric();
        }
        
        public override string ToString()
        {
            if (Triangle != null)
            {
                return $"Hit: {Hit}, " +
                       $"WorldHit: {WorldHit}, " +
                       $"UVHit: {UVHit}, " +
                       $"Triangle: {Triangle}";
            }
            
            return $"UVHit: {UVHit}";
        }
    }
}