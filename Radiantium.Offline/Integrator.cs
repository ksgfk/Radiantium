using Radiantium.Core;

namespace Radiantium.Offline
{
    public abstract class Integrator
    {
        public abstract Color3F Li(Ray3F ray, Scene scene, Random rand);
    }
}
