using System.Numerics;
using System.Runtime.CompilerServices;
using static System.MathF;
using static System.Numerics.Vector3;

namespace Radiantium.Core
{
    public static class MathExt
    {
        //*****************
        //* Memory Access *
        //***************** (why put these func here
        public static ref T IndexerUnsafe<T>(ref T t, int i) where T : unmanaged
        {
            return ref Unsafe.Add(ref t, i);
        }

        public static ref T IndexerUnsafeReadonly<T>(in T t, int i) where T : unmanaged
        {
            return ref Unsafe.Add(ref Unsafe.AsRef(in t), i);
        }

        //********************
        //* Vector Extension *
        //********************
        public static ref float IndexerUnsafe(ref Vector3 v, int i)
        {
            return ref Unsafe.Add(ref v.X, i);
        }

        public static ref float IndexerUnsafe(ref Vector2 v, int i)
        {
            return ref Unsafe.Add(ref v.X, i);
        }

        public static ref float IndexerUnsafe(ref Vector4 v, int i)
        {
            return ref Unsafe.Add(ref v.X, i);
        }

        public static float MaxElement(Vector2 v)
        {
            return Max(v.X, v.Y);
        }

        public static float MaxElement(Vector3 v)
        {
            return Max(Max(v.X, v.Y), v.Z);
        }

        public static float MaxElement(Vector4 v)
        {
            return Max(Max(Max(v.X, v.Y), v.Z), v.W);
        }

        public static float MaxElement(Color3F v)
        {
            return Max(Max(v.R, v.G), v.B);
        }

        public static Vector3 Exp(Vector3 v)
        {
            return new Vector3(MathF.Exp(v.X), MathF.Exp(v.Y), MathF.Exp(v.Z));
        }

        //********
        //* Math *
        //********
        public static float Degree(float value) { return value * (180.0f / PI); }

        public static float Radian(float value) { return value * (PI / 180.0f); }

        public static float Sqr(float value) { return value * value; }

        public static float Fma(float a, float b, float c) { return a * b + c; }

        public static float MulSign(float a1, float a2) { return a1 * CopySign(1, a2); }

        public static float SafeSqrt(float a) { return Sqrt(Max(a, 0)); }

        public static float Rsqrt(float a) { return 1.0f / Sqrt(a); }

        public static bool Refract(Vector3 wi, Vector3 n, float eta, out Vector3 wt)
        {
            float cosThetaI = Dot(n, wi);
            float sin2ThetaI = Max(0, 1 - cosThetaI * cosThetaI);
            float sin2ThetaT = eta * eta * sin2ThetaI;
            if (sin2ThetaT >= 1) { wt = default; return false; }
            float cosThetaT = Sqrt(1 - sin2ThetaT);
            wt = Normalize(eta * -wi + (eta * cosThetaI - cosThetaT) * n);
            return true;
        }

        public static Vector3 SphericalCoordinates(float theta, float phi)
        {
            (float sinTheta, float cosTheta) = SinCos(theta);
            (float sinPhi, float cosPhi) = SinCos(phi);
            return new Vector3(
                cosPhi * sinTheta,
                sinPhi * sinTheta,
                cosTheta);
        }

        public static Vector3 SphericalDirection(float sinTheta, float cosTheta, float phi,
            Vector3 x, Vector3 y, Vector3 z)
        {
            (float sinPhi, float cosPhi) = SinCos(phi);
            return sinTheta * cosPhi * x + sinTheta * sinPhi * y + cosTheta * z;
        }

        public static Vector3 SphericalDirection(float sinTheta, float cosTheta, float phi)
        {
            (float sinPhi, float cosPhi) = SinCos(phi);
            return new Vector3(sinTheta * cosPhi, sinTheta * sinPhi, cosTheta);
        }

        public static float Lerp(float t, float v1, float v2) { return (1 - t) * v1 + t * v2; }

        public static (bool, double, double) SolveQuadratic(double a, double b, double c)
        {
            bool linearCase = a == 0.0;
            bool validLinear = linearCase && b != 0.0;
            double x0 = -c / b, x1 = -c / b;
            double discrim = b * b - 4.0 * a * c;
            bool validQuadratic = !linearCase && (discrim >= 0.0);
            if (validQuadratic)
            {
                double rootDiscrim = Math.Sqrt(discrim);
                double temp = -0.5 * (b + Math.CopySign(rootDiscrim, b));
                double x0p = temp / a;
                double x1p = c / temp;
                double x0m = Math.Min(x0p, x1p);
                double x1m = Math.Max(x0p, x1p);
                x0 = linearCase ? x0 : x0m;
                x1 = linearCase ? x0 : x1m;
            }
            return (validLinear || validQuadratic, x0, x1);
        }

        public static float Pow5(float a)
        {
            return (a * a) * (a * a) * a;
        }

        //******************
        //* Linear Algebra *
        //******************
        public static Matrix4x4 LookAtLeftHand(Vector3 origin, Vector3 target, Vector3 up)
        {
            Vector3 dir = Normalize(target - origin);
            Vector3 left = Normalize(Cross(Normalize(up), dir));
            Vector3 realUp = Normalize(Cross(dir, left));
            return new() //C#矩阵储存是行优先，乘法是列优先
            {
                M11 = left.X, M12 = left.Y, M13 = left.Z, M14 = 0,
                M21 = realUp.X, M22 = realUp.Y, M23 = realUp.Z, M24 = 0,
                M31 = dir.X, M32 = dir.Y, M33 = dir.Z, M34 = 0,
                M41 = origin.X, M42 = origin.Y, M43 = origin.Z, M44 = 1,
            };
        }

        public static float AbsDot(Vector3 u, Vector3 v)
        {
            return Abs(Dot(u, v));
        }

        //************
        //* Algoritm *
        //************
        public static int Partition<T>(IList<T> list, int begin, int end, Func<T, bool> func)
        {
            int first = begin;
            int last = end;
            while (true)
            {
                while (true)
                {
                    if (first == last)
                    {
                        return first;
                    }
                    if (!func(list[first]))
                    {
                        break;
                    }
                    ++first;
                }
                while (!func(list[last]))
                {
                    if (first == last)
                    {
                        return first;
                    }
                    last--;
                }
                T temp = list[first];
                list[first] = list[last];
                list[last] = temp;
                first++;
            }
        }

        public static int FindInterval<T>(IReadOnlyList<T> list, T target, Func<T, T, bool> pred)
        {
            int first = 0, len = list.Count;
            while (len > 0)
            {
                int half = len >> 1, middle = first + half;
                if (pred(target, list[middle]))
                {
                    first = middle + 1;
                    len -= half + 1;
                }
                else
                {
                    len = half;
                }
            }
            return Math.Clamp(first - 1, 0, list.Count - 2);
        }

        //**********
        //* Thread *
        //**********
        public static float InterlockedAdd(ref float local, float value)
        {
            float t = local;
            while (true)
            {
                float currentValue = t;
                float newValue = currentValue + value;
                t = Interlocked.CompareExchange(ref local, newValue, currentValue);
                if (t == currentValue)
                {
                    return newValue;
                }
            }
        }
    }
}
