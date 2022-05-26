using Radiantium.Core;

namespace Radiantium.Offline.Mediums
{
    public class Homogeneous : Medium
    {
        public Color3F SigmaA { get; }
        public Color3F SigmaS { get; }
        public Color3F SigmaT { get; }
        public float G { get; }

        public Homogeneous(Color3F sigmaA, Color3F sigmaS, float g)
        {
            SigmaA = sigmaA;
            SigmaS = sigmaS;
            G = g;
        }
    }
}
