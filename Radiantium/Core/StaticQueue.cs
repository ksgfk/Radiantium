using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Radiantium.Core
{
    public ref struct StaticQueue<T>
    {
        private readonly Span<T> _span;
        private int _count;
        private int _head;
        private int _tail;

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

        public StaticQueue(Span<T> span)
        {
            _span = span;
            _count = 0;
            _head = 0;
            _tail = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(T item)
        {
            ReSize();
            _span[_tail] = item;
            _count++;
            MoveNext(ref _tail);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ReSize()
        {
            if (Count <= Capacity)
            {
                return;
            }
            throw new StackOverflowException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void MoveNext(ref int index)
        {
            var tmp = index + 1;
            if (tmp == Capacity)
            {
                tmp = 0;
            }
            index = tmp;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T Peek()
        {
            Debug.Assert(_count > 0);
            return ref _span[_head];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dequeue()
        {
            Debug.Assert(_count > 0);
            MoveNext(ref _head);
            _count--;
        }

        public void Clear()
        {
            _count = 0;
            _head = 0;
            _tail = 0;
        }

        public T[] ToArray()
        {
            if (_count == 0)
            {
                return Array.Empty<T>();
            }

            var arr = new T[_count];
            var arrSpan = new Span<T>(arr);
            if (_head < _tail)
            {
                var qSpan = _span.Slice(_head, _tail - _head);
                qSpan.CopyTo(arrSpan);
            }
            else
            {
                var headSpan = _span.Slice(_head, Capacity - _head);
                var arrHead = arrSpan[..(Capacity - _head)];
                headSpan.CopyTo(arrHead);
                var tailSpan = _span[.._tail];
                var arrTail = arrSpan.Slice(Capacity - _head, _tail);
                tailSpan.CopyTo(arrTail);
            }
            return arr;
        }

        public Enumerator GetEnumerator() { return new(ref this); }

        public ref struct Enumerator
        {
            private readonly StaticQueue<T> _queue;
            private int _idx;

            public Enumerator(ref StaticQueue<T> queue)
            {
                _queue = queue;
                _idx = -1;
            }

            public ref T Current
            {
                get
                {
                    if (_idx is -2 or -1) throw new InvalidOperationException();
                    return ref _queue._span[_idx];
                }
            }

            public bool MoveNext()
            {
                if (_idx == -2) return false;
                _idx++;
                if (_idx == _queue._count)
                {
                    _idx = -2;
                    return false;
                }

                var index = _queue._head + _idx;
                if (index >= _queue.Capacity)
                {
                    index -= _queue.Capacity;
                }

                _idx = index;
                return true;
            }

            public void Reset() { _idx = -1; }

            public void Dispose() { _idx = -2; }
        }
    }
}
