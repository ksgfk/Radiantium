using Radiantium.Core;
using Radiantium.Offline.Bxdf;
using Radiantium.Offline.Textures;
using System.Numerics;

namespace Radiantium.Offline.Materials
{
    public class Subsurface : Material
    {
        public Texture2D R { get; }
        public Texture2D T { get; }
        public Texture2D A { get; }
        public Texture2D ScattingDistance { get; }
        public float EtaA { get; }
        public float EtaB { get; }
        public override Material? BssrdfAdapter { get; }
        public override BxdfType Type => BxdfType.Specular | BxdfType.Transmission | BxdfType.SubsurfaceScatting;

        public Subsurface(Texture2D r, Texture2D t, Texture2D a, Texture2D scattingDistance, float etaA, float etaB)
        {
            R = r;
            T = t;
            A = a;
            ScattingDistance = scattingDistance;
            EtaA = etaA;
            EtaB = etaB;
            BssrdfAdapter = new BssrdfAdapter(new ConstColorTexture2D(new Color3F(EtaB / EtaA)));
        }

        public override Color3F Fr(Vector3 wo, Vector3 wi, Intersection inct)
        {
            Color3F r = R.Sample(inct.UV);
            Color3F t = T.Sample(inct.UV);
            return new FresnelSpecularBsdf(r, t, EtaA, EtaB).Fr(wo, wi);
        }

        public override float Pdf(Vector3 wo, Vector3 wi, Intersection inct)
        {
            Color3F r = R.Sample(inct.UV);
            Color3F t = new Color3F(0.0f);
            return new FresnelSpecularBsdf(r, t, EtaA, EtaB).Pdf(wo, wi);
        }

        public override SampleBxdfResult Sample(Vector3 wo, Intersection inct, Random rand)
        {
            Color3F r = R.Sample(inct.UV);
            Color3F t = T.Sample(inct.UV);
            SampleBxdfResult sample = new FresnelSpecularBsdf(r, t, EtaA, EtaB).Sample(wo, rand);
            Color3F sd = ScattingDistance.Sample(inct.UV);
            if (sd != Color3F.Black)
            {
                sample.Type |= BxdfType.SubsurfaceScatting;
            }
            return sample;
        }

        public override SampleBssrdfResult SamplePi(Intersection po, Scene scene, Random rand)
        {
            Color3F a = A.Sample(po.UV);
            Color3F sd = ScattingDistance.Sample(po.UV);
            float eta = EtaB / EtaA;
            NormalizedDiffusionRadialProfile n = new NormalizedDiffusionRadialProfile(a, sd);
            SeparableBssrdf<NormalizedDiffusionRadialProfile> bssrdf = new(po, eta, n);
            return bssrdf.SamplePi(scene, rand);
        }
    }
}
