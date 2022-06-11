using Radiantium.Core;
using System.Numerics;

namespace Radiantium.Offline
{
    public abstract class Material
    {
        public abstract BxdfType Type { get; }

        public virtual Material? BssrdfAdapter => null;

        public abstract Color3F Fr(Vector3 wo, Vector3 wi, Intersection inct);

        public abstract SampleBxdfResult Sample(Vector3 wo, Intersection inct, Random rand);

        public abstract float Pdf(Vector3 wo, Vector3 wi, Intersection inct);

        public virtual Color3F S(Vector3 po, Vector3 wo, Coordinate co, Vector3 pi, Vector3 wi, Coordinate ci, Vector2 uv) { return new Color3F(0.0f); }

        public virtual BssrdfSurfacePoint SampleS(Vector3 po, Vector3 wo, Coordinate co, Material mo, Vector2 uv, Scene scene, Random rand) { return new BssrdfSurfacePoint(); }
    }
}
