using System.Runtime.CompilerServices;

namespace Radiantium.Core
{
    public class ColorBuffer
    {
        readonly int _width;
        readonly int _height;
        readonly int _channel;
        readonly float[] _buffer;

        public int Width => _width;
        public int Height => _height;
        public int Channel => _channel;

        public ColorBuffer(int width, int height, int channel)
        {
            _width = width;
            _height = height;
            _channel = channel;
            _buffer = new float[width * height * channel];
        }

        private int GetIndex(int x, int y)
        {
            return (x * _height + y) * _channel;
        }

        public ref Color3F RefRGB(int x, int y)
        {
            return ref Unsafe.As<float, Color3F>(ref _buffer[GetIndex(x, y)]);
        }

        public Color3F GetRGB(int x, int y)
        {
            return Unsafe.As<float, Color3F>(ref _buffer[GetIndex(x, y)]);
        }

        public void SetRGB(int x, int y, Color3F color)
        {
            int i = GetIndex(x, y);
            _buffer[i + 0] = color.R;
            _buffer[i + 1] = color.G;
            _buffer[i + 2] = color.B;
        }
    }
}
