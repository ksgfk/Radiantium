using Radiantium.Core;
using Radiantium.Offline.Bxdf;
using System.Numerics;

namespace Radiantium.Offline.Materials
{
    public class PrefectMirror : Material
    {
        public Color3F R { get; }
        public override BxdfType Type => BxdfType.Reflection | BxdfType.Specular;

        public PrefectMirror(Color3F r)
        {
            R = r;
        }

        public override Color3F Fr(Vector3 wo, Vector3 wi)
        {
            return new IdealReflectionBrdf(R).Fr(wo, wi);
        }

        public override float Pdf(Vector3 wo, Vector3 wi)
        {
            return new IdealReflectionBrdf(R).Pdf(wo, wi);
        }

        public override SampleBxdfResult Sample(Vector3 wo, Random rand)
        {
            return new IdealReflectionBrdf(R).Sample(wo, rand);
        }
    }
}
