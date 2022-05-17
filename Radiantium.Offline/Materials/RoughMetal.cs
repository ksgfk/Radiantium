using Radiantium.Core;
using Radiantium.Offline.Bxdf;
using System.Numerics;
using static Radiantium.Offline.Coordinate;

namespace Radiantium.Offline.Materials
{
    //TODO:
    //BUG: Beckmann...edges are black...
    public class RoughMetal : Material
    {
        public Color3F Eta { get; }
        public Color3F K { get; }
        public Texture2D Roughness { get; }
        public MicrofacetDistributionType Dist { get; }
        public override BxdfType Type => BxdfType.Reflection | BxdfType.Glossy;

        public RoughMetal(Color3F eta, Color3F k, Texture2D roughness, MicrofacetDistributionType dist)
        {
            Eta = eta;
            K = k;
            Roughness = roughness ?? throw new ArgumentNullException(nameof(roughness));
            Dist = dist;
        }

        private float GetParam(Vector2 uv)
        {
            return Roughness.Sample(uv).R;
        }

        public override Color3F Fr(Vector3 wo, Vector3 wi, Intersection inct)
        {
            if (CosTheta(wo) <= 0 || CosTheta(wi) <= 0) { return new Color3F(0.0f); }
            float roughness = GetParam(inct.UV);
            return Dist switch
            {
                MicrofacetDistributionType.Beckmann =>
                    new MicrofacetReflectionBrdf<Fresnel.Conductor, Microfacet.Beckmann>(
                        new Color3F(1.0f),
                        new Fresnel.Conductor(new Color3F(1.0f), Eta, K),
                        new Microfacet.Beckmann(roughness)
                    ).Fr(wo, wi),
                MicrofacetDistributionType.GGX =>
                    new MicrofacetReflectionBrdf<Fresnel.Conductor, Microfacet.GGX>(
                        new Color3F(1.0f),
                        new Fresnel.Conductor(new Color3F(1.0f), Eta, K),
                        new Microfacet.GGX(roughness)
                    ).Fr(wo, wi),
                _ => new Color3F(0.0f)
            };
        }

        public override float Pdf(Vector3 wo, Vector3 wi, Intersection inct)
        {
            if (CosTheta(wo) <= 0 || CosTheta(wi) <= 0) { return 0.0f; }
            float roughness = GetParam(inct.UV);
            return Dist switch
            {
                MicrofacetDistributionType.Beckmann =>
                    new MicrofacetReflectionBrdf<Fresnel.Conductor, Microfacet.Beckmann>(
                        new Color3F(1.0f),
                        new Fresnel.Conductor(new Color3F(1.0f), Eta, K),
                        new Microfacet.Beckmann(roughness)
                    ).Pdf(wo, wi),
                MicrofacetDistributionType.GGX =>
                    new MicrofacetReflectionBrdf<Fresnel.Conductor, Microfacet.GGX>(
                        new Color3F(1.0f),
                        new Fresnel.Conductor(new Color3F(1.0f), Eta, K),
                        new Microfacet.GGX(roughness)
                    ).Pdf(wo, wi),
                _ => 0.0f
            };
        }

        public override SampleBxdfResult Sample(Vector3 wo, Intersection inct, Random rand)
        {
            if (CosTheta(wo) <= 0) { return new SampleBxdfResult(); }
            float roughness = GetParam(inct.UV);
            return Dist switch
            {
                MicrofacetDistributionType.Beckmann =>
                    new MicrofacetReflectionBrdf<Fresnel.Conductor, Microfacet.Beckmann>(
                        new Color3F(1.0f),
                        new Fresnel.Conductor(new Color3F(1.0f), Eta, K),
                        new Microfacet.Beckmann(roughness)
                    ).Sample(wo, rand),
                MicrofacetDistributionType.GGX =>
                    new MicrofacetReflectionBrdf<Fresnel.Conductor, Microfacet.GGX>(
                        new Color3F(1.0f),
                        new Fresnel.Conductor(new Color3F(1.0f), Eta, K),
                        new Microfacet.GGX(roughness)
                    ).Sample(wo, rand),
                _ => new SampleBxdfResult()
            };
        }
    }
}
