using System.Numerics;
using static Radiantium.Core.MathExt;
using static System.MathF;
using static System.Numerics.Vector3;

namespace Radiantium.Offline
{
    public struct PhaseFunctionSampleResult
    {
        public float P;
        public Vector3 Wi;

        public PhaseFunctionSampleResult(float p, Vector3 wi)
        {
            P = p;
            Wi = wi;
        }
    }

    public interface IPhaseFunction
    {
        float P(Vector3 wo, Vector3 wi);

        PhaseFunctionSampleResult SampleWi(Vector3 wo, Random rand);
    }

    public static class PhaseFunctionUtility
    {
        public static float HenyeyGreenstein(float cosTheta, float g)
        {
            float denom = 1 + g * g + 2 * g * cosTheta;
            float result = (1 / (4 * PI)) * (1 - g * g) / (denom * Sqrt(denom));
            return result;
        }
    }

    public struct HenyeyGreenstein : IPhaseFunction
    {
        public float G;

        public HenyeyGreenstein(float g) { G = g; }

        public float P(Vector3 wo, Vector3 wi)
        {
            float p = PhaseFunctionUtility.HenyeyGreenstein(Dot(wo, wi), G);
            return p;
        }

        public PhaseFunctionSampleResult SampleWi(Vector3 wo, Random rand)
        {
            Vector2 rng = rand.NextVec2();
            float cosTheta;
            if (Abs(G) < 0.001f)
            {
                cosTheta = 1 - 2 * rng.X;
            }
            else
            {
                float sqrTerm = (1 - G * G) / (1 + G - 2 * G * rng.X);
                cosTheta = -(1 + G * G - sqrTerm * sqrTerm) / (2 * G);
            }
            float sinTheta = Sqrt(Max(0, 1 - cosTheta * cosTheta));
            float phi = 2 * PI * rng.Y;
            Coordinate coord = new Coordinate(wo);
            Vector3 wi = SphericalDirection(sinTheta, cosTheta, phi, coord.X, coord.Y, coord.Z);
            float p = PhaseFunctionUtility.HenyeyGreenstein(cosTheta, G);
            return new PhaseFunctionSampleResult(p, wi);
        }
    }
}
