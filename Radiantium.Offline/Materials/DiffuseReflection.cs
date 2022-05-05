using Radiantium.Core;
using Radiantium.Offline.Bxdf;
using System.Numerics;

namespace Radiantium.Offline.Materials
{
    public class DiffuseReflection : Material
    {
        public Color3F Kd { get; }
        public override BxdfType Type => BxdfType.Reflection | BxdfType.Diffuse;

        public DiffuseReflection(Color3F kd)
        {
            Kd = kd;
        }

        public override Color3F Fr(Vector3 wo, Vector3 wi)
        {
            return new LambertianReflectionBrdf(Kd).Fr(wo, wi);
        }

        public override float Pdf(Vector3 wo, Vector3 wi)
        {
            return new LambertianReflectionBrdf(Kd).Pdf(wo, wi);
        }

        public override SampleBxdfResult Sample(Vector3 wo, Random rand)
        {
            return new LambertianReflectionBrdf(Kd).Sample(wo, rand);
        }
    }
}
