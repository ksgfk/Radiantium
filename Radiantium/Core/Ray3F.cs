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
    }
}
