using System.Numerics;
using static System.MathF;
using static System.Numerics.Vector3;

namespace Radiantium.Offline
{
    public struct Coordinate
    {
        public Vector3 X;
        public Vector3 Y;
        public Vector3 Z;

        public Coordinate(Vector3 n)
        {
            float sign = Sign(n.Z);
            float a = -1.0f / (sign + n.Z);
            float b = n.X * n.Y * a;
            X = new Vector3(1.0f + sign * n.X * n.X * a, sign * b, -sign * n.X);
            Y = new Vector3(b, sign + n.Y * n.Y * a, -n.Y);
            Z = n;
        }

        public Vector3 ToLocal(Vector3 v)
        {
            return new Vector3(Dot(v, X), Dot(v, Y), Dot(v, Z));
        }

        public Vector3 ToWorld(Vector3 v)
        {
            return X * v.X + Y * v.Y + Z * v.Z;
        }

        public static float CosTheta(Vector3 v) { return v.Z; }

        public static float AbsCosTheta(Vector3 v) { return Abs(CosTheta(v)); }

        public static float CosTheta2(Vector3 vec) { return vec.Z * vec.Z; }

        public static float SinTheta(Vector3 v)
        {
            var temp = SinTheta2(v);
            return temp <= 0.0f ? 0.0f : Sqrt(temp);
        }

        public static float TanTheta(Vector3 v)
        {
            var temp = SinTheta2(v);
            return temp <= 0.0f ? 0.0f : Sqrt(temp) / v.Z;
        }

        public static float TanTheta2(Vector3 vec)
        {
            float temp = 1 - vec.Z * vec.Z;
            if (temp <= 0.0f)
            {
                return 0.0f;
            }
            return temp / (vec.Z * vec.Z);
        }

        public static float SinTheta2(Vector3 v) { return 1.0f - v.Z * v.Z; }

        public static float SinPhi(Vector3 v)
        {
            var sinTheta = SinTheta(v);
            return sinTheta == 0.0f ? 1.0f : Math.Clamp(v.Y / sinTheta, -1.0f, 1.0f);
        }

        public static float CosPhi(Vector3 v)
        {
            var sinTheta = SinTheta(v);
            return sinTheta == 0.0f ? 1.0f : Math.Clamp(v.X / sinTheta, -1.0f, 1.0f);
        }

        public static float SinPhi2(Vector3 v) { return Math.Clamp(v.Y * v.Y / SinTheta2(v), 0.0f, 1.0f); }

        public static float CosPhi2(Vector3 v) { return Math.Clamp(v.X * v.X / SinTheta2(v), 0.0f, 1.0f); }
    }
}
