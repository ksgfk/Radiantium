using System.Numerics;
using static Radiantium.Core.MathExt;
using static Radiantium.Offline.Coordinate;
using static System.MathF;
using static System.Numerics.Vector3;

namespace Radiantium.Offline.Bxdf
{
    public enum MicrofacetDistributionType
    {
        Beckmann,
        GGX
    }

    public interface IMicrofacetDistribution
    {
        float AlphaX { get; }

        float AlphaY { get; }

        float D(Vector3 wh);

        float Lambda(Vector3 w);

        Vector3 SampleWh(Vector3 wo, Random rand);

        float Pdf(Vector3 wo, Vector3 wh);

        float G(Vector3 wo, Vector3 wi);

        public float G1(Vector3 w)
        {
            return 1 / (1 + Lambda(w));
        }

        public float SmithG2(Vector3 wo, Vector3 wi)
        {
            return 1 / (1 + Lambda(wo) + Lambda(wi));
        }
    }

    public static class Microfacet
    {
        public struct Beckmann : IMicrofacetDistribution
        {
            public float AlphaX { get; }
            public float AlphaY { get; }
            public Beckmann(float roughness, float anisotropic)
            {
                (AlphaX, AlphaY) = RoughnessToAlpha(roughness, anisotropic);
            }
            public float D(Vector3 wh) { return DistributionBeckmann(wh, AlphaX, AlphaY); }
            public float Lambda(Vector3 w) { return LambdaBeckmann(w, AlphaX, AlphaY); }
            public float Pdf(Vector3 wo, Vector3 wh)
            {
                return D(wh) * AbsCosTheta(wh);
            }
            public Vector3 SampleWh(Vector3 wo, Random rand)
            {
                float alphax = AlphaX;
                float alphay = AlphaY;
                float tan2Theta, phi;
                Vector2 u = rand.NextVec2();
                if (alphax == alphay)
                {
                    float logSample = Log(1 - u.X);
                    tan2Theta = -alphax * alphax * logSample;
                    phi = u.Y * 2 * PI;
                }
                else
                {
                    float logSample = Log(1 - u.X);
                    phi = Atan(alphay / alphax * Tan(2 * PI * u.Y + 0.5f * PI));
                    if (u.Y > 0.5f) { phi += PI; }
                    float sinPhi = Sin(phi), cosPhi = Cos(phi);
                    float alphax2 = alphax * alphax, alphay2 = alphay * alphay;
                    tan2Theta = -logSample / (cosPhi * cosPhi / alphax2 + sinPhi * sinPhi / alphay2);
                }
                float cosTheta = 1 / Sqrt(1 + tan2Theta);
                float sinTheta = Sqrt(Max(0, 1 - cosTheta * cosTheta));
                Vector3 wh = SphericalDirection(sinTheta, cosTheta, phi);
                if (!SameHemisphere(wo, wh)) { wh = -wh; }
                return wh;
            }
            public float G(Vector3 wo, Vector3 wi)
            {
                return 1 / (1 + Lambda(wo) + Lambda(wi));
            }
        }

        public struct GGX : IMicrofacetDistribution
        {
            public float AlphaX { get; }
            public float AlphaY { get; }
            public GGX(float roughness, float anisotropic)
            {
                (AlphaX, AlphaY) = RoughnessToAlpha(roughness, anisotropic);
            }
            public float D(Vector3 wh) { return DistributionGGX(wh, AlphaX, AlphaY); }
            public float Lambda(Vector3 w) { return LambdaGGX(w, AlphaX, AlphaY); }
            public float Pdf(Vector3 wo, Vector3 wh)
            {
                return D(wh) * AbsCosTheta(wh);
            }
            public Vector3 SampleWh(Vector3 wo, Random rand)
            {
                return SampleWhGGX(wo, rand, AlphaX, AlphaY);
            }
            public float G(Vector3 wo, Vector3 wi)
            {
                return 1 / (1 + Lambda(wo) + Lambda(wi));
            }
        }

        public static float DistributionBeckmann(Vector3 wh, float alphax, float alphay)
        {
            float tan2Theta = Tan2Theta(wh);
            if (float.IsInfinity(tan2Theta)) { return 0.0f; }
            float cos4Theta = Cos2Theta(wh) * Cos2Theta(wh);
            return Exp(-tan2Theta * (Cos2Phi(wh) / (alphax * alphax) + Sin2Phi(wh) / (alphay * alphay))) / (PI * alphax * alphay * cos4Theta);
        }

