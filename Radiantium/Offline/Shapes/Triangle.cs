using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline.Shapes
{
    public class TriangleMesh
    {
        public Vector3[] Position { get; }
        public Vector3[]? Normal { get; }
        public Vector2[]? UV { get; }
        public int[] Indices { get; }
        public int TriangleCount { get; }
        public int VertexCount => Position.Length;
        public int IndexCount => Indices.Length;
        public TriangleMesh(Matrix4x4 modelToWorld, TriangleModel model)
        {
            bool isIdentity = modelToWorld == Matrix4x4.Identity;
            if (isIdentity)
            {
                Position = model.Position;
            }
            else
            {
                Position = new Vector3[model.VertexCount];
                for (int i = 0; i < model.VertexCount; i++)
                {
                    Position[i] = Vector3.Transform(model.Position[i], modelToWorld);
                }
            }
            if (model.Normal != null)
            {
                if (isIdentity)
                {
                    Normal = model.Normal;
                }
                else
                {
                    Normal = new Vector3[model.VertexCount];
                    for (int i = 0; i < model.VertexCount; i++)
                    {
                        Normal[i] = Vector3.TransformNormal(model.Normal[i], modelToWorld);
                    }
                }
            }
            if (model.UV != null)
            {
                UV = model.UV;
            }
            Indices = model.Indices;
            TriangleCount = model.TriangleCount;
        }

        public List<Triangle> ToTriangle()
        {
            List<Triangle> result = new List<Triangle>(TriangleCount);
            for (int i = 0; i < TriangleCount; i++)
            {
                result.Add(new Triangle(this, i));
            }
            return result;
        }
    }

    public class Triangle : Shape
    {
        public TriangleMesh Mesh { get; }
        public int Index { get; }
        public override BoundingBox3F WorldBound { get; }
        public override float SurfaceArea { get; }

        public ref readonly Vector3 Pa => ref Mesh.Position[Mesh.Indices[Index * 3 + 0]];
        public ref readonly Vector3 Pb => ref Mesh.Position[Mesh.Indices[Index * 3 + 1]];
        public ref readonly Vector3 Pc => ref Mesh.Position[Mesh.Indices[Index * 3 + 2]];
        public ref readonly Vector3 Na => ref Mesh.Normal![Mesh.Indices[Index * 3 + 0]];
        public ref readonly Vector3 Nb => ref Mesh.Normal![Mesh.Indices[Index * 3 + 1]];
        public ref readonly Vector3 Nc => ref Mesh.Normal![Mesh.Indices[Index * 3 + 2]];
        public ref readonly Vector2 Ua => ref Mesh.UV![Mesh.Indices[Index * 3 + 0]];
        public ref readonly Vector2 Ub => ref Mesh.UV![Mesh.Indices[Index * 3 + 1]];
        public ref readonly Vector2 Uc => ref Mesh.UV![Mesh.Indices[Index * 3 + 2]];
        public bool HasNormal => Mesh.Normal != null;
        public bool HasUV => Mesh.UV != null;

        public Triangle(TriangleMesh mesh, int index)
        {
            Mesh = mesh ?? throw new ArgumentNullException(nameof(mesh));
            Index = index;

            BoundingBox3F bound = new BoundingBox3F(Pa);
            bound.ExpendBy(Pb);
            bound.ExpendBy(Pc);
            WorldBound = bound;

            SurfaceArea = 0.5f * Vector3.Cross(Pb - Pa, Pc - Pa).Length();
        }

        public override Intersection GetIntersection(Ray3F ray, SurfacePoint surface)
        {
            Vector3 bary = new Vector3(1 - (surface.U + surface.V), surface.U, surface.V);
            Vector3 p = bary.X * Pa + bary.Y * Pb + bary.Z * Pc;
            Vector3 n;
            if (HasNormal)
            {
                n = Vector3.Normalize(bary.X * Na + bary.Y * Nb + bary.Z * Nc);
            }
            else
            {
                n = Vector3.Normalize(Vector3.Cross(Pb - Pa, Pc - Pa));
            }
            Vector2 uv;
            if (HasUV)
            {
                uv = bary.X * Ua + bary.Y * Ub + bary.Z * Uc;
            }
            else
            {
                uv = bary.X * new Vector2(0) + bary.Y * new Vector2(1, 0) + bary.Z * new Vector2(1);
            }
            Coordinate coord = new Coordinate(n);
            return new Intersection(p, uv, surface.T, this, coord);
        }

        public override bool Intersect(Ray3F ray)
        {
            Vector3 edge1 = Pb - Pa;
            Vector3 edge2 = Pc - Pa;
            Vector3 pvec = Vector3.Cross(ray.D, edge2);
            float det = Vector3.Dot(edge1, pvec);
            if (MathF.Abs(det) < float.Epsilon)
            {
                return false;
            }
            float invDet = 1.0f / det;
            Vector3 tvec = ray.O - Pa;
            float u = Vector3.Dot(tvec, pvec) * invDet;
            if (u is < 0.0f or > 1.0f)
            {
                return false;
            }
            Vector3 qvec = Vector3.Cross(tvec, edge1);
            float v = Vector3.Dot(ray.D, qvec) * invDet;
            if (v < 0.0f || u + v > 1.0f)
            {
                return false;
            }
            float t = Vector3.Dot(edge2, qvec) * invDet;
            return t >= ray.MinT && t <= ray.MaxT;
        }

        public override bool Intersect(Ray3F ray, out SurfacePoint surface)
        {
            surface = default;
            Vector3 edge1 = Pb - Pa;
            Vector3 edge2 = Pc - Pa;
            Vector3 pvec = Vector3.Cross(ray.D, edge2);
            float det = Vector3.Dot(edge1, pvec);
            if (MathF.Abs(det) < float.Epsilon)
            {
                return false;
            }
            float invDet = 1.0f / det;
            Vector3 tvec = ray.O - Pa;
            float u = Vector3.Dot(tvec, pvec) * invDet;
            if (u is < 0.0f or > 1.0f)
            {
                return false;
            }
            Vector3 qvec = Vector3.Cross(tvec, edge1);
            float v = Vector3.Dot(ray.D, qvec) * invDet;
            if (v < 0.0f || u + v > 1.0f)
            {
                return false;
            }
            float t = Vector3.Dot(edge2, qvec) * invDet;
            bool isInct = t >= ray.MinT && t <= ray.MaxT;
            surface = isInct ? new SurfacePoint(u, v, t) : default;
            return isInct;
        }

        public override float Pdf(ShapeSampleResult inct)
        {
            return 1.0f / SurfaceArea;
        }

        public override ShapeSampleResult Sample(Random rand, out float pdf)
        {
            Vector2 rng = rand.NextVec2();
            float alpha = 1 - MathF.Sqrt(1 - rng.X);
            float beta = rng.Y * MathF.Sqrt(1 - rng.X);
            Vector3 bary = new Vector3(alpha, beta, (1 - alpha - beta));
            Vector3 p = bary.X * Pa + bary.Y * Pb + bary.Z * Pc;
            Vector3 n;
            if (HasNormal)
            {
                n = Vector3.Normalize(bary.X * Na + bary.Y * Nb + bary.Z * Nc);
            }
            else
            {
                n = Vector3.Normalize(Vector3.Cross(Pb - Pa, Pc - Pa));
            }
            Vector2 uv;
            if (HasUV)
            {
                uv = bary.X * Ua + bary.Y * Ub + bary.Z * Uc;
            }
            else
            {
                uv = bary.X * new Vector2(0) + bary.Y * new Vector2(1, 0) + bary.Z * new Vector2(1);
            }
            Coordinate coord = new Coordinate(n);
            pdf = 1.0f / SurfaceArea;
            return new ShapeSampleResult(p, uv, this, coord);
        }
    }
}
