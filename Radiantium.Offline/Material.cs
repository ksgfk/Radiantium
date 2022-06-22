using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline
{
    public enum TransportMode
    {
        Radiance,
        Importance
    }

    public abstract class Material
    {
        public abstract BxdfType Type { get; }

        public virtual Material? BssrdfAdapter => null;

        public abstract Color3F Fr(Vector3 wo, Vector3 wi, Intersection inct, TransportMode mode);

        public abstract SampleBxdfResult Sample(Vector3 wo, Intersection inct, Random rand, TransportMode mode);

        public abstract float Pdf(Vector3 wo, Vector3 wi, Intersection inct, TransportMode mode);

        public virtual SampleBssrdfResult SamplePi(Intersection po, Scene scene, Random rand) { return new SampleBssrdfResult(); }
    }
}
