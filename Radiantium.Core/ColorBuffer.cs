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
        public long UsedMemory => _buffer.Length * 4;

        public ColorBuffer(int width, int height, int channel)
        {
            _width = width;
            _height = height;
            _channel = channel;
            _buffer = new float[width * height * channel];
        }

        private int GetIndex(int x, int y)
        {
            if (x < 0 || x >= _width)
            {
                throw new ArgumentOutOfRangeException(nameof(x), $"x value {x}, max {_width}");
            }
            if (y < 0 || y >= _height)
            {
                throw new ArgumentOutOfRangeException(nameof(y), $"y value {y}, max {_height}");
            }
            return (x * _height + y) * _channel;
        }

        public ref Color3F RefRGB(int x, int y)
        {
            if (_channel < 3) { throw new InvalidOperationException("too few channels"); }
            return ref Unsafe.As<float, Color3F>(ref _buffer[GetIndex(x, y)]);
        }

        public Color3F GetRGB(int x, int y)
        {
            if (_channel < 3)
            {
                int start = GetIndex(x, y);
                Color3F result = default;
                for (int i = 0; i < 3; i++)
                {
                    Color3F.IndexerUnsafe(ref result, i) = _channel >= i ? 0 : _buffer[start + i];
                }
                return result;
            }
            else
            {
                return Unsafe.As<float, Color3F>(ref _buffer[GetIndex(x, y)]);
            }
        }

        public void SetRGB(int x, int y, Color3F color)
        {
            if (_channel < 3)
            {
                int start = GetIndex(x, y);
                for (int i = 0; i < _channel; i++)
                {
                    _buffer[start + i] = Color3F.IndexerUnsafe(ref color, i);
                }
            }
            else
            {
                int i = GetIndex(x, y);
                _buffer[i + 0] = color.R;
                _buffer[i + 1] = color.G;
                _buffer[i + 2] = color.B;
            }
        }

        public Span<float> GetPixel(int x, int y)
        {
            return new Span<float>(_buffer, GetIndex(x, y), _channel);
        }
    }
}
