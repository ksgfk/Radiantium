using Radiantium.Core;
using System.Numerics;
using static Radiantium.Core.Color3F;
using static Radiantium.Core.MathExt;
using static Radiantium.Offline.Coordinate;
using static System.MathF;
using static System.Numerics.Vector3;

namespace Radiantium.Offline.Bxdf
{
    internal struct DisneyRetroBrdf : IBxdf
    {
        public Color3F R;
        public float Roughness;
        public BxdfType Type => BxdfType.Reflection | BxdfType.Diffuse;
        public DisneyRetroBrdf(Color3F r, float roughness) { R = r; Roughness = roughness; }
        public Color3F Fr(Vector3 wo, Vector3 wi)
        {
            Vector3 wh = wo + wi;
            if (wh.X == 0 && wh.Y == 0 && wh.Z == 0) { return new Color3F(0); }
            wh = Normalize(wh);
            float cosThetaD = Dot(wi, wh);
            float fo = DisneyBsdf.SchlickWeight(AbsCosTheta(wo));
            float fi = DisneyBsdf.SchlickWeight(AbsCosTheta(wi));
            float rr = 2 * Roughness * cosThetaD * cosThetaD;
            return R * (1 / PI) * rr * (fo + fi + fo * fi * (rr - 1));
        }
        public float Pdf(Vector3 wo, Vector3 wi) { return SameHemisphere(wo, wi) ? AbsCosTheta(wi) / PI : 0.0F; }
        public SampleBxdfResult Sample(Vector3 wo, Random rand)
        {
            Vector3 wi = Normalize(Probability.SquareToCosineHemisphere(rand.NextVec2()));
            if (wo.Z < 0) { wi.Z *= -1; }
            if (!SameHemisphere(wo, wi)) { return new SampleBxdfResult(); }
            float pdf = Pdf(wo, wi);
            Color3F fr = R / PI;
            return new SampleBxdfResult(wi, fr, pdf, Type);
        }
    }

    internal struct DisneySheenBrdf : IBxdf
    {
        public Color3F R;
        public BxdfType Type => BxdfType.Reflection | BxdfType.Diffuse;
        public DisneySheenBrdf(Color3F r) { R = r; }
        public Color3F Fr(Vector3 wo, Vector3 wi)
        {
            Vector3 wh = wo + wi;
            if (wh.X == 0 && wh.Y == 0 && wh.Z == 0) { return new Color3F(0); }
            wh = Normalize(wh);
            float cosThetaD = Dot(wi, wh);
            return R * DisneyBsdf.SchlickWeight(cosThetaD);
        }
        public float Pdf(Vector3 wo, Vector3 wi) { return SameHemisphere(wo, wi) ? AbsCosTheta(wi) / PI : 0.0F; }
        public SampleBxdfResult Sample(Vector3 wo, Random rand)
        {
            Vector3 wi = Normalize(Probability.SquareToCosineHemisphere(rand.NextVec2()));
            if (wo.Z < 0) { wi.Z *= -1; }
            if (!SameHemisphere(wo, wi)) { return new SampleBxdfResult(); }
            float pdf = Pdf(wo, wi);
            Color3F fr = R / PI;
            return new SampleBxdfResult(wi, fr, pdf, Type);
        }
    }

    internal struct DisneyDiffuseBrdf : IBxdf
    {
        public Color3F R;
        public BxdfType Type => BxdfType.Reflection | BxdfType.Diffuse;
        public DisneyDiffuseBrdf(Color3F r) { R = r; }
        public Color3F Fr(Vector3 wo, Vector3 wi)
        {
            float fo = DisneyBsdf.SchlickWeight(AbsCosTheta(wo));
            float fi = DisneyBsdf.SchlickWeight(AbsCosTheta(wi));
            return R * (1 / PI) * (1 - fo / 2) * (1 - fi / 2);
        }
        public float Pdf(Vector3 wo, Vector3 wi) { return SameHemisphere(wo, wi) ? AbsCosTheta(wi) / PI : 0.0F; }
        public SampleBxdfResult Sample(Vector3 wo, Random rand)
        {
            Vector3 wi = Normalize(Probability.SquareToCosineHemisphere(rand.NextVec2()));
            if (wo.Z < 0) { wi.Z *= -1; }
            if (!SameHemisphere(wo, wi)) { return new SampleBxdfResult(); }
            float pdf = Pdf(wo, wi);
            Color3F fr = R / PI;
            return new SampleBxdfResult(wi, fr, pdf, Type);
        }
    }

