using ImageMagick;
using System.Runtime.CompilerServices;
using System.Text;

namespace Radiantium.Core
{
    public static class TextureUtility
    {
        public static void SavePpm(Stream stream, byte[] color, int maxColor, int width, int height)
        {
            if (color.Length % 3 != 0 || width * height * 3 != color.Length)
            {
                throw new ArgumentException("Invalid color array.Only RGB");
            }
            var head = $"P6\n{width} {height}\n{maxColor}\n";
            stream.Write(Encoding.ASCII.GetBytes(head));
            for (var i = 0; i < height; i++)
            {
                for (var j = 0; j < width; j++)
                {
                    var flippedR = height - 1 - i;
                    var k = flippedR * width * 3 + j * 3;
                    stream.WriteByte(color[k]);
                    stream.WriteByte(color[k + 1]);
                    stream.WriteByte(color[k + 2]);
                }
            }
        }

        public static void SavePpm(StreamWriter writer, byte[] color, int maxColor, int width, int height)
        {
            if (color.Length % 3 != 0 || width * height * 3 != color.Length)
            {
                throw new ArgumentException("Invalid color array.Only RGB");
            }
            var head = $"P3\n{width} {height}\n{maxColor}\n";
            writer.Write(head);
            for (var i = 0; i < height; i++)
            {
                for (var j = 0; j < width; j++)
                {
                    var flippedR = height - 1 - i;
                    var k = flippedR * width * 3 + j * 3;
                    writer.Write(color[k]);
                    writer.Write(' ');
                    writer.Write(color[k + 1]);
                    writer.Write(' ');
                    writer.Write(color[k + 2]);
                    writer.Write('\n');
                }
            }
        }

        public static void SavePng(this ColorBuffer colorBuffer, Stream stream)
        {
            try
            {
                int width = colorBuffer.Width;
                int height = colorBuffer.Height;
                using MagickImage result = new MagickImage(MagickColor.FromRgb(0, 0, 0), width, height);
                var settings = result.Settings;
                settings.ColorSpace = ColorSpace.sRGB;
                settings.Format = MagickFormat.Png32;
                using var pixel = result.GetPixelsUnsafe();
                Span<float> buffer = stackalloc float[3];
                for (var i = 0; i < height; i++)
                {
                    for (var j = 0; j < width; j++)
                    {
                        var k = height - i - 1;
                        Color3F color = colorBuffer.GetRGB(j, k);
                        buffer[0] = Math.Clamp(color.R * Quantum.Max, 0.0f, Quantum.Max);
                        buffer[1] = Math.Clamp(color.G * Quantum.Max, 0.0f, Quantum.Max);
                        buffer[2] = Math.Clamp(color.B * Quantum.Max, 0.0f, Quantum.Max);
                        pixel.SetPixel(j, i, buffer);
                    }
                }
                result.Write(stream);
            }
            catch (MagickException e)
            {
                throw new NotSupportedException("magick exception", e);
            }
        }

        public static void SaveOpenExr(this ColorBuffer colorBuffer, Stream stream)
        {
            try
            {
                int width = colorBuffer.Width;
                int height = colorBuffer.Height;
                using MagickImage result = new MagickImage(MagickColor.FromRgb(0, 0, 0), width, height);
                var settings = result.Settings;
                settings.Format = MagickFormat.Exr;
                using var pixel = result.GetPixelsUnsafe();
                Span<float> buffer = stackalloc float[3];
                for (var i = 0; i < height; i++)
                {
                    for (var j = 0; j < width; j++)
                    {
                        var k = height - i - 1;
                        Color3F color = colorBuffer.GetRGB(j, k);
                        buffer[0] = color.R * Quantum.Max;
                        buffer[1] = color.G * Quantum.Max;
                        buffer[2] = color.B * Quantum.Max;
                        pixel.SetPixel(j, i, buffer);
                    }
                }
                result.Write(stream);
            }
            catch (MagickException e)
            {
                throw new NotSupportedException("magick exception", e);
            }
        }

        public static ColorBuffer LoadImageFromPath(string path, bool isFlipY, bool isCastToLinear)
        {
            try
            {
                using MagickImage image = new MagickImage(path);
                int width = image.Width;
                int height = image.Height;
                using var pixels = image.GetPixelsUnsafe();
                int channel = pixels.Channels;
                IntPtr head = pixels.GetAreaPointer(0, 0, 1, 1);
                ColorBuffer buffer = new ColorBuffer(width, height, channel);
                for (int i = 0; i < width; i++)
                {
                    for (int j = 0; j < height; j++)
                    {
                        unsafe
                        {
                            int k;
                            if (isFlipY)
                            {
                                k = height - j - 1;
                            }
                            else
                            {
                                k = j;
                            }
                            int offset = (k * width + i) * channel; //Magick.NET内部储存是行优先...
                            void* ptr = Unsafe.Add<float>(head.ToPointer(), offset);
                            float r = Unsafe.Read<float>(ptr);
                            float g = Unsafe.Read<float>(Unsafe.Add<float>(ptr, 1));
                            float b = Unsafe.Read<float>(Unsafe.Add<float>(ptr, 2));
                            buffer.SetRGB(i, j, new Color3F(r, g, b));
                        }
                    }
                }
                for (int i = 0; i < width; i++) //数据缩小到[0,1]
                {
                    for (int j = 0; j < height; j++)
                    {
                        buffer.RefRGB(i, j) /= Quantum.Max;
                    }
                }
                if (isCastToLinear)
                {
                    for (int i = 0; i < width; i++)
                    {
                        for (int j = 0; j < height; j++)
                        {
                            buffer.RefRGB(i, j).ToLinearRGB();
                        }
                    }
                }
                return buffer;
            }
            catch (MagickException e)
            {
                throw new FileLoadException("magick inner exception", e);
            }
        }

        public static bool IsHdr(string path)
        {
            var ext = Path.GetExtension(path);
            return ext.Contains("exr") || ext.Contains("hdr");
        }
    }
}
