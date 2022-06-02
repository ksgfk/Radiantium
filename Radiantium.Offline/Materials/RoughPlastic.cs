using Radiantium.Core;
using Radiantium.Offline.Bxdf;
using System.Numerics;
using static Radiantium.Offline.Coordinate;

namespace Radiantium.Offline.Materials
{
    //for energy conservation. the sum of Kd and Ks should not be greater than 1
    //how did mitsuba2 do it :(
    public class RoughPlastic : Material
    {
        public Texture2D R { get; }
        public MicrofacetDistributionType Dist { get; }
        public Texture2D Roughness { get; }
        public Texture2D Anisotropic { get; }
        public float Kd { get; }
        public float Ks { get; }
        public float EtaI { get; }
        public float EtaT { get; }
        public bool IsTwoSide { get; }
        public override BxdfType Type => BxdfType.Diffuse | BxdfType.Reflection | BxdfType.Glossy;

        public RoughPlastic(Texture2D r, MicrofacetDistributionType distType, Texture2D roughness, Texture2D anisotropic, float kd, float ks, bool isTwoSide, float etaI = 1, float etaT = 1.5f)
        {
            R = r ?? throw new ArgumentNullException(nameof(r));
            Dist = distType;
            Roughness = roughness ?? throw new ArgumentNullException(nameof(roughness));
            Anisotropic = anisotropic ?? throw new AbandonedMutexException(nameof(anisotropic));
            Kd = kd;
            Ks = ks;
            EtaI = etaI;
            EtaT = etaT;
            IsTwoSide = isTwoSide;
        }

        private (Color3F, float, float) SampleParams(Vector2 uv)
        {
            return (R.Sample(uv), Kd, Ks);
        }

        private (float, float) SampleRoughness(Vector2 uv)
        {
            return (Roughness.Sample(uv).R, Anisotropic.Sample(uv).R);
        }

        private static float GlossyProbability(float kd, float ks)
        {
            float cd = kd;
            float cs = ks;
            return cs / (cd + cs);
        }

        private Color3F FrImpl<T>(Vector3 wo, Vector3 wi, T dist,
            (Color3F, float, float) param) where T : IMicrofacetDistribution
        {
            if (CosTheta(wo) <= 0 || CosTheta(wi) <= 0)
            {
                if (!IsTwoSide)
                {
                    return new Color3F(0.0f);
                }
            }
            var (r, kd, ks) = param;
            Color3F diffuse = new LambertianReflectionBrdf(r).Fr(wo, wi);
            Color3F glossy = new MicrofacetReflectionBrdf<Fresnel.Dielectric, T>(
                r,
                new Fresnel.Dielectric(EtaI, EtaT),
                dist
            ).Fr(wo, wi);
            Color3F brdf = kd * diffuse + ks * glossy;
            return brdf;
        }

        public override Color3F Fr(Vector3 wo, Vector3 wi, Intersection inct)
        {
            var (roughness, anis) = SampleRoughness(inct.UV);
            return Dist switch
            {
                MicrofacetDistributionType.Beckmann => FrImpl(wo, wi, new Microfacet.Beckmann(roughness, anis), SampleParams(inct.UV)),
                MicrofacetDistributionType.GGX => FrImpl(wo, wi, new Microfacet.GGX(roughness, anis), SampleParams(inct.UV)),
                _ => new Color3F(0.0f),
            };
        }

        private float PdfImpl<T>(Vector3 wo, Vector3 wi, T dist,
            (Color3F, float, float) param) where T : IMicrofacetDistribution
        {
            if (CosTheta(wo) <= 0 || CosTheta(wi) <= 0)
            {
                if (!IsTwoSide)
                {
                    return 0.0f;
                }
            }
            var (r, kd, ks) = param;
            float p = GlossyProbability(kd, ks);
            float diffuse = new LambertianReflectionBrdf(r).Pdf(wo, wi);
            float glossy = new MicrofacetReflectionBrdf<Fresnel.Dielectric, T>(
                r,
                new Fresnel.Dielectric(EtaI, EtaT),
                dist
            ).Pdf(wo, wi);
            float pdf = (1 - p) * diffuse + p * glossy;
            return pdf;
        }

        public override float Pdf(Vector3 wo, Vector3 wi, Intersection inct)
        {
            var (roughness, anis) = SampleRoughness(inct.UV);
            return Dist switch
            {
                MicrofacetDistributionType.Beckmann => PdfImpl(wo, wi, new Microfacet.Beckmann(roughness, anis), SampleParams(inct.UV)),
                MicrofacetDistributionType.GGX => PdfImpl(wo, wi, new Microfacet.GGX(roughness, anis), SampleParams(inct.UV)),
                _ => 0.0f,
            };
        }

        private SampleBxdfResult SampleImpl<T>(Vector3 wo, Vector2 uv, Random rand, T dist)
            where T : IMicrofacetDistribution
        {
            if (CosTheta(wo) <= 0)
            {
                if (!IsTwoSide)
                {
                    return new SampleBxdfResult();
                }
            }
            var param = SampleParams(uv);
            var (r, kd, ks) = param;
            float p = GlossyProbability(kd, ks);
            SampleBxdfResult glossy = new MicrofacetReflectionBrdf<Fresnel.Dielectric, T>(
                r,
                new Fresnel.Dielectric(EtaI, EtaT),
                dist
            ).Sample(wo, rand);
            SampleBxdfResult diffuse = new LambertianReflectionBrdf(r).Sample(wo, rand);
            Vector3 wi;
            BxdfType type;
            if (rand.NextFloat() < p)
            {
                wi = glossy.Wi;
                type = glossy.Type;
            }
            else
            {
                wi = diffuse.Wi;
                type = diffuse.Type;
            }
            float pdf = PdfImpl(wo, wi, dist, param);
            Color3F fr = FrImpl(wo, wi, dist, param);
            return new SampleBxdfResult(wi, fr, pdf, type);
        }

        public override SampleBxdfResult Sample(Vector3 wo, Intersection inct, Random rand)
        {
            var (roughness, anis) = SampleRoughness(inct.UV);
            return Dist switch
            {
                MicrofacetDistributionType.Beckmann => SampleImpl(wo, inct.UV, rand, new Microfacet.Beckmann(roughness, anis)),
                MicrofacetDistributionType.GGX => SampleImpl(wo, inct.UV, rand, new Microfacet.GGX(roughness, anis)),
                _ => new SampleBxdfResult(),
            };
        }
    }
}