    internal struct DisneyDistributionGtr2 : IMicrofacetDistribution
    {
        public float AlphaX { get; }
        public float AlphaY { get; }
        public DisneyDistributionGtr2(float alphaX, float alphaY) { AlphaX = alphaX; AlphaY = alphaY; }
        public float D(Vector3 wh) { return Microfacet.DistributionGGX(wh, AlphaX, AlphaY); }
        public float G1(Vector3 w) { return 1 / (1 + Lambda(w)); }
        public float G(Vector3 wo, Vector3 wi) { return G1(wo) * G1(wi); }
        public float Lambda(Vector3 w) { return Microfacet.LambdaGGX(w, AlphaX, AlphaY); }
        public float Pdf(Vector3 wo, Vector3 wh) { return D(wh) * AbsCosTheta(wh); }
        public Vector3 SampleWh(Vector3 wo, Random rand) { return Microfacet.SampleWhGGX(wo, rand, AlphaX, AlphaY); }
    }

    internal struct DisneyFresnel : IFresnel
    {
        public Color3F R0, C;
        public float Metallic, Eta, SpecTint, SpecScale;
        public DisneyFresnel(Color3F r0, Color3F c, float metallic, float eta, float specTint, float specScale)
        {
            R0 = r0;
            C = c;
            Metallic = metallic;
            Eta = eta;
            SpecTint = specTint;
            SpecScale = specScale;
        }
        public Color3F Eval(float cosI)
        {
            Color3F fd = Lerp(SpecTint, new Color3F(Fresnel.DielectricFunc(cosI, 1, Eta)), DisneyBsdf.FrsnelSchlick(R0, cosI));
            return Lerp(Metallic, fd * SpecScale, DisneyBsdf.FrsnelSchlick(C, cosI));
        }
    }

    internal struct DisneyClearcoatBrdf : IBxdf
    {
        public float Weight, Gloss;
        public BxdfType Type => BxdfType.Reflection | BxdfType.Glossy;
        public DisneyClearcoatBrdf(float weight, float gloss) { Weight = weight; Gloss = gloss; }
        public Color3F Fr(Vector3 wo, Vector3 wi)
        {
            Vector3 wh = wo + wi;
            if (wh.X == 0 && wh.Y == 0 && wh.Z == 0) { return new Color3F(0); }
            wh = Normalize(wh);
            float d = DisneyBsdf.Gtr1Brdf(AbsCosTheta(wh), Gloss);
            float f = DisneyBsdf.FrsnelSchlick(0.04f, Dot(wo, wh));
            float g = DisneyBsdf.SmithG1GGX(AbsCosTheta(wo), 0.25f) * DisneyBsdf.SmithG1GGX(AbsCosTheta(wi), 0.25f);
            return new Color3F(Weight * d * f * g / 4);
        }
        public float Pdf(Vector3 wo, Vector3 wi)
        {
            if (!SameHemisphere(wo, wi)) { return 0; }
            Vector3 wh = wo + wi;
            if (wh.X == 0 && wh.Y == 0 && wh.Z == 0) { return 0; }
            wh = Normalize(wh);
            float d = DisneyBsdf.Gtr1Brdf(AbsCosTheta(wh), Gloss);
            return d * AbsCosTheta(wh) / (4 * Dot(wo, wh));
        }
        public SampleBxdfResult Sample(Vector3 wo, Random rand)
        {
            if (CosTheta(wo) == 0) { return new SampleBxdfResult(); }
            Vector2 rng = rand.NextVec2();
            float alpha2 = Sqr(Gloss);
            float cosTheta = Sqrt(Max(0, (1 - Pow(alpha2, 1 - rng.X)) / (1 - alpha2)));
            float sinTheta = Sqrt(Max(0, 1 - cosTheta * cosTheta));
            float phi = 2 * PI * rng.Y;
            Vector3 wh = SphericalDirection(sinTheta, cosTheta, phi);
            if (!SameHemisphere(wo, wh)) { wh = -wh; }
            Vector3 wi = Reflect(-wo, wh);
            if (!SameHemisphere(wo, wi)) { return new SampleBxdfResult(); }
            Color3F fr = Fr(wo, wi);
            float pdf = Pdf(wo, wi);
            return new SampleBxdfResult(wi, fr, pdf, Type);
        }
    }

