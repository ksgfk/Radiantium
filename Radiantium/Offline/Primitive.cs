using Radiantium.Core;

namespace Radiantium.Offline
{
    public abstract class Primitive
    {
        public abstract Material Material { get; }
        public abstract AreaLight? Light { get; set; }
        public abstract BoundingBox3F WorldBound { get; }
        public abstract bool Intersect(Ray3F ray);
        public abstract bool Intersect(Ray3F ray, out Intersection inct);
    }

    public class GeometricPrimitive : Primitive
    {
        public Shape Shape { get; }
        public override BoundingBox3F WorldBound => Shape.WorldBound;
        public override Material Material { get; }
        public override AreaLight? Light { get; set; }

        public GeometricPrimitive(Shape shape, Material material)
        {
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            Material = material ?? throw new ArgumentNullException(nameof(shape));
        }

        public override bool Intersect(Ray3F ray)
        {
            return Shape.Intersect(ray);
        }

        public override bool Intersect(Ray3F ray, out Intersection inct)
        {
            bool isHit = Shape.Intersect(ray, out SurfacePoint surface);
            if (isHit)
            {
                ShapeIntersection shape = Shape.GetIntersection(ray, surface);
                inct = new Intersection(shape.P, shape.UV, shape.T, this, shape.Shading);
            }
            else
            {
                inct = default;
            }
            return isHit;
        }
    }

    public abstract class Aggregate : Primitive
    {
        public sealed override Material Material => throw new NotSupportedException("aggregate can't have material");
        public sealed override AreaLight? Light
        {
            get => throw new NotSupportedException("aggregate can't have light");
            set => throw new NotSupportedException("aggregate can't have light");
        }
    }
}
