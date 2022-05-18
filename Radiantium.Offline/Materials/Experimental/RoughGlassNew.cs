using Radiantium.Core;
using Radiantium.Offline.Bxdf;
using System.Numerics;
using static Radiantium.Core.MathExt;
using static Radiantium.Offline.Coordinate;
using static System.Numerics.Vector3;

namespace Radiantium.Offline.Materials.Experimental
{
    //I don't know why these two versions have different results
    public class RoughGlassNew : Material
    {
        public override BxdfType Type => BxdfType.Glossy | BxdfType.Transmission | BxdfType.Reflection | BxdfType.Specular;

        public override Color3F Fr(Vector3 wo, Vector3 wi, Intersection inct)
        {
            return Eval(wo, wi);
        }

        public override float Pdf(Vector3 wo, Vector3 wi, Intersection inct)
        {
            return Pdf(wo, wi);
        }

        public override SampleBxdfResult Sample(Vector3 wo, Intersection inct, Random rand)
        {
            return Sample(wo, rand);
        }

        public float Roughness;
        public Color3F BaseColor;
        public float EtaA;
        public float EtaB;

        public RoughGlassNew(Color3F baseColor, float rough, float intIOR, float extIOR)
        {
            BaseColor = baseColor;
            Roughness = rough;
            EtaB = intIOR;
            EtaA = extIOR;
        }

        public float GetRoughness()
        {
            return Math.Clamp(Roughness * Roughness, 0.001f, 1.0f);
        }

        public float GetPerceptualRoughness()
        {
            return Roughness;
        }

        public Color3F Eval(Vector3 wo, Vector3 wi)
        {
            float fac = CosTheta(wo) * CosTheta(wi);
            if (MathF.Abs(fac) <= float.Epsilon)
            {
                return new Color3F(0.0f);
            }
            float etai = EtaA;
            float etat = EtaB;
            bool isReflect = fac > 0.0f;
            Vector3 wh;
            if (isReflect)
            {
                wh = Normalize(wo + wi);
            }
            else
            {
                wh = Normalize(-(wo * etai + wi * etat));
            }
            wh *= MathF.Sign(CosTheta(wh));
            float alpha = Math.Clamp(Roughness * Roughness, 0.001f, 1.0f);
            float D = Microfacet.DistributionGGX(wh, alpha);
            if (MathF.Abs(D) <= float.Epsilon)
            {
                return new Color3F(0.0f);
            }
            float F = Fresnel.DielectricFunc(Dot(wo, wh), EtaA, EtaB);
            float G = Microfacet.SmithG1GGX(wo, wi, alpha);
            float bsdf;
            if (isReflect)
            {
                float brdf = MathF.Abs(F * D * G / (4.0f * CosTheta(wo) * CosTheta(wi)));
                bsdf = MathF.Abs(brdf);
            }
            else
            {
                float sqrtDenom = etai * Dot(wh, wo) + etat * Dot(wh, wi);
                if (MathF.Abs(sqrtDenom) <= float.Epsilon)
                {
                    return new Color3F(0.0f);
                }
                float eta = etat / etai;
                float btdf = (1.0f - F) * D * G * eta * eta * Dot(wh, wo) * Dot(wh, wi)
                     / (CosTheta(wo) * CosTheta(wi) * sqrtDenom * sqrtDenom);
                float factor = 1;
                bsdf = MathF.Abs(btdf * factor * factor);
            }
            return new Color3F(bsdf);
        }

        public float Pdf(Vector3 wo, Vector3 wi)
        {
            float fac = CosTheta(wo) * CosTheta(wi);
            if (MathF.Abs(fac) <= float.Epsilon)
            {
                return 0.0f;
            }
            Vector3 wh;
            float dWhdWi;
            bool isReflect = fac > 0.0f;
            float alpha = Math.Clamp(Roughness * Roughness, 0.001f, 1.0f);
            if (isReflect)
            {
                wh = Normalize(wo + wi);
                float jacobian = 1.0f / (4.0f * AbsDot(wh, wo));
                dWhdWi = jacobian;
            }
            else
            {
                float etai = EtaA;
                float etat = EtaB;
                wh = Normalize(-(wo * etai + wi * etat));
                float sqrtDenom = etai * Dot(wh, wo) + etat * Dot(wh, wi);
                dWhdWi = etat * etat * AbsDot(wh, wi) / (sqrtDenom * sqrtDenom);
            }
            wh *= MathF.Sign(CosTheta(wh));
            float D = Microfacet.DistributionGGX(wh, alpha);
            float ggxPdf = D * MathF.Abs(CosTheta(wh));
            float F = Fresnel.DielectricFunc(Dot(wo, wh), EtaA, EtaB);
            float pdf;
            if (isReflect)
            {
                pdf = F * ggxPdf * dWhdWi;
            }
            else
            {
                pdf = (1.0f - F) * ggxPdf * dWhdWi;
            }
            return pdf;
        }

        public SampleBxdfResult Sample(Vector3 wo, Random rand)
        {
            float alpha = Math.Clamp(Roughness * Roughness, 0.001f, 1.0f);
            Vector3 wh = Probability.SquareToGGX(rand.NextVec2(), alpha);
            float D = Microfacet.DistributionGGX(wh, alpha);
            float ggxPdf = D * AbsCosTheta(wh);
            float F = Fresnel.DielectricFunc(Dot(wo, wh), EtaA, EtaB);
            bool isReflect = rand.NextFloat() < F;
            Color3F bsdf;
            float pdf;
            Vector3 wi;
            if (isReflect)
            {
                wi = Reflect(-wo, wh);
                float fac = CosTheta(wo) * CosTheta(wi);
                if (fac <= 0.0f)
                {
                    return new SampleBxdfResult();
                }
                float G = Microfacet.SmithG1GGX(wo, wi, alpha);
                float jacobian = 1.0f / (4.0f * AbsDot(wh, wo));
                float brdf = MathF.Abs(F * D * G / (4.0f * CosTheta(wo) * CosTheta(wi)));
                pdf = jacobian * ggxPdf * F;
                bsdf = new Color3F(brdf);
            }
            else
            {
                float etai = EtaA;
                float etat = EtaB;
                float eta = etat / etai;
                Refract(wo, wh, eta, out wi);
                if (CosTheta(wo) * CosTheta(wi) >= 0.0f)
                {
                    return new SampleBxdfResult();
                }
                float sqrtDenom = etai * Dot(wh, wo) + etat * Dot(wh, wi);
                if (MathF.Abs(sqrtDenom) <= float.Epsilon)
                {
                    return new SampleBxdfResult();
                }
                float G = Microfacet.SmithG1GGX(wo, wi, alpha);
                float dWhdWi = etat * etat * AbsDot(wh, wi) / (sqrtDenom * sqrtDenom);
                float btdf = (1.0f - F) * D * G * eta * eta * Dot(wh, wo) * Dot(wh, wi)
                    / (CosTheta(wo) * CosTheta(wi) * sqrtDenom * sqrtDenom);
                float factor = 1;
                bsdf = new Color3F(MathF.Abs(btdf * factor * factor));
                pdf = MathF.Abs(dWhdWi) * ggxPdf * (1.0f - F);
            }
            if (pdf <= 0.0f)
            {
                return new SampleBxdfResult();
            }
            return new SampleBxdfResult(wi, bsdf, pdf, Type);
        }
    }
}
