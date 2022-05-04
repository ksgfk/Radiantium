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

        public Vector3 N => Shading.Z;
        public bool IsLight => Shape.Light != null;
        public AreaLight Light => Shape.Light!;

        public Intersection(Vector3 p, Vector2 uV, float t, Primitive shape, Coordinate shading)
        {
            P = p;
            UV = uV;
            T = t;
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            Shading = shading;
        }

        public Ray3F SpawnRay(Vector3 d)
        {
            return new Ray3F(P, d, 0.001f);
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
    }
}
