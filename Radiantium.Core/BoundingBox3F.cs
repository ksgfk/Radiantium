using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using static System.MathF;
using static System.Numerics.Vector3;

namespace Radiantium.Core
{
    [DebuggerDisplay("Min = {Min}, Max = {Max}")]
    public struct BoundingBox3F
    {
        public Vector3 Min;
        public Vector3 Max;

        public Vector3 Diagonal => Max - Min;
        public float Volume
        {
            get
            {
                Vector3 d = Diagonal;
                return d.X * d.Y * d.Z;
            }
        }
        public float SurfaceArea
        {
            get
            {
                Vector3 d = Diagonal;
                return 2 * (d.X * d.Y + d.X * d.Z + d.Y * d.Z);
            }
        }
        public Vector3 Center => (Max + Min) * 0.5f;
        public bool IsValid => Max.X >= Min.X && Max.Y >= Min.Y && Max.Z >= Min.Z;
        public bool IsPoint => Max.X == Min.X && Max.Y == Min.Y && Max.Z == Min.Z;
        public bool HasVolume => Max.X > Min.X && Max.Y > Min.Y && Max.Z > Min.Z;

        public BoundingBox3F()
        {
            Min = new Vector3(float.MaxValue);
            Max = new Vector3(float.MinValue);
        }

        public BoundingBox3F(Vector3 p) { Min = p; Max = p; }

        public BoundingBox3F(Vector3 min, Vector3 max) { Min = min; Max = max; }

        public bool Contains(Vector3 p)
        {
            return p.X > Min.X && p.X < Max.X &&
                p.Y > Min.Y && p.Y < Max.Y &&
                p.Z > Min.Z && p.Z < Max.Z;
        }

        public bool ContainsEqual(Vector3 p)
        {
            return p.X >= Min.X && p.X <= Max.X &&
                p.Y >= Min.Y && p.Y <= Max.Y &&
                p.Z >= Min.Z && p.Z <= Max.Z;
        }

        public bool Contains(BoundingBox3F box)
        {
            return box.Min.X > Min.X && box.Max.X < Max.X &&
                box.Min.Y > Min.Y && box.Max.Y < Max.Y &&
                box.Min.Z > Min.Z && box.Max.Z < Max.Z;
        }

        public bool Overlaps(BoundingBox3F box)
        {
            return box.Min.X < Max.X && box.Max.X > Min.X &&
                box.Min.Y < Max.Y && box.Max.Y > Min.Y &&
                box.Min.Z < Max.Z && box.Max.Z > Min.Z;
        }

        public float SquaredDistance(Vector3 p)
        {
            float dx = Max(Max(0, Min.X - p.X), p.X - Max.X);
            float dy = Max(Max(0, Min.Y - p.Y), p.Y - Max.Y);
            float dz = Max(Max(0, Min.Z - p.Z), p.Z - Max.Z);
            return dx * dx + dy * dy + dz * dz;
        }

        public float Distance(Vector3 p)
        {
            return Sqrt(SquaredDistance(p));
        }

        public float SquaredDistance(BoundingBox3F box)
        {
            float dx = Max(Max(0, Min.X - box.Max.X), box.Min.X - Max.X);
            float dy = Max(Max(0, Min.Y - box.Max.Y), box.Min.Y - Max.Y);
            float dz = Max(Max(0, Min.Z - box.Max.Z), box.Min.Z - Max.Z);
            return dx * dx + dy * dy + dz * dz;
        }

        public float Distance(BoundingBox3F box)
        {
            return Sqrt(SquaredDistance(box));
        }

        public void ExpendBy(Vector3 p)
        {
            Min = Min(Min, p);
            Max = Max(Max, p);
        }

        public void Union(BoundingBox3F box)
        {
            Min = Min(Min, box.Min);
            Max = Max(Max, box.Max);
        }

        public static BoundingBox3F Expend(BoundingBox3F b, Vector3 p)
        {
            return new BoundingBox3F(Min(b.Min, p), Max(b.Max, p));
        }

        public static BoundingBox3F Union(BoundingBox3F b, BoundingBox3F box)
        {
            return new BoundingBox3F(Min(b.Min, box.Min), Max(b.Max, box.Max));
        }

        public int GetLargestAxis()
        {
            Vector3 d = Diagonal;
            if (d.X >= d.Y && d.X >= d.Z) { return 0; }
            else if (d.Y >= d.X && d.Y >= d.Z) { return 1; }
            else { return 2; }
        }

        public Vector3 GetCorner(int index)
        {
            return new Vector3(
                (index & (1 << 0)) == 0 ? Min.X : Max.X,
                (index & (1 << 1)) == 0 ? Min.Y : Max.Y,
                (index & (1 << 2)) == 0 ? Min.Z : Max.Z
            );
        }

        public Vector3 Offset(Vector3 p)
        {
            Vector3 o = p - Min;
            if (Max.X > Min.X) o.X /= Max.X - Min.X;
            if (Max.Y > Min.Y) o.Y /= Max.Y - Min.Y;
            if (Max.Z > Min.Z) o.Z /= Max.Z - Min.Z;
            return o;
        }

