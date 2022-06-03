using System.Numerics;

namespace Radiantium.Core
{
    public struct Double3 : IEquatable<Double3>
    {
        public double X;
        public double Y;
        public double Z;

        public Double3(double x, double y, double z) { X = x; Y = y; Z = z; }

        public Double3(Vector3 v) { X = v.X; Y = v.Y; Z = v.Z; }

        public readonly double LengthSquared()
        {
            return Dot(this, this);
        }

        public readonly double Length()
        {
            double lengthSquared = LengthSquared();
            return Math.Sqrt(lengthSquared);
        }

        public readonly bool Equals(Double3 other)
        {
            return X == other.X && Y == other.Y && Z == other.Z;
        }

        public override readonly bool Equals(object? obj)
        {
            return obj is Double3 c && Equals(c);
        }

        public override readonly int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z);
        }

        public static Double3 operator +(Double3 l, Double3 r) { return new(l.X + r.X, l.Y + r.Y, l.Z + r.Z); }
        public static Double3 operator -(Double3 l, Double3 r) { return new(l.X - r.X, l.Y - r.Y, l.Z - r.Z); }
        public static Double3 operator *(Double3 l, Double3 r) { return new(l.X * r.X, l.Y * r.Y, l.Z * r.Z); }
        public static Double3 operator /(Double3 l, Double3 r) { return new(l.X / r.X, l.Y / r.Y, l.Z / r.Z); }
        public static Double3 operator +(Double3 l, double r) { return new(l.X + r, l.Y + r, l.Z + r); }
        public static Double3 operator -(Double3 l, double r) { return new(l.X - r, l.Y - r, l.Z - r); }
        public static Double3 operator *(Double3 l, double r) { return new(l.X * r, l.Y * r, l.Z * r); }
        public static Double3 operator /(Double3 l, double r) { return new(l.X / r, l.Y / r, l.Z / r); }
        public static Double3 operator +(double l, Double3 r) { return new(l + r.X, l + r.Y, l + r.Z); }
        public static Double3 operator -(double l, Double3 r) { return new(l - r.X, l - r.Y, l - r.Z); }
        public static Double3 operator *(double l, Double3 r) { return new(l * r.X, l * r.Y, l * r.Z); }
        public static Double3 operator /(double l, Double3 r) { return new(l / r.X, l / r.Y, l / r.Z); }
        public static Double3 operator -(Double3 v) { return new(-v.X, -v.Y, -v.Z); }
        public static bool operator ==(Double3 l, Double3 r) { return l.Equals(r); }
        public static bool operator !=(Double3 l, Double3 r) { return !(l == r); }

        public override readonly string ToString() { return $"<{X}, {Y}, {Z}>"; }

        public static double Dot(Double3 a, Double3 b)
        {
            return (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);
        }

        public static Double3 Lerp(Double3 a, Double3 b, double amount)
        {
            return (a * (1 - amount)) + (b * amount);
        }

        public static Double3 Max(Double3 a, Double3 b)
        {
            return new Double3(
                (a.X > b.X) ? a.X : b.X,
                (a.Y > b.Y) ? a.Y : b.Y,
                (a.Z > b.Z) ? a.Z : b.Z
            );
        }

        public static Double3 Min(Double3 a, Double3 b)
        {
            return new Double3(
                (a.X < b.X) ? a.X : b.X,
                (a.Y < b.Y) ? a.Y : b.Y,
                (a.Z < b.Z) ? a.Z : b.Z
            );
        }

        public static Double3 SquareRoot(Double3 value)
        {
            return new Double3(Math.Sqrt(value.X), Math.Sqrt(value.Y), Math.Sqrt(value.Z));
        }

        public static Double3 Normalize(Double3 value)
        {
            return value / value.Length();
        }

        public static double DistanceSquared(Double3 a, Double3 b)
        {
            Double3 difference = a - b;
            return Dot(difference, difference);
        }

        public static double Distance(Double3 a, Double3 b)
        {
            double distanceSquared = DistanceSquared(a, b);
            return Math.Sqrt(distanceSquared);
        }

        public static Double3 Cross(Double3 a, Double3 b)
        {
            return new Double3(
                (a.Y * b.Z) - (a.Z * b.Y),
                (a.Z * b.X) - (a.X * b.Z),
                (a.X * b.Y) - (a.Y * b.X)
            );
        }

        public static Double3 Clamp(Double3 a, Double3 min, Double3 max)
        {
            return Min(Max(a, min), max);
        }

        public static Double3 Abs(Double3 value)
        {
            return new Double3(Math.Abs(value.X), Math.Abs(value.Y), Math.Abs(value.Z));
        }
    }
}
