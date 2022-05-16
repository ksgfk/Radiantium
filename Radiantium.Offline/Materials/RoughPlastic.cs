using Radiantium.Core;
using Radiantium.Offline.Bxdf;
using System.Numerics;

namespace Radiantium.Offline.Materials
{
    // TODO:
    // BUG: GGX object edge are too bright. maybe pdf calculate error?
    public class RoughPlastic : Material
    {
        public Texture2D R { get; }
        public MicrofacetDistributionType DistType { get; }
        public Texture2D Roughness { get; }
        public Texture2D Kd { get; }
        public Texture2D Ks { get; }
        public float EtaI { get; }
        public float EtaT { get; }

        public override BxdfType Type => BxdfType.Diffuse | BxdfType.Reflection | BxdfType.Glossy;

        public RoughPlastic(Texture2D r, MicrofacetDistributionType distType, Texture2D roughness, Texture2D kd, Texture2D ks, float etaI = 1, float etaT = 1.5f)
        {
            R = r ?? throw new ArgumentNullException(nameof(r));
            DistType = distType;
            Roughness = roughness ?? throw new ArgumentNullException(nameof(roughness));
            Kd = kd ?? throw new ArgumentNullException(nameof(kd));
            Ks = ks ?? throw new ArgumentNullException(nameof(ks));
            EtaI = etaI;
            EtaT = etaT;
        }

        private (Color3F, float, Color3F, Color3F) SampleParams(Vector2 uv)
        {
            Color3F kd = Kd.Sample(uv);
            Color3F ks = Ks.Sample(uv);
            return (
                R.Sample(uv),
                Roughness.Sample(uv).R,
                kd,
                ks
            );
        }

        private static float GlossyProb(Color3F kd, Color3F ks)
        {
            float cd = MathExt.MaxElement(kd);
            float cs = MathExt.MaxElement(ks);
            return cs / (cd + cs);
        }

        private Color3F CombineFr(Vector3 wo, Vector3 wi, Vector2 uv)
        {
            if (!Coordinate.SameHemisphere(wo, wi)) { return new Color3F(0.0f); }
            var (r, roughness, kd, ks) = SampleParams(uv);
            Color3F glossy = DistType switch
            {
                MicrofacetDistributionType.Beckmann =>
                    new MicrofacetReflectionBrdf<Fresnel.Dielectric, Microfacet.Beckmann>(
                        r,
                        new Fresnel.Dielectric(EtaI, EtaT),
                        new Microfacet.Beckmann(roughness)
                    ).Fr(wo, wi),
                MicrofacetDistributionType.GGX =>
                    new MicrofacetReflectionBrdf<Fresnel.Dielectric, Microfacet.GGX>(
                        r,
                        new Fresnel.Dielectric(EtaI, EtaT),
                        new Microfacet.GGX(roughness)
                    ).Fr(wo, wi),
                _ => new Color3F(0.0f),
            };
            Color3F diffuse = new LambertianReflectionBrdf(r).Fr(wo, wi);
            return kd * diffuse + ks * glossy;
        }

        public override Color3F Fr(Vector3 wo, Vector3 wi, Intersection inct)
        {
            return CombineFr(wo, wi, inct.UV);
        }

        private float CombinePdf(Vector3 wo, Vector3 wi, Vector2 uv)
        {
            if (!Coordinate.SameHemisphere(wo, wi)) { return 0.0f; }
            var (r, roughness, kd, ks) = SampleParams(uv);
            float p = GlossyProb(kd, ks);
            float dPdf = new LambertianReflectionBrdf(r).Pdf(wo, wi);
            float sPdf = DistType switch
            {
                MicrofacetDistributionType.Beckmann =>
                    new MicrofacetReflectionBrdf<Fresnel.Dielectric, Microfacet.Beckmann>(
                        r,
                        new Fresnel.Dielectric(EtaI, EtaT),
                        new Microfacet.Beckmann(roughness)
                    ).Pdf(wo, wi),
                MicrofacetDistributionType.GGX =>
                    new MicrofacetReflectionBrdf<Fresnel.Dielectric, Microfacet.GGX>(
                        r,
                        new Fresnel.Dielectric(EtaI, EtaT),
                        new Microfacet.GGX(roughness)
                    ).Pdf(wo, wi),
                _ => 0.0f,
            };
            float pdf = (1 - p) * dPdf + p * sPdf;
            if (pdf <= 0.0f) { return 0.0f; }
            return pdf;
        }

        public override float Pdf(Vector3 wo, Vector3 wi, Intersection inct)
        {
            return CombinePdf(wo, wi, inct.UV);
        }

        private SampleBxdfResult CombineSample(Vector3 wo, Vector2 uv, Random rand)
        {
            var (r, roughness, kd, ks) = SampleParams(uv);
            SampleBxdfResult d = new LambertianReflectionBrdf(r).Sample(wo, rand);
            SampleBxdfResult s = DistType switch
            {
                MicrofacetDistributionType.Beckmann =>
                    new MicrofacetReflectionBrdf<Fresnel.Dielectric, Microfacet.Beckmann>(
                        r,
                        new Fresnel.Dielectric(EtaI, EtaT),
                        new Microfacet.Beckmann(roughness)
                    ).Sample(wo, rand),
                MicrofacetDistributionType.GGX =>
                    new MicrofacetReflectionBrdf<Fresnel.Dielectric, Microfacet.GGX>(
                        r,
                        new Fresnel.Dielectric(EtaI, EtaT),
                        new Microfacet.GGX(roughness)
                    ).Sample(wo, rand),
                _ => new SampleBxdfResult(),
            };
            float p = GlossyProb(kd, ks);
            Vector3 wi;
            if (rand.NextFloat() > p)
            {
                wi = d.Wi;
            }
            else
            {
                wi = s.Wi;
            }
            if (!Coordinate.SameHemisphere(wo, wi)) { return new SampleBxdfResult(); }
            Color3F fr = kd * d.Fr + ks * s.Fr;
            float pdf = (1 - p) * d.Pdf + p * s.Pdf;
            if (pdf <= 0.0f) { return new SampleBxdfResult(); }
            return new SampleBxdfResult(wi, fr, pdf, Type);
        }

        public override SampleBxdfResult Sample(Vector3 wo, Intersection inct, Random rand)
        {
            return CombineSample(wo, inct.UV, rand);
        }
    }
}
