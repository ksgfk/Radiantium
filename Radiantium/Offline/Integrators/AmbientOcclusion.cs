using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline.Integrators
{
    public class AmbientOcclusion : Integrator
    {
        public bool IsCosWeight { get; }

        public AmbientOcclusion(bool isCosWeight = true)
        {
            IsCosWeight = isCosWeight;
        }

        public override Color3F Li(Ray3F ray, Scene scene, Random rand)
        {
            if (!scene.Intersect(ray, out var inct))
            {
                return new Color3F(0.0f);
            }
            Vector3 h;
            float pdf;
            if (IsCosWeight)
            {
                h = Vector3.Normalize(Probability.SquareToCosineHemisphere(rand.NextVec2()));
                pdf = Probability.SquareToCosineHemispherePdf(h);
            }
            else
            {
                h = Vector3.Normalize(Probability.SquareToUniformHemisphere(rand.NextVec2()));
                pdf = Probability.SquareToUniformHemispherePdf(h);
            }
            Vector3 p = inct.Shading.ToWorld(h);
            Ray3F shadowRay = inct.SpawnRay(p);
            if (scene.Intersect(shadowRay))
            {
                return new Color3F(0.0f);
            }
            else
            {
                float cosTheta = Coordinate.CosTheta(h);
                if (cosTheta <= 0.0f || pdf <= 0.0f)
                {
                    return new Color3F(0.0f);
                }
                return new Color3F(cosTheta / MathF.PI / pdf);
            }
        }
    }
}
