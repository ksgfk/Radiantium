using System.Numerics;
using System.Runtime.CompilerServices;

namespace Radiantium.Core
{
    public static class MathExt
    {
        //*****************
        //* Memory Access *
        //***************** (why put these func here
        public static ref T IndexerUnsafe<T>(ref T t, int i)
        {
            return ref Unsafe.Add(ref t, i);
        }

        public static ref T IndexerUnsafeReadonly<T>(in T t, int i)
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

        //********
        //* Math *
        //********
        public static float Degree(float value) { return value * (180.0f / MathF.PI); }

        public static float Radian(float value) { return value * (MathF.PI / 180.0f); }

        //******************
        //* Linear Algebra *
        //******************
        public static Matrix4x4 LookAtLeftHand(Vector3 origin, Vector3 target, Vector3 up)
        {
            Vector3 dir = Vector3.Normalize(target - origin);
            Vector3 left = Vector3.Normalize(Vector3.Cross(Vector3.Normalize(up), dir));
            Vector3 realUp = Vector3.Normalize(Vector3.Cross(dir, left));
            return new() //C#矩阵储存是行优先，乘法是列优先
            {
                M11 = left.X, M12 = left.Y, M13 = left.Z, M14 = 0,
                M21 = realUp.X, M22 = realUp.Y, M23 = realUp.Z, M24 = 0,
                M31 = dir.X, M32 = dir.Y, M33 = dir.Z, M34 = 0,
                M41 = origin.X, M42 = origin.Y, M43 = origin.Z, M44 = 1,
            };
        }
    }
}
