using Radiantium.Core;

namespace Radiantium.Offline
{
    public abstract class Primitive
    {
        public abstract BoundingBox3F WorldBound { get; }
        public abstract bool Intersect(Ray3F ray);
    }
}
