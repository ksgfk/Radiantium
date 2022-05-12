using Radiantium.Core;
using System.Numerics;
using static Radiantium.Core.MathExt;
using static System.MathF;
using static System.Numerics.Vector3;

namespace Radiantium.Offline.Integrators
{
    public enum PathTracingMethod
    {
        OnlyBxdf,
        Nee,
        Mis
    }

    public class PathTracing : Integrator
    {
        public int MaxDepth { get; }
        public float RRThreshold { get; }
        public PathTracingMethod Method { get; }

        public PathTracing(int maxDepth, float rrThreshold, PathTracingMethod method)
        {
            MaxDepth = maxDepth;
            RRThreshold = rrThreshold;
            Method = method;
        }

        private static float PowerHeuristic(int nf, float fPdf, int ng, float gPdf)
        {
            float f = nf * fPdf, g = ng * gPdf;
            return (f * f) / (f * f + g * g);
        }

        public Color3F OnlySampleBxdf(Ray3F ray, Scene scene, Random rand)
        {
            Color3F radiance = new(0.0f);
            Color3F coeff = new(1.0f);
            for (int bounces = 0; ; bounces++)
            {
                if (!scene.Intersect(ray, out var inct) || bounces >= MaxDepth)
                {
                    break;
                }
                if (inct.IsLight)
                {
                    radiance += coeff * inct.Le(-ray.D);
                }
                (Vector3 wi, Color3F fr, float pdf, BxdfType _) = inct.Shape.Material.Sample(inct.ToLocal(-ray.D), inct, rand);
                if (pdf > 0.0f)
                {
                    coeff *= fr * Coordinate.AbsCosTheta(wi) / pdf;
                }
                ray = inct.SpawnRay(inct.ToWorld(wi));
                Color3F rr = coeff;
                if (MaxElement(rr) < RRThreshold && bounces > 3)
                {
                    float q = Max(0.05f, 1 - MaxElement(rr));
                    if (rand.NextFloat() < q)
                    {
                        break;
                    }
                    coeff /= 1 - q;
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
                if (!scene.Intersect(ray, out var inct) || bounces >= MaxDepth)
                {
                    break;
                }
                Vector3 wo = -ray.D;
                if (inct.IsLight)
                {
                    if (isSpecularPath)
                    {
                        radiance += coeff * inct.Le(wo);
                    }
                }
                (Vector3 wi, Color3F fr, float pdf, BxdfType type) = inct.Shape.Material.Sample(inct.ToLocal(wo), inct, rand);
                if ((type & BxdfType.Specular) == 0)
                {
                    Light light = scene.Lights[rand.Next(scene.Lights.Length)];
                    float lightPdf = 1.0f / scene.Lights.Length;
                    radiance += coeff * EstimateLight(scene, rand, light, inct, wo) / lightPdf;
                    isSpecularPath = false;
                }
                else
                {
                    isSpecularPath = true;
                }
                if (pdf > 0.0f)
                {
                    coeff *= fr * Coordinate.AbsCosTheta(wi) / pdf;
                }
                ray = inct.SpawnRay(inct.ToWorld(wi));
                Color3F rr = coeff;
                if (MaxElement(rr) < RRThreshold && bounces > 3)
                {
                    float q = Max(0.05f, 1 - MaxElement(rr));
                    if (rand.NextFloat() < q)
                    {
                        break;
                    }
                    coeff /= 1 - q;
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
                Vector3 toLight = p - inct.P;
                if (toLight.Length() < 0.001f)
                {
                    return new Color3F(0);
                }
                Ray3F shadowRay = new Ray3F(inct.P, wi, 0.001f, toLight.Length() - 0.001f);
                if (scene.Intersect(shadowRay))
                {
                    return new Color3F(0);
                }
                Color3F fr = inct.Shape.Material.Fr(inct.ToLocal(wo), inct.ToLocal(wi), inct);
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
                }
                if (!isHit || bounces >= MaxDepth)
                {
                    break;
                }
                SampleBxdfResult sample = inct.Shape.Material.Sample(inct.ToLocal(wo), inct, rand);
                if ((sample.Type & BxdfType.Specular) == 0)
                {
                    Light light = scene.Lights[rand.Next(scene.Lights.Length)];
                    float lightPdf = 1.0f / scene.Lights.Length;
                    radiance += coeff * EstimateDirect(scene, rand, light, inct, wo, sample) / lightPdf;
                    //if (!coeff.IsValid)
                    //{
                    //    Logger.Error($"???");
                    //}
                }
                isSpecularPath = (sample.Type & BxdfType.Specular) != 0;
                if (sample.Pdf > 0.0f)
                {
                    coeff *= sample.Fr * Coordinate.AbsCosTheta(sample.Wi) / sample.Pdf;
                    //if (!coeff.IsValid)
                    //{
                    //    Logger.Error($"???");
                    //}
                }
                ray = inct.SpawnRay(inct.ToWorld(sample.Wi));
                Color3F rr = coeff;
                if (MaxElement(rr) < RRThreshold && bounces > 3)
                {
                    float q = Max(0.05f, 1 - MaxElement(rr));
                    if (rand.NextFloat() < q)
                    {
                        break;
                    }
                    coeff /= 1 - q;
                }
            }
            return radiance;

            static Color3F EstimateDirect(
                Scene scene, Random rand, Light light,
                Intersection inct, Vector3 wo, SampleBxdfResult sample)
            {
                Color3F le = new Color3F(0.0f);
                (Vector3 lightP, Vector3 lightWi, float lightPdf, Color3F lightLi) = light.SampleLi(inct, rand);
                float scatteringPdf = 0.0f;
                if (lightPdf > 0.0f && lightLi != Color3F.Black)
                {
                    Color3F fr = inct.Shape.Material.Fr(inct.ToLocal(wo), inct.ToLocal(lightWi), inct);
                    scatteringPdf = inct.Shape.Material.Pdf(inct.ToLocal(wo), inct.ToLocal(lightWi), inct);
                    if (fr != Color3F.Black)
                    {
                        fr *= Coordinate.AbsCosTheta(inct.ToLocal(lightWi));
                        Vector3 toLight = lightP - inct.P;
                        if (toLight.Length() >= 0.001f)
                        {
                            Ray3F shadowRay = new Ray3F(inct.P, lightWi, 0.001f, toLight.Length() - 0.001f);
                            if (!scene.Intersect(shadowRay))
                            {
                                if (light.IsDelta)
                                {
                                    le += fr * lightLi / lightPdf;
                                }
                                else
                                {
                                    float weight = PowerHeuristic(1, lightPdf, 1, scatteringPdf);
                                    le += fr * lightLi * weight / lightPdf;
                                }
                            }
                        }
                    }
                }
                if (!light.IsDelta)
                {
                    Color3F fr = sample.Fr * Coordinate.AbsCosTheta(sample.Wi);
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
                            weight = PowerHeuristic(1, scatteringPdf, 1, bxdfToLightPdf);
                        }
                        Ray3F toLightRay = inct.SpawnRay(inct.ToWorld(sample.Wi));
                        bool isHit = scene.Intersect(toLightRay, out Intersection lightInct);
                        Color3F li = new Color3F(0);
                        if (isHit)
                        {
                            if (lightInct.IsLight && lightInct.Light == light)
                            {
                                li = lightInct.Le(inct.ToWorld(-sample.Wi));
                            }
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
                PathTracingMethod.OnlyBxdf => OnlySampleBxdf(ray, scene, rand),
                PathTracingMethod.Nee => NextEventEstimation(ray, scene, rand),
                PathTracingMethod.Mis => Mis(ray, scene, rand),
                _ => Color3F.Black
            };
        }
    }
}
