using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline.Integrators
{
    public enum GBufferType
    {
        Normal,
        Depth,
        UV
    }

    public class GBufferVisualization : MonteCarloIntegrator
    {
        public GBufferType Name { get; }

        public GBufferVisualization(GBufferType name)
        {
            Name = name;
        }

        public override Color3F Li(Ray3F ray, Scene scene, Random rand)
        {
            if (!scene.Intersect(ray, out Intersection inct))
            {
                return new Color3F();
            }
            switch (Name)
            {
                case GBufferType.Normal:
                    Vector3 normal = inct.N;
                    return new Color3F(Vector3.Clamp(normal, new Vector3(0), new Vector3(1)));
                case GBufferType.Depth:
                    float depth = inct.T / (ray.MaxT - ray.MinT);
                    return new Color3F(Math.Clamp(depth, 0, 1));
                case GBufferType.UV:
                    Vector2 uv = inct.UV;
                    return new Color3F(uv.X, uv.Y, 0.0f);
                default:
                    return new Color3F();
            }
        }
    }
}
