using Radiantium.Core;
using System.Numerics;
using static Radiantium.Core.Color3F;
using static Radiantium.Core.MathExt;
using static Radiantium.Offline.Coordinate;
using static System.MathF;
using static System.Numerics.Vector3;

namespace Radiantium.Offline.Bxdf
{
    internal struct DisneyFresnel : IFresnel
    {
        public Color3F R0;
        public float Metallic, Eta;
        public DisneyFresnel(Color3F r0, float metallic, float eta) { R0 = r0; Metallic = metallic; Eta = eta; }
        public Color3F Eval(float cosI)
        {
            return Lerp(Metallic, new Color3F(Fresnel.DielectricFunc(cosI, 1, Eta)), DisneyBsdf.FrSchlick(R0, cosI));
        }
    }
    internal struct DisneyDistributionGTR2 : IMicrofacetDistribution
    {
        public float AlphaX { get; }
        public float AlphaY { get; }
        public DisneyDistributionGTR2(float alphaX, float alphaY) { AlphaX = alphaX; AlphaY = alphaY; }
        public float D(Vector3 wh) { return Microfacet.DistributionGGX(wh, AlphaX, AlphaY); }
        public float G1(Vector3 w) { return 1 / (1 + Lambda(w)); }
        public float G(Vector3 wo, Vector3 wi) { return G1(wo) * G1(wi); }
        public float Lambda(Vector3 w) { return Microfacet.LambdaGGX(w, AlphaX, AlphaY); }
        public float Pdf(Vector3 wo, Vector3 wh) { return D(wh) * AbsCosTheta(wh); }
        public Vector3 SampleWh(Vector3 wo, Random rand) { return Microfacet.SampleWhGGX(wo, rand, AlphaX, AlphaY); }
    }

    public struct DisneyBsdf : IBxdf
    {
        private struct SampleWeights
        {
            public float Diff;
            public float Spec;
            public float Cc;
            public float Tran;

            public SampleWeights(float diffuse, float specular, float clearcoat, float transmission)
            {
                Diff = diffuse;
                Spec = specular;
                Cc = clearcoat;
                Tran = transmission;
            }
        }

        SampleWeights SampleWeight;
        bool IsEnableDiff;
        bool IsEnableSpec;
        bool IsEnableCc;
        bool IsEnableTran;

        public Color3F BaseColor;
        public Color3F ColorTint;
        public float Metallic;
        public float Roughness;
        public Color3F specular_scale_;
        public float SpecularTint;
        public float Anisotropic;
        public float Sheen;
        public float SheenTint;
        public float Clearcoat;
        public float Transmission;
        public float Ior;
        public float TransmissionToughness;
        public float TransAnisX, TransAnisy;
        public float AnisX, AnisY;
        public float ClearcoatRoughness;
        public BxdfType Type => BxdfType.Reflection | BxdfType.Transmission | BxdfType.Diffuse | BxdfType.Glossy;

        public DisneyBsdf(
                   Color3F baseColor,
                   float metallic,
                   float roughness,
                   Color3F specular_scale,
                   float specularTint,
                   float anisotropic,
                   float sheen,
                   float sheenTint,
                   float clearcoat,
                   float clearcoat_gloss,
                   float transmission,
                   float transmissionRoughness,
                   float IOR)
        {
            BaseColor = baseColor;
            ColorTint = ToColorTint(baseColor);

            Metallic = metallic;
            Roughness = roughness;
            specular_scale_ = specular_scale;
            SpecularTint = specularTint;
            Anisotropic = anisotropic;
            Sheen = sheen;
            SheenTint = sheenTint;

            Transmission = transmission;
            TransmissionToughness = transmissionRoughness;
            Ior = Max(1.01f, IOR);

            float aspect = anisotropic > 0 ? Sqrt(1 - 0.9f * anisotropic) : 1;
            AnisX = Max(0.001f, Sqr(roughness) / aspect);
            AnisY = Max(0.001f, Sqr(roughness) * aspect);
            TransAnisX = Max(0.001f, Sqr(transmissionRoughness) / aspect);
            TransAnisy = Max(0.001f, Sqr(transmissionRoughness) * aspect);

            Clearcoat = clearcoat;
            ClearcoatRoughness = Lerp(clearcoat_gloss, 0.1f, 0);
            ClearcoatRoughness *= ClearcoatRoughness;
            ClearcoatRoughness = Max(ClearcoatRoughness, 0.0001f);

            float a = Math.Clamp(baseColor.GetLuminance() * (1 - Metallic), 0.3f, 0.7f);
            float b = 1 - a;

            //SampleWeight.Diff = a * (1 - Transmission);
            //SampleWeight.Tran = a * Transmission;
            //SampleWeight.Spec = b * 2 / (2 + Clearcoat);
            //SampleWeight.Cc = b * Clearcoat / (2 + Clearcoat);

            IsEnableDiff = true;
            IsEnableSpec = false;
            IsEnableCc = false;
            IsEnableTran = false;

            SampleWeight.Diff = 1;
            SampleWeight.Tran = 0;
            SampleWeight.Spec = 0;
            SampleWeight.Cc = 0;
        }

