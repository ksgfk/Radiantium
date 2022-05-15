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
            float u = Wrapper(x, _wrap);
            float v = Wrapper(y, _wrap);
            int width = tex.Width;
            int height = tex.Height;
            float fu = u * (width - 1);
            float fv = v * (height - 1);
            int pu = (int)fu;
            int pv = (int)fv;
            switch (_filter)
            {
                case FilterMode.Nearest:
                    return tex.Read(pu, pv);
                case FilterMode.Linear:
                    {
                        int dpu = (fu > pu + 0.5f) ? 1 : -1;
                        int dpv = (fv > pv + 0.5f) ? 1 : -1;
                        int apu = Math.Clamp(pu + dpu, 0, width - 1);
                        int apv = Math.Clamp(pv + dpv, 0, height - 1);
                        float du = MathF.Min(MathF.Abs(fu - pu - 0.5f), 1);
                        float dv = MathF.Min(MathF.Abs(fv - pv - 0.5f), 1);
                        Color3F u0v0 = tex.Read(pu, pv);
                        Color3F u1v0 = tex.Read(apu, pv);
                        Color3F u0v1 = tex.Read(pu, apv);
                        Color3F u1v1 = tex.Read(apu, apv);
                        return (u0v0 * (1 - du) + u1v0 * du) * (1 - dv) + (u0v1 * (1 - du) + u1v1 * du) * dv;
                    }
                default:
                    return tex.Read(pu, pv);
            }

            static float Wrapper(float a, WrapMode mode)
            {
                switch (mode)
                {
                    case WrapMode.Clamp:
                        return Math.Clamp(a, 0, 1);
                    case WrapMode.Repeat:
                        return Math.Clamp(a - MathF.Floor(a), 0, 1);
                    default:
                        return a;
                }
            }
        }
    }
}
