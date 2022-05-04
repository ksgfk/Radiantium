using Radiantium.Core;
using System.Numerics;
using static System.MathF;
using static System.Numerics.Vector3;
using static Radiantium.Core.MathExt;

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

        public Color3F OnlySampleBxdf(Ray3F ray, Scene scene, Random rand)
        {
            Color3F radiance = new(0.0f);
            Color3F coeff = new(1.0f); //路径贡献
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
                (Vector3 wi, Color3F fr, float pdf, BxdfType type) = inct.Shape.Material.Sample(inct.ToLocal(-ray.D), rand);
                if (fr == Color3F.Black)
                {
                    break;
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
        }

        public static float PowerHeuristic(int nf, float fPdf, int ng, float gPdf)
        {
            float f = nf * fPdf, g = ng * gPdf;
            return (f * f) / (f * f + g * g);
        }

        public static Color3F EstimateLightMis(
            Scene scene, Random rand,
            Ray3F ray,
            Intersection it, Light light)
        {
            (Vector3 lightPos, Vector3 wi, float lightPdf, Color3F li) = light.SampleLi(it, rand);
            Vector3 wo = -ray.D;
            Color3F ld = Color3F.Black;
            if (lightPdf > 0)
            {
                Color3F fr;
                float scatteringPdf;
                if ((it.Shape.Material.Type & ~BxdfType.Specular) == 0)
                {
                    fr = Color3F.Black;
                    scatteringPdf = 0.0f;
                }
                else
                {
                    Vector3 woLocal = it.ToLocal(wo);
                    Vector3 wiLocal = it.ToLocal(wi);
                    if (Coordinate.CosTheta(woLocal) == 0)
                    {
                        fr = Color3F.Black;
                        scatteringPdf = 0.0f;
                    }
                    else
                    {
                        fr = it.Shape.Material.Fr(woLocal, wiLocal) * AbsDot(wi, it.N);
                        scatteringPdf = it.Shape.Material.Pdf(woLocal, wiLocal);
                    }
                }
                if (fr != Color3F.Black)
                {
                    Vector3 toLight = lightPos - it.P;
                    if (toLight.Length() <= 0.001f) //离光源太近
                    {
                        li = Color3F.Black;
                    }
                    else
                    {
                        Ray3F shadowRay = new(it.P, Normalize(toLight), 0.001f, toLight.Length() - 0.001f);
                        if (scene.Intersect(shadowRay))
                        {
                            li = Color3F.Black;
                        }
                    }
                    if (li != Color3F.Black)
                    {
                        if (light.IsDelta)
                        {
                            ld += fr * li / lightPdf;
                        }
                        else
                        {
                            float weight = PowerHeuristic(1, lightPdf, 1, scatteringPdf);
                            ld += fr * li * weight / lightPdf;
                        }
                    }
                }
            }
            return ld;
        }

        public static Color3F EstimateBxdfMis(
            Scene scene, Random rand,
            Ray3F ray,
            Intersection it)
        {
            Color3F ld = Color3F.Black;
            Vector3 wo = -ray.D;
            (Vector3 wi, Color3F fr, float scatteringPdf, BxdfType type) = it.Shape.Material.Sample(it.ToLocal(wo), rand);
            wi = it.ToWorld(wi);
            fr *= AbsDot(wi, it.N);
            bool sampledSpecular = (type & BxdfType.Specular) == 0;
            if (fr != Color3F.Black && scatteringPdf > 0)
            {
                Ray3F nextRay = it.SpawnRay(wi);
                bool foundSurfaceInteraction = scene.Intersect(nextRay, out Intersection lightInct);
                if (foundSurfaceInteraction)
                {
                    if (lightInct.Shape.Light != null)
                    {
                        Color3F li = lightInct.Le(-wi);
                        float weight = 1;
                        if (!sampledSpecular)
                        {
                            float lightPdf = lightInct.Shape.Light.PdfLi(it, -wi);
                            if (lightPdf == 0)
                            {
                                return ld;
                            }
                            weight = PowerHeuristic(1, scatteringPdf, 1, lightPdf);
                        }
                        if (li != Color3F.Black)
                        {
                            ld += fr * li * weight / scatteringPdf;
                        }
                    }
                }
            }
            return ld;
        }

        public override Color3F Li(Ray3F ray, Scene scene, Random rand)
        {
            //if (scene.Intersect(ray,out var inct))
            //{
            //    return new Color3F(inct.N) * 0.5f + 0.5f;
            //}
            //return new Color3F();

            return Method switch
            {
                PathTracingMethod.OnlyBxdf => OnlySampleBxdf(ray, scene, rand),
                _ => Color3F.Black
            };

            //Color3F l = new Color3F(0.0f);
            //Color3F beta = new Color3F(1.0f);
            //bool specularBounce = false;
            //for (int bounces = 0; ; bounces++)
            //{
            //    bool foundIntersection = scene.Intersect(ray, out Intersection inct);
            //    if (bounces == 0 || specularBounce)
            //    {
            //        if (foundIntersection)
            //        {
            //            l += beta * inct.Le(-ray.D);
            //        }
            //        else
            //        {
            //        TODO: infinity light
            //        }
            //    }
            //    if (!foundIntersection || bounces >= MaxDepth)
            //    {
            //        break;
            //    }
            //    if ((inct.Shape.Material.Type & ~BxDFType.Specular) != 0)
            //    {
            //        Color3F ld = Color3F.Black;
            //        foreach (Light light in scene.Lights)
            //        {
            //            ld += beta * EstimateLightMis(scene, rand, ray, inct, light);
            //        }
            //        ld += beta * EstimateBxdfMis(scene, rand, ray, inct);
            //        l += ld;
            //    }
            //    Vector3 wo = -ray.D;
            //    (Vector3 wi, Color3F fr, float pdf, BxDFType type) = inct.Shape.Material.Sample(inct.ToLocal(wo), rand);
            //    wi = inct.ToWorld(wi);
            //    if (fr == Color3F.Black || pdf == 0.0f)
            //    {
            //        break;
            //    }
            //    beta *= fr * AbsDot(wi, inct.N) / pdf;
            //    specularBounce = (type & BxDFType.Specular) != 0;
            //    ray = inct.SpawnRay(wi);

            //    Color3F rrBeta = beta;
            //    if (MaxElement(rrBeta) < RRThreshold && bounces > 3)
            //    {
            //        float q = Max(0.05f, 1 - MaxElement(rrBeta));
            //        if (rand.NextFloat() < q)
            //        {
            //            break;
            //        }
            //        beta /= (1 - q);
            //    }
            //}
            //return l;
        }
    }
}
