using Radiantium.Core;
using Radiantium.Offline.Bxdf;
using System.Numerics;

namespace Radiantium.Offline.Materials
{
    public class DiffuseReflection : Material
    {
        public Texture2D Kd { get; }
        public override BxdfType Type => BxdfType.Reflection | BxdfType.Diffuse;

        public DiffuseReflection(Texture2D kd)
        {
            Kd = kd;
        }

        public override Color3F Fr(Vector3 wo, Vector3 wi, Intersection inct)
        {
            return new LambertianReflectionBrdf(Kd.Sample(inct.UV)).Fr(wo, wi);
        }

        public override float Pdf(Vector3 wo, Vector3 wi, Intersection inct)
        {
            return new LambertianReflectionBrdf(Kd.Sample(inct.UV)).Pdf(wo, wi);
        }

        public override SampleBxdfResult Sample(Vector3 wo, Intersection inct, Random rand)
        {
            return new LambertianReflectionBrdf(Kd.Sample(inct.UV)).Sample(wo, rand);
        }
    }
}
