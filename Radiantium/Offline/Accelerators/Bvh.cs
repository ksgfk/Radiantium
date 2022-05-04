using Radiantium.Core;
using System.Numerics;
using System.Runtime.InteropServices;
using static Radiantium.Core.MathExt;

namespace Radiantium.Offline.Accelerators
{
    public enum SplitMethod { SAH, Middle, EqualCounts };

    public class Bvh : Aggregate
    {
        public const int StackSize = 64;

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct LinearBVHNode
        {
            [FieldOffset(0)] public BoundingBox3F bounds;
            [FieldOffset(24)] public int primitivesOffset;
            [FieldOffset(24)] public int secondChildOffset;
            [FieldOffset(28)] public ushort nPrimitives;
            [FieldOffset(30)] public byte axis;
        }

        private class BVHBuildNode
        {
            public BoundingBox3F bounds;
            public BVHBuildNode Left;
            public BVHBuildNode Right;
            public int splitAxis;
            public int firstPrimOffset;
            public int nPrimitives;

            public BVHBuildNode(int first, int n, BoundingBox3F b)
            {
                firstPrimOffset = first;
                nPrimitives = n;
                bounds = b;
                Left = null!;
                Right = null!;
            }

            public BVHBuildNode(int axis, BVHBuildNode left, BVHBuildNode right)
            {
                Left = left;
                Right = right;
                bounds = BoundingBox3F.Union(left.bounds, right.bounds);
                splitAxis = axis;
                nPrimitives = 0;
            }
        }

        private class BVHPrimitiveInfo
        {
            public int primitiveNumber;
            public BoundingBox3F bounds;
            public Vector3 centroid;
            public BVHPrimitiveInfo(int primitiveNumber, BoundingBox3F bounds)
            {
                this.primitiveNumber = primitiveNumber;
                this.bounds = bounds;
                centroid = 0.5f * bounds.Min + 0.5f * bounds.Max;
            }
        }

        private struct BucketInfo
        {
            public int count;
            public BoundingBox3F bounds;

            public BucketInfo()
            {
                count = 0;
                bounds = new BoundingBox3F();
            }
        }

        private readonly int maxPrimsInNode;
        private readonly SplitMethod splitMethod;
        private readonly List<Primitive> primitives;
        private readonly LinearBVHNode[] nodes;

        public override BoundingBox3F WorldBound => throw new NotImplementedException();

        public Bvh(List<Primitive> p, int maxPrimsInNode, SplitMethod splitMethod)
        {
            this.maxPrimsInNode = maxPrimsInNode;
            this.splitMethod = splitMethod;
            primitives = p;
            List<BVHPrimitiveInfo> primitiveInfo = new List<BVHPrimitiveInfo>(p.Count);
            for (int i = 0; i < p.Count; i++)
            {
                primitiveInfo.Add(new BVHPrimitiveInfo(i, p[i].WorldBound));
            }
            int totalNodes = 0;
            List<Primitive> orderedPrims = new List<Primitive>(p.Count);
            BVHBuildNode root = RecursiveBuild(primitiveInfo, 0, p.Count, ref totalNodes, orderedPrims);
            primitives = orderedPrims;
            nodes = new LinearBVHNode[totalNodes];
            int offset = 0;
            FlattenBVHTree(root, ref offset);
        }

        public override bool Intersect(Ray3F ray)
        {
            Span<bool> dirIsNeg = stackalloc bool[3];
            dirIsNeg[0] = ray.InvD.X < 0;
            dirIsNeg[1] = ray.InvD.X < 1;
            dirIsNeg[2] = ray.InvD.X < 2;
            Span<int> nodesToVisit = stackalloc int[64];
            int toVisitOffset = 0, currentNodeIndex = 0;
            while (true)
            {
                ref LinearBVHNode node = ref nodes[currentNodeIndex];
                if (node.bounds.Intersect(ray))
                {
                    if (node.nPrimitives > 0)
                    {
                        for (int i = 0; i < node.nPrimitives; ++i)
                        {
                            if (primitives[node.primitivesOffset + i].Intersect(ray))
                            {
                                return true;
                            }
                        }
                        if (toVisitOffset == 0) break;
                        currentNodeIndex = nodesToVisit[--toVisitOffset];
                    }
                    else
                    {
                        if (dirIsNeg[node.axis])
                        {
                            nodesToVisit[toVisitOffset++] = currentNodeIndex + 1;
                            currentNodeIndex = node.secondChildOffset;
                        }
                        else
                        {
                            nodesToVisit[toVisitOffset++] = node.secondChildOffset;
                            currentNodeIndex = currentNodeIndex + 1;
                        }
                    }
                }
                else
                {
                    if (toVisitOffset == 0) break;
                    currentNodeIndex = nodesToVisit[--toVisitOffset];
                }
            }
            return false;
        }

