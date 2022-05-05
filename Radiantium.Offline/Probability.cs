using System.Numerics;

namespace Radiantium.Offline
{
    public static class Probability
    {
        //***************************
        //* System.Random Extension *
        //***************************
        public static float NextFloat(this Random rand)
        {
            return (float)rand.NextDouble();
        }

        public static Vector2 NextVec2(this Random rand)
        {
            return new Vector2((float)rand.NextDouble(), (float)rand.NextDouble());
        }

        //***************
        //* CDF and PDF *
        //***************
        public static float UniformSquarePdf(Vector2 sample)
        {
            return sample.X is >= 0 and <= 1 && sample.Y is >= 0 and <= 1 ? 1.0f : 0.0f;
        }

        public static Vector2 SquareToTent(Vector2 sample)
        {
            static float Tent(float x)
            {
                return x < 0.5f ? MathF.Sqrt(2.0f * x) - 1.0f : 1.0f - MathF.Sqrt(2.0f - 2.0f * x);
            }

            return new Vector2(Tent(sample.X), Tent(sample.Y));
        }

        public static float SquareToTentPdf(Vector2 p)
        {
            static float TentPdf(float t)
            {
                return t is >= -1 and <= 1 ? 1 - MathF.Abs(t) : 0;
            }

            return TentPdf(p.X) * TentPdf(p.Y);
        }

        public static Vector2 SquareToUniformDisk(Vector2 sample)
        {
            var radius = MathF.Sqrt(sample.X);
            var angle = sample.Y * MathF.PI * 2;
            return new Vector2(radius * MathF.Cos(angle), radius * MathF.Sin(angle));
        }

        public static float SquareToUniformDiskPdf(Vector2 p) { return p.Length() <= 1.0f ? 1 / MathF.PI : 0.0f; }

        public static Vector3 SquareToUniformSphere(Vector2 sample)
        {
            var phi = sample.X * MathF.PI * 2;
            var theta = MathF.Acos(1 - 2 * sample.Y);
            var sinTheta = MathF.Sin(theta);
            var cosTheta = MathF.Cos(theta);
            var sinPhi = MathF.Sin(phi);
            var cosPhi = MathF.Cos(phi);
            return new Vector3(sinTheta * cosPhi, sinTheta * sinPhi, cosTheta);
        }

        public static float SquareToUniformSpherePdf(Vector3 v)
        {
            return MathF.Abs(v.Length() - 1.0f) <= float.Epsilon ? 1 / (4 * MathF.PI) : 0.0f;
        }

        public static Vector3 SquareToUniformHemisphere(Vector2 sample)
        {
            var phi = sample.X * MathF.PI * 2;
            var theta = MathF.Acos(1 - sample.Y);
            var sinTheta = MathF.Sin(theta);
            var cosTheta = MathF.Cos(theta);
            var sinPhi = MathF.Sin(phi);
            var cosPhi = MathF.Cos(phi);
            return new Vector3(sinTheta * cosPhi, sinTheta * sinPhi, cosTheta);
        }

        public static float SquareToUniformHemispherePdf(Vector3 v)
        {
            return MathF.Abs(v.Length() - 1.0f) <= float.Epsilon && v.Z >= 0 ? 1 / (2 * MathF.PI) : 0.0f;
        }

        public static Vector3 SquareToCosineHemisphere(Vector2 sample)
        {
            Vector2 bottom = SquareToUniformDisk(sample);
            var x = bottom.X;
            var y = bottom.Y;
            return new Vector3(x, y, MathF.Sqrt(1 - x * x - y * y));
        }

        public static float SquareToCosineHemispherePdf(Vector3 v)
        {
            return MathF.Abs(v.Length() - 1.0f) <= float.Epsilon && v.Z >= 0 ? v.Z / (MathF.PI) : 0.0f;
        }

        public static Vector3 SquareToBeckmann(Vector2 sample, float alpha)
        {
            var phi = MathF.PI * 2 * sample.X;
            var theta = MathF.Atan(MathF.Sqrt(-alpha * alpha * MathF.Log(1 - sample.Y)));
            var cosPhi = MathF.Cos(phi);
            var sinPhi = MathF.Sin(phi);
            var cosTheta = MathF.Cos(theta);
            var sinTheta = MathF.Sin(theta);
            var x = sinTheta * cosPhi;
            var y = sinTheta * sinPhi;
            var z = cosTheta;
            return new Vector3(x, y, z);
        }

        public static float SquareToBeckmannPdf(Vector3 m, float alpha)
        {
            if (m.Z <= 0)
            {
                return 0;
            }
            var alpha2 = alpha * alpha;
            var cosTheta = m.Z;
            var tanTheta2 = (m.X * m.X + m.Y * m.Y) / (cosTheta * cosTheta);
            var cosTheta3 = cosTheta * cosTheta * cosTheta;
            const float azimuthal = 1 / MathF.PI;
            var longitudinal = MathF.Exp(-tanTheta2 / alpha2) / (alpha2 * cosTheta3);
            return azimuthal * longitudinal;
        }

        public static Vector3 SquareToGGX(Vector2 sample, float alpha)
        {
            float phi = 2.0f * MathF.PI * sample.X;
            float theta = MathF.Acos(MathF.Sqrt((1.0f - sample.Y) / (1.0f + (alpha * alpha - 1.0f) * sample.Y)));
            var (sinPhi, cosPhi) = MathF.SinCos(phi);
            var (sinTheta, cosTheta) = MathF.SinCos(theta);
            return new Vector3(sinTheta * cosPhi, sinTheta * sinPhi, cosTheta);
        }

        public static float SquareToGGXPdf(Vector3 m, float alpha)
        {
            float cosTheta = m.Z;
            if (m.Z <= 0)
            {
                return 0.0f;
            }
            float alpha2 = alpha * alpha;
            float cosTheta2 = cosTheta * cosTheta;
            return (alpha2 * cosTheta) / (MathF.Pow(1.0f + cosTheta2 * (alpha2 - 1.0f), 2) * MathF.PI);
        }
    }
}