    internal struct DisneyBssrdf : ISeparableBssrdf
    {
        public Color3F R;
        public Color3F d;
        public DisneyBssrdf(Color3F r, Color3F d) { R = r; this.d = d; }
        public float PdfSr(int channel, float r)
        {
            float a = IndexerUnsafe(ref d, channel);
            return (0.25f * Exp(-r / a) / (2 * PI * a * r) + 0.75f * Exp(-r / (3 * a)) / (6 * PI * a * r));
        }
        public Color3F S(Vector3 po, Vector3 wo, Coordinate co, Vector3 pi, Vector3 wi, Coordinate ci)
        {
            Vector3 a = Normalize(pi - po);
            float fade = 1;
            Vector3 n = co.Z;
            float cosTheta = Dot(a, n);
            if (cosTheta > 0)
            {
                float sinTheta = Sqrt(Max(0.0f, 1 - Sqr(cosTheta)));
                Vector3 a2 = n * sinTheta - (a - n * cosTheta) * cosTheta / sinTheta;
                fade = Max(0, Dot(ci.Z, a2));
            }
            float fo = DisneyBsdf.SchlickWeight(AbsCosTheta(wo));
            float fi = DisneyBsdf.SchlickWeight(AbsCosTheta(wi));
            return fade * (1 - fo / 2) * (1 - fi / 2) * this.Sp(po, pi) / PI;
        }
        public SampleBssrdfResult SampleS(Vector3 po, Vector3 wo, Coordinate co, Material mo, Scene scene, Random rand)
        {
            return Bssrdf.SeparableSampleS(po, co, mo, scene, rand, this);
        }
        public float SampleSr(int channel, float u)
        {
            if (u < 0.25f)
            {
                u = Min(u * 4, 0.9999f);
                return IndexerUnsafe(ref d, channel) * Log(1 / (1 - u));
            }
            else
            {
                u = Min((u - 0.25f) / 0.75f, 0.9999f);
                return 3 * IndexerUnsafe(ref d, channel) * Log(1 / (1 - u));
            }
        }
        public Color3F Sr(float r)
        {
            if (r < 1e-6f) { r = 1e-6f; }
            return R * (Exp(-new Color3F(r) / d) + Exp(-new Color3F(r) / (3 * d))) / (8 * PI * d * r);
        }
    }

    public struct DisneyBsdf : IBxdf
    {
        public Color3F Color;
        public float Metallic;
        public float Eta;
        public float Roughness;
        public float SpecularTint;
        public float Anisotropic;
        public float Sheen;
        public float SheenTint;
        public float Clearcoat;
        public float ClearcoatGloss;
        public float SpeclarScale;
        public float Transmission;
        public BxdfType Type => BxdfType.Reflection | BxdfType.Transmission | BxdfType.Diffuse | BxdfType.Glossy;
        public Color3F ColorTint
        {
            get
            {
                float luminance = Color.GetLuminance();
                return luminance > 0 ? Color / luminance : new Color3F(1.0f);
            }
        }
        public float MetallicWeight => Metallic;
        public float DiffuseWeight => 1 - MetallicWeight;
        public float SheenWeight => Sheen;
        public (float, float) AnisAlpha
        {
            get
            {
                float aspect = Sqrt(1 - Anisotropic * 0.9f);
                float ax = Max(0.001f, Sqr(Roughness) / aspect);
                float ay = Max(0.001f, Sqr(Roughness) * aspect);
                return (ax, ay);
            }
        }

        public DisneyBsdf(Color3F color, float metallic, float eta, float roughness, float specularTint, float anisotropic, float sheen, float sheenTint, float clearcoat, float clearcoatGloss, float specTrans, float transmission)
        {
            Color = color;
            Metallic = metallic;
            Eta = eta;
            Roughness = roughness;
            SpecularTint = specularTint;
            Anisotropic = anisotropic;
            Sheen = sheen;
            SheenTint = sheenTint;
            Clearcoat = clearcoat;
            ClearcoatGloss = clearcoatGloss;
            SpeclarScale = specTrans;
            Transmission = transmission;
        }

        public Color3F Fr(Vector3 wo, Vector3 wi)
        {
            Color3F diffuse = new Color3F(0.0f);
            if (DiffuseWeight > 0)
            {
                Color3F retro = GetRetroBrdf().Fr(wo, wi);
                Color3F sheen = new Color3F(0.0f);
                if (Sheen > 0)
                {
                    Color3F sheenColor = Lerp(SheenTint, new Color3F(1.0f), ColorTint);
                    sheen = GetSheenBrdf(sheenColor).Fr(wo, wi);
                }
                Color3F disneyDiffuse = GetDiffuseBrdf().Fr(wo, wi);
                diffuse = retro + sheen + disneyDiffuse;
            }
            Color3F specular = GetSpecularBrdf().Fr(wo, wi);
            Color3F clearcoat = new Color3F(0.0f);
            if (Clearcoat > 0)
            {
                clearcoat = GetClearcoatBrdf().Fr(wo, wi);
            }
            return diffuse + specular + clearcoat;
        }

