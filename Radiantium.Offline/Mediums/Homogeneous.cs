using Radiantium.Core;
using System.Numerics;
using static System.MathF;

namespace Radiantium.Offline.Mediums
{
    public class Homogeneous : Medium
    {
        public Color3F SigmaA { get; }
        public Color3F SigmaS { get; }
        public Color3F SigmaT { get; }
        public float G { get; }

        public Homogeneous(Color3F sigmaA, Color3F sigmaS, float g)
        {
            SigmaA = sigmaA;
            SigmaS = sigmaS;
            SigmaT = sigmaA + sigmaS;
            G = g;
        }

        public override Color3F Tr(Ray3F ray, Random rand)
        {
            Color3F tr = -SigmaT * Min(ray.MaxT, float.MaxValue);
            return new Color3F(Exp(tr.R), Exp(tr.G), Exp(tr.B));
        }

        public override MediumSampleResult Sample(Ray3F ray, Random rand)
        {
            int channel = rand.Next(3);
            if (channel < 0 || channel >= 3) { throw new Exception(); }
            Color3F sigmaT = SigmaT;
            float dist = -Log(1 - rand.NextFloat()) / Color3F.IndexerUnsafe(ref sigmaT, channel);
            float t = Min(dist, ray.MaxT);
            Color3F tr = -SigmaT * Min(t, float.MaxValue);
            tr = new Color3F(Exp(tr.R), Exp(tr.G), Exp(tr.B));
            bool isSampleMedium = t < ray.MaxT;
            if (isSampleMedium)
            {
                Color3F density = sigmaT * tr;
                float pdf = (density.R + density.G + density.B) / 3;
                if (pdf == 0) { pdf = 1; }
                Color3F resultTr = tr * SigmaS / pdf;
                Vector3 p = ray.At(t);
                Vector3 wo = -ray.D;
                return new MediumSampleResult(p, wo, resultTr, t);
            }
            else
            {
                float pdf = (tr.R + tr.G + tr.B) / 3;
                if (pdf == 0) { pdf = 1; }
                return new MediumSampleResult(tr / pdf);
            }
        }

        public override float P(Vector3 wo, Vector3 wi)
        {
            return new HenyeyGreenstein(G).P(wo, wi);
        }

        public override PhaseFunctionSampleResult SampleWi(Vector3 wo, Random rand)
        {
            return new HenyeyGreenstein(G).SampleWi(wo, rand);
        }
    }
}