        public override bool Intersect(Ray3F ray, out Intersection inct)
        {
            Span<bool> dirIsNeg = stackalloc bool[3];
            dirIsNeg[0] = ray.InvD.X < 0;
            dirIsNeg[1] = ray.InvD.X < 1;
            dirIsNeg[2] = ray.InvD.X < 2;
            Span<int> nodesToVisit = stackalloc int[64];
            int toVisitOffset = 0, currentNodeIndex = 0;
            Intersection nowInct = default;
            nowInct.T = float.MaxValue;
            bool anyHit = false;
            while (true)
            {
                ref LinearBVHNode node = ref nodes[currentNodeIndex];
                if (node.bounds.Intersect(ray))
                {
                    if (node.nPrimitives > 0)
                    {
                        for (int i = 0; i < node.nPrimitives; ++i)
                        {
                            if (primitives[node.primitivesOffset + i].Intersect(ray, out var thisInct))
                            {
                                anyHit = true;
                                if (thisInct.T < nowInct.T)
                                {
                                    nowInct = thisInct;
                                    ray.MaxT = thisInct.T;
                                }
                            }
                        }
                        if (toVisitOffset == 0) break;
                        currentNodeIndex = nodesToVisit[--toVisitOffset];
                    }
                    else
                    {
                        if (dirIsNeg[node.axis])
                        {
                            nodesToVisit[toVisitOffset++] = currentNodeIndex + 1;
                            currentNodeIndex = node.secondChildOffset;
                        }
                        else
                        {
                            nodesToVisit[toVisitOffset++] = node.secondChildOffset;
                            currentNodeIndex = currentNodeIndex + 1;
                        }
                    }
                }
                else
                {
                    if (toVisitOffset == 0) break;
                    currentNodeIndex = nodesToVisit[--toVisitOffset];
                }
            }
            inct = anyHit ? nowInct : default;
            return anyHit;
        }

