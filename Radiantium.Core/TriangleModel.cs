using System.Numerics;

namespace Radiantium.Core
{
    public class TriangleModel
    {
        public Vector3[] Position { get; }
        public Vector3[]? Normal { get; }
        public Vector2[]? UV { get; }
        public int[] Indices { get; }
        public int TriangleCount { get; }
        public int VertexCount => Position.Length;
        public int IndexCount => Indices.Length;
        public long UsedMemory
        {
            get
            {
                return Position.Length * 12 +
                    ((Normal == null) ? 0 : (Normal.Length * 12)) +
                    ((UV == null) ? 0 : (UV.Length * 8)) +
                    Indices.Length * 4;
            }
        }

        public TriangleModel(Vector3[] position, int[] indices, Vector3[]? normal = null, Vector2[]? uv = null)
        {
            Position = position ?? throw new ArgumentNullException(nameof(position));
            Indices = indices ?? throw new ArgumentNullException(nameof(indices));
            Normal = normal;
            UV = uv;
            if (indices.Length % 3 != 0) { throw new ArgumentException("invalid indices data"); }
            TriangleCount = indices.Length / 3;
            if (Normal != null && Normal.Length != VertexCount) { throw new ArgumentException(nameof(normal)); }
            if (UV != null && UV.Length != VertexCount) { throw new ArgumentException(nameof(uv)); }
        }
    }
}
