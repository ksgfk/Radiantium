using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline.Bxdf
{
    public struct LambertianReflectionBrdf : IBxdf //或许可以弄个flag控制采样是均匀的还是带cos权重的?
    {
        public Color3F R;
        public BxdfType Type => BxdfType.Reflection | BxdfType.Diffuse;

        public LambertianReflectionBrdf(Color3F r)
        {
            R = r;
        }

        public Color3F Fr(Vector3 wo, Vector3 wi, TransportMode mode)
        {
            return Coordinate.SameHemisphere(wo, wi) ? R / MathF.PI : Color3F.Black;
        }

        public float Pdf(Vector3 wo, Vector3 wi, TransportMode mode)
        {
            return Coordinate.SameHemisphere(wo, wi) ? Coordinate.AbsCosTheta(wi) / MathF.PI : 0.0F;
        }

        public SampleBxdfResult Sample(Vector3 wo, Random rand, TransportMode mode)
        {
            Vector3 wi = Vector3.Normalize(Probability.SquareToCosineHemisphere(rand.NextVec2()));
            if (wo.Z < 0)
            {
                wi.Z *= -1;
            }
            if (!Coordinate.SameHemisphere(wo, wi)) { return new SampleBxdfResult(); }
            float pdf = Pdf(wo, wi, mode);
            Color3F fr = R / MathF.PI;
            return new SampleBxdfResult(wi, fr, pdf, Type);
        }
    }
}
