using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Radiantium.Core
{
    [DebuggerDisplay("<{R}, {G}, {B}>")]
    public struct Color3F
    {
        public float R;
        public float G;
        public float B;

        public bool IsValid
        {
            get
            {
                if (R < 0 || float.IsInfinity(R) || float.IsNaN(R)) return false;
                if (G < 0 || float.IsInfinity(G) || float.IsNaN(G)) return false;
                if (B < 0 || float.IsInfinity(B) || float.IsNaN(B)) return false;
                return true;
            }
        }

        public Color3F(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        public Color3F(Vector3 v) : this(v.X, v.Y, v.Z) { }

        public Color3F(float v) : this(v, v, v) { }

        public static Color3F ToLinearRGB(Color3F c) { return new(ToLiner(c.R), ToLiner(c.G), ToLiner(c.B)); }

        public static Color3F ToSRGB(Color3F c) { return new(ToSrgb(c.R), ToSrgb(c.G), ToSrgb(c.B)); }

        public static ref float IndexerUnsafe(ref Color3F c, int i) { return ref Unsafe.Add(ref c.R, i); }

        public void ToLinearRGB() { R = ToLiner(R); G = ToLiner(G); B = ToLiner(B); }

        public void ToSrgb() { R = ToSrgb(R); G = ToSrgb(G); B = ToSrgb(B); }

        public float GetLuminance() { return R * 0.212671f + G * 0.715160f + B * 0.072169f; }

        public static Color3F operator +(Color3F l, Color3F r) { return new(l.R + r.R, l.G + r.G, l.B + r.B); }
        public static Color3F operator -(Color3F l, Color3F r) { return new(l.R - r.R, l.G - r.G, l.B - r.B); }
        public static Color3F operator *(Color3F l, Color3F r) { return new(l.R * r.R, l.G * r.G, l.B * r.B); }
        public static Color3F operator /(Color3F l, Color3F r) { return new(l.R / r.R, l.G / r.G, l.B / r.B); }
        public static Color3F operator +(Color3F l, float r) { return new(l.R + r, l.G + r, l.B + r); }
        public static Color3F operator -(Color3F l, float r) { return new(l.R - r, l.G - r, l.B - r); }
        public static Color3F operator *(Color3F l, float r) { return new(l.R * r, l.G * r, l.B * r); }
        public static Color3F operator /(Color3F l, float r) { return new(l.R / r, l.G / r, l.B / r); }
        public static Color3F operator +(float l, Color3F r) { return new(l + r.R, l + r.G, l + r.B); }
        public static Color3F operator -(float l, Color3F r) { return new(l - r.R, l - r.G, l - r.B); }
        public static Color3F operator *(float l, Color3F r) { return new(l * r.R, l * r.G, l * r.B); }
        public static Color3F operator /(float l, Color3F r) { return new(l / r.R, l / r.G, l / r.B); }

        public override string ToString() { return $"<{R}, {G}, {B}>"; }

        private static float ToLiner(float value)
        {
            return value <= 0.04045f ? value * (1.0f / 12.92f) : MathF.Pow((value + 0.055f) * (1.0f / 1.055f), 2.4f);
        }

        private static float ToSrgb(float value)
        {
            return value <= 0.0031308f ? 12.92f * value : (1.0f + 0.055f) * MathF.Pow(value, 1.0f / 2.4f) - 0.055f;
        }
    }
}