        private BVHBuildNode RecursiveBuild(
            List<BVHPrimitiveInfo> primitiveInfo,
            int start, int end,
            ref int totalNodes,
            List<Primitive> orderedPrims)
        {
            totalNodes++;
            BoundingBox3F bounds = new BoundingBox3F();
            BVHBuildNode node;
            for (int i = start; i < end; i++)
            {
                bounds.Union(primitiveInfo[i].bounds);
            }
            int nPrimitives = end - start;
            if (nPrimitives == 1)
            {
                int firstPrimOffset = orderedPrims.Count;
                for (int i = start; i < end; i++)
                {
                    int primNum = primitiveInfo[i].primitiveNumber;
                    orderedPrims.Add(primitives[primNum]);
                }
                node = new BVHBuildNode(firstPrimOffset, nPrimitives, bounds);
                return node;
            }
            else
            {
                BoundingBox3F centroidBounds = new BoundingBox3F();
                for (int i = start; i < end; i++)
                {
                    centroidBounds.ExpendBy(primitiveInfo[i].centroid);
                }
                int dim = centroidBounds.MaximumExtent();
                int mid = (start + end) / 2;
                if (IndexerUnsafe(ref centroidBounds.Max, dim) == IndexerUnsafe(ref centroidBounds.Min, dim))
                {
                    int firstPrimOffset = orderedPrims.Count;
                    for (int i = start; i < end; i++)
                    {
                        int primNum = primitiveInfo[i].primitiveNumber;
                        orderedPrims.Add(primitives[primNum]);
                    }
                    node = new BVHBuildNode(firstPrimOffset, nPrimitives, bounds);
                    return node;
                }
                else
                {
                    switch (splitMethod)
                    {
                        case SplitMethod.SAH:
                            {
                                if (nPrimitives <= 2)
                                {
                                    mid = (start + end) / 2;
                                    primitiveInfo.Sort(start, nPrimitives, Comparer<BVHPrimitiveInfo>.Create((l, r) =>
                                    {
                                        var lv = IndexerUnsafe(ref l.centroid, dim);
                                        var rv = IndexerUnsafe(ref r.centroid, dim);
                                        if (lv == rv) return 0;
                                        return lv < rv ? -1 : 1;
                                    }));
                                }
                                else
                                {
                                    const int nBuckets = 12;
                                    Span<BucketInfo> buckets = stackalloc BucketInfo[nBuckets];
                                    for (int i = 0; i < nBuckets; i++)
                                    {
                                        buckets[i].bounds = new BoundingBox3F();
                                    }
                                    for (int i = start; i < end; i++)
                                    {
                                        var offset = centroidBounds.Offset(primitiveInfo[i].centroid);
                                        int b = (int)(nBuckets * IndexerUnsafe(ref offset.X, dim));
                                        if (b == nBuckets) b = nBuckets - 1;
                                        buckets[b].count++;
                                        buckets[b].bounds.Union(primitiveInfo[i].bounds);
                                    }
                                    Span<float> cost = stackalloc float[nBuckets - 1];
                                    for (int i = 0; i < nBuckets - 1; i++)
                                    {
                                        BoundingBox3F b0 = new BoundingBox3F();
                                        BoundingBox3F b1 = new BoundingBox3F();
                                        int count0 = 0;
                                        int count1 = 0;
                                        for (int j = 0; j <= i; j++)
                                        {
                                            b0.Union(buckets[j].bounds);
                                            count0 += buckets[j].count;
                                        }
                                        for (int j = i + 1; j < nBuckets; j++)
                                        {
                                            b1.Union(buckets[j].bounds);
                                            count1 += buckets[j].count;
                                        }
                                        cost[i] = 1 + (count0 * b0.SurfaceArea + count1 * b1.SurfaceArea) / bounds.SurfaceArea;
                                    }

                                    float minCost = cost[0];
                                    int minCostSplitBucket = 0;
                                    for (int i = 1; i < nBuckets - 1; i++)
                                    {
                                        if (cost[i] < minCost)
                                        {
                                            minCost = cost[i];
                                            minCostSplitBucket = i;
                                        }
                                    }

                                    float leafCost = nPrimitives;
                                    if (nPrimitives > maxPrimsInNode || minCost < leafCost)
                                    {
                                        mid = Partition(primitiveInfo, start, end - 1, pi =>
                                        {
                                            var offset = centroidBounds.Offset(pi.centroid);
                                            int b = (int)(nBuckets * IndexerUnsafe(ref offset, dim));
                                            return b <= minCostSplitBucket;
                                        });
                                    }
                                    else
                                    {
                                        int firstPrimOffset = orderedPrims.Count;
                                        for (int i = start; i < end; i++)
                                        {
                                            int primNum = primitiveInfo[i].primitiveNumber;
                                            orderedPrims.Add(primitives[primNum]);
                                        }
                                        node = new BVHBuildNode(firstPrimOffset, nPrimitives, bounds);
                                        return node;
                                    }
                                }
                                break;
                            }
                        case SplitMethod.Middle:
                            {
                                float pmid = (IndexerUnsafe(ref centroidBounds.Max, dim) + IndexerUnsafe(ref centroidBounds.Min, dim)) / 2;
                                mid = Partition(primitiveInfo, start, end, pi =>
                                {
                                    return IndexerUnsafe(ref pi.centroid, dim) < pmid;
                                });
                                if (mid == start || mid == end)
                                {
                                    mid = (start + end) / 2;
                                    primitiveInfo.Sort(start, nPrimitives, Comparer<BVHPrimitiveInfo>.Create((l, r) =>
                                    {
                                        var lv = IndexerUnsafe(ref l.centroid, dim);
                                        var rv = IndexerUnsafe(ref r.centroid, dim);
                                        if (lv == rv) return 0;
                                        return lv < rv ? -1 : 1;
                                    }));
                                }
                                break;
                            }
                        case SplitMethod.EqualCounts:
                            {
                                mid = (start + end) / 2;
                                primitiveInfo.Sort(start, nPrimitives, Comparer<BVHPrimitiveInfo>.Create((l, r) =>
                                {
                                    var lv = IndexerUnsafe(ref l.centroid, dim);
                                    var rv = IndexerUnsafe(ref r.centroid, dim);
                                    if (lv == rv) return 0;
                                    return lv < rv ? -1 : 1;
                                }));
                                break;
                            }
                        default:
                            break;
                    }
                }
                node = new BVHBuildNode(dim,
                    RecursiveBuild(primitiveInfo, start, mid, ref totalNodes, orderedPrims),
                    RecursiveBuild(primitiveInfo, mid, end, ref totalNodes, orderedPrims));
            }
            return node;
        }

        private int FlattenBVHTree(BVHBuildNode node, ref int offset)
        {
            ref LinearBVHNode linearNode = ref nodes[offset];
            linearNode.bounds = node.bounds;
            int myOffset = offset++;
            if (node.nPrimitives > 0)
            {
                linearNode.primitivesOffset = node.firstPrimOffset;
                linearNode.nPrimitives = (ushort)node.nPrimitives;
            }
            else
            {
                linearNode.axis = (byte)node.splitAxis;
                linearNode.nPrimitives = 0;
                FlattenBVHTree(node.Left, ref offset);
                linearNode.secondChildOffset = FlattenBVHTree(node.Right, ref offset);
            }
            return myOffset;
        }
    }
}
