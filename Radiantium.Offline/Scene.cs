using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline
{
    public class Scene
    {
        public Primitive Aggregate { get; }
        public Light[] Lights { get; }
        public InfiniteLight[] InfiniteLights { get; }
        public Medium? GlobalMedium { get; }

        public Scene(Primitive primitive, Light[] lights, InfiniteLight[] infiniteLights, Medium? globalMedium)
        {
            Aggregate = primitive ?? throw new ArgumentNullException(nameof(primitive));
            Lights = lights ?? throw new ArgumentNullException(nameof(lights));
            InfiniteLights = infiniteLights ?? throw new ArgumentNullException(nameof(primitive));
            GlobalMedium = globalMedium;
            foreach (InfiniteLight infLight in infiniteLights)
            {
                infLight.Preprocess(this);
            }
        }

        public bool Intersect(Ray3F ray)
        {
            return Aggregate.Intersect(ray);
        }

        public bool Intersect(Ray3F ray, out Intersection inct)
        {
            return Aggregate.Intersect(ray, out inct);
        }

        public Color3F EvalAllInfiniteLights(Ray3F ray)
        {
            Color3F l = new Color3F(0.0f);
            foreach (Light light in InfiniteLights)
            {
                l += light.Le(ray);
            }
            return l;
        }

        public float SampleLight(Random rand, out Light light)
        {
            if (Lights.Length == 0)
            {
                light = null!;
                return 0.0f;
            }
            light = Lights[rand.Next(Lights.Length)];
            float pdf = 1.0f / Lights.Length;
            return pdf;
        }

        public bool IsOccluded(Vector3 from, Vector3 to)
        {
            Vector3 vec = to - from;
            float dis = vec.Length();
            if (dis < 0.001f) // two points too close
            {
                return true;
            }
            Vector3 dir = Vector3.Normalize(vec);
            Ray3F shadowRay = new Ray3F(from, dir, 0.0001f, dis - 0.0001f);
            return Intersect(shadowRay);
        }

        public Color3F Transmittance(Vector3 from, Vector3 to, Medium? rayEnv, Random rand)
        {
            Color3F tr = new Color3F(1.0f);
            Vector3 start = from;
            Vector3 dir = Vector3.Normalize(to - start);
            while (true)
            {
                Vector3 vec = to - start;
                float dis = vec.Length();
                if (dis < 0.0001f)
                {
                    break;
                }
                Ray3F ray = new Ray3F(start, dir, 0.0001f, dis - 0.0001f);
                bool anyHit = Intersect(ray, out Intersection inct);
                if (anyHit && inct.HasSurface)
                {
                    return new Color3F(0.0f);
                }
                if (rayEnv != null)
                {
                    tr *= rayEnv.Tr(ray, rand);
                }
                if (!anyHit)
                {
                    break;
                }
                start = inct.P;
                rayEnv = inct.GetMedium(dir);
            }
            return tr;
        }

        public bool IntersectTr(Ray3F ray, Medium? rayEnv, Random rand, out Intersection inct, out Color3F tr)
        {
            tr = new Color3F(1.0f);
            while (true)
            {
                bool anyHit = Intersect(ray, out inct);
                if (!anyHit)
                {
                    if (rayEnv != null)
                    {
                        tr *= rayEnv.Tr(ray, rand);
                    }
                    return false;
                }
                if (rayEnv != null)
                {
                    Ray3F realPath = new Ray3F(ray.O, ray.D, ray.MinT, ray.At(inct.T).Length());
                    tr *= rayEnv.Tr(realPath, rand);
                }
                if (inct.HasSurface)
                {
                    return true;
                }
                ray = new Ray3F(inct.P, ray.D, 0.001f);
                rayEnv = inct.GetMedium(ray.D);
            }
        }
    }
}
