using Radiantium.Core;

namespace Radiantium.Offline.Integrators
{
    public class VolumetricPathTracer : Integrator
    {
        public int MaxDepth { get; }
        public int MinDepth { get; }
        public float RRThreshold { get; }

        public VolumetricPathTracer(int maxDepth, int minDepth, float rrThreshold)
        {
            MaxDepth = maxDepth;
            MinDepth = minDepth;
            RRThreshold = rrThreshold;
        }

        public override Color3F Li(Ray3F ray, Scene scene, Random rand)
        {
            return new Color3F(0.0f);
        }
    }
}
