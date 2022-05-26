using System.Numerics;

namespace Radiantium.Core
{
    public struct Ray3F
    {
        public Vector3 O;
        public Vector3 D;
        public float MinT;
        public float MaxT;
        public Vector3 InvD;

        public Ray3F(Vector3 o, Vector3 d, float minT = float.Epsilon, float maxT = float.MaxValue)
        {
            O = o;
            D = d;
            MinT = minT;
            MaxT = maxT;
            InvD = new Vector3(1.0f / d.X, 1.0f / d.Y, 1.0f / d.Z);
        }

        public Vector3 At(float t)
        {
            return O + t * D;
        }

        public static Ray3F Transform(Ray3F ray, Matrix4x4 m)
        {
            Vector3 o = Vector3.Transform(ray.O, m);
            Vector3 d = Vector3.Normalize(Vector3.TransformNormal(ray.D, m));
            Vector3 max = Vector3.Transform(ray.At(ray.MaxT), m);
            float maxT = Vector3.Distance(o, max);
            return new Ray3F(o, d, ray.MinT, maxT);
        }
    }
}
