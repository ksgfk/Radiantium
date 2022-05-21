using Radiantium.Core;
using System.Numerics;
using static Radiantium.Core.MathExt;
using static Radiantium.Offline.Coordinate;
using static System.Numerics.Vector3;

namespace Radiantium.Offline.Bxdf
{
    public struct MicrofacetTransmissionBtdf<TMicrofacet> : IBxdf
        where TMicrofacet : IMicrofacetDistribution
    {
        public Color3F T;
        public Fresnel.Dielectric Fresnel;
        public TMicrofacet Distribution;
        public BxdfType Type => BxdfType.Glossy | BxdfType.Transmission;

        public MicrofacetTransmissionBtdf(Color3F t, float etaA, float etaB, TMicrofacet distribution)
        {
            T = t;
            Fresnel = new Fresnel.Dielectric(etaA, etaB);
            Distribution = distribution;
        }

        public Color3F Fr(Vector3 wo, Vector3 wi)
        {
            if (SameHemisphere(wo, wi)) { return new Color3F(); }
            float cosThetaO = CosTheta(wo);
            float cosThetaI = CosTheta(wi);
            if (cosThetaI == 0 || cosThetaO == 0) { return new Color3F(); }
            bool entering = CosTheta(wo) > 0;
            float etaI = entering ? Fresnel.EtaI : Fresnel.EtaT;
            float etaT = entering ? Fresnel.EtaT : Fresnel.EtaI;
            float eta = etaT / etaI;
            Vector3 wh = Normalize(wo + wi * eta);
            if (CosTheta(wh) < 0) { wh = -wh; }
            if (Dot(wo, wh) * Dot(wi, wh) > 0) { return new Color3F(); }
            Color3F f = Fresnel.Eval(Dot(wo, wh));
            float sqrtDenom = Dot(wo, wh) + eta * Dot(wi, wh);
            float factor = 1 / eta;
            float d = Distribution.D(wh);
            float g = Distribution.SmithG2(wo, wi);
            return (1 - f) * T *
                MathF.Abs(d * g * eta * eta * AbsDot(wi, wh) * AbsDot(wo, wh) * factor * factor /
                (cosThetaI * cosThetaO * sqrtDenom * sqrtDenom));
        }

        public float Pdf(Vector3 wo, Vector3 wi)
        {
            if (SameHemisphere(wo, wi)) { return 0.0f; }
            bool entering = CosTheta(wo) > 0;
            float etaI = entering ? Fresnel.EtaI : Fresnel.EtaT;
            float etaT = entering ? Fresnel.EtaT : Fresnel.EtaI;
            float eta = etaT / etaI;
            Vector3 wh = Normalize(wo + wi * eta);
            if (Dot(wo, wh) * Dot(wi, wh) > 0) { return 0; }
            float sqrtDenom = Dot(wo, wh) + eta * Dot(wi, wh);
            float dwhdwi = MathF.Abs((eta * eta * Dot(wi, wh)) / (sqrtDenom * sqrtDenom));
            return Distribution.Pdf(wo, wh) * dwhdwi;
        }

        public SampleBxdfResult Sample(Vector3 wo, Random rand)
        {
            if (wo.Z == 0) { return new SampleBxdfResult(); }
            Vector3 wh = Distribution.SampleWh(wo, rand);
            if (Dot(wo, wh) < 0) { return new SampleBxdfResult(); }
            bool entering = CosTheta(wo) > 0;
            float etaI = entering ? Fresnel.EtaI : Fresnel.EtaT;
            float etaT = entering ? Fresnel.EtaT : Fresnel.EtaI;
            float eta = etaI / etaT;
            if (!Refract(wo, wh, eta, out Vector3 wi)) { return new SampleBxdfResult(); }
            Color3F btdf = Fr(wo, wi);
            float pdf = Pdf(wo, wi);
            if (!btdf.IsValid || float.IsInfinity(pdf)) { return new SampleBxdfResult(); }
            return new SampleBxdfResult(wi, btdf, pdf, Type);
        }
    }
}
