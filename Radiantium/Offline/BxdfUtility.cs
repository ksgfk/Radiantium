using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.MathF;

namespace Radiantium.Offline
{
    public static class BxdfUtility
    {
        public static float FresnelDielectric(float cosThetaI, float etaI, float etaT)
        {
            cosThetaI = Math.Clamp(cosThetaI, -1, 1);
            // Potentially swap indices of refraction
            bool entering = cosThetaI > 0.0f;
            if (!entering)
            {
                float t = etaI;
                etaI = etaT;
                etaT = t;
                cosThetaI = Abs(cosThetaI);
            }
            // Compute _cosThetaT_ using Snell's law
            float sinThetaI = Sqrt(Max(0, 1 - cosThetaI * cosThetaI));
            float sinThetaT = etaI / etaT * sinThetaI;
            // Handle total internal reflection
            if (sinThetaT >= 1) return 1;
            float cosThetaT = Sqrt(Max(0, 1 - sinThetaT * sinThetaT));
            float rparl = ((etaT * cosThetaI) - (etaI * cosThetaT)) / ((etaT * cosThetaI) + (etaI * cosThetaT));
            float rperp = ((etaI * cosThetaI) - (etaT * cosThetaT)) / ((etaI * cosThetaI) + (etaT * cosThetaT));
            return (rparl * rparl + rperp * rperp) / 2;
        }
    }
}
