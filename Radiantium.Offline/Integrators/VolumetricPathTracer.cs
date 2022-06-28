using Radiantium.Core;
using System.Numerics;
using static Radiantium.Core.MathExt;
using static Radiantium.Offline.Coordinate;
using static System.MathF;
using static System.Numerics.Vector3;

namespace Radiantium.Offline.Integrators
{
    public class VolumetricPathTracer : MonteCarloIntegrator
    {
        public int MaxDepth { get; }
        public int MinDepth { get; }
        public float RRThreshold { get; }
        public LightSampleStrategy Strategy { get; }

        public VolumetricPathTracer(int maxDepth, int minDepth, float rrThreshold, LightSampleStrategy strategy)
        {
            MaxDepth = maxDepth;
            MinDepth = minDepth;
            RRThreshold = rrThreshold;
            Strategy = strategy;
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
                if (ray.MaxT <= ray.MinT)
                {
                    break;
                }
                bool isHit = scene.Intersect(ray, out Intersection inct);
                MediumSampleResult msr = new MediumSampleResult { IsSampleMedium = false };
                if (rayEnv != null)
                {
                    Ray3F realRay = new Ray3F(ray.O, ray.D, ray.MinT, isHit ? inct.T : float.MaxValue);
                    msr = rayEnv.Sample(realRay, rand);
                    coeff *= msr.Tr;
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
                    Medium envMedium = rayEnv!;
                    radiance += coeff * SampleLightToEstimateDirectMedium(scene, rand, msr, envMedium);
                    PhaseFunctionSampleResult sample = envMedium.SampleWi(wo, rand);
                    ray = new Ray3F(msr.P, sample.Wi, ray.MinT);
                    isSpecularPath = false;
                }
                else
                {
                    if (bounces == 0 || isSpecularPath)
                    {
                        if (isHit)
                        {
                            radiance += coeff * inct.Le(wo);
                        }
                        else
                        {
                            radiance += coeff * scene.EvalAllInfiniteLights(ray);
                        }
                    }
                    if (!isHit)
                    {
                        break;
                    }
                    if (MaxDepth != -1 && bounces >= MaxDepth)
                    {
                        break;
                    }
                    if (!inct.HasSurface)
                    {
                        ray = new Ray3F(inct.P, ray.D, ray.MinT);
                        rayEnv = inct.GetMedium(ray.D);
                        bounces--;
                        continue;
                    }
                    radiance += coeff * SampleLightToEstimateDirectSurface(scene, rand, inct, rayEnv);
                    SampleBxdfResult sample = inct.Surface.Sample(inct.ToLocal(wo), inct, rand, TransportMode.Radiance);
                    isSpecularPath = (sample.Type & BxdfType.Specular) != 0;
                    if (sample.Pdf > 0.0f)
                    {
                        coeff *= sample.Fr * AbsCosTheta(sample.Wi) / sample.Pdf;
                    }
                    else
                    {
                        break;
                    }
                    Vector3 nextDir = inct.ToWorld(sample.Wi);
                    ray = new Ray3F(inct.P, nextDir, ray.MinT);
                    rayEnv = inct.GetMedium(nextDir);
                }
                if (bounces > MinDepth)
                {
                    float q = Min(MaxElement(coeff), RRThreshold);
                    if (rand.NextFloat() > q)
                    {
                        break;
                    }
                    coeff /= q;
                }
            }
            return radiance;
        }

        private Color3F SampleLightToEstimateDirectSurface(
            Scene scene, Random rand,
            Intersection inct, Medium? rayEnv)
        {
            Color3F result = new Color3F(0.0f);
            switch (Strategy)
            {
                case LightSampleStrategy.Uniform:
                    {
                        float lightPdf = scene.SampleLight(rand, out Light light);
                        if (lightPdf > 0.0f)
                        {
                            result += EstimateDirectSurface(scene, rand, light, inct, rayEnv) / lightPdf;
                        }
                    }
                    break;
                case LightSampleStrategy.All:
                    {
                        foreach (Light light in scene.Lights)
                        {
                            result += EstimateDirectSurface(scene, rand, light, inct, rayEnv);
                        }
                    }
                    break;
            }
            return result;
        }

