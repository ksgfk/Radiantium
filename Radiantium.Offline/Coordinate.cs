using System.Numerics;
using static Radiantium.Core.MathExt;
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
            /* Based on "Building an Orthonormal Basis, Revisited" by
             * Tom Duff, James Burgess, Per Christensen,
             * Christophe Hery, Andrew Kensler, Max Liani,
             * and Ryusuke Villemin (JCGT Vol 6, No 1, 2017)
             */
            float sign = CopySign(1.0f, n.Z);
            float a = -1 / (sign + n.Z);
            float b = n.X * n.Y * a;
            X = new Vector3(
                MulSign(Sqr(n.X) * a, n.Z) + 1.0f,
                MulSign(b, n.Z),
                MulSign(n.X, -n.Z)
            );
            Y = new Vector3(
                b,
                sign + Sqr(n.Y) * a,
                -n.Y
            );
            Z = n;

            //https://zhuanlan.zhihu.com/p/351071035
            //if (Abs(n.X) > Abs(n.Y))
            //{
            //    X = new Vector3(-n.Z, 0, n.X) / Sqrt(n.X * n.X + n.Z * n.Z);
            //}
            //else
            //{
            //    X = new Vector3(0, n.Z, -n.Y) / Sqrt(n.Y * n.Y + n.Z * n.Z);
            //}
            //Y = Cross(n, X);
            //Z = n;
        }

        public Coordinate(Vector3 x, Vector3 y, Vector3 z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vector3 ToLocal(Vector3 v)
        {
            return new Vector3(Dot(v, X), Dot(v, Y), Dot(v, Z));
        }

        public Vector3 ToWorld(Vector3 v)
        {
            return X * v.X + Y * v.Y + Z * v.Z;
        }

        public static float CosTheta(Vector3 v) => v.Z;

        public static float AbsCosTheta(Vector3 v) => Abs(CosTheta(v));

        public static float Cos2Theta(Vector3 v) => Sqr(v.Z);

        public static float Sin2Theta(Vector3 v) => Fma(v.X, v.X, Sqr(v.Y));

        public static float SinTheta(Vector3 v) => SafeSqrt(Sin2Theta(v));

        public static float TanTheta(Vector3 v) => SafeSqrt(Fma(-v.Z, v.Z, 1.0f)) / v.Z;

        public static float Tan2Theta(Vector3 v) => Max(Fma(-v.Z, v.Z, 1.0f), 0.0f) / Sqr(v.Z);

        public static float CosPhi(Vector3 v)
        {
            float sin2Theta = Sin2Theta(v);
            float invSinTheta = Rsqrt(Sin2Theta(v));
            return Abs(sin2Theta) <= 4.0f * float.Epsilon ? 1.0f : Math.Clamp(v.X * invSinTheta, -1.0f, 1.0f);
        }

        public static float SinPhi(Vector3 v)
        {
            float sin2Theta = Sin2Theta(v);
            float invSinTheta = Rsqrt(Sin2Theta(v));
            return Abs(sin2Theta) <= 4.0f * float.Epsilon ? 0.0f : Math.Clamp(v.Y * invSinTheta, -1.0f, 1.0f);
        }

        public static float Cos2Phi(Vector3 v)
        {
            float sin2Theta = Sin2Theta(v);
            return Abs(sin2Theta) <= 4.0f * float.Epsilon ? 1.0f : Math.Clamp(Sqr(v.X) / sin2Theta, -1.0f, 1.0f);
        }

        public static float Sin2Phi(Vector3 v)
        {
            float sin2Theta = Sin2Theta(v);
            return Abs(sin2Theta) <= 4.0f * float.Epsilon ? 0.0f : Math.Clamp(Sqr(v.Y) / sin2Theta, -1.0f, 1.0f);
        }

        public static bool SameHemisphere(Vector3 x, Vector3 y) => x.Z * y.Z > 0;

        public static Vector3 Faceforward(Vector3 v, Vector3 v2) => (Dot(v, v2) < 0.0f) ? -v : v;
    }
}
