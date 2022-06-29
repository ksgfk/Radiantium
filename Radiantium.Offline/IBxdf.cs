using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline
{
    [Flags]
    public enum BxdfType
    {
        Reflection = 0b000001,
        Transmission = 0b000010,
        Diffuse = 0b000100,
        Glossy = 0b001000,
        Specular = 0b010000,
        SubsurfaceScatting = 0b100000
    }

    public struct SampleBxdfResult
    {
        public Vector3 Wi;
        public Color3F Fr;
        public float Pdf;
        public BxdfType Type;

        public bool HasSubsurface => (Type & BxdfType.SubsurfaceScatting) != 0;
        public bool HasTransmission => (Type & BxdfType.Transmission) != 0;
        public bool HasSpecular => (Type & BxdfType.Specular) != 0;

        public SampleBxdfResult(Vector3 wi, Color3F fr, float pdf, BxdfType type)
        {
            Wi = wi;
            Fr = fr;
            Pdf = pdf;
            Type = type;
        }

        public void Deconstruct(out Vector3 wi, out Color3F fr, out float pdf, out BxdfType type)
        {
            wi = Wi;
            fr = Fr;
            pdf = Pdf;
            type = Type;
        }
    }

    public interface IBxdf
    {
        BxdfType Type { get; }

        /// <summary>
        /// 评估 双向反射/透射/散射分布函数值
        /// </summary>
        /// <param name="wo">入射方向</param>
        /// <param name="wi">出射方向</param>
        /// <param name="mode">光线传输方法</param>
        Color3F Fr(Vector3 wo, Vector3 wi, TransportMode mode);

        /// <summary>
        /// 采样一个出射方向
        /// </summary>
        /// <param name="wo">入射方向</param>
        /// <param name="rand">随机数发生器</param>
        /// <param name="mode">光线传输方法</param>
        SampleBxdfResult Sample(Vector3 wo, Random rand, TransportMode mode);

        /// <summary>
        /// 入射与出射方向的概率密度
        /// </summary>
        /// <param name="wo">入射方向</param>
        /// <param name="wi">出射方向</param>
        /// <param name="mode">光线传输方法</param>
        float Pdf(Vector3 wo, Vector3 wi, TransportMode mode);
    }
}
