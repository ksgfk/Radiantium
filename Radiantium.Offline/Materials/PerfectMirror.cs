using Radiantium.Core;
using Radiantium.Offline.Bxdf;
using System.Numerics;

namespace Radiantium.Offline.Materials
{
    public class PerfectMirror : Material
    {
        public Texture2D R { get; }
        public override BxdfType Type => BxdfType.Reflection | BxdfType.Specular;

        public PerfectMirror(Texture2D r)
        {
            R = r;
        }

        public override Color3F Fr(Vector3 wo, Vector3 wi, Intersection inct)
        {
            return new IdealReflectionBrdf(R.Sample(inct.UV)).Fr(wo, wi);
        }

        public override float Pdf(Vector3 wo, Vector3 wi, Intersection inct)
        {
            return new IdealReflectionBrdf(R.Sample(inct.UV)).Pdf(wo, wi);
        }

        public override SampleBxdfResult Sample(Vector3 wo, Intersection inct, Random rand)
        {
            return new IdealReflectionBrdf(R.Sample(inct.UV)).Sample(wo, rand);
        }
    }
}
