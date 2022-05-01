using Radiantium.Core;

namespace Radiantium.Offline
{
    public abstract class Primitive
    {
        public abstract BoundingBox3F WorldBound { get; }
        public abstract bool Intersect(Ray3F ray);
        public abstract bool Intersect(Ray3F ray, out Intersection inct);
    }

    public class GeometricPrimitive : Primitive
    {
        public Shape Shape { get; }
        public override BoundingBox3F WorldBound => Shape.WorldBound;

        public GeometricPrimitive(Shape shape)
        {
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
        }

        public override bool Intersect(Ray3F ray)
        {
            return Shape.Intersect(ray);
        }

        public override bool Intersect(Ray3F ray, out Intersection inct)
        {
            bool isHit = Shape.Intersect(ray, out SurfacePoint surface);
            inct = isHit ? Shape.GetIntersection(ray, surface) : default;
            return isHit;
        }
    }

    public abstract class Aggregate : Primitive
    {
    }
}
