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
        public Shape Shape;
        public Coordinate Shading;

        public Vector3 N => Shading.Z;

        public Intersection(Vector3 p, Vector2 uV, float t, Shape shape, Coordinate shading)
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
    }
}
