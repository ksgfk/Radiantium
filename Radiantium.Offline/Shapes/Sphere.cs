using Radiantium.Core;
using System.Numerics;
using static System.Math;
using static System.MathF;
using static System.Numerics.Vector3;

namespace Radiantium.Offline.Shapes
{
    public class Sphere : Shape
    {
        public float Radius { get; }
        public Matrix4x4 ModelToWorld { get; }
        public Matrix4x4 WorldToModel { get; }
        public override BoundingBox3F WorldBound { get; }
        public override float SurfaceArea => 4 * Radius * Radius * MathF.PI;

        public Sphere(float radius, Matrix4x4 modelToWorld)
        {
            Radius = radius;
            ModelToWorld = modelToWorld;
            if (!Matrix4x4.Invert(ModelToWorld, out var inv))
            {
                throw new ArgumentException("invalid model matrix");
            }
            WorldToModel = inv;
            BoundingBox3F modelBound = new BoundingBox3F(new Vector3(-Radius), new Vector3(Radius));
            WorldBound = BoundingBox3F.Transform(modelBound, modelToWorld);
        }

        public override ShapeIntersection GetIntersection(Ray3F ray, SurfacePoint surface)
        {
            float t = surface.T;
            ray = Ray3F.Transform(ray, WorldToModel);
            Vector3 p = ray.At(t);
            Vector3 n = Normalize(p);
            Coordinate coord = new Coordinate(n);

            float theta = Acos(-n.Y);
            float phi = Atan2(-n.Z, n.X) + MathF.PI;
            float u = phi / (2 * MathF.PI);
            float v = theta / MathF.PI;

            return new ShapeIntersection(Transform(p, ModelToWorld), new Vector2(u, v), t, coord);
        }

        public override bool Intersect(Ray3F ray)
        {
            ray = Ray3F.Transform(ray, WorldToModel);
            float a = ray.D.LengthSquared();
            float b = 2 * Dot(ray.O - new Vector3(0.0f), ray.D);
            float c = (-ray.O).LengthSquared() - Radius * Radius;
            float delta = b * b - 4 * a * c;
            if (delta < 0) { return false; }
            float t1 = (-b - Sqrt(delta)) / (2 * a);
            float t2 = (-b + Sqrt(delta)) / (2 * a);
            if (t1 >= ray.MinT && t1 <= ray.MaxT) { return true; }
            if (t2 >= ray.MinT && t2 <= ray.MaxT) { return true; }
            return false;
        }

        public override bool Intersect(Ray3F ray, out SurfacePoint surface)
        {
            surface = default;
            ray = Ray3F.Transform(ray, WorldToModel);
            float a = ray.D.LengthSquared();
            float b = 2 * Dot(ray.O, ray.D);
            float c = (-ray.O).LengthSquared() - Radius * Radius;
            float delta = b * b - 4 * a * c;
            if (delta < 0) { return false; }
            float t1 = (-b - Sqrt(delta)) / (2 * a);
            float t2 = (-b + Sqrt(delta)) / (2 * a);
            if (t1 >= ray.MinT && t1 <= ray.MaxT)
            {
                surface = new SurfacePoint(0, 0, t1);
                return true;
            }
            if (t2 >= ray.MinT && t2 <= ray.MaxT)
            {
                surface = new SurfacePoint(0, 0, t2);
                return true;
            }
            return false;
        }

        public override float Pdf(ShapeIntersection inct)
        {
            return 1 / SurfaceArea;
        }

        public override ShapeIntersection Sample(Random rand, out float pdf)
        {
            Vector3 rng = Probability.SquareToUniformSphere(rand.NextVec2());
            Vector3 n = Normalize(rng);
            Coordinate coord = new Coordinate(Normalize(TransformNormal(n, ModelToWorld)));
            Vector3 p = Radius * n;
            pdf = 1 / SurfaceArea;

            float theta = Acos(-n.Y);
            float phi = Atan2(-n.Z, n.X) + MathF.PI;
            float u = phi / (2 * MathF.PI);
            float v = theta / MathF.PI;

            return new ShapeIntersection(Transform(p, ModelToWorld), new Vector2(u, v), 0, coord);
        }
    }
}
