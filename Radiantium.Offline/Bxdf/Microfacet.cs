using System.Numerics;
using static Radiantium.Offline.Coordinate;
using static System.MathF;

namespace Radiantium.Offline.Bxdf
{
    public enum MicrofacetDistributionType
    {
        Beckmann,
        GGX
    }

    public interface IMicrofacetDistribution
    {
        float Alpha { get; }

        float D(Vector3 wh);

        float Lambda(Vector3 w);

        Vector3 SampleWh(Vector3 wo, Random rand);

        float Pdf(Vector3 wo, Vector3 wh);

        public float G1(Vector3 w)
        {
            return 1 / (1 + Lambda(w));
        }

        public float G(Vector3 wo, Vector3 wi)
        {
            return 1 / (1 + Lambda(wo) + Lambda(wi));
        }
    }

    public static class Microfacet
    {
        public struct Beckmann : IMicrofacetDistribution
        {
            public float Alpha { get; }
            public Beckmann(float roughness) { Alpha = Math.Clamp(roughness * roughness, 0.001f, 1.0f); }
            public float D(Vector3 wh) { return DistributionBeckmann(wh, Alpha); }
            public float Lambda(Vector3 w) { return LambdaBeckmann(w, Alpha); }
            public float Pdf(Vector3 wo, Vector3 wh)
            {
                return D(wh) * AbsCosTheta(wh);
            }
            public Vector3 SampleWh(Vector3 wo, Random rand)
            {
                float logSample = Log(1 - rand.Next());
                float tan2Theta = -Alpha * Alpha * logSample;
                float phi = rand.Next() * 2 * PI;
                float cosTheta = 1 / Sqrt(1 + tan2Theta);
                float sinTheta = Sqrt(Max(0, 1 - cosTheta * cosTheta));
                (float sinPhi, float cosPhi) = SinCos(phi);
                Vector3 wh = new Vector3(sinTheta * cosPhi, sinTheta * sinPhi, cosTheta);
                if (!SameHemisphere(wo, wh)) wh = -wh;
                return wh;
            }
        }

        public struct GGX : IMicrofacetDistribution
        {
            public float Alpha { get; }
            public GGX(float roughness) { Alpha = Math.Clamp(roughness * roughness, 0.001f, 1.0f); }
            public float D(Vector3 wh) { return DistributionGGX(wh, Alpha); }
            public float Lambda(Vector3 w) { return LambdaGGX(w, Alpha); }
            public float Pdf(Vector3 wo, Vector3 wh)
            {
                return D(wh) * AbsCosTheta(wh);
            }
            public Vector3 SampleWh(Vector3 wo, Random rand)
            {
                float phi = (2 * PI) * rand.NextFloat();
                float tanTheta2 = Alpha * Alpha * rand.NextFloat() / (1.0f - rand.NextFloat());
                float cosTheta = 1 / Sqrt(1 + tanTheta2);
                float sinTheta = Sqrt(Max(0, 1 - cosTheta * cosTheta));
                (float sinPhi, float cosPhi) = SinCos(phi);
                Vector3 wh = new Vector3(sinTheta * cosPhi, sinTheta * sinPhi, cosTheta);
                if (!SameHemisphere(wo, wh)) wh = -wh;
                return wh;
            }
        }

        public static float DistributionBeckmann(Vector3 wh, float alpha)
        {
            float tan2Theta = Tan2Theta(wh);
            if (float.IsInfinity(tan2Theta)) { return 0.0f; }
            float numerator = Exp((-tan2Theta) / (alpha * alpha));
            float cos2Theta = Cos2Theta(wh);
            float denominator = PI * alpha * alpha * cos2Theta * cos2Theta;
            return numerator / denominator;
        }

        public static float DistributionGGX(Vector3 wh, float alpha)
        {
            float cos2Theta = Cos2Theta(wh);
            float numerator = alpha * alpha;
            float denominator = PI * (cos2Theta * (alpha * alpha - 1) + 1) * (cos2Theta * (alpha * alpha - 1) + 1);
            return numerator / denominator;
            //float tan2Theta = Tan2Theta(wh);
            //if (float.IsInfinity(tan2Theta)) { return 0.0f; }
            //float cos4Theta = Cos2Theta(wh) * Cos2Theta(wh);
            //float e =
            //    (Cos2Phi(wh) / (alpha * alpha) + Sin2Phi(wh) / (alpha * alpha)) *
            //    tan2Theta;
            //return 1 / (PI * alpha * alpha * cos4Theta * (1 + e) * (1 + e));
        }

        public static float LambdaBeckmann(Vector3 w, float alpha)
        {
            float absTanTheta = Abs(TanTheta(w));
            if (float.IsInfinity(absTanTheta)) { return 0.0f; }
            float a = 1 / (alpha * absTanTheta);
            if (a >= 1.6f) { return 0; }
            return (1 - 1.259f * a + 0.396f * a * a) / (3.535f * a + 2.181f * a * a);
        }

        public static float LambdaGGX(Vector3 w, float alpha)
        {
            float absTanTheta = Abs(TanTheta(w));
            if (float.IsInfinity(absTanTheta)) { return 0.0f; }
            float a = 1 / (alpha * absTanTheta);
            return (-1 + Sqrt(1 + (1 / (a * a)))) / 2;
            //float absTanTheta = Abs(TanTheta(w));
            //if (float.IsInfinity(absTanTheta)) { return 0.0f; }
            //float a =
            //    Sqrt(Cos2Phi(w) * alpha * alpha + Sin2Phi(w) * alpha * alpha);
            //float alpha2Tan2Theta = (a * absTanTheta) * (a * absTanTheta);
            //return (-1 + Sqrt(1.0f + alpha2Tan2Theta)) / 2;
        }

        public static float SmithGBeckmann(Vector3 wo, Vector3 wi, float alpha)
        {
            return 1.0f / (1.0f + LambdaBeckmann(wo, alpha) + LambdaBeckmann(wi, alpha));
        }

        public static float SmithGGGX(Vector3 wo, Vector3 wi, float alpha)
        {
            return 1.0f / (1.0f + LambdaGGX(wo, alpha) + LambdaGGX(wi, alpha));
        }
    }
}
