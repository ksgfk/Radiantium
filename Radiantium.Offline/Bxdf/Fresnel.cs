using Radiantium.Core;
using System.Numerics;
using static System.MathF;

namespace Radiantium.Offline.Bxdf
{
    public interface IFresnel
    {
        Color3F Eval(float cosI);
    }

    public static class Fresnel
    {
        public struct None : IFresnel
        {
            public Color3F Eval(float cosI)
            {
                return new Color3F(1.0f);
            }
        }

        public struct Dielectric : IFresnel
        {
            public float EtaI;
            public float EtaT;
            public Dielectric(float etaI, float etaT)
            {
                EtaI = etaI;
                EtaT = etaT;
            }
            public Color3F Eval(float cosI) { return new Color3F(DielectricFunc(cosI, EtaI, EtaT)); }
        }

        public struct Conductor : IFresnel
        {
            public Color3F EtaI;
            public Color3F EtaT;
            public Color3F K;
            public Conductor(Color3F etaI, Color3F etaT, Color3F k)
            {
                EtaI = etaI;
                EtaT = etaT;
                K = k;
            }
            public Color3F Eval(float cosI) { return new Color3F(ConductorFunc(cosI, EtaI, EtaT, K)); }
        }

        public static float DielectricFunc(float cosThetaI, float etaI, float etaT)
        {
            cosThetaI = Math.Clamp(cosThetaI, -1, 1);
            // swap indices of refraction
            bool entering = cosThetaI > 0.0f;
            if (!entering)
            {
                float t = etaI;
                etaI = etaT;
                etaT = t;
                cosThetaI = Abs(cosThetaI);
            }
            // Snell's law
            float sinThetaI = Sqrt(Max(0, 1 - cosThetaI * cosThetaI));
            float sinThetaT = etaI / etaT * sinThetaI;
            // total internal reflection
            if (sinThetaT >= 1) return 1;
            float cosThetaT = Sqrt(Max(0, 1 - sinThetaT * sinThetaT));
            float rparl = ((etaT * cosThetaI) - (etaI * cosThetaT)) / ((etaT * cosThetaI) + (etaI * cosThetaT));
            float rperp = ((etaI * cosThetaI) - (etaT * cosThetaT)) / ((etaI * cosThetaI) + (etaT * cosThetaT));
            return (rparl * rparl + rperp * rperp) / 2;
        }

        public static Color3F ConductorFunc(float cosThetaI, Color3F etai, Color3F etat, Color3F k)
        {
            cosThetaI = Math.Clamp(cosThetaI, -1, 1);
            Color3F eta = etat / etai;
            Color3F etak = k / etai;

            float cosThetaI2 = cosThetaI * cosThetaI;
            float sinThetaI2 = 1.0f - cosThetaI2;
            Color3F eta2 = eta * eta;
            Color3F etak2 = etak * etak;

            Color3F t0 = eta2 - etak2 - sinThetaI2;
            Color3F a2plusb2 = new Color3F(Vector3.SquareRoot(t0 * t0 + 4 * eta2 * etak2));
            Color3F t1 = a2plusb2 + cosThetaI2;
            Color3F a = new Color3F(Vector3.SquareRoot(0.5f * (a2plusb2 + t0)));
            Color3F t2 = 2 * cosThetaI * a;
            Color3F Rs = (t1 - t2) / (t1 + t2);

            Color3F t3 = cosThetaI2 * a2plusb2 + sinThetaI2 * sinThetaI2;
            Color3F t4 = t2 * sinThetaI2;
            Color3F Rp = Rs * (t3 - t4) / (t3 + t4);

            return 0.5f * (Rp + Rs);
        }
    }
}
