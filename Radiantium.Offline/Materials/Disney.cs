using Radiantium.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static Radiantium.Core.MathExt;
using static System.MathF;
using static Radiantium.Offline.Coordinate;
using static System.Numerics.Vector3;
using static Radiantium.Core.Color3F;
using Radiantium.Offline.Bxdf;

namespace Radiantium.Offline.Materials
{
    public class Disney : Material
    {
        public struct DisneyFresnel : IFresnel
        {
            public Color3F R0;
            public float Metallic;
            public float Eta;

            public DisneyFresnel(Color3F r0, float metallic, float eta)
            {
                R0 = r0;
                Metallic = metallic;
                Eta = eta;
            }

            public Color3F Eval(float cosI)
            {
                return Lerp(Metallic, new Color3F(Fresnel.DielectricFunc(cosI, 1, Eta)), FrSchlick(R0, cosI));
            }
        }

        public struct DisneyMicrofacet : IMicrofacetDistribution
        {
            public float AlphaX => throw new NotImplementedException();

            public float AlphaY => throw new NotImplementedException();

            public float D(Vector3 wh)
            {
                throw new NotImplementedException();
            }

            public float G(Vector3 wo, Vector3 wi)
            {
                throw new NotImplementedException();
            }

            public float Lambda(Vector3 w)
            {
                throw new NotImplementedException();
            }

            public float Pdf(Vector3 wo, Vector3 wh)
            {
                throw new NotImplementedException();
            }

            public Vector3 SampleWh(Vector3 wo, Random rand)
            {
                throw new NotImplementedException();
            }
        }

        public Texture2D Color { get; }
        public Texture2D Metallic { get; }
        public Texture2D Eta { get; }
        public Texture2D Roughness { get; }
        public Texture2D SpecularTint { get; }
        public Texture2D Anisotropic { get; }
        public Texture2D Sheen { get; }
        public Texture2D SheenTint { get; }
        public Texture2D ClearCoat { get; }
        public Texture2D ClearCoatGloss { get; }
        public Texture2D SpecTrans { get; }
        public Texture2D DiffTrans { get; }
        public Texture2D ScatterDistance { get; }
        public Texture2D Flatness { get; }
        public bool Thin { get; }

        public Disney(Texture2D color, Texture2D metallic, Texture2D eta, Texture2D roughness, Texture2D specularTint, Texture2D anisotropic, Texture2D sheen, Texture2D sheenTint, Texture2D clearCoat, Texture2D clearCoatGloss, Texture2D specTrans, Texture2D diffTrans, Texture2D scatterDistance, Texture2D flatness, bool thin)
        {
            Color = color ?? throw new ArgumentNullException(nameof(color));
            Metallic = metallic ?? throw new ArgumentNullException(nameof(metallic));
            Eta = eta ?? throw new ArgumentNullException(nameof(eta));
            Roughness = roughness ?? throw new ArgumentNullException(nameof(roughness));
            SpecularTint = specularTint ?? throw new ArgumentNullException(nameof(specularTint));
            Anisotropic = anisotropic ?? throw new ArgumentNullException(nameof(anisotropic));
            Sheen = sheen ?? throw new ArgumentNullException(nameof(sheen));
            SheenTint = sheenTint ?? throw new ArgumentNullException(nameof(sheenTint));
            ClearCoat = clearCoat ?? throw new ArgumentNullException(nameof(clearCoat));
            ClearCoatGloss = clearCoatGloss ?? throw new ArgumentNullException(nameof(clearCoatGloss));
            SpecTrans = specTrans ?? throw new ArgumentNullException(nameof(specTrans));
            DiffTrans = diffTrans ?? throw new ArgumentNullException(nameof(diffTrans));
            ScatterDistance = scatterDistance ?? throw new ArgumentNullException(nameof(scatterDistance));
            Flatness = flatness ?? throw new ArgumentNullException(nameof(flatness));
            Thin = thin;
        }

        public override BxdfType Type => BxdfType.Glossy | BxdfType.Transmission | BxdfType.Diffuse | BxdfType.Reflection;

