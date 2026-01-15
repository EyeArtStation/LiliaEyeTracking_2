using System;
using System.Collections.Generic;
using UnityEngine;
using XDPaint.Tools.Raycast;

namespace XDPaint.Tools.Triangles
{
    public static class TrianglesData
    {
        public static Triangle[] GetData(Mesh mesh, int subMeshIndex, int uvChannel)
        {
            var indices = mesh.GetTriangles(subMeshIndex);
            if (indices.Length == 0)
            {
                Debug.LogError("Mesh doesn't have indices!");
                return Array.Empty<Triangle>();
            }

            var uvData = new List<Vector2>();
            mesh.GetUVs(uvChannel, uvData);
            if (uvData.Count == 0)
            {
                Debug.LogError("Mesh doesn't have UV in the selected channel!");
                return Array.Empty<Triangle>();
            }

            var indexesCount = indices.Length;
            var triangles = new Triangle[indexesCount / 3];
            for (var i = 0; i < indexesCount; i += 3)
            {
                var index = i / 3;
                var index0 = indices[i + 0];
                var index1 = indices[i + 1];
                var index2 = indices[i + 2];
                triangles[index] = new Triangle(index, index0, index1, index2);
            }

            return triangles;
        }

        public static Triangle[] GetPlaneData()
        {
            var triangleFirst = new Triangle(0, 0, 1, 2);
            var triangleLast = new Triangle(1, 0, 2, 3);
            return new []{ triangleFirst, triangleLast };
        }
    }
}