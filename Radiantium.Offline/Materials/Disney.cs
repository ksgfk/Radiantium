using Radiantium.Core;
using Radiantium.Offline.Bxdf;
using System.Numerics;

namespace Radiantium.Offline.Materials
{
    public class Disney : Material
    {
        readonly BssrdfAdapter _adapter;
        public Texture2D BaseColor { get; }
        public Texture2D Metallic { get; }
        public Texture2D Eta { get; }
        public Texture2D Roughness { get; }
        public Texture2D SpecularTint { get; }
        public Texture2D Anisotropic { get; }
        public Texture2D Sheen { get; }
        public Texture2D SheenTint { get; }
        public Texture2D Clearcoat { get; }
        public Texture2D ClearcoatGloss { get; }
        public Texture2D SpecularScale { get; }
        public Texture2D Transmission { get; }
        public Texture2D ScattingDistance { get; }

        public override BxdfType Type => BxdfType.Reflection | BxdfType.Transmission | BxdfType.Diffuse | BxdfType.Glossy;

        public override Material BssrdfAdapter => _adapter;

        public Disney(Texture2D baseColor, Texture2D metallic, Texture2D roughness, Texture2D transmission, Texture2D eta, Texture2D specularScale, Texture2D specularTint, Texture2D anisotropic, Texture2D sheen, Texture2D sheenTint, Texture2D clearcoat, Texture2D clearcoatGloss, Texture2D scattingDistance)
        {
            BaseColor = baseColor ?? throw new ArgumentNullException(nameof(baseColor));
            Metallic = metallic ?? throw new ArgumentNullException(nameof(metallic));
            Roughness = roughness ?? throw new ArgumentNullException(nameof(roughness));
            Transmission = transmission ?? throw new ArgumentNullException(nameof(transmission));
            Eta = eta ?? throw new ArgumentNullException(nameof(eta));
            SpecularScale = specularScale ?? throw new ArgumentNullException(nameof(specularScale));
            SpecularTint = specularTint ?? throw new ArgumentNullException(nameof(specularTint));
            Anisotropic = anisotropic ?? throw new ArgumentNullException(nameof(anisotropic));
            Sheen = sheen ?? throw new ArgumentNullException(nameof(sheen));
            SheenTint = sheenTint ?? throw new ArgumentNullException(nameof(sheenTint));
            Clearcoat = clearcoat ?? throw new ArgumentNullException(nameof(clearcoat));
            ClearcoatGloss = clearcoatGloss ?? throw new ArgumentNullException(nameof(clearcoatGloss));
            ScattingDistance = scattingDistance ?? throw new ArgumentNullException(nameof(scattingDistance));
            _adapter = new BssrdfAdapter(Eta);
        }

        private DisneyBsdf CreateBsdf(Vector2 uv)
        {
            return new DisneyBsdf(
                BaseColor.Sample(uv),
                Metallic.Sample(uv).R,
                Eta.Sample(uv).R,
                Roughness.Sample(uv).R,
                SpecularTint.Sample(uv).R,
                Anisotropic.Sample(uv).R,
                Sheen.Sample(uv).R,
                SheenTint.Sample(uv).R,
                Clearcoat.Sample(uv).R,
                ClearcoatGloss.Sample(uv).R,
                SpecularScale.Sample(uv).R,
                Transmission.Sample(uv).R,
                ScattingDistance.Sample(uv)
            );
        }

        public override Color3F Fr(Vector3 wo, Vector3 wi, Intersection inct)
        {
            return CreateBsdf(inct.UV).Fr(wo, wi);
        }

        public override float Pdf(Vector3 wo, Vector3 wi, Intersection inct)
        {
            return CreateBsdf(inct.UV).Pdf(wo, wi);
        }

        public override SampleBxdfResult Sample(Vector3 wo, Intersection inct, Random rand)
        {
            return CreateBsdf(inct.UV).Sample(wo, rand);
        }

        public override Color3F S(Vector3 po, Vector3 wo, Coordinate co, Vector3 pi, Vector3 wi, Coordinate ci, Vector2 uv)
        {
            return CreateBsdf(uv).S(po, wo, co, pi, wi, ci);
        }

        public override SampleBssrdfResult SampleS(Vector3 po, Vector3 wo, Coordinate co, Material mo, Vector2 uv, Scene scene, Random rand)
        {
            return CreateBsdf(uv).SampleS(po, wo, co, mo, scene, rand);
        }
    }
}