        public override Color3F Fr(Vector3 wo, Vector3 wi, Intersection inct)
        {
            Vector2 uv = inct.UV;

            Color3F c = Color.Sample(uv);
            float metallicWeight = Metallic.Sample(uv).R;
            float e = Eta.Sample(uv).R;
            float strans = SpecTrans.Sample(uv).R;
            float diffuseWeight = (1 - metallicWeight) * (1 - strans);
            float dt = DiffTrans.Sample(uv).R / 2;
            float rough = Roughness.Sample(uv).R;
            float lum = c.GetLuminance();
            Color3F Ctint = lum > 0 ? (c / lum) : new Color3F(1);
            float sheenWeight = Sheen.Sample(uv).R;
            Color3F Csheen = new Color3F(0.0f);
            if (sheenWeight > 0)
            {
                float stint = SheenTint.Sample(uv).R;
                Csheen = Lerp(stint, new Color3F(1.0f), Ctint);
            }
            float anisotropic = Anisotropic.Sample(uv).R;
            float specTint = SpecularTint.Sample(uv).R;
            Color3F Cspec0 = Lerp(metallicWeight, SchlickR0FromEta(e) * Lerp(specTint, new Color3F(1.0f), Ctint), c);

            Color3F result = new Color3F(0.0f);
            if (diffuseWeight > 0)
            {
                if (Thin)
                {
                    float flat = Flatness.Sample(uv).R;
                    Color3F disneyDiffuse = Diffuse(wo, wi, diffuseWeight * (1 - flat) * (1 - dt) * c);
                    Color3F disneyFakeSS = FakeSS(wo, wi, diffuseWeight * flat * (1 - dt) * c, rough);
                    result += disneyDiffuse;
                    result += disneyFakeSS;
                }
                else
                {
                    Color3F sd = ScatterDistance.Sample(uv);
                    if (sd == Black)
                    {
                        Color3F disneyDiffuse = Diffuse(wo, wi, diffuseWeight * c);
                    }
                    else
                    {
                        //TODO: BSSRDF
                    }
                }
                Color3F disneyRetro = Retro(wo, wi, diffuseWeight * c, rough);
                result += disneyRetro;
                if (sheenWeight > 0)
                {
                    Color3F disneySheen = SheenFunc(wo, wi, diffuseWeight * sheenWeight * Csheen);
                    result += disneySheen;
                }
            }
            float aspect = Sqrt(1 - anisotropic * 0.9f);
            float ax = Max(0.001f, rough * rough / aspect);
            float ay = Max(0.001f, rough * rough * aspect);

            throw new NotImplementedException();
        }

        public override float Pdf(Vector3 wo, Vector3 wi, Intersection inct)
        {
            throw new NotImplementedException();
        }

        public override SampleBxdfResult Sample(Vector3 wo, Intersection inct, Random rand)
        {
            throw new NotImplementedException();
        }

        public static float SchlickWeight(float cosTheta)
        {
            //R = R(0) + (1 - R(0)) (1 - cos theta)^5
            float m = Math.Clamp(1 - cosTheta, 0, 1);
            return (m * m) * (m * m) * m;
        }

        public static float SchlickR0FromEta(float eta)
        {
            //R(0) = (eta - 1)^2 / (eta + 1)^2
            return ((eta - 1) * (eta - 1)) / ((eta + 1) * (eta + 1));
        }

        public static Color3F FrSchlick(Color3F R0, float cosTheta)
        {
            return Lerp(SchlickWeight(cosTheta), R0, new Color3F(1.0f));
        }

        public static Color3F Diffuse(Vector3 wo, Vector3 wi, Color3F r)
        {
            float Fo = SchlickWeight(AbsCosTheta(wo));
            float Fi = SchlickWeight(AbsCosTheta(wi));
            return r * (1 / PI) * (1 - Fo / 2) * (1 - Fi / 2);
        }

        public static Color3F FakeSS(Vector3 wo, Vector3 wi, Color3F r, float roughness)
        {
            Vector3 wh = wi + wo;
            if (wh.X == 0 && wh.Y == 0 && wh.Z == 0) { return new Color3F(0.0f); }
            wh = Normalize(wh);
            float cosThetaD = Dot(wi, wh);
            float Fss90 = cosThetaD * cosThetaD * roughness;
            float Fo = SchlickWeight(AbsCosTheta(wo));
            float Fi = SchlickWeight(AbsCosTheta(wi));
            float Fss = Lerp(Fo, 1.0f, Fss90) * Lerp(Fi, 1.0f, Fss90);
            float ss = 1.25f * (Fss * (1 / (AbsCosTheta(wo) + AbsCosTheta(wi)) - 0.5f) + 0.5f);
            return r * (1 / PI) * ss;
        }

        public static Color3F Retro(Vector3 wo, Vector3 wi, Color3F r, float roughness)
        {
            Vector3 wh = wi + wo;
            if (wh.X == 0 && wh.Y == 0 && wh.Z == 0) { return new Color3F(0.0f); }
            wh = Normalize(wh);
            float cosThetaD = Dot(wi, wh);
            float Fo = SchlickWeight(AbsCosTheta(wo));
            float Fi = SchlickWeight(AbsCosTheta(wi));
            float Rr = 2 * roughness * cosThetaD * cosThetaD;
            return r * (1 / PI) * Rr * (Fo + Fi + Fo * Fi * (Rr - 1));
        }

        public static Color3F SheenFunc(Vector3 wo, Vector3 wi, Color3F r)
        {
            Vector3 wh = wi + wo;
            if (wh.X == 0 && wh.Y == 0 && wh.Z == 0) { return new Color3F(0.0f); }
            wh = Normalize(wh);
            float cosThetaD = Dot(wi, wh);
            return r * SchlickWeight(cosThetaD);
        }
    }
}
