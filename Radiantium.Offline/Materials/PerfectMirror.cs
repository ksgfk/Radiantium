using Radiantium.Core;
using Radiantium.Offline.Bxdf;
using System.Numerics;

namespace Radiantium.Offline.Materials
{
    public class PerfectMirror : Material
    {
        public Texture2D R { get; }
        public bool IsTwoSide { get; }
        public override BxdfType Type => BxdfType.Reflection | BxdfType.Specular;

        public PerfectMirror(Texture2D r, bool isTwoSide)
        {
            R = r;
            IsTwoSide = isTwoSide;
        }

        public override Color3F Fr(Vector3 wo, Vector3 wi, Intersection inct)
        {
            return new SpecularReflectionBrdf(R.Sample(inct.UV)).Fr(wo, wi);
        }

        public override float Pdf(Vector3 wo, Vector3 wi, Intersection inct)
        {
            return new SpecularReflectionBrdf(R.Sample(inct.UV)).Pdf(wo, wi);
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
            return new SpecularReflectionBrdf(R.Sample(inct.UV)).Sample(wo, rand);
        }
    }
}
