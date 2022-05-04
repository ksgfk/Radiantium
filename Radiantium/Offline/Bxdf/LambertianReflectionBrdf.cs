using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline.Bxdf
{
    public struct LambertianReflectionBrdf : IBxdf
    {
        public Color3F R;
        public BxdfType Type => BxdfType.Reflection | BxdfType.Diffuse;

        public LambertianReflectionBrdf(Color3F r)
        {
            R = r;
        }

        public Color3F Fr(Vector3 wo, Vector3 wi)
        {
            return Coordinate.SameHemisphere(wo, wi) ? R / MathF.PI : Color3F.Black;
        }

        public float Pdf(Vector3 wo, Vector3 wi)
        {
            return Coordinate.SameHemisphere(wo, wi) ? Coordinate.AbsCosTheta(wi) / MathF.PI : 0.0F;
        }

        public SampleBxdfResult Sample(Vector3 wo, Random rand)
        {
            Vector3 wi = Vector3.Normalize(Probability.SquareToCosineHemisphere(rand.NextVec2()));
            if (wo.Z < 0)
            {
                wi.Z *= -1;
            }
            float pdf = Pdf(wo, wi);
            Color3F fr = R / MathF.PI;
            return new SampleBxdfResult(wi, fr, pdf, Type);
        }
    }
}
