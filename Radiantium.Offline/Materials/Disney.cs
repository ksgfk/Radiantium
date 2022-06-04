using Radiantium.Core;
using Radiantium.Offline.Bxdf;
using System.Numerics;

namespace Radiantium.Offline.Materials
{
    public class Disney : Material
    {
        public Texture2D BaseColor { get; }
        public Texture2D Metallic { get; }
        public Texture2D Roughness { get; }
        public Texture2D Transmission { get; }
        public Texture2D TransmissionRoughness { get; }
        public Texture2D Ior { get; }
        public Texture2D SpecularScale { get; }
        public Texture2D SpecularTint { get; }
        public Texture2D Anisotropic { get; }
        public Texture2D Sheen { get; }
        public Texture2D SheenTint { get; }
        public Texture2D Clearcoat { get; }
        public Texture2D ClearcoatGloss { get; }
        public override BxdfType Type => BxdfType.Reflection | BxdfType.Transmission | BxdfType.Diffuse | BxdfType.Glossy;

        public Disney(Texture2D baseColor, Texture2D metallic, Texture2D roughness, Texture2D transmission, Texture2D transmissionRoughness, Texture2D ior, Texture2D specularScale, Texture2D specularTint, Texture2D anisotropic, Texture2D sheen, Texture2D sheenTint, Texture2D clearcoat, Texture2D clearcoatGloss)
        {
            BaseColor = baseColor ?? throw new ArgumentNullException(nameof(baseColor));
            Metallic = metallic ?? throw new ArgumentNullException(nameof(metallic));
            Roughness = roughness ?? throw new ArgumentNullException(nameof(roughness));
            Transmission = transmission ?? throw new ArgumentNullException(nameof(transmission));
            TransmissionRoughness = transmissionRoughness ?? throw new ArgumentNullException(nameof(transmissionRoughness));
            Ior = ior ?? throw new ArgumentNullException(nameof(ior));
            SpecularScale = specularScale ?? throw new ArgumentNullException(nameof(specularScale));
            SpecularTint = specularTint ?? throw new ArgumentNullException(nameof(specularTint));
            Anisotropic = anisotropic ?? throw new ArgumentNullException(nameof(anisotropic));
            Sheen = sheen ?? throw new ArgumentNullException(nameof(sheen));
            SheenTint = sheenTint ?? throw new ArgumentNullException(nameof(sheenTint));
            Clearcoat = clearcoat ?? throw new ArgumentNullException(nameof(clearcoat));
            ClearcoatGloss = clearcoatGloss ?? throw new ArgumentNullException(nameof(clearcoatGloss));
        }

        private DisneyBsdf CreateBsdf(Vector2 uv)
        {
            return new DisneyBsdf(
                BaseColor.Sample(uv),
                Metallic.Sample(uv).R,
                Roughness.Sample(uv).R,
                SpecularScale.Sample(uv),
                SpecularTint.Sample(uv).R,
                Anisotropic.Sample(uv).R,
                Sheen.Sample(uv).R,
                SheenTint.Sample(uv).R,
                Clearcoat.Sample(uv).R,
                ClearcoatGloss.Sample(uv).R,
                Transmission.Sample(uv).R,
                TransmissionRoughness.Sample(uv).R,
                Ior.Sample(uv).R
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
    }
}
