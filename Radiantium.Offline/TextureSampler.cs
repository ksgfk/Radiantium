using Radiantium.Core;

namespace Radiantium.Offline
{
    public enum WrapMode
    {
        Clamp,
        Repeat
    }

    public enum FilterMode
    {
        Nearest,
        Linear
    }

    public class TextureSampler
    {
        readonly WrapMode _wrap;
        readonly FilterMode _filter;

        public TextureSampler(WrapMode wrap, FilterMode filter)
        {
            _wrap = wrap;
            _filter = filter;
        }

        public Color3F Sample(float x, float y, Texture2D tex)
        {
            static float WrapCoord(float a, WrapMode mode)
            {
                return mode switch
                {
                    WrapMode.Clamp => Math.Clamp(a, 0, 1),
                    WrapMode.Repeat => a - MathF.Truncate(a),
                    _ => a,
                };
            }

            float realX = Math.Clamp(WrapCoord(x, _wrap) * tex.Width, 0, tex.Width - 1);
            float realY = Math.Clamp(WrapCoord(y, _wrap) * tex.Height, 0, tex.Height - 1);

            switch (_filter)
            {
                case FilterMode.Nearest:
                    return tex.Read((int)realX, (int)realY);
                case FilterMode.Linear:
                    {
                        int lowX = (int)MathF.Truncate(realX);
                        int highX = Math.Clamp(lowX + 1, 0, tex.Width - 1);
                        int lowY = (int)MathF.Truncate(realY);
                        int highY = Math.Clamp(lowY + 1, 0, tex.Height - 1);
                        Color3F a = tex.Read(lowX, lowY);
                        Color3F b = tex.Read(highX, lowY);
                        Color3F c = tex.Read(lowX, highY);
                        Color3F d = tex.Read(highX, highY);
                        float xWeight = realX - MathF.Truncate(realX);
                        Color3F xLerpA = a * xWeight + b * (1.0f - xWeight);
                        Color3F xLerpB = c * xWeight + d * (1.0f - xWeight);
                        float yWeight = realY - MathF.Truncate(realY);
                        return xLerpA * yWeight + xLerpB * (1.0f - yWeight);
                    }
                default:
                    return tex.Read((int)realX, (int)realY);
            }
        }
    }
}
