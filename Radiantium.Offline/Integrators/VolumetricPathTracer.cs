using Radiantium.Core;
using System.Numerics;

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
            Color3F radiance = new(0.0f);
            Color3F coeff = new(1.0f);
            bool isSpecularPath = true;
            Medium? rayEnv = scene.GlobalMedium;
            for (int bounces = 0; ; bounces++)
            {
                Vector3 wo = -ray.D;
                bool isHit = scene.Intersect(ray, out Intersection inct);
                MediumSampleResult msr = new MediumSampleResult { IsSampleMedium = false };
                if (rayEnv != null)
                {
                    msr = rayEnv.Sample(ray, rand);
                    coeff *= msr.Tr; //apply ray transmission path transmittance
                }
                if (coeff == Color3F.Black)
                {
                    break;
                }
                if (msr.IsSampleMedium)
                {
                    if (bounces >= MaxDepth)
                    {
                        break;
                    }
                    float lightPdf = scene.SampleLight(rand, out Light light);
                    if (lightPdf > 0.0f)
                    {
                        radiance += coeff * EstimateDirectMedium(scene, rand, light, wo, msr, rayEnv!) / lightPdf;
                    }
                }
                else
                {

                }
            }
            return radiance;
        }

        private Color3F EstimateDirectMedium(
            Scene scene, Random rand, Light light,
            Vector3 wo,
            MediumSampleResult msr, Medium medium)
        {
            Color3F le = new Color3F(0.0f);
            //(Vector3 lightP, Vector3 lightWi, float lightPdf, Color3F lightLi) = light.SampleLi(inct, rand);
            throw new NotImplementedException();
        }
    }
}