        public Color3F Fr(Vector3 wo, Vector3 wi)
        {
            if (!SameHemisphere(wo, wi))
            {
                return new Color3F(0.0f);
            }
            Color3F diffuse = new Color3F(0.0f);
            Color3F sheen = new Color3F(0.0f);
            if (Metallic < 1 && IsEnableDiff)
            {
                diffuse = FrDisneyDiffuse(wo, wi);
                if (Sheen > 0)
                {
                    sheen = FrSheen(wo, wi);
                }
            }
            return diffuse + sheen;

            //if (!SameHemisphere(wo, wi))
            //{
            //    if (Transmission == 0 || !IsEnableTran) { return new Color3F(0.0f); }
            //    return (1 - Metallic) * FrTransmission(wo, wi);
            //}
            //if (CosTheta(wo) < 0 && CosTheta(wi) < 0)
            //{
            //    if (Transmission == 0 || !IsEnableTran) { return new Color3F(0.0f); }
            //    return FrInnerReflection(wo, wi);
            //}
            //if (CosTheta(wo) <= 0 || CosTheta(wi) <= 0)
            //{
            //    return new Color3F(0.0f);
            //}
            //Color3F diffuse = new Color3F(0.0f);
            //Color3F sheen = new Color3F(0.0f);
            //if (Metallic < 1 && IsEnableDiff)
            //{
            //    diffuse = FrDisneyDiffuse(wo, wi);
            //    if (Sheen > 0)
            //    {
            //        sheen = FrSheen(wo, wi);
            //    }
            //}
            //Color3F specular = new Color3F(0.0f);
            //if (IsEnableSpec)
            //{
            //    specular = FrSpecular(wo, wi);
            //}
            //Color3F clearcoat = new Color3F(0.0f);
            //if (Clearcoat > 0 && IsEnableCc)
            //{
            //    clearcoat = FrClearcoat(wo, wi);
            //}
            //Color3F f = (1 - Metallic) * (1 - Transmission) * (diffuse + sheen) + specular + clearcoat;
            //return f;
        }

        public float Pdf(Vector3 wo, Vector3 wi)
        {
            if (!SameHemisphere(wo, wi))
            {
                return 0.0f;
            }
            return PdfDiffuse(wo, wi);

            //if (CosTheta(wo) == 0) { return 0.0f; }
            //if (CosTheta(wo) < 0)
            //{
            //    if (Transmission == 0 || !IsEnableTran) { return 0.0f; }
            //    float macroF = Fresnel.DielectricFunc(CosTheta(wo), 1, Ior);
            //    macroF = Math.Clamp(macroF, 0.1f, 0.9f);
            //    if (CosTheta(wi) > 0)
            //    {
            //        float transPdf = (1 - macroF) * PdfTransmission(wo, wi);
            //        return transPdf;
            //    }
            //    else
            //    {
            //        float innerReflPdf = macroF * PdfInnerReflect(wo, wi);
            //        return innerReflPdf;
            //    }
            //}
            //if (CosTheta(wi) < 0 && IsEnableTran)
            //{
            //    return SampleWeight.Tran * PdfTransmission(wo, wi);
            //}
            //if (CosTheta(wi) <= 0) { return 0.0f; }
            //float diffuse = IsEnableDiff ? PdfDiffuse(wo, wi) : 0;
            //float specular = IsEnableSpec ? PdfSpecular(wo, wi) : 0;
            //float clearcoat = IsEnableCc ? PdfClearcoat(wo, wi) : 0;
            //float result = SampleWeight.Diff * diffuse + SampleWeight.Spec * specular + SampleWeight.Cc * clearcoat;
            ////Console.WriteLine($"{result} {diffuse} {specular} {clearcoat}");
            //return result;
        }

