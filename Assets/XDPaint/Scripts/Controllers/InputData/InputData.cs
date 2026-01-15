using UnityEngine;
using XDPaint.Core;

namespace XDPaint.Controllers.InputData
{
    public struct InputData
    {
        public int FingerId { get; }
        public InputSource InputSource { get; private set; }
        public Vector3 Position { get; internal set; }
        public float Pressure { get; set; }
        public Ray Ray { get; internal set; }

        public InputData(int fingerId = 0)
        {
            FingerId = fingerId;
            InputSource = InputSource.Screen;
            Position = default;
            Pressure = 1f;
            Ray = default;
        }
        
        public InputData(int fingerId = 0, float pressure = 1.0f)
        {
            FingerId = fingerId;
            InputSource = InputSource.Screen;
            Position = default;
            Pressure = pressure;
            Ray = default;
        }
        
        public InputData(Ray ray, Vector3 position, float pressure = 1f, InputSource inputSource = InputSource.Screen, int fingerId = 0)
        {
            Ray = ray;
            Position = position;
            Pressure = pressure;
            InputSource = inputSource;
            FingerId = fingerId;
        }

        internal InputData Update(Ray ray, Vector3 screenPosition, float pressure = 1f, InputSource inputSource = InputSource.Screen)
        {
            Ray = ray;
            Position = screenPosition;
            Pressure = pressure;
            InputSource = inputSource;
            return this;
        }
        
        internal InputData Update(Vector3 screenPosition, float pressure = 1f, InputSource inputSource = InputSource.Screen)
        {
            Position = screenPosition;
            Pressure = pressure;
            InputSource = inputSource;
            return this;
        }
        
        public override string ToString()
        {
            return $"FingerId: {FingerId}, " +
                   $"InputSource: {InputSource}, " +
                   $"Position: {Position}, " +
                   $"Pressure: {Pressure}, " +
                   $"Ray: {Ray}";
        }
    }
}