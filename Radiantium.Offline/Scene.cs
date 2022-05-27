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
            Ray3F shadowRay = new Ray3F(from, dir, 0.001f, dis - 0.001f);
            return Intersect(shadowRay);
        }

        public Color3F Transmittance(Vector3 from, Vector3 to, Medium? rayEnv, Random rand)
        {
            Color3F tr = new Color3F(1.0f);
            Vector3 vec = to - from;
            while (true)
            {
                float dis = vec.Length();
                if (dis < 0.001f)
                {
                    break;
                }
                Vector3 dir = Vector3.Normalize(vec);
                Ray3F ray = new Ray3F(from, dir, 0.001f, dis - 0.001f);
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
                vec = to - inct.P;
                rayEnv = inct.GetMedium(dir);
            }
            return tr;
        }
    }
}
