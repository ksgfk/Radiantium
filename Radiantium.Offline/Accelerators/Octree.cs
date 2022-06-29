using Radiantium.Core;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Radiantium.Offline.Accelerators
{
    public class Octree : Aggregate //理论上, 这是一颗稀疏+松散八叉树
    {
        public const int StackSize = 256; //该常数控制求交时, 临时stackalloc栈大小, 不能设的太大

        private class Node //建树时节点
        {
            public BoundingBox3F Box;
            public List<Primitive>? Primitives;
            public Node[]? Children;
        }

        private class Leaf //储存最终每个叶节点包含图元
        {
            public Primitive[] Primitives;
            public Leaf(Primitive[] primitives)
            {
                Primitives = primitives ?? throw new ArgumentNullException(nameof(primitives));
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)] //我们希望手动控制结构体布局
        private struct LinearNode
        {
            [FieldOffset(0)] public BoundingBox3F Bound;
            [FieldOffset(24)] public int NodeCount; //中间节点包含的子节点数量
            [FieldOffset(24)] public int LeafIndex; //叶节点数据下标
            [FieldOffset(28)] public int NextHead; //-1: leaf, >=0: node
        }

        readonly List<Leaf> _leaf;
        readonly LinearNode[] _tree;
        readonly int _maxDepth;
        readonly int _maxCount;
        readonly float _outBound;
        int _nowDepth;
        int _leafCount;
        int _nodeCount;

        public override BoundingBox3F WorldBound => _tree[0].Bound;

        public Octree(IReadOnlyList<Primitive> primitives, int maxDepth = 10, int maxCount = 10, float outBound = 1)
        {
            _maxDepth = maxDepth;
            _maxCount = maxCount;
            _outBound = outBound;
            long before = GC.GetTotalMemory(true);
            {
                Stopwatch sw = new Stopwatch();
                sw.Start();
                BoundingBox3F bound = new BoundingBox3F(); //计算根节点包围盒
                List<int> idx = new List<int>(primitives.Count);
                for (int i1 = 0; i1 < primitives.Count; i1++)
                {
                    Primitive? i = primitives[i1];
                    bound.Union(i.WorldBound);
                    idx.Add(i1);
                }
                Node root = RecursionBuild(primitives, idx, bound, bound, 0)!;
                if (root == null) //一个节点都无法建立, 就创建一颗空树
                {
                    _leaf = new List<Leaf>();
                    _tree = Array.Empty<LinearNode>();
                }
                else
                {
                    _leaf = new List<Leaf>(_leafCount);
                    _tree = new LinearNode[_nodeCount];
                    int allocNode = 1;
                    FlattenNode(root, 0, ref allocNode); //线性化二叉树, 同时转化为稀疏八叉树, 数组内稠密排列
                    _leaf.TrimExcess();
                    if (allocNode != _nodeCount) { throw new NotSupportedException("maybe a bug"); }
                }
                sw.Stop();
                Logger.Lock();
                Logger.Info($"[Offline.Octree] -> build octree done.");
                Logger.Info($"    max depth {_nowDepth}");
                Logger.Info($"    leaf count {_leafCount}");
                Logger.Info($"    node count {_nodeCount}");
                Logger.Info($"    build time {sw.Elapsed.TotalMilliseconds} ms");
                Logger.Info($"    memory used {(32.0f * _nodeCount) / 1024 / 1024} MB");
            }
            long after = GC.GetTotalMemory(true);
            //我们无法精确统计managed内存使用情况
            Logger.Info($"    possible used memory {(after - before) / 1024.0f / 1024} MB (reference only)");
            Logger.Release();
        }

        public override bool Intersect(Ray3F ray)
        {
            if (_tree.Length == 0) { return false; }
            if (!_tree[0].Bound.Intersect(ray))
            {
                return false;
            }
            StaticStack<int> q = new StaticStack<int>(stackalloc int[StackSize]);
            q.Push(0);
            while (q.Count > 0)
            {
                int idx = q.Peek();
                q.Pop();
                ref readonly LinearNode n = ref _tree[idx];
                if (n.NextHead == -1)
                {
                    foreach (Primitive p in _leaf[n.LeafIndex].Primitives)
                    {
                        if (p.Intersect(ray))
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < n.NodeCount; i++)
                    {
                        if (_tree[n.NextHead + i].Bound.Intersect(ray))
                        {
                            q.Push(n.NextHead + i);
                        }
                    }
                }
            }
            return false;
        }

        public override bool Intersect(Ray3F ray, out Intersection inct)
        {
            if (_tree.Length == 0)
            {
                inct = default;
                return false;
            }
            if (!_tree[0].Bound.Intersect(ray)) //如果射线不与根节点相交, 那它一定不与任何节点相交
            {
                inct = default;
                return false;
            }
            Intersection nowInct = default;
            bool anyHit = false;
            nowInct.T = float.MaxValue;
            StaticStack<int> q = new StaticStack<int>(stackalloc int[StackSize]);
            //Span<(float, int)> heap = stackalloc (float, int)[10];
            q.Push(0);
            while (q.Count > 0)
            {
                int idx = q.Peek();
                q.Pop();
                ref readonly LinearNode n = ref _tree[idx];
                if (n.NextHead == -1) //叶节点
                {
                    foreach (Primitive p in _leaf[n.LeafIndex].Primitives) //精确求交
                    {
                        if (p.Intersect(ray, out Intersection thisInct))
                        {
                            anyHit = true;
                            if (thisInct.T < nowInct.T)
                            {
                                nowInct = thisInct;
                                ray.MaxT = thisInct.T;
                            }
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < n.NodeCount; i++) //TODO: 根据子节点相交距离插入栈中? 但是排序后它更慢了
                    {
                        if (_tree[n.NextHead + i].Bound.Intersect(ray))
                        {
                            q.Push(n.NextHead + i);
                        }
                    }

                    //int heapCount = 1;
                    //for (int i = 0; i < n.NodeCount; i++)
                    //{
                    //    if (_tree[n.NextHead + i].Bound.Intersect(ray, out float minT, out _))
                    //    {
                    //        int ptr = n.NextHead + i;
                    //        int newNode = heapCount++;
                    //        heap[newNode] = (minT, ptr);
                    //        int parent = newNode / 2;
                    //        while (newNode > 1 && heap[newNode].Item1 < heap[parent].Item1)
                    //        {
                    //            var t = heap[newNode];
                    //            heap[newNode] = heap[parent];
                    //            heap[parent] = t;
                    //            newNode = parent;
                    //            parent /= 2;
                    //        }
                    //    }
                    //}
                    //for (int i = 1; i < heapCount; i++)
                    //{
                    //    q.Push(heap[i].Item2);
                    //}
                    ////for (int i = heapCount - 1; i > 0; i--)
                    ////{
                    ////    q.Push(heap[1].Item2);
                    ////    heap[1] = heap[i];
                    ////    int x = 1;
                    ////    int cnt = i - 1;
                    ////    while (x * 2 <= cnt)
                    ////    {
                    ////        int t = x * 2;
                    ////        if (t + 1 <= cnt && heap[t + 1].Item1 > heap[t].Item1) t++;
                    ////        if (heap[t].Item1 <= heap[x].Item1) break;
                    ////        var temp = heap[x];
                    ////        heap[x] = heap[t];
                    ////        heap[t] = temp;
                    ////        x = t;
                    ////    }
                    ////}
                }
            }
            inct = anyHit ? nowInct : default;
            return anyHit;
        }

        private Node? RecursionBuild(
            IReadOnlyList<Primitive> primitives,
            List<int> primIndex,
            BoundingBox3F innerBound, //代表八叉树节点包围盒
            BoundingBox3F outBound, //代表正好包含范围内所有图元的真实包围盒
            int depth)
        {
            int cnt = primIndex.Count;
            if (cnt == 0) { return null; }
            _nowDepth = Math.Max(_nowDepth, depth); //更新最大深度
            if (cnt <= _maxCount || depth >= _maxDepth) //如果没超过一个节点最大容纳数量, 或者超过树最大深度, 就创建叶节点
            {
                List<Primitive> leafData = new List<Primitive>(primIndex.Count);
                foreach (int i in primIndex)
                {
                    leafData.Add(primitives[i]);
                }
                Node leaf = new Node();
                leaf.Box = outBound;
                leaf.Primitives = leafData;
                _leafCount++;
                _nodeCount++;
                return leaf;
            }
            Vector3 center = innerBound.Center; //包围盒中心
            Span<BoundingBox3F> inner = stackalloc BoundingBox3F[8]; //八叉树 子包围盒
            Span<BoundingBox3F> outer = stackalloc BoundingBox3F[8]; //松散八叉树 子包围盒
            List<int>[] childData = new List<int>[8]; //八个子节点包含图元索引
            float extendLen = (_outBound - 1) * 0.5f; //计算松散八叉树允许的扩展范围
            for (int i = 0; i < 8; i++)
            {
                inner[i] = new BoundingBox3F(center);
                inner[i].ExpendBy(innerBound.GetCorner(i));
                Vector3 min = inner[i].Min - new Vector3(extendLen);
                Vector3 max = inner[i].Max + new Vector3(extendLen);
                outer[i] = new BoundingBox3F(min, max);
                childData[i] = new List<int>();
            }
            foreach (int i in primIndex) //遍历所有图元, 进行包围盒包含测试
            {
                BoundingBox3F thisObjBound = primitives[i].WorldBound; //图元包围盒
                Vector3 centerPoint = thisObjBound.Center; //图元中心
                bool isInsert = false;
                for (int j = 0; j < 8; j++)
                {
                    //如果图元中心在八叉树内, 并且图元包围盒在松散八叉树允许的包围盒内, 就将它插入这个节点
                    if (inner[j].Contains(centerPoint) && outer[j].Contains(thisObjBound))
                    {
                        childData[j].Add(i);
                        isInsert = true;
                        break;
                    }
                }
                if (!isInsert) //没有一个松散节点可以插入, 说明这个节点实在太大了
                {
                    for (int j = 0; j < 8; j++) //只能将它放入所有覆盖了这个图元的节点
                    {
                        if (inner[j].Overlaps(thisObjBound))
                        {
                            childData[j].Add(i);
                        }
                    }
                }
            }
            for (int i = 0; i < 8; i++) //决定好插入的子节点后, 重新计算松散八叉树的包围盒, 精确收缩
            {
                BoundingBox3F realBound = new BoundingBox3F();
                for (int j = 0; j < childData[i].Count; j++)
                {
                    realBound.Union(primitives[childData[i][j]].WorldBound);
                }
                outer[i] = realBound;
            }
            Node node = new Node(); //创建中间节点
            node.Box = outBound;
            node.Children = new Node[8];
            for (int i = 0; i < 8; i++) //递归八个子节点建树
            {
                node.Children[i] = RecursionBuild(primitives, childData[i], inner[i], outer[i], depth + 1)!;
            }
            _nodeCount++;
            return node;
        }

        private void FlattenNode(Node node, int thisIndex, ref int all)
        {
            int thisNodeIndex = thisIndex;
            ref LinearNode linear = ref _tree[thisNodeIndex];
            linear.Bound = node.Box;
            if (node.Children == null) //叶子节点
            {
                if (node.Primitives == null) { throw new ArgumentException("maybe a bug"); }
                Leaf leaf = new Leaf(node.Primitives.ToArray());
                _leaf.Add(leaf);
                linear.LeafIndex = _leaf.Count - 1;
                linear.NextHead = -1;
            }
            else
            {
                int notNullChild = 0;
                for (int i = 0; i < node.Children.Length; i++) //先计算一下非空子节点数量
                {
                    if (node.Children[i] != null)
                    {
                        notNullChild++;
                    }
                }
                linear.NodeCount = notNullChild;
                linear.NextHead = all;
                all += notNullChild; //为非空子节点分配空间
                for (int i = 0, j = 0; i < node.Children.Length || j != notNullChild; i++) //递归非空子节点
                {
                    if (node.Children[i] != null)
                    {
                        FlattenNode(node.Children[i], linear.NextHead + j, ref all);
                        j++;
                    }
                }
            }
        }
    }
}
