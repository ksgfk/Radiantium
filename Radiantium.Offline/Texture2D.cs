using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline
{
    public abstract class Texture2D
    {
        public abstract int Width { get; }

        public abstract int Height { get; }

        public abstract TextureSampler Sampler { get; }

        public abstract Color3F Read(int x, int y);

        public virtual Color3F Sample(float x, float y)
        {
            return Sampler.Sample(x, y, this);
        }

        public Color3F Sample(Vector2 uv)
        {
            return Sample(uv.X, uv.Y);
        }
    }
}
