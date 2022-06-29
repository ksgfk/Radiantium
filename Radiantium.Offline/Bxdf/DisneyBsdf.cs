using Radiantium.Core;
using System.Numerics;
using static Radiantium.Core.Color3F;
using static Radiantium.Core.MathExt;
using static Radiantium.Offline.Coordinate;
using static System.MathF;
using static System.Numerics.Vector3;

namespace Radiantium.Offline.Bxdf
{
    //TODO: 不建议使用, 有很多未能解决的问题, 放弃了

    internal struct DisneyRetroBrdf : IBxdf
    {
        public Color3F R;
        public float Roughness;
        public BxdfType Type => BxdfType.Reflection | BxdfType.Diffuse;
        public DisneyRetroBrdf(Color3F r, float roughness) { R = r; Roughness = roughness; }
        public Color3F Fr(Vector3 wo, Vector3 wi, TransportMode mode)
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
        public float Pdf(Vector3 wo, Vector3 wi, TransportMode mode) { return DisneyBsdf.DiffusePdf(wo, wi); }
        public SampleBxdfResult Sample(Vector3 wo, Random rand, TransportMode mode) { return DisneyBsdf.DiffuseSample(wo, rand, R, Type); }
    }

    internal struct DisneySheenBrdf : IBxdf
    {
        public Color3F R;
        public BxdfType Type => BxdfType.Reflection | BxdfType.Diffuse;
        public DisneySheenBrdf(Color3F r) { R = r; }
        public Color3F Fr(Vector3 wo, Vector3 wi, TransportMode mode)
        {
            Vector3 wh = wo + wi;
            if (wh.X == 0 && wh.Y == 0 && wh.Z == 0) { return new Color3F(0); }
            wh = Normalize(wh);
            float cosThetaD = Dot(wi, wh);
            return R * DisneyBsdf.SchlickWeight(cosThetaD);
        }
        public float Pdf(Vector3 wo, Vector3 wi, TransportMode mode) { return DisneyBsdf.DiffusePdf(wo, wi); }
        public SampleBxdfResult Sample(Vector3 wo, Random rand, TransportMode mode) { return DisneyBsdf.DiffuseSample(wo, rand, R, Type); }
    }

