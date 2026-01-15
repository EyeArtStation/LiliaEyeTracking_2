using UnityEngine;

namespace XDPaint.Core.PaintObject.Base
{
    public class BaseWorldData
    {
        private Vector4[] positions = new Vector4[Constants.PaintWorldShader.PositionsCount];
        private Vector4[] normals = new Vector4[Constants.PaintWorldShader.PositionsCount];
        private float[] rotations = new float[Constants.PaintWorldShader.PositionsCount];
        private int count;

        public Vector4[] Positions
        {
            get { return positions; }
            set { positions = value; }
        }
        
        public Vector4[] Normals
        {
            get { return normals; }
            set { normals = value; }
        }
        
        public float[] Rotations
        {
            get { return rotations; }
            set { rotations = value; }
        }

        public int Count
        {
            get { return count; }
            set { count = value; }
        }
    }
}