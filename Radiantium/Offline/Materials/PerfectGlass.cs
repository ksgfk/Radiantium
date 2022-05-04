using Radiantium.Core;
using Radiantium.Offline.Bxdf;
using System.Numerics;

namespace Radiantium.Offline.Materials
{
    public class PerfectGlass : Material
    {
        public Color3F R { get; }
        public Color3F T { get; }
        public float EtaA { get; }
        public float EtaB { get; }
        public override BxdfType Type => BxdfType.Specular | BxdfType.Transmission | BxdfType.Specular;

        public PerfectGlass(Color3F r, Color3F t, float etaA, float etaB)
        {
            R = r;
            T = t;
            EtaA = etaA;
            EtaB = etaB;
        }

        public override Color3F Fr(Vector3 wo, Vector3 wi)
        {
            return new FresnelSpecularBsdf(R, T, EtaA, EtaB).Fr(wo, wi);
        }

        public override float Pdf(Vector3 wo, Vector3 wi)
        {
            return new FresnelSpecularBsdf(R, T, EtaA, EtaB).Pdf(wo, wi);
        }

        public override SampleBxdfResult Sample(Vector3 wo, Random rand)
        {
            return new FresnelSpecularBsdf(R, T, EtaA, EtaB).Sample(wo, rand);
        }
    }
}
