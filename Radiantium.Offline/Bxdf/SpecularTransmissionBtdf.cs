using Radiantium.Core;
using System.Numerics;
using static Radiantium.Core.MathExt;
using static Radiantium.Offline.Coordinate;

namespace Radiantium.Offline.Bxdf
{
    public struct SpecularTransmissionBtdf : IBxdf
    {
        public Color3F T;
        public float EtaA;
        public float EtaB;
        public BxdfType Type => BxdfType.Transmission | BxdfType.Specular;

        public SpecularTransmissionBtdf(Color3F t, float etaA, float etaB)
        {
            T = t;
            EtaA = etaA;
            EtaB = etaB;
        }

        public Color3F Fr(Vector3 wo, Vector3 wi)
        {
            return new Color3F(0.0f);
        }

        public float Pdf(Vector3 wo, Vector3 wi)
        {
            return 0.0f;
        }

        public SampleBxdfResult Sample(Vector3 wo, Random rand)
        {
            bool entering = CosTheta(wo) > 0;
            float etaI = entering ? EtaA : EtaB;
            float etaT = entering ? EtaB : EtaA;
            Vector3 n = entering ? new Vector3(0, 0, 1) : new Vector3(0, 0, -1);
            if (!Refract(wo, n, etaI / etaT, out Vector3 wi) || CosTheta(wi) == 0.0f)
            {
                return new SampleBxdfResult();
            }
            Color3F ft = T * (1.0f - Fresnel.DielectricFunc(CosTheta(wo), EtaA, EtaB));
            ft *= (etaI * etaI) / (etaT * etaT);
            ft /= AbsCosTheta(wi);
            return new SampleBxdfResult(wi, ft, 1, BxdfType.Transmission | BxdfType.Specular);
        }
    }
}
