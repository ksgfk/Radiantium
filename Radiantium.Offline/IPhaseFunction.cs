using System.Numerics;
using static Radiantium.Core.MathExt;
using static System.MathF;
using static System.Numerics.Vector3;

namespace Radiantium.Offline
{
    public struct SamplePhaseFunctionResult
    {
        public float P;
        public Vector3 Wi;

        public SamplePhaseFunctionResult(float p, Vector3 wi)
        {
            P = p;
            Wi = wi;
        }
    }

    public interface IPhaseFunction
    {
        /// <summary>
        /// 评估相位函数
        /// </summary>
        /// <param name="wo">入射方向</param>
        /// <param name="wi">出射方向</param>
        float P(Vector3 wo, Vector3 wi);

        /// <summary>
        /// 根据入射方向, 采样一个出射结果
        /// </summary>
        /// <param name="wo">入射方向</param>
        /// <param name="rand">随机数发生器</param>
        SamplePhaseFunctionResult SampleWi(Vector3 wo, Random rand);
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

        public HenyeyGreenstein(float g)
        {
            G = Math.Clamp(g, -0.999f, 0.999f);
        }

        public float P(Vector3 wo, Vector3 wi)
        {
            float p = PhaseFunctionUtility.HenyeyGreenstein(Dot(wo, wi), G);
            return p;
        }

        public SamplePhaseFunctionResult SampleWi(Vector3 wo, Random rand)
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
            return new SamplePhaseFunctionResult(p, wi);
        }
    }
}
