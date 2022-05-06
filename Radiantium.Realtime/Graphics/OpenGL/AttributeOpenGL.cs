using Silk.NET.OpenGL;

namespace Radiantium.Realtime.Graphics.OpenGL
{
    public class AttributeOpenGL
    {
        public string Name { get; }
        public uint Location { get; }
        public VertexAttribType Type { get; }
        public int Size { get; }
        public int Length { get; }

        internal AttributeOpenGL(string name, uint location, VertexAttribType type, int size, int length)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Location = location;
            Type = type;
            Size = size;
            Length = length;
        }
    }
}
