using Radiantium.Core;
using Radiantium.Offline.Bxdf;
using System.Numerics;
using static Radiantium.Core.MathExt;
using static Radiantium.Offline.Coordinate;
using static System.Numerics.Vector3;

namespace Radiantium.Offline.Materials
{
    //TODO:
    //BUG: There are some differences with the results of mitsuba2
    public class RoughGlass : Material
    {
        public Texture2D R { get; }
        public Texture2D T { get; }
        public Texture2D Roughness { get; }
        public float EtaA { get; }
        public float EtaB { get; }
        public MicrofacetDistributionType Dist { get; }
        public override BxdfType Type => BxdfType.Glossy | BxdfType.Transmission | BxdfType.Reflection;

        public RoughGlass(Texture2D r, Texture2D t, Texture2D roughness, float etaA, float etaB, MicrofacetDistributionType dist)
        {
            R = r ?? throw new ArgumentNullException(nameof(r));
            T = t ?? throw new ArgumentNullException(nameof(t));
            Roughness = roughness ?? throw new ArgumentNullException(nameof(roughness));
            EtaA = etaA;
            EtaB = etaB;
            Dist = dist;
        }

        private (Color3F, Color3F, float) SampleParams(Vector2 uv)
        {
            return (
                R.Sample(uv),
                T.Sample(uv),
                Roughness.Sample(uv).R
            );
        }

        public override Color3F Fr(Vector3 wo, Vector3 wi, Intersection inct)
        {
            var (r, t, roughness) = SampleParams(inct.UV);
            if (SameHemisphere(wo, wi))
            {
                return Dist switch
                {
                    MicrofacetDistributionType.Beckmann => new MicrofacetReflectionBrdf<Fresnel.Dielectric, Microfacet.Beckmann>(
                        r,
                        new Fresnel.Dielectric(EtaA, EtaB),
                        new Microfacet.Beckmann(roughness)
                    ).Fr(wo, wi),
                    MicrofacetDistributionType.GGX => new MicrofacetReflectionBrdf<Fresnel.Dielectric, Microfacet.GGX>(
                        r,
                        new Fresnel.Dielectric(EtaA, EtaB),
                        new Microfacet.GGX(roughness)
                    ).Fr(wo, wi),
                    _ => new Color3F(0.0f),
                };
            }
            else
            {
                return Dist switch
                {
                    MicrofacetDistributionType.Beckmann => new MicrofacetTransmissionBtdf<Microfacet.Beckmann>(
                        t,
                        EtaA, EtaB,
                        new Microfacet.Beckmann(roughness)
                    ).Fr(wo, wi),
                    MicrofacetDistributionType.GGX => new MicrofacetTransmissionBtdf<Microfacet.GGX>(
                        t,
                        EtaA, EtaB,
                        new Microfacet.GGX(roughness)
                    ).Fr(wo, wi),
                    _ => new Color3F(0.0f),
                };
            }
        }

        public override float Pdf(Vector3 wo, Vector3 wi, Intersection inct)
        {
            var (r, t, roughness) = SampleParams(inct.UV);
            if (SameHemisphere(wo, wi))
            {
                Vector3 wh = Normalize(wo + wi);
                Fresnel.Dielectric fresnel = new Fresnel.Dielectric(EtaA, EtaB);
                float f = fresnel.Eval(Dot(wo, wh)).R;
                float pdf = Dist switch
                {
                    MicrofacetDistributionType.Beckmann => new MicrofacetReflectionBrdf<Fresnel.Dielectric, Microfacet.Beckmann>(
                        r,
                        new Fresnel.Dielectric(EtaA, EtaB),
                        new Microfacet.Beckmann(roughness)
                    ).Pdf(wo, wi),
                    MicrofacetDistributionType.GGX => new MicrofacetReflectionBrdf<Fresnel.Dielectric, Microfacet.GGX>(
                        r,
                        new Fresnel.Dielectric(EtaA, EtaB),
                        new Microfacet.GGX(roughness)
                    ).Pdf(wo, wi),
                    _ => 0.0f,
                };
                return pdf * f;
            }
            else
            {
                bool entering = CosTheta(wo) > 0;
                float etaI = entering ? EtaA : EtaB;
                float etaT = entering ? EtaB : EtaA;
                float eta = etaT / etaI;
                Vector3 wh = Normalize(wo + wi * eta);
                if (!SameHemisphere(wo, wh)) { wh = -wh; }
                Fresnel.Dielectric fresnel = new Fresnel.Dielectric(EtaA, EtaB);
                float f = fresnel.Eval(Dot(wo, wh)).R;
                float pdf = Dist switch
                {
                    MicrofacetDistributionType.Beckmann => new MicrofacetTransmissionBtdf<Microfacet.Beckmann>(
                        t,
                        EtaA, EtaB,
                        new Microfacet.Beckmann(roughness)
                    ).Pdf(wo, wi),
                    MicrofacetDistributionType.GGX => new MicrofacetTransmissionBtdf<Microfacet.GGX>(
                        t,
                        EtaA, EtaB,
                        new Microfacet.GGX(roughness)
                    ).Pdf(wo, wi),
                    _ => 0.0f,
                };
                return pdf * (1 - f);
            }
        }

        private SampleBxdfResult SampleImpl<T>(Vector3 wo, Vector2 uv, Random rand, T dist)
            where T : IMicrofacetDistribution
        {
            Color3F r = R.Sample(uv);
            Color3F t = this.T.Sample(uv);
            Vector3 wh = dist.SampleWh(wo, rand);
            Fresnel.Dielectric fresnel = new Fresnel.Dielectric(EtaA, EtaB);
            float f = fresnel.Eval(Dot(wo, wh)).R;
            if (rand.NextFloat() < f)
            {
                MicrofacetReflectionBrdf<Fresnel.Dielectric, T> brdf = new(r, fresnel, dist);
                Vector3 wi = Reflect(-wo, wh);
                if (!SameHemisphere(wo, wi)) { return new SampleBxdfResult(); }
                float pdf = brdf.Pdf(wo, wi) * f;
                Color3F fr = brdf.Fr(wo, wi);
                return new SampleBxdfResult(wi, fr, pdf, brdf.Type);
            }
            else
            {
                MicrofacetTransmissionBtdf<T> btdf = new(t, EtaA, EtaB, dist);
                if (Dot(wo, wh) < 0) { return new SampleBxdfResult(); }
                bool entering = CosTheta(wo) > 0;
                float etaI = entering ? fresnel.EtaI : fresnel.EtaT;
                float etaT = entering ? fresnel.EtaT : fresnel.EtaI;
                float eta = etaI / etaT;
                if (!Refract(wo, wh, eta, out Vector3 wi))
                {
                    return new SampleBxdfResult();
                }
                Color3F ft = btdf.Fr(wo, wi);
                float pdf = btdf.Pdf(wo, wi) * (1 - f);
                return new SampleBxdfResult(wi, ft, pdf, btdf.Type);
            }
        }

        public override SampleBxdfResult Sample(Vector3 wo, Intersection inct, Random rand)
        {
            float roughness = Roughness.Sample(inct.UV).R;
            return Dist switch
            {
                MicrofacetDistributionType.Beckmann => SampleImpl(wo, inct.UV, rand, new Microfacet.Beckmann(roughness)),
                MicrofacetDistributionType.GGX => SampleImpl(wo, inct.UV, rand, new Microfacet.GGX(roughness)),
                _ => new SampleBxdfResult(),
            };
        }
    }
}
