using System;

namespace XDPaint.Utils
{
    public class FrameDataBuffer<T> : XDPaint.Core.IDisposable
    {
        private readonly T[] buffer;

        public int Count { get; private set; }

        public FrameDataBuffer(int length)
        {
            buffer = new T[length];
            Count = 0;
        }
        
        public void AddFrameData(T data)
        {
            if (Count > 0)
            {
                var limit = Math.Min(Count, buffer.Length - 1);
                for (var i = limit; i > 0; i--)
                {
                    buffer[i] = buffer[i - 1];
                }
            }

            buffer[0] = data;
            if (Count < buffer.Length)
            {
                Count++;
            }
        }

        public T GetFrameData(int index)
        {
            if (index < 0 || index >= buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            return Count > index ? buffer[index] : default;
        }

        public T UpdateFrameData(Func<T, T> updateFunc, int index = 0)
        {
            if (index < 0 || index >= buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            var data = updateFunc(buffer[index]);
            buffer[index] = data;
            return data;
        }
        
        public void DoDispose()
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = default;
            }
            
            Count = 0;
        }
    }
}