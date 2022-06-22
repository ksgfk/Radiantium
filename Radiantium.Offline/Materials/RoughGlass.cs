using Radiantium.Core;
using Radiantium.Offline.Bxdf;
using System.Numerics;
using static Radiantium.Core.MathExt;
using static Radiantium.Offline.Coordinate;
using static System.Numerics.Vector3;

namespace Radiantium.Offline.Materials
{
    public class RoughGlass : Material
    {
        public Texture2D R { get; }
        public Texture2D T { get; }
        public Texture2D Roughness { get; }
        public Texture2D Anisotropic { get; }
        public float EtaA { get; }
        public float EtaB { get; }
        public MicrofacetDistributionType Dist { get; }
        public override BxdfType Type => BxdfType.Glossy | BxdfType.Transmission | BxdfType.Reflection;

        public RoughGlass(Texture2D r, Texture2D t, Texture2D roughness, Texture2D anisotropic, float etaA, float etaB, MicrofacetDistributionType dist)
        {
            R = r ?? throw new ArgumentNullException(nameof(r));
            T = t ?? throw new ArgumentNullException(nameof(t));
            Roughness = roughness ?? throw new ArgumentNullException(nameof(roughness));
            Anisotropic = anisotropic ?? throw new AbandonedMutexException(nameof(anisotropic));
            EtaA = etaA;
            EtaB = etaB;
            Dist = dist;
        }

        private (float, float) GetParam(Vector2 uv)
        {
            return (Roughness.Sample(uv).R, Anisotropic.Sample(uv).R);
        }

        private Color3F FrImpl<T>(Vector3 wo, Vector3 wi, Vector2 uv, T dist, TransportMode mode)
            where T : IMicrofacetDistribution
        {
            Color3F r = R.Sample(uv);
            Color3F t = this.T.Sample(uv);

            bool isReflect = SameHemisphere(wo, wi);
            bool entering = CosTheta(wo) > 0;
            float etaI = entering ? EtaA : EtaB;
            float etaT = entering ? EtaB : EtaA;
            float eta = isReflect ? 1 : etaT / etaI;
            Vector3 wh = Normalize(wo + wi * eta);
            if (CosTheta(wh) < 0) { wh = -wh; }
            float sqrtDenom = Dot(wo, wh) + eta * Dot(wi, wh);
            float d = dist.D(wh);
            float g = dist.G(wo, wi);
            float f = Fresnel.DielectricFunc(Dot(wo, wh), etaI, etaT);
            Color3F bsdf;
            if (isReflect)
            {
                Color3F brdf = r * (d * g * f / (4.0f * CosTheta(wi) * CosTheta(wo)));
                bsdf = brdf;
            }
            else
            {
                if (Dot(wo, wh) * Dot(wi, wh) > 0) { return new Color3F(); }
                float factor = mode == TransportMode.Radiance ? (1 / eta) : 1;
                Color3F btdf = (1 - f) * t *
                    MathF.Abs(d * g * eta * eta * AbsDot(wi, wh) * AbsDot(wo, wh) * factor * factor /
                    (CosTheta(wo) * CosTheta(wi) * sqrtDenom * sqrtDenom));
                bsdf = btdf;
            }
            return bsdf.IsValid ? bsdf : new Color3F(0.0f);
        }

        public override Color3F Fr(Vector3 wo, Vector3 wi, Intersection inct, TransportMode mode)
        {
            var (roughness, anis) = GetParam(inct.UV);
            return Dist switch
            {
                MicrofacetDistributionType.Beckmann => FrImpl(wo, wi, inct.UV, new Microfacet.Beckmann(roughness, anis), mode),
                MicrofacetDistributionType.GGX => FrImpl(wo, wi, inct.UV, new Microfacet.GGX(roughness, anis), mode),
                _ => new Color3F(0.0f),
            };
        }

        private float PdfImpl<T>(Vector3 wo, Vector3 wi, T dist, TransportMode mode)
            where T : IMicrofacetDistribution
        {
            bool isReflect = SameHemisphere(wo, wi);
            bool entering = CosTheta(wo) > 0;
            float etaI = entering ? EtaA : EtaB;
            float etaT = entering ? EtaB : EtaA;
            float pdf;
            if (isReflect)
            {
                Vector3 wh = Normalize(wo + wi);
                if (CosTheta(wh) < 0) { wh = -wh; }
                if (Dot(wo, wh) * CosTheta(wo) <= 0 || Dot(wi, wh) * CosTheta(wi) <= 0) { return 0.0f; }
                float jacobian = 1.0f / (4.0f * AbsDot(wh, wi));
                float f = Fresnel.DielectricFunc(Dot(wo, wh), etaI, etaT);
                pdf = dist.Pdf(wo, wh) * jacobian * f;
            }
            else
            {
                float eta = etaT / etaI;
                Vector3 wh = Normalize(wo + wi * eta);
                if (CosTheta(wh) < 0) { wh = -wh; }
                if (Dot(wo, wh) * Dot(wi, wh) > 0) { return 0.0f; }
                if (Dot(wo, wh) * CosTheta(wo) <= 0 || Dot(wi, wh) * CosTheta(wi) <= 0) { return 0.0f; }
                float sqrtDenom = Dot(wo, wh) + eta * Dot(wi, wh);
                float dwhdwi = MathF.Abs((eta * eta * Dot(wi, wh)) / (sqrtDenom * sqrtDenom));
                float f = Fresnel.DielectricFunc(Dot(wo, wh), etaI, etaT);
                pdf = dist.Pdf(wo, wh) * dwhdwi * (1 - f);
            }
            return float.IsInfinity(pdf) || float.IsNaN(pdf) ? 0.0f : pdf;
        }

