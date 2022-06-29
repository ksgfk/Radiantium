using Radiantium.Core;

namespace Radiantium.Offline.Accelerators
{
    public class Naive : Aggregate //这没啥好写的, 就是暴力遍历所有图元
    {
        public IReadOnlyList<Primitive> Primitives { get; }
        public override BoundingBox3F WorldBound { get; }

        public Naive(IReadOnlyList<Primitive> primitives)
        {
            Primitives = primitives ?? throw new ArgumentNullException(nameof(primitives));

            BoundingBox3F bound = new BoundingBox3F();
            foreach (var p in primitives)
            {
                bound.Union(p.WorldBound);
            }
            WorldBound = bound;
        }

        public override bool Intersect(Ray3F ray)
        {
            foreach (var p in Primitives)
            {
                if (p.Intersect(ray)) { return true; }
            }
            return false;
        }

        public override bool Intersect(Ray3F ray, out Intersection inct)
        {
            Intersection nowInct = default;
            nowInct.T = float.MaxValue;
            bool anyHit = false;
            foreach (var p in Primitives)
            {
                if (p.Intersect(ray, out var thisInct))
                {
                    anyHit = true;
                    if (thisInct.T < nowInct.T)
                    {
                        nowInct = thisInct;
                        ray.MaxT = thisInct.T;
                    }
                }
            }
            inct = anyHit ? nowInct : default;
            return anyHit;
        }
    }
}
