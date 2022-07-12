using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline
{
    public class MediumAdapter
    {
        public Medium? Inside { get; set; }
        public Medium? Outside { get; set; }

        public bool HasMedium => Inside != null || Outside != null;
        public bool HasOutsideMedium => Outside != null;
        public bool HasInsideMedium => Inside != null;

        public MediumAdapter(Medium? inside, Medium? outside)
        {
            Inside = inside;
            Outside = outside;
        }

        public MediumAdapter(Medium? same) : this(same, same) { }
    }

    public struct SampleMediumResult
    {
        public Medium Medium;
        public Vector3 P;
        public Vector3 Wo;
        public Color3F Tr;
        public float T;
        public bool IsSampleMedium;

        public SampleMediumResult(Medium medium, Color3F tr)
        {
            Medium = medium;
            Tr = tr;
            IsSampleMedium = false;
            P = default;
            Wo = default;
            T = default;
        }

        public SampleMediumResult(Medium medium, Vector3 p, Vector3 wo, Color3F tr, float t)
        {
            Medium = medium;
            P = p;
            Wo = wo;
            Tr = tr;
            T = t;
            IsSampleMedium = true;
        }
    }

    public abstract class Medium : IPhaseFunction
    {
        public abstract Color3F Tr(Ray3F ray, Random rand);
        public abstract SampleMediumResult Sample(Ray3F ray, Random rand);
        public abstract float P(Vector3 wo, Vector3 wi);
        public abstract SamplePhaseFunctionResult SampleWi(Vector3 wo, Random rand);
    }
}
