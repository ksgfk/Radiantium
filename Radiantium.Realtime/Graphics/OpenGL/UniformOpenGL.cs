using Silk.NET.OpenGL;

namespace Radiantium.Realtime.Graphics.OpenGL
{
    public class UniformOpenGL
    {
        public string Name { get; }
        public int Location { get; }
        public UniformType Type { get; }
        public int Length { get; }

        internal UniformOpenGL(string name, int location, UniformType type, int length)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Location = location;
            Type = type;
            Length = length;
        }
    }
}
