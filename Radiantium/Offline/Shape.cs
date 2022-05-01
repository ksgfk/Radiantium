using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline
{
    public struct ShapeSampleResult
    {
        public Vector3 P;
        public Vector2 UV;
        public Shape Shape;
        public Coordinate Shading;

        public ShapeSampleResult(Vector3 p, Vector2 uV, Shape shape, Coordinate shading)
        {
            P = p;
            UV = uV;
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            Shading = shading;
        }
    }

    public abstract class Shape
    {
        public abstract BoundingBox3F WorldBound { get; }
        public abstract float SurfaceArea { get; }
        public abstract bool Intersect(Ray3F ray);
        public abstract bool Intersect(Ray3F ray, out SurfacePoint surface);
        public abstract Intersection GetIntersection(Ray3F ray, SurfacePoint surface);
        public abstract ShapeSampleResult Sample(Random rand, out float pdf);
        public abstract float Pdf(ShapeSampleResult r);
    }
}
