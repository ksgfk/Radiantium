using Radiantium.Core;
using System.Numerics;
using static Radiantium.Core.Double3;
using static Radiantium.Core.MathExt;
using static System.Math;
using static System.MathF;
using static System.Numerics.Vector3;

namespace Radiantium.Offline.Shapes
{
    public class Sphere : Shape
    {
        public float Radius { get; }
        public Vector3 Center { get; }
        public Matrix4x4 ModelToWorld { get; }
        public Matrix4x4 WorldToModel { get; }
        public override BoundingBox3F WorldBound { get; }
        public override float SurfaceArea => 4 * Radius * Radius * MathF.PI;

        public Sphere(float radius, Vector3 center, Matrix4x4 modelToWorld)
        {
            Radius = radius;
            Center = center;
            ModelToWorld = modelToWorld;
            if (!Matrix4x4.Invert(ModelToWorld, out var inv))
            {
                throw new ArgumentException("invalid model matrix");
            }
            WorldToModel = inv;
            BoundingBox3F modelBound = new BoundingBox3F(Center - new Vector3(Radius), Center + new Vector3(Radius));
            WorldBound = modelBound;
        }

        public override ShapeIntersection GetIntersection(Ray3F ray, SurfacePoint surface)
        {
            float t = surface.T;
            Vector3 n = Normalize(ray.At(t) - Center);
            Vector3 p = n * Radius + Center;
            Coordinate coord = new Coordinate(n);

            Vector3 local = TransformNormal(n, WorldToModel);
            float theta = Acos(-local.Y);
            float phi = Atan2(-local.Z, local.X) + MathF.PI;
            float u = phi / (2 * MathF.PI);
            float v = theta / MathF.PI;

            return new ShapeIntersection(p, new Vector2(u, v), t, coord);
        }

        public override bool Intersect(Ray3F ray)
        {
            double mint = ray.MinT;
            double maxt = ray.MaxT;
            Double3 o = new Double3(ray.O) - new Double3(Center);
            Double3 d = new Double3(ray.D);
            double a = d.LengthSquared();
            double b = 2 * Dot(o, d);
            double c = o.LengthSquared() - Sqr(Radius);
            var (found, nearT, farT) = SolveQuadratic(a, b, c);
            bool out_bounds = !(nearT <= maxt && farT >= mint);
            bool in_bounds = nearT < mint && farT > maxt;
            return found && !out_bounds && !in_bounds;
        }

        public override bool Intersect(Ray3F ray, out SurfacePoint surface)
        {
            double mint = ray.MinT;
            double maxt = ray.MaxT;
            Double3 o = new Double3(ray.O) - new Double3(Center);
            Double3 d = new Double3(ray.D);
            double a = d.LengthSquared();
            double b = 2 * Dot(o, d);
            double c = o.LengthSquared() - Sqr(Radius);
            var (found, nearT, farT) = SolveQuadratic(a, b, c);
            bool outBounds = !(nearT <= maxt && farT >= mint);
            bool inBounds = nearT < mint && farT > maxt;
            bool isHit = found && !outBounds && !inBounds;
            surface = new SurfacePoint(0, 0, nearT < mint ? (float)farT : (float)nearT);
            return isHit;
        }

        public override float Pdf(ShapeIntersection inct)
        {
            return 1 / SurfaceArea;
        }

        public override ShapeIntersection Sample(Random rand, out float pdf)
        {
            Vector3 rng = Probability.SquareToUniformSphere(rand.NextVec2());
            Vector3 n = Normalize(rng);
            Coordinate coord = new Coordinate(n);
            Vector3 p = n * Radius + Center;
            pdf = 1 / SurfaceArea;

            Vector3 local = TransformNormal(n, WorldToModel);
            float theta = Acos(-local.Y);
            float phi = Atan2(-local.Z, local.X) + MathF.PI;
            float u = phi / (2 * MathF.PI);
            float v = theta / MathF.PI;

            return new ShapeIntersection(p, new Vector2(u, v), 0, coord);
        }
    }
}
