using Radiantium.Core;

namespace Radiantium.Offline
{
    public class Scene
    {
        public Primitive Aggregate { get; }
        public Light[] Lights { get; }
        public Light[] InfiniteLights { get; }

        public Scene(Primitive primitive, Light[] lights, Light[] infiniteLights)
        {
            Aggregate = primitive ?? throw new ArgumentNullException(nameof(primitive));
            Lights = lights ?? throw new ArgumentNullException(nameof(lights));
            InfiniteLights = infiniteLights ?? throw new ArgumentNullException(nameof(primitive));
        }

        public bool Intersect(Ray3F ray)
        {
            return Aggregate.Intersect(ray);
        }

        public bool Intersect(Ray3F ray, out Intersection inct)
        {
            return Aggregate.Intersect(ray, out inct);
        }
    }
}
