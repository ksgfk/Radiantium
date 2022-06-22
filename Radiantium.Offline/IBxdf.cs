using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline
{
    [Flags]
    public enum BxdfType
    {
        Reflection = 0b000001,
        Transmission = 0b000010,
        Diffuse = 0b000100,
        Glossy = 0b001000,
        Specular = 0b010000,
        SubsurfaceScatting = 0b100000
    }

    public struct SampleBxdfResult
    {
        public Vector3 Wi;
        public Color3F Fr;
        public float Pdf;
        public BxdfType Type;

        public bool HasSubsurface => (Type & BxdfType.SubsurfaceScatting) != 0;
        public bool HasTransmission => (Type & BxdfType.Transmission) != 0;
        public bool HasSpecular => (Type & BxdfType.Specular) != 0;

        public SampleBxdfResult(Vector3 wi, Color3F fr, float pdf, BxdfType type)
        {
            Wi = wi;
            Fr = fr;
            Pdf = pdf;
            Type = type;
        }

        public void Deconstruct(out Vector3 wi, out Color3F fr, out float pdf, out BxdfType type)
        {
            wi = Wi;
            fr = Fr;
            pdf = Pdf;
            type = Type;
        }
    }

    public interface IBxdf
    {
        BxdfType Type { get; }

        Color3F Fr(Vector3 wo, Vector3 wi, TransportMode mode);

        SampleBxdfResult Sample(Vector3 wo, Random rand, TransportMode mode);

        float Pdf(Vector3 wo, Vector3 wi, TransportMode mode);
    }
}
