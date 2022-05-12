using Radiantium.Core;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Radiantium.Offline.Accelerators
{
    public class Octree : Aggregate
    {
        public const int StackSize = 256;

        private class Node
        {
            public BoundingBox3F Box;
            public List<Primitive>? Primitives;
            public Node[]? Children;
        }

        private class Leaf
        {
            public Primitive[] Primitives;
            public Leaf(Primitive[] primitives)
            {
                Primitives = primitives ?? throw new ArgumentNullException(nameof(primitives));
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct LinearNode
        {
            [FieldOffset(0)] public BoundingBox3F Bound;
            [FieldOffset(24)] public int NodeCount;
            [FieldOffset(24)] public int LeafIndex;
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
                BoundingBox3F bound = new BoundingBox3F();
                List<int> idx = new List<int>(primitives.Count);
                for (int i1 = 0; i1 < primitives.Count; i1++)
                {
                    Primitive? i = primitives[i1];
                    bound.Union(i.WorldBound);
                    idx.Add(i1);
                }
                Node root = RecursionBuild(primitives, idx, bound, bound, 0)!;
                if (root == null)
                {
                    _leaf = new List<Leaf>();
                    _tree = Array.Empty<LinearNode>();
                }
                else
                {
                    _leaf = new List<Leaf>(_leafCount);
                    _tree = new LinearNode[_nodeCount];
                    int allocNode = 1;
                    FlattenNode(root, 0, ref allocNode);
                    _leaf.TrimExcess();
                    if (allocNode != _nodeCount) { throw new NotSupportedException("maybe a bug"); }
                }
                sw.Stop();
                Logger.Info($"Octree: max depth {_nowDepth}");
                Logger.Info($"Octree: leaf count {_leafCount}");
                Logger.Info($"Octree: node count {_nodeCount}");
                Logger.Info($"Octree: build time {sw.Elapsed.TotalMilliseconds} ms");
                Logger.Info($"Octree: memory used {(32.0f * _nodeCount) / 1024 / 1024} MB");
            }
            long after = GC.GetTotalMemory(true);
            //we don't know how many bytes a managed object used
            Logger.Info($"Octree: possible used memory {(after - before) / 1024.0f / 1024} MB (reference only)");
        }

        public override bool Intersect(Ray3F ray)
        {
            if (_tree.Length == 0) { return false; }
            if (!_tree[0].Bound.Intersect(ray)) //first we test root node
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
                if (n.NextHead == -1) //leaf
                {
                    foreach (Primitive p in _leaf[n.LeafIndex].Primitives) //test primitives
                    {
                        if (p.Intersect(ray))
                        {
                            return true;
                        }
                    }
                }
                else //node
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
            if (!_tree[0].Bound.Intersect(ray))
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
                if (n.NextHead == -1) //leaf
                {
                    foreach (Primitive p in _leaf[n.LeafIndex].Primitives) //test primitives
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
                else //node
                {
                    for (int i = 0; i < n.NodeCount; i++) //TODO: sort by T?
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
            BoundingBox3F innerBound,
            BoundingBox3F outBound,
            int depth)
        {
            int cnt = primIndex.Count;
            if (cnt == 0) { return null; }
            _nowDepth = Math.Max(_nowDepth, depth);
            if (cnt <= _maxCount || depth >= _maxDepth)
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
            Vector3 center = innerBound.Center;
            Span<BoundingBox3F> inner = stackalloc BoundingBox3F[8];
            Span<BoundingBox3F> outer = stackalloc BoundingBox3F[8];
            List<int>[] childData = new List<int>[8];
            float extendLen = (_outBound - 1) * 0.5f;
            for (int i = 0; i < 8; i++)
            {
                inner[i] = new BoundingBox3F(center);
                inner[i].ExpendBy(innerBound.GetCorner(i));
                Vector3 min = inner[i].Min - new Vector3(extendLen);
                Vector3 max = inner[i].Max + new Vector3(extendLen);
                outer[i] = new BoundingBox3F(min, max);
                childData[i] = new List<int>();
            }
            foreach (int i in primIndex)
            {
                BoundingBox3F thisObjBound = primitives[i].WorldBound;
                Vector3 centerPoint = thisObjBound.Center;
                bool isInsert = false;
                for (int j = 0; j < 8; j++)
                {
                    if (inner[j].Contains(centerPoint) && outer[j].Contains(thisObjBound))
                    {
                        childData[j].Add(i);
                        isInsert = true;
                        break;
                    }
                }
                if (!isInsert) //primitive too big
                {
                    for (int j = 0; j < 8; j++)
                    {
                        if (inner[j].Overlaps(thisObjBound))
                        {
                            childData[j].Add(i);
                        }
                    }
                }
            }
            for (int i = 0; i < 8; i++) //recalculate bounding box
            {
                BoundingBox3F realBound = new BoundingBox3F();
                for (int j = 0; j < childData[i].Count; j++)
                {
                    realBound.Union(primitives[childData[i][j]].WorldBound);
                }
                outer[i] = realBound;
            }
            Node node = new Node();
            node.Box = outBound;
            node.Children = new Node[8];
            for (int i = 0; i < 8; i++)
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
                for (int i = 0; i < node.Children.Length; i++)
                {
                    if (node.Children[i] != null)
                    {
                        notNullChild++;
                    }
                }
                linear.NodeCount = notNullChild;
                linear.NextHead = all;
                all += notNullChild;
                for (int i = 0, j = 0; i < node.Children.Length || j != notNullChild; i++)
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
