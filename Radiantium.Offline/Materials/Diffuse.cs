using Radiantium.Core;
using Radiantium.Offline.Bxdf;
using System.Numerics;

namespace Radiantium.Offline.Materials
{
    public class Diffuse : Material
    {
        public Texture2D Kd { get; }
        public bool IsTwoSide { get; }
        public override BxdfType Type => BxdfType.Reflection | BxdfType.Diffuse;

        public Diffuse(Texture2D kd, bool isTwoSide)
        {
            Kd = kd;
            IsTwoSide = isTwoSide;
        }

        public override Color3F Fr(Vector3 wo, Vector3 wi, Intersection inct)
        {
            if (Coordinate.CosTheta(wo) <= 0 || Coordinate.CosTheta(wi) <= 0)
            {
                if (!IsTwoSide)
                {
                    return new Color3F(0.0f);
                }
            }
            return new LambertianReflectionBrdf(Kd.Sample(inct.UV)).Fr(wo, wi);
        }

        public override float Pdf(Vector3 wo, Vector3 wi, Intersection inct)
        {
            if (Coordinate.CosTheta(wo) <= 0 || Coordinate.CosTheta(wi) <= 0)
            {
                if (!IsTwoSide)
                {
                    return 0.0f;
                }
            }
            return new LambertianReflectionBrdf(Kd.Sample(inct.UV)).Pdf(wo, wi);
        }

        public override SampleBxdfResult Sample(Vector3 wo, Intersection inct, Random rand)
        {
            if (Coordinate.CosTheta(wo) <= 0)
            {
                if (!IsTwoSide)
                {
                    return new SampleBxdfResult();
                }
            }
            return new LambertianReflectionBrdf(Kd.Sample(inct.UV)).Sample(wo, rand);
        }
    }
}
