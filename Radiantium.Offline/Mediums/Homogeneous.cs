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

        public Homogeneous(Color3F sigmaA, Color3F sigmaS, float g, float scale)
        {
            SigmaA = sigmaA * scale;
            SigmaS = sigmaS * scale;
            SigmaT = SigmaA + SigmaS;
            G = g;
        }

        public override Color3F Tr(Ray3F ray, Random rand)
        {
            float negLength = ray.MinT - ray.MaxT;
            Color3F tr;
            tr.R = SigmaT.R == 0 ? 1.0f : Exp(SigmaT.R * negLength);
            tr.G = SigmaT.G == 0 ? 1.0f : Exp(SigmaT.G * negLength);
            tr.B = SigmaT.B == 0 ? 1.0f : Exp(SigmaT.B * negLength);
            return tr;
        }

        public override SampleMediumResult Sample(Ray3F ray, Random rand)
        {
            int channel = rand.Next(3);
            Color3F sigmaT = SigmaT;
            float samplingDensity = Color3F.IndexerUnsafe(ref sigmaT, channel);
            float sampledDistance = -Log(1 - rand.NextFloat()) / samplingDensity;
            float distSurf = ray.MaxT - ray.MinT;
            if (sampledDistance < distSurf)
            {
                float t = ray.MinT + sampledDistance;
                Vector3 p = ray.At(t);
                Color3F tr;
                tr.R = Exp(-sigmaT.R * sampledDistance);
                tr.G = Exp(-sigmaT.G * sampledDistance);
                tr.B = Exp(-sigmaT.B * sampledDistance);
                float pdf = (tr.R * sigmaT.R + tr.G * sigmaT.G + tr.B * sigmaT.B) / 3;
                if (pdf == 0) { pdf = 0.0001f; }
                return new SampleMediumResult(this, p, -ray.D, tr * SigmaS / pdf, t);
            }
            else
            {
                Color3F tr;
                tr.R = Exp(-SigmaT.R * distSurf);
                tr.G = Exp(-SigmaT.G * distSurf);
                tr.B = Exp(-SigmaT.B * distSurf);
                float pdf = (tr.R + tr.G + tr.B) / 3;
                if (pdf == 0) { pdf = 0.0001f; }
                return new SampleMediumResult(this, tr / pdf);
            }
        }

        public override float P(Vector3 wo, Vector3 wi)
        {
            return new HenyeyGreenstein(G).P(wo, wi);
        }

        public override SamplePhaseFunctionResult SampleWi(Vector3 wo, Random rand)
        {
            return new HenyeyGreenstein(G).SampleWi(wo, rand);
        }
    }
}