        public float Pdf(Vector3 wo, Vector3 wi)
        {
            var (diffuseWeight, specularWeight, clearcoatWeight) = GetSampleWeight();

            float diffusePdf = GetDiffuseBrdf().Pdf(wo, wi);
            float specularPdf = GetSpecularBrdf().Pdf(wo, wi);
            float clearcoatPdf = GetClearcoatBrdf().Pdf(wo, wi);

            return diffusePdf * diffuseWeight + specularPdf * specularWeight + clearcoatPdf * clearcoatWeight;
        }

        public SampleBxdfResult Sample(Vector3 wo, Random rand)
        {
            var (diffuseWeight, specularWeight, clearcoatWeight) = GetSampleWeight();
            float rng = rand.NextFloat();
            if (rng < diffuseWeight)
            {
                SampleBxdfResult result = GetDiffuseBrdf().Sample(wo, rand);
                if (result.Pdf == 0) { return new SampleBxdfResult(); }
                result.Fr = Fr(wo, result.Wi);
                result.Pdf = Pdf(wo, result.Wi);
                return result;
            }
            else
            {
                rng -= diffuseWeight;
                if (rng < specularWeight)
                {
                    SampleBxdfResult result = GetSpecularBrdf().Sample(wo, rand);
                    if (result.Pdf == 0) { return new SampleBxdfResult(); }
                    result.Fr = Fr(wo, result.Wi);
                    result.Pdf = Pdf(wo, result.Wi);
                    return result;
                }
                else
                {
                    SampleBxdfResult result = GetClearcoatBrdf().Sample(wo, rand);
                    if (result.Pdf == 0) { return new SampleBxdfResult(); }
                    result.Fr = Fr(wo, result.Wi);
                    result.Pdf = Pdf(wo, result.Wi);
                    return result;
                }
            }
        }

        public static float SchlickWeight(float cosTheta)
        {
            return Pow5(Math.Clamp(1 - cosTheta, 0, 1));
        }

        public static Color3F FrsnelSchlick(Color3F r0, float cosTheta)
        {
            return Lerp(SchlickWeight(cosTheta), r0, new Color3F(1.0f));
        }

        public static float FrsnelSchlick(float r0, float cosTheta)
        {
            return r0 + (1.0f - r0) * SchlickWeight(cosTheta);
        }

        public static float SchlickR0FromEta(float eta) { return Sqr(eta - 1) / Sqr(eta + 1); }

        public static float Gtr1Brdf(float cosTheta, float alpha)
        {
            float alpha2 = alpha * alpha;
            return (alpha2 - 1) / (PI * Log(alpha2) * (1 + (alpha2 - 1) * cosTheta * cosTheta));
        }

        public static float SmithG1GGX(float cosTheta, float alpha)
        {
            float alpha2 = alpha * alpha;
            float cosTheta2 = cosTheta * cosTheta;
            return 2 / (1 + Sqrt(1 + alpha2 * (1 - cosTheta2) / cosTheta2));
        }

        private DisneyRetroBrdf GetRetroBrdf()
        {
            return new DisneyRetroBrdf(DiffuseWeight * Color, Roughness);
        }

        private DisneySheenBrdf GetSheenBrdf(Color3F sheenColor)
        {
            return new DisneySheenBrdf(DiffuseWeight * SheenWeight * sheenColor);
        }

        private DisneyDiffuseBrdf GetDiffuseBrdf()
        {
            return new DisneyDiffuseBrdf(DiffuseWeight * Color);
        }

        private MicrofacetReflectionBrdf<DisneyFresnel, DisneyDistributionGtr2> GetSpecularBrdf()
        {
            var (ax, ay) = AnisAlpha;
            DisneyDistributionGtr2 dist = new DisneyDistributionGtr2(ax, ay);
            Color3F r0 = ColorTint * SchlickR0FromEta(Eta);
            DisneyFresnel fresnel = new DisneyFresnel(r0, Color, MetallicWeight, Eta, SpecularTint, SpeclarScale);
            return new MicrofacetReflectionBrdf<DisneyFresnel, DisneyDistributionGtr2>(new Color3F(1.0f), fresnel, dist);
        }

        private DisneyClearcoatBrdf GetClearcoatBrdf()
        {
            float gloss = Lerp(ClearcoatGloss, 0.1f, 0.001f);
            return new DisneyClearcoatBrdf(Clearcoat, gloss);
        }

        private (float, float, float) GetSampleWeight()
        {
            float a = Math.Clamp(Color.GetLuminance() * (1 - Metallic), 0.3f, 0.7f);
            float b = 1 - a;

            //float diff = a * (1 - Transmission);
            //float tran = a * Transmission;
            float diff = a;
            float spec = b * 2 / (2 + Clearcoat);
            float cc = b * Clearcoat / (2 + Clearcoat);
            return (diff, spec, cc);
        }
    }
}
