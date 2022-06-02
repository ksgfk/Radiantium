using static Radiantium.Core.MathExt;
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

        public static float Cos2Theta(Vector3 v) => v.Z * v.Z;

        public static float Sin2Theta(Vector3 v) => Max(0, 1.0f - Cos2Theta(v));

        public static float SinTheta(Vector3 v) => Sqrt(Sin2Theta(v));

        public static float TanTheta(Vector3 v) => SinTheta(v) / CosTheta(v);

        public static float Tan2Theta(Vector3 v) => Sin2Theta(v) / Cos2Theta(v);

        public static float CosPhi(Vector3 v)
        {
            float sinTheta = SinTheta(v);
            return (sinTheta == 0) ? 1 : Math.Clamp(v.X / sinTheta, -1, 1);
        }

        public static float SinPhi(Vector3 v)
        {
            float sinTheta = SinTheta(v);
            return (sinTheta == 0) ? 0 : Math.Clamp(v.Y / sinTheta, -1, 1);
        }

        public static float Cos2Phi(Vector3 v) => CosPhi(v) * CosPhi(v);

        public static float Sin2Phi(Vector3 v) => SinPhi(v) * SinPhi(v);

        public static bool SameHemisphere(Vector3 x, Vector3 y) => x.Z * y.Z > 0;

        public static Vector3 Faceforward(Vector3 v, Vector3 v2) => (Dot(v, v2) < 0.0f) ? -v : v;
    }
}
