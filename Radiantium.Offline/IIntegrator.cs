using Radiantium.Core;

namespace Radiantium.Offline
{
    public interface IIntegrator
    {
        string TargetRendererName { get; }
    }

    public abstract class MonteCarloIntegrator : IIntegrator
    {
        public virtual string TargetRendererName => "block_based";

        public abstract Color3F Li(Ray3F ray, Scene scene, Random rand);
    }
}
