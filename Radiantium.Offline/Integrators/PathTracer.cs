using Radiantium.Core;
using System.Numerics;
using static Radiantium.Core.MathExt;
using static System.MathF;
using static System.Numerics.Vector3;

namespace Radiantium.Offline.Integrators
{
    public enum PathSampleMethod
    {
        Bxdf,
        Nee,
        Mis
    }

    public enum LightSampleStrategy
    {
        Uniform,
        All
    }

    public class PathTracer : MonteCarloIntegrator
    {
        public int MaxDepth { get; }
        public int MinDepth { get; }
        public float RRThreshold { get; }
        public PathSampleMethod Method { get; }
        public LightSampleStrategy Strategy { get; }

        public PathTracer(int maxDepth, int minDepth, float rrThreshold, PathSampleMethod method, LightSampleStrategy strategy)
        {
            MaxDepth = maxDepth;
            MinDepth = minDepth;
            RRThreshold = rrThreshold;
            Method = method;
            Strategy = strategy;
        }

        public Color3F OnlySampleBxdf(Ray3F ray, Scene scene, Random rand)
        {
            Color3F radiance = new(0.0f);
            Color3F coeff = new(1.0f);
            for (int bounces = 0; ; bounces++)
            {
                if (!scene.Intersect(ray, out var inct))
                {
                    radiance += coeff * scene.EvalAllInfiniteLights(ray);
                    break;
                }
                if (MaxDepth != -1 && bounces >= MaxDepth)
                {
                    break;
                }
                if (!inct.HasSurface)
                {
                    ray = new Ray3F(inct.P, ray.D, ray.MinT);
                    bounces--;
                    continue;
                }
                Vector3 wo = -ray.D;
                if (inct.IsLight)
                {
                    radiance += coeff * inct.Le(wo);
                }
                (Vector3 wi, Color3F fr, float pdf, BxdfType _) = inct.Surface.Sample(inct.ToLocal(-ray.D), inct, rand, TransportMode.Radiance);
                if (pdf > 0.0f)
                {
                    coeff *= fr * Coordinate.AbsCosTheta(wi) / pdf;
                }
                else
                {
                    break;
                }
                ray = inct.SpawnRay(inct.ToWorld(wi));
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

        public Color3F NextEventEstimation(Ray3F ray, Scene scene, Random rand)
        {
            Color3F radiance = new(0.0f);
            Color3F coeff = new(1.0f);
            bool isSpecularPath = true;
            for (int bounces = 0; ; bounces++)
            {
                bool isHit = scene.Intersect(ray, out Intersection inct);
                Vector3 wo = -ray.D;
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
                    bounces--;
                    continue;
                }
                switch (Strategy)
                {
                    case LightSampleStrategy.Uniform:
                        {
                            float lightPdf = scene.SampleLight(rand, out Light light);
                            if (lightPdf > 0.0f)
                            {
                                radiance += coeff * EstimateLight(scene, rand, light, inct, wo) / lightPdf;
                            }
                        }
                        break;
                    case LightSampleStrategy.All:
                        {
                            foreach (Light light in scene.Lights)
                            {
                                radiance += coeff * EstimateLight(scene, rand, light, inct, wo);
                            }
                        }
                        break;
                }
                (Vector3 wi, Color3F fr, float pdf, BxdfType type) = inct.Surface.Sample(inct.ToLocal(wo), inct, rand, TransportMode.Radiance);
                isSpecularPath = (type & BxdfType.Specular) != 0;
                if (pdf > 0.0f)
                {
                    coeff *= fr * Coordinate.AbsCosTheta(wi) / pdf;
                }
                else
                {
                    break;
                }
                ray = inct.SpawnRay(inct.ToWorld(wi));
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

            static Color3F EstimateLight(Scene scene, Random rand, Light light, Intersection inct, Vector3 wo)
            {
                (Vector3 p, Vector3 wi, float pdf, Color3F li) = light.SampleLi(inct, rand);
                if (pdf <= 0.0f)
                {
                    return new Color3F(0);
                }
                if (scene.IsOccluded(inct.P, p))
                {
                    return new Color3F(0.0f);
                }
                Color3F fr = inct.Surface.Fr(inct.ToLocal(wo), inct.ToLocal(wi), inct, TransportMode.Radiance);
                return fr * li * Coordinate.AbsCosTheta(inct.ToLocal(wi)) / pdf;
            }
        }

        public Color3F Mis(Ray3F ray, Scene scene, Random rand)
        {
            Color3F radiance = new(0.0f);
            Color3F coeff = new(1.0f);
            bool isSpecularPath = true;
            for (int bounces = 0; ; bounces++)
            {
                bool isHit = scene.Intersect(ray, out Intersection inct);
                Vector3 wo = -ray.D;
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
                    bounces--;
                    continue;
                }
                radiance += coeff * SampleLightToEstimateDirect(scene, rand, inct, Strategy);
                SampleBxdfResult sample = inct.Surface.Sample(inct.ToLocal(wo), inct, rand, TransportMode.Radiance);
                isSpecularPath = (sample.Type & BxdfType.Specular) != 0;
                if (sample.Pdf > 0.0f)
                {
                    coeff *= sample.Fr * Coordinate.AbsCosTheta(sample.Wi) / sample.Pdf;
#if DEBUG
                    if (!coeff.IsValid)
                    {
                        throw new InvalidOperationException($"{coeff}");
                    }
#endif
                }
                else
                {
                    break;
                }
                ray = inct.SpawnRay(inct.ToWorld(sample.Wi));
                if (sample.HasSubsurface && sample.HasTransmission)
                {
                    ref readonly Intersection po = ref inct;
                    SampleBssrdfResult samSss = inct.Surface.SamplePi(po, scene, rand);
                    if (samSss.Pdf == 0 || samSss.S == Color3F.Black) { break; }
                    coeff *= samSss.S / samSss.Pdf;
                    ref readonly BssrdfSurfacePoint surface = ref samSss.Pi;
                    Intersection pi = new Intersection(surface.Pos, surface.UV, Distance(inct.P, surface.Pos), inct.Shape, surface.Coord, surface.Wr);
                    radiance += coeff * SampleLightToEstimateDirect(scene, rand, pi, Strategy, inct.Surface.BssrdfAdapter);
                    Material adapter = inct.Surface.BssrdfAdapter!;
                    SampleBxdfResult newSample = adapter.Sample(pi.ToLocal(surface.Wr), pi, rand, TransportMode.Radiance);
                    if (newSample.Pdf == 0 || newSample.Fr == Color3F.Black) { break; }
                    coeff *= newSample.Fr * Coordinate.AbsCosTheta(newSample.Wi) / newSample.Pdf;
                    isSpecularPath = newSample.HasSpecular;
                    ray = pi.SpawnRay(pi.ToWorld(newSample.Wi));
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

            static Color3F SampleLightToEstimateDirect(
                Scene scene, Random rand,
                Intersection inct,
                LightSampleStrategy strategy,
                Material? bssrdfAdapter = null)
            {
                Color3F result = new Color3F();
                switch (strategy)
                {
                    case LightSampleStrategy.Uniform:
                        {
                            float lightPdf = scene.SampleLight(rand, out Light light);
                            if (lightPdf > 0.0f)
                            {
                                result += EstimateDirect(scene, rand, light, inct, bssrdfAdapter) / lightPdf;
#if DEBUG
                                if (!result.IsValid)
                                {
                                    throw new InvalidOperationException($"{result}");
                                }
#endif
                            }
                        }
                        break;
                    case LightSampleStrategy.All:
                        {
                            foreach (Light light in scene.Lights)
                            {
                                result += EstimateDirect(scene, rand, light, inct, bssrdfAdapter);
                            }
                        }
                        break;
                }
                return result;
            }

            static Color3F EstimateDirect(
                Scene scene, Random rand, Light light,
                Intersection inct,
                Material? bssrdfAdapter)
            {
                Vector3 wo = inct.Wr;
                Color3F le = new Color3F(0.0f);
                (Vector3 lightP, Vector3 lightWi, float lightPdf, Color3F lightLi) = light.SampleLi(inct, rand);
                if (lightPdf > 0.0f && lightLi != Color3F.Black)
                {
                    Color3F fr;
                    float scatteringPdf;
                    if (bssrdfAdapter == null)
                    {
                        fr = inct.Surface.Fr(inct.ToLocal(wo), inct.ToLocal(lightWi), inct, TransportMode.Radiance);
                        scatteringPdf = inct.Surface.Pdf(inct.ToLocal(wo), inct.ToLocal(lightWi), inct, TransportMode.Radiance);
                    }
                    else
                    {
                        fr = bssrdfAdapter.Fr(inct.ToLocal(wo), inct.ToLocal(lightWi), inct, TransportMode.Radiance);
                        scatteringPdf = bssrdfAdapter.Pdf(inct.ToLocal(wo), inct.ToLocal(lightWi), inct, TransportMode.Radiance);
                    }
                    if (fr != Color3F.Black)
                    {
                        if (!scene.IsOccluded(inct.P, lightP))
                        {
                            fr *= Coordinate.AbsCosTheta(inct.ToLocal(lightWi));
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
                    SampleBxdfResult sample;
                    if (bssrdfAdapter == null)
                    {
                        sample = inct.Surface.Sample(inct.ToLocal(wo), inct, rand, TransportMode.Radiance);
                    }
                    else
                    {
                        sample = bssrdfAdapter.Sample(inct.ToLocal(wo), inct, rand, TransportMode.Radiance);
                    }
                    Color3F fr = sample.Fr * Coordinate.AbsCosTheta(sample.Wi);
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
                        bool isHit = scene.Intersect(toLight, out Intersection lightInct);
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
                            le += fr * li * weight / scatteringPdf;
                        }
                    }
                }
                return le;
            }
        }

        public override Color3F Li(Ray3F ray, Scene scene, Random rand)
        {
            return Method switch
            {
                PathSampleMethod.Bxdf => OnlySampleBxdf(ray, scene, rand),
                PathSampleMethod.Nee => NextEventEstimation(ray, scene, rand),
                PathSampleMethod.Mis => Mis(ray, scene, rand),
                _ => Color3F.Black
            };
        }
    }
}