        public SampleBxdfResult Sample(Vector3 wo, Random rand)
        {
            if (CosTheta(wo) <= 0)
            {
                return new SampleBxdfResult();
            }
            if (!SampleDiffuse(wo, rand, out Vector3 wi)) { return new SampleBxdfResult(); }
            Color3F fr = Fr(wo, wi);
            float pdf = Pdf(wo, wi);
            return new SampleBxdfResult(wi, fr, pdf, BxdfType.Reflection | BxdfType.Diffuse);

            //if (CosTheta(wo) == 0) { return new SampleBxdfResult(); }
            //if (CosTheta(wo) < 0)
            //{
            //    if (Transmission == 0 || !IsEnableTran) { return new SampleBxdfResult(); }
            //    float macroF = Fresnel.DielectricFunc(CosTheta(wo), 1, Ior);
            //    macroF = Math.Clamp(macroF, 0.1f, 0.9f);
            //    if (rand.NextFloat() >= macroF)
            //    {
            //        if (!SampleTransmission(wo, rand, out Vector3 wi)) { return new SampleBxdfResult(); }
            //        Color3F fr = Fr(wo, wi);
            //        float pdf = Pdf(wo, wi);
            //        return new SampleBxdfResult(wi, fr, pdf, BxdfType.Transmission | BxdfType.Glossy);
            //    }
            //    else
            //    {
            //        if (!SampleInnerReflect(wo, rand, out Vector3 wi)) { return new SampleBxdfResult(); }
            //        Color3F fr = Fr(wo, wi);
            //        float pdf = Pdf(wo, wi);
            //        return new SampleBxdfResult(wi, fr, pdf, BxdfType.Reflection | BxdfType.Glossy);
            //    }
            //}
            //else
            //{
            //    float sam_selector = rand.NextFloat();
            //    if (sam_selector < SampleWeight.Diff)
            //    {
            //        if (!IsEnableDiff || !SampleDiffuse(wo, rand, out Vector3 wi)) { return new SampleBxdfResult(); }
            //        Color3F fr = Fr(wo, wi);
            //        float pdf = Pdf(wo, wi);
            //        Color3F li = fr * AbsCosTheta(wi) / pdf;
            //        //if (li.R >= 1 || li.G >= 1 || li.B >= 1)
            //        //{
            //        //    Console.WriteLine($"{li}");
            //        //}
            //        return new SampleBxdfResult(wi, fr, pdf, BxdfType.Reflection | BxdfType.Diffuse);
            //    }
            //    else
            //    {
            //        sam_selector -= SampleWeight.Diff;
            //        if (sam_selector < SampleWeight.Tran)
            //        {
            //            if (!IsEnableTran || !SampleTransmission(wo, rand, out Vector3 wi)) { return new SampleBxdfResult(); }
            //            Color3F fr = Fr(wo, wi);
            //            float pdf = Pdf(wo, wi);
            //            return new SampleBxdfResult(wi, fr, pdf, BxdfType.Transmission | BxdfType.Glossy);
            //        }
            //        else
            //        {
            //            sam_selector -= SampleWeight.Tran;
            //            if (sam_selector < SampleWeight.Spec)
            //            {
            //                if (!IsEnableSpec || !SampleSpecular(wo, rand, out Vector3 wi))
            //                {
            //                    return new SampleBxdfResult();
            //                }
            //                if (CosTheta(wi) <= 0)
            //                {
            //                    return new SampleBxdfResult();
            //                }
            //                Color3F fr = Fr(wo, wi);
            //                float pdf = Pdf(wo, wi);
            //                return new SampleBxdfResult(wi, fr, pdf, BxdfType.Reflection | BxdfType.Glossy);
            //            }
            //            else
            //            {
            //                if (!IsEnableCc || !SampleClearcoat(wo, rand, out Vector3 wi))
            //                {
            //                    return new SampleBxdfResult();
            //                }
            //                Color3F fr = Fr(wo, wi);
            //                float pdf = Pdf(wo, wi);
            //                return new SampleBxdfResult(wi, fr, pdf, BxdfType.Reflection | BxdfType.Glossy);
            //            }
            //        }
            //    }
            //}
        }

        private Color3F FrDisneyDiffuse(Vector3 wo, Vector3 wi)
        {
            Vector3 wh = Normalize(wo + wi);
            float cosThetaD = Dot(wh, wi);
            Color3F lambert = BaseColor / PI;
            float fi = SchlickWeight(AbsCosTheta(wi));
            float fo = SchlickWeight(AbsCosTheta(wo));
            Color3F diffuse = lambert * (1 - fo / 2) * (1 - fi / 2);
            float rr = 2 * Roughness * Sqr(cosThetaD);
            Color3F retro = lambert * rr * (fi + fo + fi * fo * (rr - 1));
            Color3F result = diffuse + retro;
            return result;
        }

