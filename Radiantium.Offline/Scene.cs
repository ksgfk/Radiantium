using Radiantium.Core;

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
    }
}