        private Color3F EstimateDirectSurface(
            Scene scene, Random rand, Light light,
            Intersection inct, Medium? medium)
        {
            Color3F le = new Color3F(0.0f);
            Vector3 wo = inct.Wr;
            (Vector3 lightP, Vector3 lightWi, float lightPdf, Color3F lightLi) = light.SampleLi(inct, rand);
            if (lightPdf > 0.0f && lightLi != Color3F.Black)
            {
                Color3F fr = inct.Surface.Fr(inct.ToLocal(wo), inct.ToLocal(lightWi), inct, TransportMode.Radiance);
                float scatteringPdf = inct.Surface.Pdf(inct.ToLocal(wo), inct.ToLocal(lightWi), inct, TransportMode.Radiance);
                if (fr != Color3F.Black)
                {
                    lightLi *= scene.Transmittance(inct.P, lightP, medium, rand);
                    if (lightLi != Color3F.Black)
                    {
                        fr *= AbsCosTheta(inct.ToLocal(lightWi));
                        if (light.IsDelta)
                        {
                            le += fr * lightLi / lightPdf;
                        }
                        else
                        {
                            float weight = PathUtility.PowerHeuristic(1, lightPdf, 1, scatteringPdf);
                            le += fr * lightLi * weight / lightPdf;
                        }
                    }
                }
            }
            if (!light.IsDelta)
            {
                SampleBxdfResult sample = inct.Surface.Sample(inct.ToLocal(wo), inct, rand, TransportMode.Radiance);
                Color3F fr = sample.Fr * AbsCosTheta(sample.Wi);
                float scatteringPdf = sample.Pdf;
                bool sampledSpecular = (sample.Type & BxdfType.Specular) != 0;
                if (fr != Color3F.Black && scatteringPdf > 0)
                {
                    float weight = 1;
                    if (!sampledSpecular)
                    {
                        float bxdfToLightPdf = light.PdfLi(inct, inct.ToWorld(sample.Wi));
                        if (bxdfToLightPdf == 0.0f)
                        {
                            return le;
                        }
                        weight = PathUtility.PowerHeuristic(1, scatteringPdf, 1, bxdfToLightPdf);
                    }
                    Ray3F toLight = inct.SpawnRay(inct.ToWorld(sample.Wi));
                    bool isHit = scene.IntersectTr(toLight, medium, rand, out Intersection lightInct, out Color3F tr);
                    Color3F li = new Color3F(0);
                    if (isHit)
                    {
                        if (lightInct.IsLight && lightInct.Light == light)
                        {
                            li = lightInct.Le(inct.ToWorld(-sample.Wi));
                        }
                    }
                    else
                    {
                        li = scene.EvalAllInfiniteLights(toLight);
                    }
                    if (li != Color3F.Black)
                    {
                        le += fr * li * tr * weight / scatteringPdf;
                    }
                }
            }
            return le;
        }

        private Color3F SampleLightToEstimateDirectMedium(
            Scene scene, Random rand,
            MediumSampleResult msr, Medium medium)
        {
            Color3F result = new Color3F();
            switch (Strategy)
            {
                case LightSampleStrategy.Uniform:
                    {
                        float lightPdf = scene.SampleLight(rand, out Light light);
                        if (lightPdf > 0.0f)
                        {
                            result += EstimateDirectMedium(scene, rand, light, msr, medium) / lightPdf;
                        }
                    }
                    break;
                case LightSampleStrategy.All:
                    {
                        foreach (Light light in scene.Lights)
                        {
                            result += EstimateDirectMedium(scene, rand, light, msr, medium);
                        }
                    }
                    break;
            }
            return result;
        }

        private Color3F EstimateDirectMedium(
            Scene scene, Random rand, Light light,
            MediumSampleResult msr, Medium medium)
        {
            Color3F le = new Color3F(0.0f);
            (Vector3 lightP, Vector3 lightWi, float lightPdf, Color3F lightLi) = light.SampleLi(msr, rand);
            if (lightPdf > 0.0f && lightLi != Color3F.Black)
            {
                float p = medium.P(msr.Wo, lightWi);
                float scatteringPdf = p;
                if (p != 0.0f)
                {
                    lightLi *= scene.Transmittance(msr.P, lightP, medium, rand);
                    Color3F f = new Color3F(p);
                    if (lightLi != Color3F.Black && f != Color3F.Black)
                    {
                        if (light.IsDelta)
                        {
                            le += f * lightLi / lightPdf;
                        }
                        else
                        {
                            float weight = PathUtility.PowerHeuristic(1, lightPdf, 1, scatteringPdf);
                            le += f * lightLi * weight / lightPdf;
                        }
                    }
                }
            }
            if (!light.IsDelta)
            {
                PhaseFunctionSampleResult sample = medium.SampleWi(msr.Wo, rand);
                Color3F f = new Color3F(sample.P);
                float scatteringPdf = sample.P;
                if (scatteringPdf > 0)
                {
                    float bxdfToLightPdf = light.PdfLi(msr, sample.Wi);
                    if (bxdfToLightPdf == 0.0f)
                    {
                        return le;
                    }
                    float weight = PathUtility.PowerHeuristic(1, scatteringPdf, 1, bxdfToLightPdf);
                    Ray3F toLight = new Ray3F(msr.P, sample.Wi);
                    bool isHit = scene.IntersectTr(toLight, medium, rand, out Intersection lightInct, out Color3F tr);
                    Color3F li = new Color3F(0);
                    if (isHit)
                    {
                        if (lightInct.IsLight && lightInct.Light == light)
                        {
                            li = lightInct.Le(-sample.Wi);
                        }
                    }
                    else
                    {
                        li = scene.EvalAllInfiniteLights(toLight);
                    }
                    if (li != Color3F.Black)
                    {
                        le += f * li * tr * weight / scatteringPdf;
                    }
                }
            }
            return le;
        }
    }
}
