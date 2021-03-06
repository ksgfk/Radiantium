using Radiantium.Core;
using Radiantium.Offline.Bxdf;
using System.Numerics;
using static Radiantium.Offline.Coordinate;

namespace Radiantium.Offline.Materials
{
    public class RoughMetal : Material
    {
        public Color3F Eta { get; }
        public Color3F K { get; }
        public Texture2D Roughness { get; }
        public Texture2D Anisotropic { get; }
        public MicrofacetDistributionType Dist { get; }
        public bool IsTwoSide { get; }
        public override BxdfType Type => BxdfType.Reflection | BxdfType.Glossy;

        public RoughMetal(Color3F eta, Color3F k, Texture2D roughness, Texture2D anisotropic, MicrofacetDistributionType dist, bool isTwoSide)
        {
            Eta = eta;
            K = k;
            Roughness = roughness ?? throw new ArgumentNullException(nameof(roughness));
            Anisotropic = anisotropic ?? throw new AbandonedMutexException(nameof(anisotropic));
            Dist = dist;
            IsTwoSide = isTwoSide;
        }

        private (float, float) GetParam(Vector2 uv)
        {
            return (Roughness.Sample(uv).R, Anisotropic.Sample(uv).R);
        }

        private Color3F FrImpl<T>(Vector3 wo, Vector3 wi, T dist, TransportMode mode) where T : IMicrofacetDistribution
        {
            if (CosTheta(wo) <= 0 || CosTheta(wi) <= 0)
            {
                if (!IsTwoSide)
                {
                    return new Color3F(0.0f);
                }
            }
            return new MicrofacetReflectionBrdf<Fresnel.Conductor, T>(
                new Color3F(1.0f),
                new Fresnel.Conductor(new Color3F(1.0f), Eta, K),
                dist
            ).Fr(wo, wi, mode);
        }

        public override Color3F Fr(Vector3 wo, Vector3 wi, Intersection inct, TransportMode mode)
        {
            var (roughness, anis) = GetParam(inct.UV);
            return Dist switch
            {
                MicrofacetDistributionType.Beckmann => FrImpl(wo, wi, new Microfacet.Beckmann(roughness, anis), mode),
                MicrofacetDistributionType.GGX => FrImpl(wo, wi, new Microfacet.GGX(roughness, anis), mode),
                _ => new Color3F(0.0f)
            };
        }

        private float PdfImpl<T>(Vector3 wo, Vector3 wi, T dist, TransportMode mode) where T : IMicrofacetDistribution
        {
            if (CosTheta(wo) <= 0 || CosTheta(wi) <= 0)
            {
                if (!IsTwoSide)
                {
                    return 0.0f;
                }
            }
            return new MicrofacetReflectionBrdf<Fresnel.Conductor, T>(
                new Color3F(1.0f),
                new Fresnel.Conductor(new Color3F(1.0f), Eta, K),
                dist
            ).Pdf(wo, wi, mode);
        }

        public override float Pdf(Vector3 wo, Vector3 wi, Intersection inct, TransportMode mode)
        {
            var (roughness, anis) = GetParam(inct.UV);
            return Dist switch
            {
                MicrofacetDistributionType.Beckmann => PdfImpl(wo, wi, new Microfacet.Beckmann(roughness, anis), mode),
                MicrofacetDistributionType.GGX => PdfImpl(wo, wi, new Microfacet.GGX(roughness, anis), mode),
                _ => 0.0f
            };
        }

        private SampleBxdfResult SampleImpl<T>(Vector3 wo, Random rand, T dist, TransportMode mode) where T : IMicrofacetDistribution
        {
            if (CosTheta(wo) <= 0)
            {
                if (!IsTwoSide)
                {
                    return new SampleBxdfResult();
                }
            }
            return new MicrofacetReflectionBrdf<Fresnel.Conductor, T>(
                new Color3F(1.0f),
                new Fresnel.Conductor(new Color3F(1.0f), Eta, K),
                dist
            ).Sample(wo, rand, mode);
        }

        public override SampleBxdfResult Sample(Vector3 wo, Intersection inct, Random rand, TransportMode mode)
        {
            var (roughness, anis) = GetParam(inct.UV);
            return Dist switch
            {
                MicrofacetDistributionType.Beckmann => SampleImpl(wo, rand, new Microfacet.Beckmann(roughness, anis), mode),
                MicrofacetDistributionType.GGX => SampleImpl(wo, rand, new Microfacet.GGX(roughness, anis), mode),
                _ => new SampleBxdfResult()
            };
        }
    }
}
