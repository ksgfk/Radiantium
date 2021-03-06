using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline
{
    public struct SurfacePoint
    {
        public float U;
        public float V;
        public float T;

        public SurfacePoint(float u, float v, float t)
        {
            U = u;
            V = v;
            T = t;
        }
    }

    public struct Intersection
    {
        public Vector3 P;
        public Vector2 UV;
        public float T;
        public Primitive Shape;
        public Coordinate Shading;
        public Vector3 Wr;

        public Vector3 N => Shading.Z;
        public bool IsLight => Shape.Light != null;
        public AreaLight Light => Shape.Light!;
        public bool HasSurface => Shape.Material != null;
        public Material Surface => Shape.Material!;
        public bool HasMedium => Shape.Medium.HasMedium;
        public bool HasOutsideMedium => Shape.Medium.HasOutsideMedium;
        public bool HasInsideMedium => Shape.Medium.HasInsideMedium;

        public Intersection(Vector3 p, Vector2 uv, float t, Primitive shape, Coordinate shading, Vector3 wr)
        {
            P = p;
            UV = uv;
            T = t;
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            Shading = shading;
            Wr = wr;
        }

        public Ray3F SpawnRay(Vector3 d)
        {
            return new Ray3F(P, d, 0.0001f);
        }

        public Color3F Le(Vector3 w)
        {
            return Shape.Light == null ? new Color3F(0.0f) : Shape.Light.L(this, w);
        }

        public Vector3 ToLocal(Vector3 v)
        {
            return Shading.ToLocal(v);
        }

        public Vector3 ToWorld(Vector3 v)
        {
            return Shading.ToWorld(v);
        }

        public Medium GetMedium(Vector3 w)
        {
            return Vector3.Dot(w, N) > 0 ? Shape.Medium.Outside! : Shape.Medium.Inside!;
        }
    }
}
