using Radiantium.Core;
using Radiantium.Offline.Bxdf;
using System.Numerics;

namespace Radiantium.Offline.Materials
{
    public class PerfectGlass : Material
    {
        public Texture2D R { get; }
        public Texture2D T { get; }
        public float EtaA { get; }
        public float EtaB { get; }
        public override BxdfType Type => BxdfType.Specular | BxdfType.Transmission;

        public PerfectGlass(Texture2D r, Texture2D t, float etaA, float etaB)
        {
            R = r;
            T = t;
            EtaA = etaA;
            EtaB = etaB;
        }

        public override Color3F Fr(Vector3 wo, Vector3 wi, Intersection inct, TransportMode mode)
        {
            return new FresnelSpecularBsdf(R.Sample(inct.UV), T.Sample(inct.UV), EtaA, EtaB).Fr(wo, wi, mode);
        }

        public override float Pdf(Vector3 wo, Vector3 wi, Intersection inct, TransportMode mode)
        {
            return new FresnelSpecularBsdf(R.Sample(inct.UV), T.Sample(inct.UV), EtaA, EtaB).Pdf(wo, wi, mode);
        }

        public override SampleBxdfResult Sample(Vector3 wo, Intersection inct, Random rand, TransportMode mode)
        {
            return new FresnelSpecularBsdf(R.Sample(inct.UV), T.Sample(inct.UV), EtaA, EtaB).Sample(wo, rand, mode);
        }
    }
}
