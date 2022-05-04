using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline.Bxdf
{
    public struct IdealReflectionBrdf : IBxdf
    {
        public Color3F R;
        public BxdfType Type => BxdfType.Reflection | BxdfType.Specular;

        public IdealReflectionBrdf(Color3F r)
        {
            R = r;
        }

        public Color3F Fr(Vector3 wo, Vector3 wi)
        {
            return new Color3F(0.0f);
        }

        public float Pdf(Vector3 wo, Vector3 wi)
        {
            return 0.0f;
        }

        public SampleBxdfResult Sample(Vector3 wo, Random rand)
        {
            Vector3 wi = new Vector3(-wo.X, -wo.Y, wo.Z);
            Color3F fr = R / Coordinate.AbsCosTheta(wi);
            return new SampleBxdfResult(wi, fr, 1.0f, Type);
        }
    }
}