        public static float DistributionGGX(Vector3 wh, float alphax, float alphay)
        {
            float tan2Theta = Tan2Theta(wh);
            if (float.IsInfinity(tan2Theta)) { return 0.0f; }
            float cos4Theta = Cos2Theta(wh) * Cos2Theta(wh);
            float e = (Cos2Phi(wh) / (alphax * alphax) + Sin2Phi(wh) / (alphay * alphay)) * tan2Theta;
            return 1 / (PI * alphax * alphay * cos4Theta * (1 + e) * (1 + e));
        }

        public static float LambdaBeckmann(Vector3 w, float alphax, float alphay)
        {
            float absTanTheta = Abs(TanTheta(w));
            if (float.IsInfinity(absTanTheta)) { return 0.0f; }
            float alpha = Sqrt(Cos2Phi(w) * alphax * alphax + Sin2Phi(w) * alphay * alphay);
            float a = 1 / (alpha * absTanTheta);
            if (a >= 1.6f) { return 0; }
            return (1 - 1.259f * a + 0.396f * a * a) / (3.535f * a + 2.181f * a * a);
        }

        public static float LambdaGGX(Vector3 w, float alphax, float alphay)
        {
            float absTanTheta = Abs(TanTheta(w));
            if (float.IsInfinity(absTanTheta)) { return 0.0f; }
            float alpha = Sqrt(Cos2Phi(w) * alphax * alphax + Sin2Phi(w) * alphay * alphay);
            float alpha2Tan2Theta = (alpha * absTanTheta) * (alpha * absTanTheta);
            return (-1 + Sqrt(1.0f + alpha2Tan2Theta)) / 2;
        }

        public static float SmithG2Beckmann(Vector3 wo, Vector3 wi, float alphaX, float alphaY)
        {
            return 1.0f / (1.0f + LambdaBeckmann(wo, alphaX, alphaY) + LambdaBeckmann(wi, alphaX, alphaY));
        }

        public static float SmithG2GGX(Vector3 wo, Vector3 wi, float alphaX, float alphaY)
        {
            return 1.0f / (1.0f + LambdaGGX(wo, alphaX, alphaY) + LambdaGGX(wi, alphaX, alphaY));
        }

        public static float SmithG2BeckmannMaskingShadowing(Vector3 wo, Vector3 wi, float alphaX, float alphaY)
        {
            float o = 1 / (1 + LambdaBeckmann(wo, alphaX, alphaY));
            float i = 1 / (1 + LambdaBeckmann(wi, alphaX, alphaY));
            return o * i;
        }

        public static float SmithG2GGXMaskingShadowing(Vector3 wo, Vector3 wi, float alphaX, float alphaY)
        {
            float o = 1 / (1 + LambdaGGX(wo, alphaX, alphaY));
            float i = 1 / (1 + LambdaGGX(wi, alphaX, alphaY));
            return o * i;
        }

        public static (float, float) RoughnessToAlpha(float roughness, float anisotropic)
        {
            float k = anisotropic;
            float alphaX = Math.Max(roughness * roughness * (1 + k), 0.001f);
            float alphaY = Math.Max(roughness * roughness * (1 - k), 0.001f);
            return (alphaX, alphaY);
        }

        public static Vector3 SampleWhGGX(Vector3 wo, Random rand, float alphax, float alphay)
        {
            Vector2 u = rand.NextVec2();
            float phi = (2 * PI) * u.Y;
            float cosTheta;
            if (alphax == alphay)
            {
                float tanTheta2 = alphax * alphax * u.X / (1.0f - u.X);
                cosTheta = 1 / Sqrt(1 + tanTheta2);
            }
            else
            {
                phi = Atan(alphay / alphax * Tan(2 * PI * u.Y + .5f * PI));
                if (u.Y > 0.5f) { phi += PI; }
                float sinPhi = Sin(phi), cosPhi = Cos(phi);
                float alphax2 = alphax * alphax, alphay2 = alphay * alphay;
                float alpha2 = 1 / (cosPhi * cosPhi / alphax2 + sinPhi * sinPhi / alphay2);
                float tanTheta2 = alpha2 * u.X / (1 - u.X);
                cosTheta = 1 / Sqrt(1 + tanTheta2);
            }
            float sinTheta = Sqrt(Max(0.0f, 1.0f - cosTheta * cosTheta));
            Vector3 wh = SphericalDirection(sinTheta, cosTheta, phi);
            if (!SameHemisphere(wo, wh)) { wh = -wh; }
            return wh;
        }

        public static float PdfGGX(Vector3 wo, Vector3 wh, float alphaX, float alphaY)
        {
            return DistributionGGX(wh, alphaX, alphaY) * AbsCosTheta(wh);
        }
    }
}
