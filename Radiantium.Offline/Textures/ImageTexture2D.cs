using Radiantium.Core;

namespace Radiantium.Offline.Textures
{
    public class ImageTexture2D : Texture2D
    {
        public ColorBuffer Image { get; }
        public override int Width => Image.Width;
        public override int Height => Image.Height;
        public override TextureSampler Sampler { get; }

        public ImageTexture2D(ColorBuffer image, TextureSampler sampler)
        {
            Image = image ?? throw new ArgumentNullException(nameof(image));
            Sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
        }

        public override Color3F Read(int x, int y)
        {
            return Image.GetRGB(x, y);
        }
    }
}
