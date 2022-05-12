using Radiantium.Core;

namespace Radiantium.Offline.Textures
{
    public class ConstColorTexture2D : Texture2D
    {
        public Color3F Color { get; }
        public override int Width => 1;
        public override int Height => 1;
        public override TextureSampler Sampler { get; }

        public ConstColorTexture2D(Color3F color)
        {
            Color = color;
            Sampler = new TextureSampler(WrapMode.Clamp, FilterMode.Nearest);
        }

        public override Color3F Read(int x, int y)
        {
            return Color;
        }
    }
}
