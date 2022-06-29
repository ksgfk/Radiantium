using Radiantium.Core;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using static Radiantium.Core.MathExt;

namespace Radiantium.Offline.Accelerators
{
    //https://github.com/mmp/pbrt-v3/blob/master/src/accelerators/bvh.h
    //大部分基于pbrt

    public enum SplitMethod { SAH, Middle, EqualCounts };

    public class Bvh : Aggregate
    {
        public const int StackSize = 64; //该常数控制求交时, 临时stackalloc栈大小, 不能设的太大

        [StructLayout(LayoutKind.Explicit, Size = 32)] // 我们希望手动控制结构体布局
        private struct LinearBVHNode
        {
            [FieldOffset(0)] public BoundingBox3F Bounds;
            [FieldOffset(24)] public int PrimitivesIndex; //叶节点访问这个值, 代表 这个节点 包含的图元 在图元数组中的索引
            [FieldOffset(24)] public int SecondChildIndex;//中间节点访问这个值, 代表左子节点在线性化节点数组_nodes中下标
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

            public BVHBuildNode(int first, int n, BoundingBox3F b) //构造叶节点
            {
                FirstPrimIndex = first;
                PrimitiveCount = n;
                Bounds = b;
                Left = null!;
                Right = null!;
            }

            public BVHBuildNode(int axis, BVHBuildNode left, BVHBuildNode right) //构造中间节点
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
            public int PrimitiveNumber; //图元在_primitives中的索引
            public BoundingBox3F Bounds;
            public Vector3 Centroid; //质心
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

        readonly int _maxPrimsInNode; //单个节点中最大图元数量
        readonly SplitMethod _splitMethod;
        readonly IReadOnlyList<Primitive> _primitives;
        readonly LinearBVHNode[] _nodes;
        int _leafCount;

        public override BoundingBox3F WorldBound => _nodes[0].Bounds;

        public Bvh(IReadOnlyList<Primitive> p, int maxPrimsInNode, SplitMethod splitMethod)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            if (p.Count == 0) //如果传入的p没有值, 就建一颗空树
            {
                _primitives = new List<Primitive>();
                _nodes = Array.Empty<LinearBVHNode>();
                return;
            }
            _maxPrimsInNode = maxPrimsInNode;
            _splitMethod = splitMethod;
            _primitives = p;
            //计算所有图元在世界空间的包围盒
            List<BVHPrimitiveInfo> primitiveInfo = new List<BVHPrimitiveInfo>(p.Count);
            for (int i = 0; i < p.Count; i++)
            {
                primitiveInfo.Add(new BVHPrimitiveInfo(i, p[i].WorldBound));
            }
            int totalNodes = 0;
            //递归建树后, 图元在数组中排列顺序会改变, 所以传入一个数组储存改变
            List<Primitive> orderedPrims = new List<Primitive>(p.Count);
            BVHBuildNode root = RecursiveBuild(primitiveInfo, 0, p.Count, ref totalNodes, orderedPrims);
            _primitives = orderedPrims; //储存建树后图元数组
            _nodes = new LinearBVHNode[totalNodes];
            int offset = 0;
            FlattenBVHTree(root, ref offset); //将二叉树线性化, 也就是按前序遍历存入数组, 这样访问左子树就只要在当前节点索引+1
            sw.Stop();
            Logger.Lock();
            Logger.Info($"[Offline.Bvh] -> build BVH done.");
            Logger.Info($"    leaf count {_leafCount}");
            Logger.Info($"    node count {_nodes.Length}");
            Logger.Info($"    build time {sw.Elapsed.TotalMilliseconds} ms");
            Logger.Info($"    node used memory {(32.0f * _nodes.Length) / 1024 / 1024} MB");
            Logger.Release();
        }