        public override float Pdf(Vector3 wo, Vector3 wi, Intersection inct, TransportMode mode)
        {
            var (roughness, anis) = GetParam(inct.UV);
            return Dist switch
            {
                MicrofacetDistributionType.Beckmann => PdfImpl(wo, wi, new Microfacet.Beckmann(roughness, anis), mode),
                MicrofacetDistributionType.GGX => PdfImpl(wo, wi, new Microfacet.GGX(roughness, anis), mode),
                _ => 0.0f,
            };
        }

        private SampleBxdfResult SampleImpl<T>(Vector3 wo, Vector2 uv, Random rand, T dist, TransportMode mode)
            where T : IMicrofacetDistribution
        {
            Color3F r = R.Sample(uv);
            Color3F t = this.T.Sample(uv);
            Vector3 wh = dist.SampleWh(wo, rand);
            float f = Fresnel.DielectricFunc(Dot(wo, wh), EtaA, EtaB);
            if (rand.NextFloat() < f)
            {
                Vector3 wi = Reflect(-wo, wh);
                if (!SameHemisphere(wo, wi)) { return new SampleBxdfResult(); }
                float d = dist.D(wh);
                float g = dist.G(wo, wi);
                Color3F brdf = r * (d * g * f / (4.0f * CosTheta(wi) * CosTheta(wo)));
                float jacobian = 1.0f / (4.0f * AbsDot(wh, wi));
                float pdf = dist.Pdf(wo, wh) * jacobian * f;
                if (float.IsInfinity(pdf) || float.IsNaN(pdf) || !brdf.IsValid) { return new SampleBxdfResult(); }
                return new SampleBxdfResult(wi, brdf, pdf, BxdfType.Reflection | BxdfType.Glossy);
            }
            else
            {
                bool entering = CosTheta(wo) > 0;
                float etaI = entering ? EtaA : EtaB;
                float etaT = entering ? EtaB : EtaA;
                float eta = etaT / etaI;
                if (Refract(wo, wh, etaI / etaT, out Vector3 wi))
                {
                    if (Dot(wo, wh) * Dot(wi, wh) > 0) { return new SampleBxdfResult(); }
                }
                else
                {
                    wi = Reflect(-wo, wh);
                    eta = 1;
                }
                float sqrtDenom = Dot(wo, wh) + eta * Dot(wi, wh);
                float factor = mode == TransportMode.Radiance ? (1 / eta) : 1;
                float d = dist.D(wh);
                float g = dist.G(wo, wi);
                Color3F btdf = (1 - f) * t *
                    MathF.Abs(d * g * eta * eta * AbsDot(wi, wh) * AbsDot(wo, wh) * factor * factor /
                    (CosTheta(wo) * CosTheta(wi) * sqrtDenom * sqrtDenom));
                float dwhdwi = MathF.Abs((eta * eta * Dot(wi, wh)) / (sqrtDenom * sqrtDenom));
                float pdf = dist.Pdf(wo, wh) * dwhdwi * (1 - f);
                if (float.IsInfinity(pdf) || float.IsNaN(pdf) || !btdf.IsValid) { return new SampleBxdfResult(); }
                return new SampleBxdfResult(wi, btdf, pdf, BxdfType.Transmission | BxdfType.Glossy);
            }
        }

        public override SampleBxdfResult Sample(Vector3 wo, Intersection inct, Random rand, TransportMode mode)
        {
            var (roughness, anis) = GetParam(inct.UV);
            return Dist switch
            {
                MicrofacetDistributionType.Beckmann => SampleImpl(wo, inct.UV, rand, new Microfacet.Beckmann(roughness, anis), mode),
                MicrofacetDistributionType.GGX => SampleImpl(wo, inct.UV, rand, new Microfacet.GGX(roughness, anis), mode),
                _ => new SampleBxdfResult(),
            };
        }
    }
}
