using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline.Bxdf
{
    //完美反射, 就是镜子
    public struct SpecularReflectionBrdf : IBxdf
    {
        public Color3F R;
        public BxdfType Type => BxdfType.Reflection | BxdfType.Specular;

        public SpecularReflectionBrdf(Color3F r)
        {
            R = r;
        }

        public Color3F Fr(Vector3 wo, Vector3 wi, TransportMode mode)
        {
            return new Color3F(0.0f);
        }

        public float Pdf(Vector3 wo, Vector3 wi, TransportMode mode)
        {
            return 0.0f;
        }

        public SampleBxdfResult Sample(Vector3 wo, Random rand, TransportMode mode)
        {
            if (Coordinate.AbsCosTheta(wo) == 0)
            {
                return new SampleBxdfResult();
            }
            Vector3 wi = new Vector3(-wo.X, -wo.Y, wo.Z);
            Color3F fr = R / Coordinate.AbsCosTheta(wi);
            return new SampleBxdfResult(wi, fr, 1.0f, Type);
        }
    }
}
