using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Radiantium.Core
{
    public ref struct StaticStack<T>
    {
        private readonly Span<T> _span;
        private int _count;

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _count;
        }

        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _span.Length;
        }

        public StaticStack(Span<T> span)
        {
            _span = span;
            _count = 0;
        }

        public unsafe StaticStack(void* ptr, int length) : this(new Span<T>(ptr, length)) { }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Push(T item)
        {
            var last = _count;
            _count++;
            if (Count > Capacity)
            {
                throw new StackOverflowException();
            }
            _span[last] = item;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Peek()
        {
            Debug.Assert(_count > 0);
            return ref _span[_count - 1];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Pop()
        {
            Debug.Assert(_count > 0);
            _count--;
        }

        public void Clear() { _count = 0; }

        public T[] ToArray()
        {
            if (_count == 0)
            {
                return Array.Empty<T>();
            }
            var hasValueSpan = _span[0.._count];
            var arr = hasValueSpan.ToArray();
            arr.AsSpan().Reverse(); //标准库里是从栈顶往下ToArray...
            return arr;
        }

        public Enumerator GetEnumerator() { return new Enumerator(ref this); }

        public ref struct Enumerator
        {
            private readonly StaticStack<T> _stack;
            private int _idx;

            public ref T Current
            {
                get
                {
                    if (_idx == -2 || _idx == -1) throw new InvalidOperationException();
                    return ref _stack._span[_idx];
                }
            }

            public Enumerator(ref StaticStack<T> stack)
            {
                _stack = stack;
                _idx = -2;
            }

            public bool MoveNext()
            {
                if (_idx == -2)
                {
                    _idx = _stack._count;
                }

                return --_idx >= 0;
            }

            public void Reset() { _idx = -2; }

            public void Dispose() { _idx = -1; }
        }
    }
}