    internal struct DisneyDiffuseBrdf : IBxdf
    {
        public Color3F R;
        public BxdfType Type => BxdfType.Reflection | BxdfType.Diffuse;
        public DisneyDiffuseBrdf(Color3F r) { R = r; }
        public Color3F Fr(Vector3 wo, Vector3 wi, TransportMode mode)
        {
            float fo = DisneyBsdf.SchlickWeight(AbsCosTheta(wo));
            float fi = DisneyBsdf.SchlickWeight(AbsCosTheta(wi));
            return R * (1 / PI) * (1 - fo / 2) * (1 - fi / 2);
        }
        public float Pdf(Vector3 wo, Vector3 wi, TransportMode mode) { return DisneyBsdf.DiffusePdf(wo, wi); }
        public SampleBxdfResult Sample(Vector3 wo, Random rand, TransportMode mode) { return DisneyBsdf.DiffuseSample(wo, rand, R, Type); }
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
        public Color3F Fr(Vector3 wo, Vector3 wi, TransportMode mode)
        {
            Vector3 wh = wo + wi;
            if (wh.X == 0 && wh.Y == 0 && wh.Z == 0) { return new Color3F(0); }
            wh = Normalize(wh);
            float d = DisneyBsdf.Gtr1Brdf(AbsCosTheta(wh), Gloss);
            float f = DisneyBsdf.FrsnelSchlick(0.04f, Dot(wo, wh));
            float g = DisneyBsdf.SmithG1GGX(AbsCosTheta(wo), 0.25f) * DisneyBsdf.SmithG1GGX(AbsCosTheta(wi), 0.25f);
            return new Color3F(Weight * d * f * g / 4);
        }
        public float Pdf(Vector3 wo, Vector3 wi, TransportMode mode)
        {
            if (!SameHemisphere(wo, wi)) { return 0; }
            Vector3 wh = wo + wi;
            if (wh.X == 0 && wh.Y == 0 && wh.Z == 0) { return 0; }
            wh = Normalize(wh);
            float d = DisneyBsdf.Gtr1Brdf(AbsCosTheta(wh), Gloss);
            return d * AbsCosTheta(wh) / (4 * Dot(wo, wh));
        }
        public SampleBxdfResult Sample(Vector3 wo, Random rand, TransportMode mode)
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
            Color3F fr = Fr(wo, wi, mode);
            float pdf = Pdf(wo, wi, mode);
            return new SampleBxdfResult(wi, fr, pdf, Type);
        }
    }

    internal struct DisneyFakeSubsurfaceBrdf : IBxdf
    {
        public Color3F R;
        public float Roughness;
        public BxdfType Type => BxdfType.Reflection | BxdfType.Diffuse;
        public DisneyFakeSubsurfaceBrdf(Color3F r, float roughness) { R = r; Roughness = roughness; }
        public Color3F Fr(Vector3 wo, Vector3 wi, TransportMode mode)
        {
            Vector3 wh = wo + wi;
            if (wh.X == 0 && wh.Y == 0 && wh.Z == 0) { return new Color3F(0); }
            wh = Normalize(wh);
            float cosThetaD = Dot(wi, wh);
            float fss90 = cosThetaD * cosThetaD * Roughness;
            float fo = DisneyBsdf.SchlickWeight(AbsCosTheta(wo));
            float fi = DisneyBsdf.SchlickWeight(AbsCosTheta(wi));
            float fss = Lerp(fo, 1.0f, fss90) * Lerp(fi, 1.0f, fss90);
            float ss = 1.25f * (fss * (1 / (AbsCosTheta(wo) + AbsCosTheta(wi)) - 0.5f) + 0.5f);
            return R * (1 / PI) * ss;
        }
        public float Pdf(Vector3 wo, Vector3 wi, TransportMode mode) { return DisneyBsdf.DiffusePdf(wo, wi); }
        public SampleBxdfResult Sample(Vector3 wo, Random rand, TransportMode mode) { return DisneyBsdf.DiffuseSample(wo, rand, R, Type); }
    }

    public struct DisneyBsdf : IBxdf
    {
        public Color3F Color;
        public Color3F ScattingDistance;
        public float Metallic;
        public float Eta;
        public float Roughness;
        public float SpecularTint;
        public float Anisotropic;
        public float Sheen;
        public float SheenTint;
        public float Clearcoat;
        public float ClearcoatGloss;
        public float SpecularScale;
        public float Transmission;
        public float TransmissionRoughness;
        public float Flatness;
        public bool IsThin;
        public BxdfType Type => BxdfType.Reflection | BxdfType.Transmission | BxdfType.Diffuse | BxdfType.Glossy | BxdfType.Specular | BxdfType.SubsurfaceScatting;
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

        public DisneyBsdf(
            Color3F color,
            float metallic,
            float eta,
            float roughness,
            float specularTint,
            float anisotropic,
            float sheen,
            float sheenTint,
            float clearcoat,
            float clearcoatGloss,
            float speclarScale,
            Color3F scattingDistance,
            bool isThin,
            float transmission,
            float transmissionRoughness,
            float flatness)
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
            SpecularScale = speclarScale;
            ScattingDistance = scattingDistance;
            IsThin = isThin;
            Transmission = transmission;
            TransmissionRoughness = transmissionRoughness;
            Flatness = flatness;
        }

        private Color3F FrImpl(Vector3 wo, Vector3 wi, TransportMode mode)
        {
            if (!SameHemisphere(wo, wi))
            {
                if (Transmission == 0) { return new Color3F(0.0f); }
                Color3F transmission = GetSpecularBtdf().Fr(wo, wi, mode);
                return transmission;
            }
            if (CosTheta(wo) < 0 && CosTheta(wi) < 0)
            {
                Color3F inner = GetInnerBrdf().Fr(wo, wi, mode);
                return inner;
            }
            Color3F diffuse = new Color3F(0.0f);
            if (DiffuseWeight > 0)
            {
                Color3F disneyDiffuse;
                if (IsThin)
                {
                    Color3F diff = GetDiffuseBrdf().Fr(wo, wi, mode) * (1 - Flatness);
                    Color3F fakeSS = GetFakeSSBrdf().Fr(wo, wi, mode) * Flatness;
                    disneyDiffuse = diff + fakeSS;
                }
                else
                {
                    disneyDiffuse = new Color3F(0.0f);
                    if (ScattingDistance == Black)
                    {
                        disneyDiffuse = GetDiffuseBrdf().Fr(wo, wi, mode);
                    }
                }
                Color3F retro = GetRetroBrdf().Fr(wo, wi, mode);
                Color3F sheen = new Color3F(0.0f);
                if (SheenWeight > 0)
                {
                    Color3F sheenColor = Lerp(SheenTint, new Color3F(1.0f), ColorTint);
                    sheen = GetSheenBrdf(sheenColor).Fr(wo, wi, mode);
                }
                diffuse = (retro + sheen + disneyDiffuse) * (1 - Transmission);
            }
            Color3F specular = GetSpecularBrdf().Fr(wo, wi, mode);
            Color3F clearcoat = new Color3F(0.0f);
            if (Clearcoat > 0)
            {
                clearcoat = GetClearcoatBrdf().Fr(wo, wi, mode);
            }
            return diffuse + specular + clearcoat;
        }

        public Color3F Fr(Vector3 wo, Vector3 wi, TransportMode mode)
        {
            return FrImpl(wo, wi, mode);
        }

        private float PdfImpl(Vector3 wo, Vector3 wi, TransportMode mode)
        {
            var (diffuseWeight, transmissionWeight, specularWeight, clearcoatWeight) = GetSampleWeight();
            if (CosTheta(wo) < 0)
            {
                if (Transmission == 0) { return 0; }
                float fresnel = Fresnel.DielectricFunc(CosTheta(wo), 1, Eta);
                fresnel = Math.Clamp(fresnel, 0.1f, 0.9f);
                if (CosTheta(wi) > 0)
                {
                    return GetSpecularBtdf().Pdf(wo, wi, mode) * (1 - fresnel);
                }
                else
                {
                    return GetInnerBrdf().Pdf(wo, wi, mode) * fresnel;
                }
            }
            if (CosTheta(wi) < 0)
            {
                return GetSpecularBtdf().Pdf(wo, wi, mode) * transmissionWeight;
            }
            float diffusePdf = 0;
            if (IsThin || ScattingDistance == Black)
            {
                diffusePdf = GetDiffuseBrdf().Pdf(wo, wi, mode);
            }
            float specularPdf = GetSpecularBrdf().Pdf(wo, wi, mode);
            float clearcoatPdf = GetClearcoatBrdf().Pdf(wo, wi, mode);
            return diffusePdf * diffuseWeight +
                specularPdf * specularWeight +
                clearcoatPdf * clearcoatWeight;
        }

        public float Pdf(Vector3 wo, Vector3 wi, TransportMode mode)
        {
            return PdfImpl(wo, wi, mode);
        }

        public SampleBxdfResult Sample(Vector3 wo, Random rand, TransportMode mode)
        {
            if (CosTheta(wo) < 0)
            {
                if (Transmission == 0) { return new SampleBxdfResult(); }
                float fresnel = Fresnel.DielectricFunc(CosTheta(wo), 1, Eta);
                fresnel = Math.Clamp(fresnel, 0.1f, 0.9f);
                if (rand.NextFloat() >= fresnel)
                {
                    SampleBxdfResult result = GetSpecularBtdf().Sample(wo, rand, mode);
                    if (result.Pdf == 0) { return new SampleBxdfResult(); }
                    result.Fr = Fr(wo, result.Wi, mode);
                    result.Pdf = Pdf(wo, result.Wi, mode);
                    return result;
                }
                else
                {
                    SampleBxdfResult result = GetInnerBrdf().Sample(wo, rand, mode);
                    if (result.Pdf == 0) { return new SampleBxdfResult(); }
                    result.Fr = Fr(wo, result.Wi, mode);
                    result.Pdf = Pdf(wo, result.Wi, mode);
                    return result;
                }
            }
            var (diffuseWeight, transmissionWeight, specularWeight, _) = GetSampleWeight();
            float rng = rand.NextFloat();
            if (rng < diffuseWeight)
            {
                if (IsThin || ScattingDistance == Black)
                {
                    SampleBxdfResult result = GetDiffuseBrdf().Sample(wo, rand, mode);
                    if (result.Pdf == 0) { return new SampleBxdfResult(); }
                    result.Fr = Fr(wo, result.Wi, mode);
                    result.Pdf = Pdf(wo, result.Wi, mode);
                    return result;
                }
                else
                {
                    SampleBxdfResult result = new SpecularTransmissionBtdf(new Color3F(1), 1, Eta).Sample(wo, rand, mode);
                    result.Pdf *= diffuseWeight;
                    result.Type |= BxdfType.SubsurfaceScatting;
                    return result;
                }
            }
            else
            {
                rng -= diffuseWeight;
                if (rng < transmissionWeight)
                {
                    SampleBxdfResult result = GetSpecularBtdf().Sample(wo, rand, mode);
                    if (result.Pdf == 0) { return new SampleBxdfResult(); }
                    result.Fr = Fr(wo, result.Wi, mode);
                    result.Pdf = Pdf(wo, result.Wi, mode);
                    return result;
                }
                else
                {
                    rng -= transmissionWeight;
                    if (rng < specularWeight)
                    {
                        SampleBxdfResult result = GetSpecularBrdf().Sample(wo, rand, mode);
                        if (result.Pdf == 0) { return new SampleBxdfResult(); }
                        result.Fr = Fr(wo, result.Wi, mode);
                        result.Pdf = Pdf(wo, result.Wi, mode);
                        return result;
                    }
                    else
                    {
                        SampleBxdfResult result = GetClearcoatBrdf().Sample(wo, rand, mode);
                        if (result.Pdf == 0) { return new SampleBxdfResult(); }
                        result.Fr = Fr(wo, result.Wi, mode);
                        result.Pdf = Pdf(wo, result.Wi, mode);
                        return result;
                    }
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

        public static float DiffusePdf(Vector3 wo, Vector3 wi) { return SameHemisphere(wo, wi) ? AbsCosTheta(wi) / PI : 0.0F; }

        public static SampleBxdfResult DiffuseSample(Vector3 wo, Random rand, Color3F r, BxdfType type)
        {
            Vector3 wi = Normalize(Probability.SquareToCosineHemisphere(rand.NextVec2()));
            if (wo.Z < 0) { wi.Z *= -1; }
            if (!SameHemisphere(wo, wi)) { return new SampleBxdfResult(); }
            float pdf = DiffusePdf(wo, wi);
            Color3F fr = r / PI;
            return new SampleBxdfResult(wi, fr, pdf, type);
        }

        private (float, float) AnisAlpha(float roughness)
        {
            float aspect = Sqrt(1 - Anisotropic * 0.9f);
            float ax = Max(0.001f, Sqr(roughness) / aspect);
            float ay = Max(0.001f, Sqr(roughness) * aspect);
            return (ax, ay);
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

        private DisneyFakeSubsurfaceBrdf GetFakeSSBrdf()
        {
            return new DisneyFakeSubsurfaceBrdf(DiffuseWeight * Color, Roughness);
        }

        private MicrofacetReflectionBrdf<DisneyFresnel, DisneyDistributionGtr2> GetSpecularBrdf()
        {
            var (ax, ay) = AnisAlpha(Roughness);
            DisneyDistributionGtr2 dist = new DisneyDistributionGtr2(ax, ay);
            Color3F r0 = ColorTint * SchlickR0FromEta(Eta);
            DisneyFresnel fresnel = new DisneyFresnel(r0, Color, MetallicWeight, Eta, SpecularTint, SpecularScale);
            return new MicrofacetReflectionBrdf<DisneyFresnel, DisneyDistributionGtr2>(new Color3F(1.0f), fresnel, dist);
        }

        private MicrofacetTransmissionBtdf<DisneyDistributionGtr2> GetSpecularBtdf()
        {
            var (ax, ay) = AnisAlpha(TransmissionRoughness);
            Color3F t = (1 - Metallic) * Transmission * Sqrt(Color);
            DisneyDistributionGtr2 dist = new DisneyDistributionGtr2(ax, ay);
            return new MicrofacetTransmissionBtdf<DisneyDistributionGtr2>(t, 1, Eta, dist);
        }

        private MicrofacetReflectionBrdf<Fresnel.Dielectric, DisneyDistributionGtr2> GetInnerBrdf()
        {
            Color3F t = Transmission * Color;
            var (ax, ay) = AnisAlpha(TransmissionRoughness);
            Fresnel.Dielectric fresnel = new Fresnel.Dielectric(1, Eta);
            DisneyDistributionGtr2 dist = new DisneyDistributionGtr2(ax, ay);
            return new MicrofacetReflectionBrdf<Fresnel.Dielectric, DisneyDistributionGtr2>(t, fresnel, dist);
        }

        private DisneyClearcoatBrdf GetClearcoatBrdf()
        {
            float gloss = Lerp(ClearcoatGloss, 0.1f, 0.001f);
            return new DisneyClearcoatBrdf(Clearcoat, gloss);
        }

        private (float, float, float, float) GetSampleWeight()
        {
            float a = Math.Clamp(Color.GetLuminance() * (1 - Metallic), 0.3f, 0.7f);
            float b = 1 - a;
            float diff = a * (1 - Transmission);
            float tran = a * Transmission;
            float spec = b * 2 / (2 + Clearcoat);
            float cc = b * Clearcoat / (2 + Clearcoat);
            return (diff, tran, spec, cc);
        }

        public SeparableBssrdf<NormalizedDiffusionRadialProfile> GetBssrdf(Intersection po)
        {
            NormalizedDiffusionRadialProfile func = new(Color * DiffuseWeight, ScattingDistance);
            return new SeparableBssrdf<NormalizedDiffusionRadialProfile>(po, Eta, func);
        }
    }
}
