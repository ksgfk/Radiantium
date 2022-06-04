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
            public float diffuse;
            public float specular;
            public float clearcoat;
            public float transmission;

            public SampleWeights(float diffuse, float specular, float clearcoat, float transmission)
            {
                this.diffuse = diffuse;
                this.specular = specular;
                this.clearcoat = clearcoat;
                this.transmission = transmission;
            }
        }

        public Color3F BaseColor;
        public Color3F ColorTint;
        public float Metallic;
        public float Roughness;
        public Color3F specular_scale_;
        public float SpecularTint;
        public float anisotropic_;
        public float Sheen;
        public float SheenTint;
        public float clearcoat_;
        public float transmission_;
        public float Ior;
        public float transmission_roughness_;
        public float trans_ax_, trans_ay_;
        public float AnisX, AnisY;
        public float clearcoat_roughness_;
        SampleWeights sample_w;
        public BxdfType Type => BxdfType.Reflection | BxdfType.Transmission | BxdfType.Diffuse | BxdfType.Glossy;

        public DisneyBsdf(
                   Color3F base_color,
                   float metallic,
                   float roughness,
                   Color3F specular_scale,
                   float specular_tint,
                   float anisotropic,
                   float sheen,
                   float sheen_tint,
                   float clearcoat,
                   float clearcoat_gloss,
                   float transmission,
                   float transmission_roughness,
                   float IOR)
        {
            BaseColor = base_color;
            ColorTint = to_tint(base_color);

            Metallic = metallic;
            Roughness = roughness;
            specular_scale_ = specular_scale;
            SpecularTint = specular_tint;
            anisotropic_ = anisotropic;
            Sheen = sheen;
            SheenTint = sheen_tint;

            transmission_ = transmission;
            transmission_roughness_ = transmission_roughness;
            Ior = Max(1.01f, IOR);

            float aspect = anisotropic > 0 ? Sqrt(1 - 0.9f * anisotropic) : 1;
            AnisX = Max(0.001f, Sqr(roughness) / aspect);
            AnisY = Max(0.001f, Sqr(roughness) * aspect);
            trans_ax_ = Max(0.001f, Sqr(transmission_roughness) / aspect);
            trans_ay_ = Max(0.001f, Sqr(transmission_roughness) * aspect);

            clearcoat_ = clearcoat;
            clearcoat_roughness_ = Lerp(clearcoat_gloss, 0.1f, 0);
            clearcoat_roughness_ *= clearcoat_roughness_;
            clearcoat_roughness_ = Max(clearcoat_roughness_, 0.0001f);

            float A = Math.Clamp(base_color.GetLuminance() * (1 - Metallic), 0.3f, 0.7f);
            float B = 1 - A;

            sample_w.diffuse = A * (1 - transmission_);
            sample_w.transmission = A * transmission_;
            sample_w.specular = B * 2 / (2 + clearcoat_);
            sample_w.clearcoat = B * clearcoat_ / (2 + clearcoat_);
        }

        public Color3F Fr(Vector3 wo, Vector3 wi)
        {
            if (CosTheta(wo) <= 0 || CosTheta(wi) <= 0)
            {
                return new Color3F(0.0f);
            }
            Color3F diffuse = new Color3F(0.0f);
            Color3F sheen = new Color3F(0.0f);
            if (Metallic < 1)
            {
                diffuse = FrDisneyDiffuse(wo, wi);
                if (Sheen > 0)
                {
                    sheen = FrSheen(wo, wi);
                }
            }
            Color3F specular = FrSpecular(wo, wi);
            Color3F clearcoat = new Color3F(0.0f);
            //if (CosTheta(lwo) * CosTheta(lwi) < 0)
            //{
            //    if (transmission_ == 0) { return new Color3F(0.0f); }
            //    return TransmissionF(lwo, lwi);
            //}
            //if (CosTheta(lwo) < 0 && CosTheta(lwi) < 0)
            //{
            //    if (transmission_ == 0) { return new Color3F(0.0f); }
            //    return InnerReflectF(lwo, lwi);
            //}
            //if (CosTheta(lwo) <= 0 || CosTheta(lwi) <= 0)
            //{
            //    return new Color3F(0.0f);
            //}
            //float cos_theta_i = CosTheta(lwi);
            //float cos_theta_o = CosTheta(lwo);
            //Vector3 lwh = Normalize(lwo + lwi);
            //float cos_theta_d = Dot(lwi, lwh);

            //Color3F diffuse = new Color3F(0.0f);
            //Color3F sheen = new Color3F(0.0f);
            //if (metallic_ < 1)
            //{
            //    diffuse = DiffuseF(cos_theta_i, cos_theta_o, cos_theta_d);
            //    if (sheen_ > 0)
            //    {
            //        sheen = SheenF(cos_theta_d);
            //    }
            //}

            //Color3F specular = SpecularF(lwo, lwi);

            //Color3F clearcoat = new Color3F(0.0f);
            //if (clearcoat_ > 0)
            //{
            //    float tan_theta_i = TanTheta(lwi);
            //    float tan_theta_o = TanTheta(lwo);
            //    float cos_theta_h = CosTheta(lwh);
            //    float sin_theta_h = SinTheta(lwh);
            //    clearcoat = ClearcoatF(cos_theta_i, cos_theta_o, tan_theta_i, tan_theta_o, sin_theta_h, cos_theta_h, cos_theta_d);
            //}

            Color3F f = (1 - Metallic) * (1 - transmission_) * (diffuse + sheen) + specular + clearcoat;
            return f;
        }

        public float Pdf(Vector3 wo, Vector3 wi)
        {
            if (CosTheta(wo) < 0)
            {
                return 0.0f;
            }
            if (CosTheta(wi) < 0)
            {
                return 0.0f;
            }
            float diffuse = PdfDiffuse(wo, wi);
            float specular = PdfSpecular(wo, wi);
            return 0.5f * (diffuse + specular);

            //if (CosTheta(lwo) < 0)
            //{
            //    if (transmission_ == 0) { return 0.0f; }
            //    float macro_F = Fresnel.DielectricFunc(CosTheta(lwo), 1, IOR_);
            //    macro_F = Math.Clamp(macro_F, 0.1f, 0.9f);
            //    if (CosTheta(lwi) > 0)
            //    {
            //        float transPdf = (1 - macro_F) * PdfTransmission(lwo, lwi);
            //        return transPdf;
            //    }
            //    float innerReflPdf = macro_F * PdfInnerReflect(lwo, lwi);
            //    return innerReflPdf;
            //}
            //if (CosTheta(lwi) < 0)
            //{
            //    return sample_w.transmission * PdfTransmission(lwo, lwi);
            //}
            //float diffuse = PdfDiffuse(lwo, lwi);
            //(float specular, float clearcoat) = PdfSpecularClearcoat(lwo, lwi);
            //return sample_w.diffuse * diffuse + sample_w.specular * specular + sample_w.clearcoat * clearcoat;
        }

        public SampleBxdfResult Sample(Vector3 wo, Random rand)
        {
            if (CosTheta(wo) < 0)
            {
                return new SampleBxdfResult(); //TODO: transmission
            }
            else
            {
                float rng = rand.NextFloat();
                if (rng < 0.5f)
                {
                    Vector3 wi = SampleDiffuse(rand);
                    Color3F fr = Fr(wo, wi);
                    float pdf = Pdf(wo, wi);
                    return new SampleBxdfResult(wi, fr, pdf, BxdfType.Reflection | BxdfType.Diffuse);
                }
                else
                {
                    if (!SampleSpecular(wo, rand, out Vector3 wi))
                    {
                        return new SampleBxdfResult();
                    }
                    Color3F fr = Fr(wo, wi);
                    float pdf = Pdf(wo, wi);
                    return new SampleBxdfResult(wi, fr, pdf, BxdfType.Reflection | BxdfType.Glossy);
                }
            }

            //if (CosTheta(lwo) == 0) { return new SampleBxdfResult(); }
            //if (CosTheta(lwo) < 0)
            //{
            //    if (transmission_ == 0) { return new SampleBxdfResult(); }
            //    float macro_F = Fresnel.DielectricFunc(CosTheta(lwo), 1, IOR_);
            //    macro_F = Math.Clamp(macro_F, 0.1f, 0.9f);
            //    Vector3 lwi;
            //    BxdfType type;
            //    if (rand.NextFloat() >= macro_F)
            //    {
            //        lwi = SampleTransmission(lwo, rand);
            //        type = BxdfType.Transmission | BxdfType.Glossy;
            //    }
            //    else
            //    {
            //        lwi = SampleInnerReflect(lwo, rand);
            //        type = BxdfType.Reflection | BxdfType.Glossy;
            //    }
            //    if (lwi == new Vector3(0.0f)) { return new SampleBxdfResult(); }
            //    Color3F f = Fr(lwo, lwi);
            //    float pdf = Pdf(lwo, lwi);
            //    return new SampleBxdfResult(lwi, f, pdf, type);
            //}
            //else
            //{
            //    Vector3 lwi;
            //    BxdfType type;
            //    float sam_selector = rand.NextFloat();
            //    if (sam_selector < sample_w.diffuse)
            //    {
            //        lwi = SampleDiffuse(rand);
            //        type = BxdfType.Diffuse | BxdfType.Reflection;
            //    }
            //    else
            //    {
            //        sam_selector -= sample_w.diffuse;
            //        if (sam_selector < sample_w.transmission)
            //        {
            //            lwi = SampleTransmission(lwo, rand);
            //            type = BxdfType.Glossy | BxdfType.Transmission;
            //        }
            //        else
            //        {
            //            sam_selector -= sample_w.transmission;
            //            if (sam_selector < sample_w.specular)
            //            {
            //                lwi = SampleSpecular(lwo, rand);
            //                type = BxdfType.Glossy | BxdfType.Reflection;
            //            }
            //            else
            //            {
            //                lwi = SampleClearcoat(lwo, rand);
            //                type = BxdfType.Glossy | BxdfType.Reflection;
            //            }
            //        }
            //    }
            //    if (lwi == new Vector3(0.0f)) { return new SampleBxdfResult(); }
            //    Color3F f = Fr(lwo, lwi);
            //    float pdf = Pdf(lwo, lwi);
            //    return new SampleBxdfResult(lwi, f, pdf, type);
            //}
        }

        private Color3F FrDisneyDiffuse(Vector3 wo, Vector3 wi)
        {
            Vector3 wh = Normalize(wo + wi);
            float cosThetaD = Dot(wh, wi);
            Color3F lambert = BaseColor / PI;
            float fL = SchlickWeight(CosTheta(wi));
            float fV = SchlickWeight(CosTheta(wo));
            float rr = 2 * Roughness * Sqr(cosThetaD);
            Color3F retroReflection = BaseColor / PI * rr * (fL + fV + fL * fV * (rr - 1));
            return lambert * (1 - 0.5f * fL) * (1 - 0.5f * fV) + retroReflection;
        }

        private Color3F FrSheen(Vector3 wo, Vector3 wi)
        {
            Vector3 wh = Normalize(wo + wi);
            float cosThetaD = Dot(wh, wi);
            return Sheen * Lerp(SheenTint, new Color3F(1.0f), ColorTint) * SchlickWeight(cosThetaD);
        }

        private Color3F FrSpecular(Vector3 wo, Vector3 wi)
        {
            //Color3F cSpec0 = Lerp(Metallic, SchlickR0FromEta(Ior) * Lerp(SpecularTint, new Color3F(1.0f), ColorTint), BaseColor);
            //DisneyFresnel fresnel = new(cSpec0, Metallic, Ior);
            Fresnel.Dielectric fresnel = new Fresnel.Dielectric(1, Ior);
            DisneyDistributionGTR2 dist = new(AnisX, AnisY);
            return new MicrofacetReflectionBrdf<Fresnel.Dielectric, DisneyDistributionGTR2>(new Color3F(1.0f), fresnel, dist).Fr(wo, wi);
        }

        private Color3F TransmissionF(Vector3 lwo, Vector3 lwi)
        {
            float cos_theta_i = CosTheta(lwi);
            float cos_theta_o = CosTheta(lwo);
            float eta = cos_theta_o > 0 ? Ior : 1 / Ior;
            Vector3 lwh = Normalize(lwo + eta * lwi);
            if (lwh.Z < 0) { lwh = -lwh; }

            float cos_theta_d = Dot(lwo, lwh);
            float F = Fresnel.DielectricFunc(cos_theta_d, 1, Ior);

            float sin_phi_h = SinPhi(lwh);
            float cos_phi_h = CosPhi(lwh);
            float sin_theta_h = SinTheta(lwh);
            float cos_theta_h = CosTheta(lwh);
            float D = anisotropic_gtr2(sin_phi_h, cos_phi_h, sin_theta_h, cos_theta_h, trans_ax_, trans_ay_);

            float sin_phi_i = SinPhi(lwi);
            float cos_phi_i = CosPhi(lwi);
            float tan_theta_i = TanTheta(lwi);
            float sin_phi_o = SinPhi(lwo);
            float cos_phi_o = CosPhi(lwo);
            float tan_theta_o = TanTheta(lwo);
            float Gi = smith_anisotropic_gtr2(cos_phi_i, sin_phi_i, trans_ax_, trans_ay_, tan_theta_i);
            float Go = smith_anisotropic_gtr2(cos_phi_o, sin_phi_o, trans_ax_, trans_ay_, tan_theta_o);
            float G = Gi * Go;

            float SqrtDenom = cos_theta_d + eta * Dot(lwi, lwh);
            float factor = 1 / eta;

            Color3F SqrtC = Sqrt(BaseColor);

            float f = (1 - F) * D * G * eta * eta * Dot(lwi, lwh) * Dot(lwo, lwh) * factor * factor
                / (cos_theta_i * cos_theta_o * SqrtDenom * SqrtDenom);
            float trans_factor = cos_theta_o > 0 ? transmission_ : 1;
            return (1 - Metallic) * trans_factor * SqrtC * Abs(f);
        }

        private Color3F InnerReflectF(Vector3 lwo, Vector3 lwi)
        {
            Vector3 lwh = Normalize(-(lwo + lwi));

            float cos_theta_d = Dot(lwo, lwh);
            float F = Fresnel.DielectricFunc(cos_theta_d, 1, Ior);

            float sin_phi_h = SinPhi(lwh);
            float cos_phi_h = CosPhi(lwh);
            float sin_theta_h = SinTheta(lwh);
            float cos_theta_h = CosTheta(lwh);
            float D = anisotropic_gtr2(sin_phi_h, cos_phi_h, sin_theta_h, cos_theta_h, trans_ax_, trans_ay_);

            float sin_phi_i = SinPhi(lwi);
            float cos_phi_i = CosPhi(lwi);
            float tan_theta_i = TanTheta(lwi);
            float sin_phi_o = SinPhi(lwo);
            float cos_phi_o = CosPhi(lwo);
            float tan_theta_o = TanTheta(lwo);
            float Gi = smith_anisotropic_gtr2(cos_phi_i, sin_phi_i, trans_ax_, trans_ay_, tan_theta_i);
            float Go = smith_anisotropic_gtr2(cos_phi_o, sin_phi_o, trans_ax_, trans_ay_, tan_theta_o);
            float G = Gi * Go;

            float f = F * D * G / (4 * CosTheta(lwi) * CosTheta(lwo));

            return transmission_ * BaseColor * Abs(f);
        }

        private Color3F DiffuseF(float cos_theta_i, float cos_theta_o, float cos_theta_d)
        {
            Color3F f_lambert = BaseColor / PI;
            float FL = one_minus_5(cos_theta_i);
            float FV = one_minus_5(cos_theta_o);
            float RR = 2 * Roughness * cos_theta_d * cos_theta_d;
            Color3F F_retro_refl = BaseColor / PI * RR * (FL + FV + FL * FV * (RR - 1));
            return f_lambert * (1 - 0.5f * FL) * (1 - 0.5f * FV) + F_retro_refl;
        }

        private Color3F SheenF(float cos_theta_d)
        {
            return 4 * Sheen * Lerp(SheenTint, new Color3F(1.0f), ColorTint) * one_minus_5(cos_theta_d);
        }

        private Color3F SpecularF(Vector3 lwo, Vector3 lwi)
        {
            float cos_theta_i = CosTheta(lwi);
            float cos_theta_o = CosTheta(lwo);
            Vector3 lwh = Normalize(lwo + lwi);
            float cos_theta_d = Dot(lwi, lwh);

            Color3F Cspec = Lerp(Metallic, Lerp(SpecularTint, new Color3F(1.0f), ColorTint), BaseColor);

            Color3F dielectric_fresnel = Cspec * Fresnel.DielectricFunc(cos_theta_d, 1, Ior);
            Color3F conductor_fresnel = schlick(Cspec, cos_theta_d);
            Color3F F = Lerp(Metallic, specular_scale_ * dielectric_fresnel, conductor_fresnel);

            float sin_phi_h = SinPhi(lwh);
            float cos_phi_h = CosPhi(lwh);
            float sin_theta_h = SinTheta(lwh);
            float cos_theta_h = CosTheta(lwh);
            float D = anisotropic_gtr2(sin_phi_h, cos_phi_h, sin_theta_h, cos_theta_h, trans_ax_, trans_ay_);

            float sin_phi_i = SinPhi(lwi);
            float cos_phi_i = CosPhi(lwi);
            float tan_theta_i = TanTheta(lwi);
            float sin_phi_o = SinPhi(lwo);
            float cos_phi_o = CosPhi(lwo);
            float tan_theta_o = TanTheta(lwo);
            float Gi = smith_anisotropic_gtr2(cos_phi_i, sin_phi_i, trans_ax_, trans_ay_, tan_theta_i);
            float Go = smith_anisotropic_gtr2(cos_phi_o, sin_phi_o, trans_ax_, trans_ay_, tan_theta_o);
            float G = Gi * Go;

            return F * D * G / Abs(4 * cos_theta_i * cos_theta_o);
        }

        private Color3F ClearcoatF(float cos_theta_i, float cos_theta_o, float tan_theta_i, float tan_theta_o, float sin_theta_h, float cos_theta_h, float cos_theta_d)
        {
            float D = gtr1(sin_theta_h, cos_theta_h, clearcoat_roughness_);
            float F = schlick(0.04f, cos_theta_d);
            float G = smith_gtr2(tan_theta_i, 0.25f) * smith_gtr2(tan_theta_o, 0.25f);
            return new Color3F(clearcoat_ * D * F * G / Abs(4 * cos_theta_i * cos_theta_o));
        }

        private Vector3 SampleTransmission(Vector3 lwo, Random rand)
        {
            Vector3 lwh = sample_anisotropic_gtr2(trans_ax_, trans_ay_, rand.NextVec2());
            if (lwh.Z <= 0)
            {
                return new Vector3(0.0f);
            }
            if ((CosTheta(lwo) > 0) != (Dot(lwh, lwo) > 0))
            {
                return new Vector3(0.0f);
            }
            float eta = (CosTheta(lwo)) > 0 ? 1 / Ior : Ior;
            Vector3 owh = Dot(lwh, lwo) > 0 ? lwh : -lwh;
            if (!Refract(lwo, owh, eta, out Vector3 lwi))
            {
                return new Vector3(0.0f);
            }
            if (lwi.Z * lwo.Z > 0 || ((lwi.Z > 0) != (Dot(lwh, lwi) > 0)))
            {
                return new Vector3(0.0f);
            }
            return lwi;
        }

        private Vector3 SampleInnerReflect(Vector3 lwo, Random rand)
        {
            Vector3 lwh = sample_anisotropic_gtr2(trans_ax_, trans_ay_, rand.NextVec2());
            if (lwh.Z <= 0)
            {
                return new Vector3(0.0f);
            }
            Vector3 lwi = 2 * Dot(lwo, lwh) * lwh - lwo;
            if (lwi.Z > 0)
            {
                return new Vector3(0.0f);
            }
            return Normalize(lwi);
        }

        private Vector3 SampleDiffuse(Random rand)
        {
            return Probability.SquareToCosineHemisphere(rand.NextVec2());
        }

        private bool SampleSpecular(Vector3 wo, Random rand, out Vector3 wi)
        {
            //Color3F cSpec0 = Lerp(Metallic, SchlickR0FromEta(Ior) * Lerp(SpecularTint, new Color3F(1.0f), ColorTint), BaseColor);
            //DisneyFresnel fresnel = new(cSpec0, Metallic, Ior);
            Fresnel.Dielectric fresnel = new Fresnel.Dielectric(1, Ior);
            DisneyDistributionGTR2 dist = new(AnisX, AnisY);
            SampleBxdfResult sample = new MicrofacetReflectionBrdf<Fresnel.Dielectric, DisneyDistributionGTR2>(new Color3F(1.0f), fresnel, dist).Sample(wo, rand);
            if (sample.Pdf == 0) { wi = default; return false; }
            wi = sample.Wi;
            return true;
        }

        private Vector3 SampleClearcoat(Vector3 lwo, Random rand)
        {
            Vector3 lwh = sample_gtr1(clearcoat_roughness_, rand.NextVec2());
            if (lwh.Z <= 0)
            {
                return new Vector3(0.0f);
            }
            Vector3 lwi = Normalize(2 * Dot(lwo, lwh) * lwh - lwo);
            if (lwi.Z <= 0)
            {
                return new Vector3(0.0f);
            }
            return lwi;
        }

        private float PdfTransmission(Vector3 lwo, Vector3 lwi)
        {
            float eta = (CosTheta(lwo)) > 0 ? Ior : 1 / Ior;
            Vector3 lwh = Normalize(lwo + eta * lwi);
            if (lwh.Z < 0) { lwh = -lwh; }
            if (((lwo.Z > 0) != (Dot(lwh, lwo) > 0)) || ((lwi.Z > 0) != (Dot(lwh, lwi) > 0)))
            {
                return 0;
            }
            float sdem = Dot(lwo, lwh) + eta * Dot(lwi, lwh);
            float dwh_to_dwi = eta * eta * Dot(lwi, lwh) / (sdem * sdem);
            float sin_phi_h = SinPhi(lwh);
            float cos_phi_h = CosPhi(lwh);
            float sin_theta_h = SinTheta(lwh);
            float cos_theta_h = CosTheta(lwh);
            float D = anisotropic_gtr2(sin_phi_h, cos_phi_h, sin_theta_h, cos_theta_h, trans_ax_, trans_ay_);
            return Abs(Dot(lwi, lwh) * D * dwh_to_dwi);
        }

        private float PdfInnerReflect(Vector3 lwo, Vector3 lwi)
        {
            Vector3 lwh = Normalize(-(lwi + lwo));
            float cos_theta_d = Dot(lwi, lwh);
            float sin_phi_h = SinPhi(lwh);
            float cos_phi_h = CosPhi(lwh);
            float sin_theta_h = SinTheta(lwh);
            float cos_theta_h = CosTheta(lwh);
            float D = anisotropic_gtr2(sin_phi_h, cos_phi_h, sin_theta_h, cos_theta_h, trans_ax_, trans_ay_);
            return Abs(cos_phi_h * D / (4 * cos_theta_d));
        }

        private float PdfDiffuse(Vector3 wo, Vector3 wi)
        {
            return AbsCosTheta(wi) / PI;
        }

        private float PdfSpecular(Vector3 wo, Vector3 wi)
        {
            if (!SameHemisphere(wo, wi)) { return 0; }
            Vector3 wh = Normalize(wo + wi);
            return Microfacet.PdfGGX(wo, wh, AnisX, AnisY) / (4 * Dot(wo, wh));
        }

        private static float SchlickWeight(float cosTheta)
        {
            return Pow5(Math.Clamp(1 - cosTheta, 0, 1));
        }

        internal static Color3F FrSchlick(Color3F R0, float cosTheta)
        {
            return Lerp(SchlickWeight(cosTheta), R0, new Color3F(1.0f));
        }

        private static float SchlickR0FromEta(float eta)
        {
            return Sqr(eta - 1) / Sqr(eta + 1);
        }

        public static Color3F to_tint(Color3F base_color)
        {
            float lum = base_color.GetLuminance();
            return lum > 0 ? base_color / lum : new Color3F(1);
        }

        private static float one_minus_5(float x)
        {
            float t = 1 - x;
            float t2 = t * t;
            return t2 * t2 * t;
        }

        private static Color3F schlick(Color3F R0, float cos_theta)
        {
            return R0 + (new Color3F(1) - R0) * one_minus_5(cos_theta);
        }

        private static float schlick(float R0, float cos_theta)
        {
            return R0 + (1 - R0) * one_minus_5(cos_theta);
        }

        private static float compute_a(float sin_phi_h, float cos_phi_h, float ax, float ay)
        {
            return Sqr(cos_phi_h / ax) + Sqr(sin_phi_h / ay);
        }

        private static float anisotropic_gtr2(
            float sin_phi_h, float cos_phi_h,
            float sin_theta_h, float cos_theta_h,
            float ax, float ay)
        {
            float A = compute_a(sin_phi_h, cos_phi_h, ax, ay);
            float RD = Sqr(sin_theta_h) * A + Sqr(cos_theta_h);
            return 1 / (PI * ax * ay * Sqr(RD));
        }

        private static float smith_anisotropic_gtr2(float cos_phi, float sin_phi, float ax, float ay, float tan_theta)
        {
            float t = Sqr(ax * cos_phi) + Sqr(ay * sin_phi);
            float Sqr_val = 1 + t * Sqr(tan_theta);
            float lambda = -0.5f + 0.5f * Sqrt(Sqr_val);
            return 1 / (1 + lambda);
        }

        private static float gtr1(float sin_theta_h, float cos_theta_h, float alpha)
        {
            float U = Sqr(alpha) - 1;
            float LD = 2 * PI * Log(alpha);
            float RD = Sqr(alpha * cos_theta_h) + Sqr(sin_theta_h);
            return U / (LD * RD);
        }

        private static float smith_gtr2(float tan_theta, float alpha)
        {
            if (tan_theta == 0) { return 1; }
            float root = alpha * tan_theta;
            return 2 / (1 + Sqrt(1 + root * root));
        }

        private static Vector3 sample_anisotropic_gtr2(float ax, float ay, Vector2 sample)
        {
            float sin_phi_h = ay * Sin(2 * PI * sample.X);
            float cos_phi_h = ax * Cos(2 * PI * sample.X);
            float nor = 1 / Sqrt(Sqr(sin_phi_h) + Sqr(cos_phi_h));
            sin_phi_h *= nor;
            cos_phi_h *= nor;
            float A = compute_a(sin_phi_h, cos_phi_h, ax, ay);
            float cos_theta_h = Sqrt(A * (1 - sample.Y) / ((1 - A) * sample.Y + A));
            float sin_theta_h = Sqrt(Max(0, 1 - Sqr(cos_theta_h)));
            return Normalize(new Vector3(sin_theta_h * cos_phi_h, sin_theta_h * sin_phi_h, cos_theta_h));
        }

        private static Vector3 sample_anisotropic_gtr2_vnor(Vector3 ve, float ax, float ay, Vector2 sam)
        {
            Vector3 vh = Normalize(new Vector3(ax * ve.X, ay * ve.Y, ve.Z));
            float lensq = vh.X * vh.X + vh.Y * vh.Y;
            Vector3 t1 = lensq > 0.00001f ? new Vector3(-vh.Y, vh.X, 0) / Sqrt(lensq) : new Vector3(1, 0, 0);
            Vector3 t2 = Cross(vh, t1);
            float r = Sqrt(sam.X);
            float phi = 2 * PI * sam.Y;
            float t_1 = r * Cos(phi);
            float _t_2 = r * Sin(phi);
            float s = 0.5f * (1 + vh.Z);
            float t_2 = (1 - s) * Sqrt(1 - t_1 * t_1) + s * _t_2;
            Vector3 nh = t_1 * t1 + t_2 * t2 + Sqrt(Max(0, 1 - t_1 * t_1 - t_2 * t_2)) * vh;
            Vector3 ne = Normalize(new Vector3(ax * nh.X, ay * nh.Y, Max(0, nh.Z)));
            return ne;
        }

        private static Vector3 sample_gtr1(float alpha, Vector2 sample)
        {
            float phi = 2 * PI * sample.X;
            float cos_theta = Sqrt((Pow(alpha, 2 - 2 * sample.Y) - 1) / (Sqr(alpha) - 1));
            float sin_theta = Sqrt(Max(0, 1 - cos_theta * cos_theta));
            return Normalize(new Vector3(
                sin_theta * Cos(phi),
                sin_theta * Sin(phi),
                cos_theta));
        }
    }
}
