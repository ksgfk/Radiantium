using Radiantium.Core;
using System.Numerics;
using static Radiantium.Offline.Coordinate;
using static System.Numerics.Vector3;

namespace Radiantium.Offline.Bxdf
{
    public struct MicrofacetReflectionBrdf<TFresnel, TMicrofacet> : IBxdf
        where TFresnel : IFresnel
        where TMicrofacet : IMicrofacetDistribution
    {
        public Color3F R;
        public TFresnel Fresnel;
        public TMicrofacet Distribution;

        public BxdfType Type => BxdfType.Reflection | BxdfType.Glossy;

        public MicrofacetReflectionBrdf(Color3F r, TFresnel fresnel, TMicrofacet distribution)
        {
            R = r;
            Fresnel = fresnel;
            Distribution = distribution;
        }

        public Color3F Fr(Vector3 wo, Vector3 wi)
        {
            if (!SameHemisphere(wo, wi)) { return new Color3F(0.0f); }
            float cosThetaO = AbsCosTheta(wo);
            float cosThetaI = AbsCosTheta(wi);
            Vector3 wh = wi + wo;
            if (cosThetaI == 0 || cosThetaO == 0) { return new Color3F(0); }
            if (wh.X == 0 && wh.Y == 0 && wh.Z == 0) { return new Color3F(0); }
            wh = Normalize(wh);
            Color3F f = Fresnel.Eval(Dot(wi, Faceforward(wh, new Vector3(0, 0, 1))));
            float d = Distribution.D(wh);
            float g = Distribution.G(wo, wi);
            Color3F result = R * Color3F.Abs((d * g * f) / (4 * cosThetaI * cosThetaO));
            return result;
        }

        public float Pdf(Vector3 wo, Vector3 wi)
        {
            if (!SameHemisphere(wo, wi)) { return 0; }
            Vector3 wh = Normalize(wo + wi);
            return Distribution.Pdf(wo, wh) / (4 * Dot(wo, wh));
        }

        public SampleBxdfResult Sample(Vector3 wo, Random rand)
        {
            if (CosTheta(wo) == 0) { return new SampleBxdfResult(); }
            Vector3 wh = Distribution.SampleWh(wo, rand);
            if (Dot(wo, wh) < 0) { return new SampleBxdfResult(); }
            Vector3 wi = Reflect(-wo, wh); //reflect
            if (!SameHemisphere(wo, wi)) { return new SampleBxdfResult(); }
            float pdf = Distribution.Pdf(wo, wh) / (4 * Dot(wi, wh));
            Color3F fr = Fr(wo, wi);
            return new SampleBxdfResult(wi, fr, pdf, Type);
        }
    }
}
