using Radiantium.Core;

namespace Radiantium.Offline
{
    public interface IIntegrator
    {
        /// <summary>
        /// 积分器使用的渲染器名称
        /// </summary>
        string TargetRendererName { get; }
    }

    public abstract class MonteCarloIntegrator : IIntegrator
    {
        public virtual string TargetRendererName => "block_based";

        /// <summary>
        /// 给定射线, 评估该射线在场景中的辐照度
        /// </summary>
        /// <param name="ray">世界空间下射线</param>
        /// <param name="scene">场景</param>
        /// <param name="rand">随机数发生器</param>
        public abstract Color3F Li(Ray3F ray, Scene scene, Random rand);
    }
}
