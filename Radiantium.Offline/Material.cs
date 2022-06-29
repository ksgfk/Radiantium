using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline
{
    public enum TransportMode
    {
        Radiance,
        Importance
    }

    public abstract class Material
    {
        public abstract BxdfType Type { get; }

        /// <summary>
        /// 次表面散射的表面材质
        /// </summary>
        public virtual Material? BssrdfAdapter => null;

        /// <summary>
        /// 评估 双向反射/透射/散射分布函数值
        /// </summary>
        /// <param name="wo">入射方向</param>
        /// <param name="wi">出射方向</param>
        /// <param name="inct">着色点信息</param>
        /// <param name="mode">光线传输方法</param>
        public abstract Color3F Fr(Vector3 wo, Vector3 wi, Intersection inct, TransportMode mode);

        /// <summary>
        /// 采样一个出射方向
        /// </summary>
        /// <param name="wo">入射方向</param>
        /// <param name="inct">着色点信息</param>
        /// <param name="rand">随机数发生器</param>
        /// <param name="mode">光线传输方法</param>
        public abstract SampleBxdfResult Sample(Vector3 wo, Intersection inct, Random rand, TransportMode mode);

        /// <summary>
        /// 入射与出射方向的概率密度
        /// </summary>
        /// <param name="wo">入射方向</param>
        /// <param name="wi">出射方向</param>
        /// <param name="inct">着色点信息</param>
        /// <param name="mode">光线传输方法</param>
        public abstract float Pdf(Vector3 wo, Vector3 wi, Intersection inct, TransportMode mode);

        /// <summary>
        /// 采样此表面散射的出射点
        /// </summary>
        /// <param name="po">入射点</param>
        /// <param name="scene">场景</param>
        /// <param name="rand">随机数发生器</param>
        public virtual SampleBssrdfResult SamplePi(Intersection po, Scene scene, Random rand) { return new SampleBssrdfResult(); }
    }
}
