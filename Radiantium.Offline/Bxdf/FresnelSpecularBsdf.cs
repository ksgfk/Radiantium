using Radiantium.Core;
using System.Numerics;
using static Radiantium.Core.MathExt;

namespace Radiantium.Offline.Bxdf
{
    public struct FresnelSpecularBsdf : IBxdf
    {
        public Color3F R;
        public Color3F T;
        public float EtaA;
        public float EtaB;
        public BxdfType Type => BxdfType.Reflection | BxdfType.Transmission | BxdfType.Specular;

        public FresnelSpecularBsdf(Color3F r, Color3F t, float etaA, float etaB)
        {
            R = r;
            T = t;
            EtaA = etaA;
            EtaB = etaB;
        }

        public Color3F Fr(Vector3 wo, Vector3 wi, TransportMode mode)
        {
            return Color3F.Black;
        }

        public float Pdf(Vector3 wo, Vector3 wi, TransportMode mode)
        {
            return 0.0f;
        }

        public SampleBxdfResult Sample(Vector3 wo, Random rand, TransportMode mode)
        {
            float f = Fresnel.DielectricFunc(Coordinate.CosTheta(wo), EtaA, EtaB);
            if (rand.NextFloat() < f)
            {
                if (Coordinate.CosTheta(wo) == 0)
                {
                    return new SampleBxdfResult();
                }
                Vector3 wi = new Vector3(-wo.X, -wo.Y, wo.Z);
                Color3F fr = f * R / Coordinate.AbsCosTheta(wi);
                return new SampleBxdfResult(wi, fr, f, BxdfType.Reflection | BxdfType.Specular);
            }
            else
            {
                bool entering = Coordinate.CosTheta(wo) > 0;
                float etaI = entering ? EtaA : EtaB;
                float etaT = entering ? EtaB : EtaA;
                Vector3 n = entering ? new Vector3(0, 0, 1) : new Vector3(0, 0, -1);
                if (!Refract(wo, n, etaI / etaT, out Vector3 wi) || Coordinate.CosTheta(wi) == 0.0f)
                {
                    return new SampleBxdfResult();
                }
                Color3F ft = T * (1.0f - f);
                if (mode == TransportMode.Radiance)
                {
                    ft *= (etaI * etaI) / (etaT * etaT);
                }
                ft /= Coordinate.AbsCosTheta(wi);
                return new SampleBxdfResult(wi, ft, 1 - f, BxdfType.Transmission | BxdfType.Specular);
            }
        }
    }
}
