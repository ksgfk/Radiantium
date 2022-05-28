﻿using Radiantium.Core;
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

    //TODO: NEE and MIS impl more light sampling methods
    public class PathTracer : Integrator
    {
        public int MaxDepth { get; }
        public int MinDepth { get; }
        public float RRThreshold { get; }
        public PathSampleMethod Method { get; }

        public PathTracer(int maxDepth, int minDepth, float rrThreshold, PathSampleMethod method)
        {
            MaxDepth = maxDepth;
            MinDepth = minDepth;
            RRThreshold = rrThreshold;
            Method = method;
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
                    ray = new Ray3F(inct.P, ray.D, 0.001f);
                    bounces--;
                    continue;
                }
                Vector3 wo = -ray.D;
                if (inct.IsLight)
                {
                    radiance += coeff * inct.Le(wo);
                }
                (Vector3 wi, Color3F fr, float pdf, BxdfType _) = inct.Surface.Sample(inct.ToLocal(-ray.D), inct, rand);
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
                    ray = new Ray3F(inct.P, ray.D, 0.001f);
                    bounces--;
                    continue;
                }
                (Vector3 wi, Color3F fr, float pdf, BxdfType type) = inct.Surface.Sample(inct.ToLocal(wo), inct, rand);
                isSpecularPath = (type & BxdfType.Specular) != 0;
                if (!isSpecularPath)
                {
                    float lightPdf = scene.SampleLight(rand, out Light light);
                    if (lightPdf > 0.0f)
                    {
                        radiance += coeff * EstimateLight(scene, rand, light, inct, wo) / lightPdf;
                    }
                }
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
                Color3F fr = inct.Surface.Fr(inct.ToLocal(wo), inct.ToLocal(wi), inct);
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
                    ray = new Ray3F(inct.P, ray.D, 0.001f);
                    bounces--;
                    continue;
                }
                SampleBxdfResult sample = inct.Surface.Sample(inct.ToLocal(wo), inct, rand);
                isSpecularPath = (sample.Type & BxdfType.Specular) != 0;
                if (!isSpecularPath)
                {
                    float lightPdf = scene.SampleLight(rand, out Light light);
                    if (lightPdf > 0.0f)
                    {
                        radiance += coeff * EstimateDirect(scene, rand, light, inct, wo, sample) / lightPdf;
#if DEBUG
                        if (!radiance.IsValid)
                        {
                            throw new InvalidOperationException($"{radiance}");
                        }
#endif
                    }
                }
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

            static Color3F EstimateDirect(
                Scene scene, Random rand, Light light,
                Intersection inct, Vector3 wo, SampleBxdfResult sample)
            {
                Color3F le = new Color3F(0.0f);
                (Vector3 lightP, Vector3 lightWi, float lightPdf, Color3F lightLi) = light.SampleLi(inct, rand);
                float scatteringPdf = 0.0f;
                if (lightPdf > 0.0f && lightLi != Color3F.Black)
                {
                    Color3F fr = inct.Surface.Fr(inct.ToLocal(wo), inct.ToLocal(lightWi), inct);
                    scatteringPdf = inct.Surface.Pdf(inct.ToLocal(wo), inct.ToLocal(lightWi), inct);
                    if (fr != Color3F.Black)
                    {
                        fr *= Coordinate.AbsCosTheta(inct.ToLocal(lightWi));
                        if (!scene.IsOccluded(inct.P, lightP))
                        {
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
                            weight = PathUtility.PowerHeuristic(1, scatteringPdf, 1, bxdfToLightPdf);
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
                        else
                        {
                            li = scene.EvalAllInfiniteLights(toLightRay);
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