        private static float Gamma(int n)
        {
            float e = float.Epsilon * 0.5f;
            return (n * e) / (1 - n * e);
        }

        public bool Intersect(Ray3F ray)
        {
            bool dirIsNegX = ray.InvD.X < 0;
            bool dirIsNegY = ray.InvD.Y < 0;
            bool dirIsNegZ = ray.InvD.Z < 0;
            float tMin = ((dirIsNegX ? Max : Min).X - ray.O.X) * ray.InvD.X;
            float tMax = ((dirIsNegX ? Min : Max).X - ray.O.X) * ray.InvD.X;
            float tyMin = ((dirIsNegY ? Max : Min).Y - ray.O.Y) * ray.InvD.Y;
            float tyMax = ((dirIsNegY ? Min : Max).Y - ray.O.Y) * ray.InvD.Y;
            tMax *= 1 + 2 * Gamma(3);
            tyMax *= 1 + 2 * Gamma(3);
            if (tMin > tyMax || tyMin > tMax) { return false; }
            if (tyMin > tMin) tMin = tyMin;
            if (tyMax < tMax) tMax = tyMax;
            float tzMin = ((dirIsNegZ ? Max : Min).Z - ray.O.Z) * ray.InvD.Z;
            float tzMax = ((dirIsNegZ ? Min : Max).Z - ray.O.Z) * ray.InvD.Z;
            tzMax *= 1 + 2 * Gamma(3);
            if (tMin > tzMax || tzMin > tMax) { return false; }
            if (tzMin > tMin) tMin = tzMin;
            if (tzMax < tMax) tMax = tzMax;
            return (tMin < ray.MaxT) && (tMax > 0);
        }

        public bool Intersect(Ray3F ray, out float minT, out float maxT)
        {
            bool dirIsNegX = ray.InvD.X < 0;
            bool dirIsNegY = ray.InvD.Y < 0;
            bool dirIsNegZ = ray.InvD.Z < 0;
            float tMin = ((dirIsNegX ? Max : Min).X - ray.O.X) * ray.InvD.X;
            float tMax = ((dirIsNegX ? Min : Max).X - ray.O.X) * ray.InvD.X;
            float tyMin = ((dirIsNegY ? Max : Min).Y - ray.O.Y) * ray.InvD.Y;
            float tyMax = ((dirIsNegY ? Min : Max).Y - ray.O.Y) * ray.InvD.Y;
            tMax *= 1 + 2 * Gamma(3);
            tyMax *= 1 + 2 * Gamma(3);
            if (tMin > tyMax || tyMin > tMax)
            {
                minT = default; maxT = default;
                return false;
            }
            if (tyMin > tMin) tMin = tyMin;
            if (tyMax < tMax) tMax = tyMax;
            float tzMin = ((dirIsNegZ ? Max : Min).Z - ray.O.Z) * ray.InvD.Z;
            float tzMax = ((dirIsNegZ ? Min : Max).Z - ray.O.Z) * ray.InvD.Z;
            tzMax *= 1 + 2 * Gamma(3);
            if (tMin > tzMax || tzMin > tMax)
            {
                minT = default; maxT = default;
                return false;
            }
            if (tzMin > tMin) tMin = tzMin;
            if (tzMax < tMax) tMax = tzMax;
            bool result = (tMin < ray.MaxT) && (tMax > 0);
            minT = tMin; maxT = tMax;
            return result;
        }

        public override string ToString()
        {
            return $"<Min = {Min}, Max = {Max}>";
        }

        public int MaximumExtent()
        {
            Vector3 d = Diagonal;
            if (d.X > d.Y && d.X > d.Z)
                return 0;
            else if (d.Y > d.Z)
                return 1;
            else
                return 2;
        }

        public void Transform(Matrix4x4 m)
        {
            this = Transform(this, m);
        }

        public static BoundingBox3F Transform(BoundingBox3F b, Matrix4x4 m)
        {
            BoundingBox3F result = new BoundingBox3F(Vector3.Transform(new Vector3(b.Min.X, b.Min.Y, b.Min.Z), m));
            result.ExpendBy(Vector3.Transform(new Vector3(b.Max.X, b.Min.Y, b.Min.Z), m));
            result.ExpendBy(Vector3.Transform(new Vector3(b.Min.X, b.Max.Y, b.Min.Z), m));
            result.ExpendBy(Vector3.Transform(new Vector3(b.Min.X, b.Min.Y, b.Max.Z), m));
            result.ExpendBy(Vector3.Transform(new Vector3(b.Min.X, b.Max.Y, b.Max.Z), m));
            result.ExpendBy(Vector3.Transform(new Vector3(b.Max.X, b.Max.Y, b.Min.Z), m));
            result.ExpendBy(Vector3.Transform(new Vector3(b.Max.X, b.Min.Y, b.Max.Z), m));
            result.ExpendBy(Vector3.Transform(new Vector3(b.Max.X, b.Max.Y, b.Max.Z), m));
            return result;
        }

        public static ref Vector3 IndexerUnsafe(ref BoundingBox3F box, int i)
        {
            return ref Unsafe.Add(ref box.Min, i);
        }
    }
}
