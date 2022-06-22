using Radiantium.Core;
using System.Numerics;
using static Radiantium.Offline.Coordinate;
using static System.MathF;

namespace Radiantium.Offline.Bxdf
{
    public struct LambertianTransmissionBtdf : IBxdf
    {
        public Color3F T;
        public BxdfType Type => BxdfType.Transmission | BxdfType.Diffuse;

        public LambertianTransmissionBtdf(Color3F t)
        {
            T = t;
        }

        public Color3F Fr(Vector3 wo, Vector3 wi, TransportMode mode)
        {
            return T * (1 / PI);
        }

        public float Pdf(Vector3 wo, Vector3 wi, TransportMode mode)
        {
            return !SameHemisphere(wo, wi) ? AbsCosTheta(wi) * (1 / PI) : 0;
        }

        public SampleBxdfResult Sample(Vector3 wo, Random rand, TransportMode mode)
        {
            Vector3 wi = Probability.SquareToCosineHemisphere(rand.NextVec2());
            if (wo.Z > 0) { wi.Z *= -1; }
            float pdf = Pdf(wo, wi, mode);
            Color3F fr = Fr(wo, wi, mode);
            return new SampleBxdfResult(wi, fr, pdf, Type);
        }
    }
}
