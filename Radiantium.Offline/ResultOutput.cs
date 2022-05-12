using Radiantium.Core;

namespace Radiantium.Offline
{
    public class ResultOutput
    {
        public bool IsSavePng { get; }
        public bool IsPngToSrgb { get; }
        public bool IsSaveExr { get; }
        public string SavePath { get; }
        public string SaveName { get; }

        public ResultOutput(bool isSavePng, bool isPngToSrgb, bool isSaveExr, string savePath, string saveName)
        {
            IsSavePng = isSavePng;
            IsPngToSrgb = isPngToSrgb;
            IsSaveExr = isSaveExr;
            SavePath = savePath ?? throw new ArgumentNullException(nameof(savePath));
            SaveName = saveName ?? throw new ArgumentNullException(nameof(saveName));
        }

        public void Save(Renderer renderer)
        {
            if (IsSavePng)
            {
                ColorBuffer buffer;
                if (IsPngToSrgb)
                {
                    ColorBuffer color = renderer.RenderTarget;
                    buffer = new ColorBuffer(color.Width, color.Height, color.Channel);
                    for (int i = 0; i < color.Width; i++)
                    {
                        for (int j = 0; i < color.Height; i++)
                        {
                            buffer.RefRGB(i, j) = Color3F.ToSRGB(color.GetRGB(i, j));
                        }
                    }
                }
                else
                {
                    buffer = renderer.RenderTarget;
                }
                string fullPath = Path.Combine(SavePath, $"{SaveName}.png");
                using FileStream stream = File.OpenWrite(fullPath);
                buffer.SavePng(stream);
                Logger.Info($"[Offline.Output] -> save result {fullPath}");
            }
            if (IsSaveExr)
            {
                string fullPath = Path.Combine(SavePath, $"{SaveName}.exr");
                using FileStream stream = File.OpenWrite(fullPath);
                renderer.RenderTarget.SaveOpenExr(stream);
                Logger.Info($"[Offline.Output] -> save result {fullPath}");
            }
        }
    }
}