        private Color3F FrSheen(Vector3 wo, Vector3 wi)
        {
            Vector3 wh = Normalize(wo + wi);
            float cosThetaD = Dot(wh, wi);
            return Sheen * Lerp(SheenTint, new Color3F(1.0f), ColorTint) * SchlickWeight(cosThetaD);
        }

        private Color3F FrSpecular(Vector3 wo, Vector3 wi)
        {
            Color3F cSpec0 = Lerp(Metallic, SchlickR0FromEta(Ior) * Lerp(SpecularTint, new Color3F(1.0f), ColorTint), BaseColor);
            DisneyFresnel fresnel = new(cSpec0, Metallic, Ior);
            DisneyDistributionGTR2 dist = new(AnisX, AnisY);
            return new MicrofacetReflectionBrdf<DisneyFresnel, DisneyDistributionGTR2>(new Color3F(1.0f), fresnel, dist).Fr(wo, wi);
        }

        private Color3F FrClearcoat(Vector3 wo, Vector3 wi)
        {
            Vector3 wh = wo + wi;
            if (wh.X == 0 && wh.Y == 0 && wh.Z == 0) { return new Color3F(0.0f); }
            wh = Normalize(wh);
            float d = Gtr1(AbsCosTheta(wh), Lerp(ClearcoatRoughness, 0.1f, 0.001f));
            float f = FrSchlick(0.04f, Dot(wo, wh));
            float g = SmithG1GGX(AbsCosTheta(wo), 0.25f) * SmithG1GGX(AbsCosTheta(wi), 0.25f);
            return new Color3F(Clearcoat * d * f * g / 4);
        }

        private Color3F FrTransmission(Vector3 wo, Vector3 wi)
        {
            Color3F t = Transmission * Sqrt(BaseColor);
            DisneyDistributionGTR2 dist = new(TransAnisX, TransAnisy);
            return new MicrofacetTransmissionBtdf<DisneyDistributionGTR2>(t, 1, Ior, dist).Fr(wo, wi);
        }

        private Color3F FrInnerReflection(Vector3 wo, Vector3 wi)
        {
            Color3F r = Transmission * BaseColor;
            Fresnel.Dielectric fresnel = new(1, Ior);
            DisneyDistributionGTR2 dist = new(TransAnisX, TransAnisy);
            return new MicrofacetReflectionBrdf<Fresnel.Dielectric, DisneyDistributionGTR2>(r, fresnel, dist).Fr(wo, wi);
        }

        private bool SampleDiffuse(Vector3 wo, Random rand, out Vector3 wi)
        {
            wi = Normalize(Probability.SquareToCosineHemisphere(rand.NextVec2()));
            if (wo.Z < 0)
            {
                wi.Z *= -1;
            }
            return SameHemisphere(wo, wi);
        }

        private bool SampleSpecular(Vector3 wo, Random rand, out Vector3 wi)
        {
            Color3F cSpec0 = Lerp(Metallic, SchlickR0FromEta(Ior) * Lerp(SpecularTint, new Color3F(1.0f), ColorTint), BaseColor);
            DisneyFresnel fresnel = new(cSpec0, Metallic, Ior);
            DisneyDistributionGTR2 dist = new(AnisX, AnisY);
            SampleBxdfResult sample = new MicrofacetReflectionBrdf<DisneyFresnel, DisneyDistributionGTR2>(new Color3F(1.0f), fresnel, dist).Sample(wo, rand);
            if (sample.Pdf == 0) { wi = default; return false; }
            wi = sample.Wi;
            return true;
        }

        private bool SampleClearcoat(Vector3 wo, Random rand, out Vector3 wi)
        {
            Vector2 sample = rand.NextVec2();
            float phi = 2 * PI * sample.X;
            float alpha2 = ClearcoatRoughness * ClearcoatRoughness;
            float cosTheta = Sqrt(Max(0, (1 - Pow(alpha2, 1 - sample.Y)) / (1 - alpha2)));
            float sinTheta = Sqrt(Max(0, 1 - cosTheta * cosTheta));
            Vector3 wh = SphericalDirection(sinTheta, cosTheta, phi);
            if (!SameHemisphere(wo, wh)) { wh = -wh; }
            wi = Reflect(-wo, wh);
            return SameHemisphere(wo, wi);
        }

