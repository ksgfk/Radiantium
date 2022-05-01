using Radiantium.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Radiantium.Offline
{
    public class Scene
    {
        public Primitive Aggregate { get; }

        public Scene(Primitive primitive)
        {
            Aggregate = primitive ?? throw new ArgumentNullException(nameof(primitive));
        }

        public bool Intersect(Ray3F ray)
        {
            return Aggregate.Intersect(ray);
        }

        public bool Intersect(Ray3F ray, out Intersection inct)
        {
            return Aggregate.Intersect(ray, out inct);
        }
    }
}
