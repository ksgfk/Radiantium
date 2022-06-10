using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline
{
    public struct ShapeIntersection
    {
        public Vector3 P;
        public Vector2 UV;
        public float T;
        public Coordinate Shading;

        public Vector3 N => Shading.Z;

        public ShapeIntersection(Vector3 p, Vector2 uv, float t, Coordinate shading)
        {
            P = p;
            UV = uv;
            T = t;
            Shading = shading;
        }
    }

    public abstract class Shape
    {
        public abstract BoundingBox3F WorldBound { get; }
        public abstract float SurfaceArea { get; }
        public abstract bool Intersect(Ray3F ray);
        public abstract bool Intersect(Ray3F ray, out SurfacePoint surface);
        public abstract ShapeIntersection GetIntersection(Ray3F ray, SurfacePoint surface);
        public abstract ShapeIntersection Sample(Random rand, out float pdf);
        public abstract float Pdf(ShapeIntersection inct);
    }
}
