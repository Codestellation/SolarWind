using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Codestellation.SolarWind
{
    [DebuggerDisplay("Count = {Count}")]
    public class SimpleQueue<T> : IReadOnlyCollection<T>
    {
        private T[] _array;
        private int _head; // The index from which to dequeue if the queue isn't empty.
        private int _tail; // The index at which to enqueue if the queue isn't full.
        private int _size; // Number of elements.

        private const int MinimumGrow = 4;
        private const int GrowFactor = 200; // double each time

        // Creates a queue with room for capacity objects. The default grow factor
        // is used.
        public SimpleQueue(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _array = new T[capacity];
        }

        public int Count => _size;

        // Removes all Objects from the queue.
        public void Clear()
        {
            if (_size != 0)
            {
                if (_head < _tail)
                {
                    Array.Clear(_array, _head, _size);
                }
                else
                {
                    Array.Clear(_array, _head, _array.Length - _head);
                    Array.Clear(_array, 0, _tail);
                }

                _size = 0;
            }

            _head = 0;
            _tail = 0;
        }

        //public ValueTask<T> WaitNext()
        //{

        //}

        // Adds item to the tail of the queue.
        public void Enqueue(in T item)
        {
            if (_size == _array.Length)
            {
                int newcapacity = (int)((long)_array.Length * (long)GrowFactor / 100);
                if (newcapacity < _array.Length + MinimumGrow)
                {
                    newcapacity = _array.Length + MinimumGrow;
                }

                SetCapacity(newcapacity);
            }

            _array[_tail] = item;
            MoveNext(ref _tail);
            _size++;
        }

        // GetEnumerator returns an IEnumerator over this Queue.  This
        // Enumerator will support removing.
        public Enumerator GetEnumerator() => new Enumerator(this);

        /// <internalonly/>
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        public bool TryDequeue(out T result)
        {
            if (_size == 0)
            {
                result = default;
                return false;
            }

            int head = _head;
            T[] array = _array;

            result = array[head];
            array[head] = default;

            MoveNext(ref _head);
            _size--;
            return true;
        }

        // PRIVATE Grows or shrinks the buffer to hold capacity objects. Capacity
        // must be >= _size.
        private void SetCapacity(int capacity)
        {
            T[] newarray = new T[capacity];
            if (_size > 0)
            {
                if (_head < _tail)
                {
                    Array.Copy(_array, _head, newarray, 0, _size);
                }
                else
                {
                    Array.Copy(_array, _head, newarray, 0, _array.Length - _head);
                    Array.Copy(_array, 0, newarray, _array.Length - _head, _tail);
                }
            }

            _array = newarray;
            _head = 0;
            _tail = (_size == capacity) ? 0 : _size;
        }

        // Increments the index wrapping it if necessary.
        private void MoveNext(ref int index)
        {
            // It is tempting to use the remainder operator here but it is actually much slower
            // than a simple comparison and a rarely taken branch.
            // JIT produces better code than with ternary operator ?:
            int tmp = index + 1;
            if (tmp == _array.Length)
            {
                tmp = 0;
            }

            index = tmp;
        }

        private void ThrowForEmptyQueue() => throw new InvalidOperationException("Queue is empty");

        [SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Justification = "not an expected scenario")]
        public struct Enumerator : IEnumerator<T>
        {
            private readonly SimpleQueue<T> _q;
            private int _index; // -1 = not started, -2 = ended/disposed
            private T _currentElement;

            internal Enumerator(SimpleQueue<T> q)
            {
                _q = q;
                _index = -1;
                _currentElement = default;
            }

            public void Dispose()
            {
                _index = -2;
                _currentElement = default;
            }

            public bool MoveNext()
            {
                if (_index == -2)
                {
                    return false;
                }

                _index++;

                if (_index == _q._size)
                {
                    // We've run past the last element
                    _index = -2;
                    _currentElement = default;
                    return false;
                }

                // Cache some fields in locals to decrease code size
                T[] array = _q._array;
                int capacity = array.Length;

                // _index represents the 0-based index into the queue, however the queue
                // doesn't have to start from 0 and it may not even be stored contiguously in memory.

                int arrayIndex = _q._head + _index; // this is the actual index into the queue's backing array
                if (arrayIndex >= capacity)
                {
                    // NOTE: Originally we were using the modulo operator here, however
                    // on Intel processors it has a very high instruction latency which
                    // was slowing down the loop quite a bit.
                    // Replacing it with simple comparison/subtraction operations sped up
                    // the average foreach loop by 2x.

                    arrayIndex -= capacity; // wrap around if needed
                }

                _currentElement = array[arrayIndex];
                return true;
            }

            public T Current
            {
                get
                {
                    if (_index < 0)
                    {
                        ThrowEnumerationNotStartedOrEnded();
                    }

                    return _currentElement;
                }
            }

            private void ThrowEnumerationNotStartedOrEnded() => throw new InvalidOperationException(_index == -1 ? "Enumeration has not started" : "Enumeration has ended");

            object IEnumerator.Current => Current;

            void IEnumerator.Reset()
            {
                _index = -1;
                _currentElement = default;
            }
        }
    }
}