        private bool SampleTransmission(Vector3 wo, Random rand, out Vector3 wi)
        {
            Color3F t = Transmission * Sqrt(BaseColor);
            DisneyDistributionGTR2 dist = new(TransAnisX, TransAnisy);
            SampleBxdfResult sample = new MicrofacetTransmissionBtdf<DisneyDistributionGTR2>(t, 1, Ior, dist).Sample(wo, rand);
            wi = sample.Wi;
            return sample.Pdf != 0;
        }

        private bool SampleInnerReflect(Vector3 wo, Random rand, out Vector3 wi)
        {
            Color3F r = Transmission * BaseColor;
            Fresnel.Dielectric fresnel = new(1, Ior);
            DisneyDistributionGTR2 dist = new(TransAnisX, TransAnisy);
            SampleBxdfResult sample = new MicrofacetReflectionBrdf<Fresnel.Dielectric, DisneyDistributionGTR2>(r, fresnel, dist).Sample(wo, rand);
            wi = sample.Wi;
            return sample.Pdf != 0;
        }

        private float PdfTransmission(Vector3 wo, Vector3 wi)
        {
            Color3F t = Transmission * Sqrt(BaseColor);
            DisneyDistributionGTR2 dist = new(TransAnisX, TransAnisy);
            return new MicrofacetTransmissionBtdf<DisneyDistributionGTR2>(t, 1, Ior, dist).Pdf(wo, wi);
        }

        private float PdfInnerReflect(Vector3 wo, Vector3 wi)
        {
            Color3F r = Transmission * BaseColor;
            Fresnel.Dielectric fresnel = new(1, Ior);
            DisneyDistributionGTR2 dist = new(TransAnisX, TransAnisy);
            return new MicrofacetReflectionBrdf<Fresnel.Dielectric, DisneyDistributionGTR2>(r, fresnel, dist).Pdf(wo, wi);
        }

        private float PdfDiffuse(Vector3 wo, Vector3 wi)
        {
            return SameHemisphere(wo, wi) ? AbsCosTheta(wi) * (1 / PI) : 0;
        }

        private float PdfSpecular(Vector3 wo, Vector3 wi)
        {
            Color3F cSpec0 = Lerp(Metallic, SchlickR0FromEta(Ior) * Lerp(SpecularTint, new Color3F(1.0f), ColorTint), BaseColor);
            DisneyFresnel fresnel = new(cSpec0, Metallic, Ior);
            DisneyDistributionGTR2 dist = new(AnisX, AnisY);
            return new MicrofacetReflectionBrdf<DisneyFresnel, DisneyDistributionGTR2>(new Color3F(1.0f), fresnel, dist).Pdf(wo, wi);
        }

        private float PdfClearcoat(Vector3 wo, Vector3 wi)
        {
            if (!SameHemisphere(wo, wi)) { return 0; }
            Vector3 wh = Normalize(wo + wi);
            float d = Gtr1(AbsCosTheta(wh), Lerp(ClearcoatRoughness, 0.1f, 0.001f));
            return d * AbsCosTheta(wh) / (4 * Dot(wo, wh));
        }

        private static float SchlickWeight(float cosTheta)
        {
            return Pow5(Math.Clamp(1 - cosTheta, 0, 1));
        }

        internal static Color3F FrSchlick(Color3F r0, float cosTheta)
        {
            return Lerp(SchlickWeight(cosTheta), r0, new Color3F(1.0f));
        }

        private static float FrSchlick(float r0, float cosTheta)
        {
            return Lerp(SchlickWeight(cosTheta), r0, 1);
        }

        private static float SchlickR0FromEta(float eta)
        {
            return Sqr(eta - 1) / Sqr(eta + 1);
        }

        private static float Gtr1(float cosTheta, float alpha)
        {
            float alpha2 = alpha * alpha;
            return (alpha2 - 1) / (PI * Log(alpha2) * (1 + (alpha2 - 1) * cosTheta * cosTheta));
        }

        private static float SmithG1GGX(float cosTheta, float alpha)
        {
            float alpha2 = alpha * alpha;
            float cosTheta2 = cosTheta * cosTheta;
            return 1 / (cosTheta + Sqrt(alpha2 + cosTheta2 - alpha2 * cosTheta2));
        }

        public static Color3F ToColorTint(Color3F baseColor)
        {
            float lum = baseColor.GetLuminance();
            return lum > 0 ? baseColor / lum : new Color3F(1);
        }
    }
}