        public override bool Intersect(Ray3F ray) //这边和下面那个一模一样就不写注释了
        {
            Span<bool> dirIsNeg = stackalloc bool[3];
            dirIsNeg[0] = ray.InvD.X < 0;
            dirIsNeg[1] = ray.InvD.Y < 0;
            dirIsNeg[2] = ray.InvD.Z < 0;
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
            dirIsNeg[1] = ray.InvD.Y < 0;
            dirIsNeg[2] = ray.InvD.Z < 0;
            // 手动模拟栈, nodesToVisit: 储存_nodes下标
            Span<int> nodesToVisit = stackalloc int[64];
            // toVisitOffset: 栈元素数量, currentNodeIndex: 栈顶下标
            int toVisitOffset = 0, currentNodeIndex = 0;
            Intersection nowInct = default;
            nowInct.T = float.MaxValue;
            bool anyHit = false;
            while (true)
            {
                ref LinearBVHNode node = ref _nodes[currentNodeIndex];
                if (node.Bounds.Intersect(ray)) //该节点与光线有交点
                {
                    if (node.PrimitiveCount > 0) //是叶节点
                    {
                        for (int i = 0; i < node.PrimitiveCount; ++i) //精确求交
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
                        currentNodeIndex = nodesToVisit[--toVisitOffset]; //出栈
                    }
                    else
                    {
                        //根据分裂轴和当前光线方向选择下一个是左节点还是右节点
                        if (dirIsNeg[node.Axis]) //方向是负的就选左节点
                        {
                            nodesToVisit[toVisitOffset++] = currentNodeIndex + 1;
                            currentNodeIndex = node.SecondChildIndex;
                        }
                        else //方向是正的就选右节点
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
            BoundingBox3F bounds = new BoundingBox3F(); //计算这个节点的包围盒
            BVHBuildNode node;
            for (int i = start; i < end; i++)
            {
                bounds.Union(primitiveInfo[i].Bounds);
            }
            int nPrimitives = end - start;
            if (nPrimitives == 1) //已经剩一个图元, 就直接为它创建节点
            {
                int firstPrimOffset = orderedPrims.Count;
                for (int i = start; i < end; i++)
                {
                    int primNum = primitiveInfo[i].PrimitiveNumber;
                    orderedPrims.Add(_primitives[primNum]);
                }
                node = new BVHBuildNode(firstPrimOffset, nPrimitives, bounds);
                _leafCount++;
                return node;
            }
            else
            {
                BoundingBox3F centroidBounds = new BoundingBox3F(); //计算图元质心
                for (int i = start; i < end; i++)
                {
                    centroidBounds.ExpendBy(primitiveInfo[i].Centroid);
                }
                int dim = centroidBounds.MaximumExtent(); //根据质心选择分裂的轴. 选择的是长度最长的轴
                int mid = (start + end) / 2; //将图元数组分为两部分, 并分别构建子树
                //如果分裂轴最大和最小值都一样, 也就是图元都挤在一起
                //就将图元都放进同一个节点
                if (IndexerUnsafe(ref centroidBounds.Max, dim) == IndexerUnsafe(ref centroidBounds.Min, dim))
                {
                    int firstPrimOffset = orderedPrims.Count;
                    for (int i = start; i < end; i++)
                    {
                        int primNum = primitiveInfo[i].PrimitiveNumber;
                        orderedPrims.Add(_primitives[primNum]);
                    }
                    node = new BVHBuildNode(firstPrimOffset, nPrimitives, bounds);
                    _leafCount++;
                    return node;
                }
                else
                {
                    //否则根据事先设置好的枚举来分裂
                    switch (_splitMethod)
                    {
                        case SplitMethod.SAH: //TODO: SAH原理还没搞懂, 注释之后再写
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
                                        _leafCount++;
                                        return node;
                                    }
                                }
                                break;
                            }
                        case SplitMethod.Middle: //直接使用质心的中点来分裂图元
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
                        case SplitMethod.EqualCounts: //平均节点内图元数量, 对分裂轴上的图元排序, 保证左节点所有图元坐标都小于右节点
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
                //将数组分裂两部分后, 递归建树
                node = new BVHBuildNode(dim,
                    RecursiveBuild(primitiveInfo, start, mid, ref totalNodes, orderedPrims),
                    RecursiveBuild(primitiveInfo, mid, end, ref totalNodes, orderedPrims));
            }
            return node;
        }

        private int FlattenBVHTree(BVHBuildNode node, ref int offset)
        {
            // 按照深度遍历顺序, 线性化二叉树, 即: 左子树紧靠当前节点后面, 右子树由当前节点储存索引
            // 例如这样一颗二叉树:
            //           A
            //       B       C
            //      D E     F
            // 线性化后在数组中:
            // A B D E C F
            ref LinearBVHNode linearNode = ref _nodes[offset];
            linearNode.Bounds = node.Bounds;
            int myOffset = offset++; //myOffset代表该节点在线性化数组中的下标
            if (node.PrimitiveCount > 0) //节点内有图元说明这是一个叶节点
            {
                linearNode.PrimitivesIndex = node.FirstPrimIndex; //储存图元索引和图元数量
                linearNode.PrimitiveCount = (ushort)node.PrimitiveCount; //注意图元数量最多是ushort.MaxValue
            }
            else //否则是中间节点
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
