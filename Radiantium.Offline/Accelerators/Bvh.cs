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
            [FieldOffset(0)] public BoundingBox3F Bounds;
            [FieldOffset(24)] public int PrimitivesIndex;
            [FieldOffset(24)] public int SecondChildIndex;
            [FieldOffset(28)] public ushort PrimitiveCount;
            [FieldOffset(30)] public byte Axis;
        }

        private class BVHBuildNode
        {
            public BoundingBox3F Bounds;
            public BVHBuildNode Left;
            public BVHBuildNode Right;
            public int SplitAxis;
            public int FirstPrimIndex;
            public int PrimitiveCount;

            public BVHBuildNode(int first, int n, BoundingBox3F b)
            {
                FirstPrimIndex = first;
                PrimitiveCount = n;
                Bounds = b;
                Left = null!;
                Right = null!;
            }

            public BVHBuildNode(int axis, BVHBuildNode left, BVHBuildNode right)
            {
                Left = left;
                Right = right;
                Bounds = BoundingBox3F.Union(left.Bounds, right.Bounds);
                SplitAxis = axis;
                PrimitiveCount = 0;
            }
        }

        private class BVHPrimitiveInfo
        {
            public int PrimitiveNumber;
            public BoundingBox3F Bounds;
            public Vector3 Centroid;
            public BVHPrimitiveInfo(int primitiveNumber, BoundingBox3F bounds)
            {
                PrimitiveNumber = primitiveNumber;
                Bounds = bounds;
                Centroid = 0.5f * bounds.Min + 0.5f * bounds.Max;
            }
        }

        private struct BucketInfo
        {
            public int Count;
            public BoundingBox3F Bounds;

            public BucketInfo()
            {
                Count = 0;
                Bounds = new BoundingBox3F();
            }
        }

        private readonly int _maxPrimsInNode;
        private readonly SplitMethod _splitMethod;
        private readonly List<Primitive> _primitives;
        private readonly LinearBVHNode[] _nodes;

        public override BoundingBox3F WorldBound => _nodes[0].Bounds;

        public Bvh(List<Primitive> p, int maxPrimsInNode, SplitMethod splitMethod)
        {
            _maxPrimsInNode = maxPrimsInNode;
            _splitMethod = splitMethod;
            _primitives = p;
            List<BVHPrimitiveInfo> primitiveInfo = new List<BVHPrimitiveInfo>(p.Count);
            for (int i = 0; i < p.Count; i++)
            {
                primitiveInfo.Add(new BVHPrimitiveInfo(i, p[i].WorldBound));
            }
            int totalNodes = 0;
            List<Primitive> orderedPrims = new List<Primitive>(p.Count);
            BVHBuildNode root = RecursiveBuild(primitiveInfo, 0, p.Count, ref totalNodes, orderedPrims);
            _primitives = orderedPrims;
            _nodes = new LinearBVHNode[totalNodes];
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
                ref LinearBVHNode node = ref _nodes[currentNodeIndex];
                if (node.Bounds.Intersect(ray))
                {
                    if (node.PrimitiveCount > 0)
                    {
                        for (int i = 0; i < node.PrimitiveCount; ++i)
                        {
                            if (_primitives[node.PrimitivesIndex + i].Intersect(ray))
                            {
                                return true;
                            }
                        }
                        if (toVisitOffset == 0) break;
                        currentNodeIndex = nodesToVisit[--toVisitOffset];
                    }
                    else
                    {
                        if (dirIsNeg[node.Axis])
                        {
                            nodesToVisit[toVisitOffset++] = currentNodeIndex + 1;
                            currentNodeIndex = node.SecondChildIndex;
                        }
                        else
                        {
                            nodesToVisit[toVisitOffset++] = node.SecondChildIndex;
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
                ref LinearBVHNode node = ref _nodes[currentNodeIndex];
                if (node.Bounds.Intersect(ray))
                {
                    if (node.PrimitiveCount > 0)
                    {
                        for (int i = 0; i < node.PrimitiveCount; ++i)
                        {
                            if (_primitives[node.PrimitivesIndex + i].Intersect(ray, out var thisInct))
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
                        if (dirIsNeg[node.Axis])
                        {
                            nodesToVisit[toVisitOffset++] = currentNodeIndex + 1;
                            currentNodeIndex = node.SecondChildIndex;
                        }
                        else
                        {
                            nodesToVisit[toVisitOffset++] = node.SecondChildIndex;
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
                bounds.Union(primitiveInfo[i].Bounds);
            }
            int nPrimitives = end - start;
            if (nPrimitives == 1)
            {
                int firstPrimOffset = orderedPrims.Count;
                for (int i = start; i < end; i++)
                {
                    int primNum = primitiveInfo[i].PrimitiveNumber;
                    orderedPrims.Add(_primitives[primNum]);
                }
                node = new BVHBuildNode(firstPrimOffset, nPrimitives, bounds);
                return node;
            }
            else
            {
                BoundingBox3F centroidBounds = new BoundingBox3F();
                for (int i = start; i < end; i++)
                {
                    centroidBounds.ExpendBy(primitiveInfo[i].Centroid);
                }
                int dim = centroidBounds.MaximumExtent();
                int mid = (start + end) / 2;
                if (IndexerUnsafe(ref centroidBounds.Max, dim) == IndexerUnsafe(ref centroidBounds.Min, dim))
                {
                    int firstPrimOffset = orderedPrims.Count;
                    for (int i = start; i < end; i++)
                    {
                        int primNum = primitiveInfo[i].PrimitiveNumber;
                        orderedPrims.Add(_primitives[primNum]);
                    }
                    node = new BVHBuildNode(firstPrimOffset, nPrimitives, bounds);
                    return node;
                }
                else
                {
                    switch (_splitMethod)
                    {
                        case SplitMethod.SAH:
                            {
                                if (nPrimitives <= 2)
                                {
                                    mid = (start + end) / 2;
                                    primitiveInfo.Sort(start, nPrimitives, Comparer<BVHPrimitiveInfo>.Create((l, r) =>
                                    {
                                        var lv = IndexerUnsafe(ref l.Centroid, dim);
                                        var rv = IndexerUnsafe(ref r.Centroid, dim);
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
                                        buckets[i].Bounds = new BoundingBox3F();
                                    }
                                    for (int i = start; i < end; i++)
                                    {
                                        var offset = centroidBounds.Offset(primitiveInfo[i].Centroid);
                                        int b = (int)(nBuckets * IndexerUnsafe(ref offset.X, dim));
                                        if (b == nBuckets) b = nBuckets - 1;
                                        buckets[b].Count++;
                                        buckets[b].Bounds.Union(primitiveInfo[i].Bounds);
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
                                            b0.Union(buckets[j].Bounds);
                                            count0 += buckets[j].Count;
                                        }
                                        for (int j = i + 1; j < nBuckets; j++)
                                        {
                                            b1.Union(buckets[j].Bounds);
                                            count1 += buckets[j].Count;
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
                                    if (nPrimitives > _maxPrimsInNode || minCost < leafCost)
                                    {
                                        mid = Partition(primitiveInfo, start, end - 1, pi =>
                                        {
                                            var offset = centroidBounds.Offset(pi.Centroid);
                                            int b = (int)(nBuckets * IndexerUnsafe(ref offset, dim));
                                            return b <= minCostSplitBucket;
                                        });
                                    }
                                    else
                                    {
                                        int firstPrimOffset = orderedPrims.Count;
                                        for (int i = start; i < end; i++)
                                        {
                                            int primNum = primitiveInfo[i].PrimitiveNumber;
                                            orderedPrims.Add(_primitives[primNum]);
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
                                    return IndexerUnsafe(ref pi.Centroid, dim) < pmid;
                                });
                                if (mid == start || mid == end)
                                {
                                    mid = (start + end) / 2;
                                    primitiveInfo.Sort(start, nPrimitives, Comparer<BVHPrimitiveInfo>.Create((l, r) =>
                                    {
                                        var lv = IndexerUnsafe(ref l.Centroid, dim);
                                        var rv = IndexerUnsafe(ref r.Centroid, dim);
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
                                    var lv = IndexerUnsafe(ref l.Centroid, dim);
                                    var rv = IndexerUnsafe(ref r.Centroid, dim);
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
            ref LinearBVHNode linearNode = ref _nodes[offset];
            linearNode.Bounds = node.Bounds;
            int myOffset = offset++;
            if (node.PrimitiveCount > 0)
            {
                linearNode.PrimitivesIndex = node.FirstPrimIndex;
                linearNode.PrimitiveCount = (ushort)node.PrimitiveCount;
            }
            else
            {
                linearNode.Axis = (byte)node.SplitAxis;
                linearNode.PrimitiveCount = 0;
                FlattenBVHTree(node.Left, ref offset);
                linearNode.SecondChildIndex = FlattenBVHTree(node.Right, ref offset);
            }
            return myOffset;
        }
    }
}
