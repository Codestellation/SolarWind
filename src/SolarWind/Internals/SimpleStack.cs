// This is stripped version of the .net core System.Collection.Stack.
// See the original code at https://github.com/dotnet/corefx/blob/master/src/System.Collections/src/System/Collections/Generic/Stack.cs

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Codestellation.SolarWind.Internals
{
    [DebuggerDisplay("Count = {_size}")]
    [Serializable]
    public class SimpleStack<T>
    {
        private const int DefaultCapacity = 4;
        private T[] _array; // Storage for stack elements. Do not rename (binary serialization)
        private int _size;


        public SimpleStack()
        {
            _array = Array.Empty<T>();
        }

        // Create a stack with a specific initial capacity.  The initial capacity

        // must be a non-negative number.

        public SimpleStack(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _array = new T[capacity];
        }

        // Removes all Objects from the Stack.
        public void Clear(Action<T> beforeClear = null)
        {
            if (beforeClear != null)
            {
                Array.ForEach(_array, beforeClear);
            }

            Array.Clear(_array, 0, _size); // Don't need to doc this but we clear the elements so that the gc can reclaim the references.
            _size = 0;
        }

        // Returns an IEnumerator for this Stack.
        public Enumerator GetEnumerator() => new Enumerator(this);


        public bool TryPop(out T result)
        {
            var size = _size - 1;
            T[] array = _array;

            if ((uint)size >= (uint)array.Length)
            {
                result = default;
                return false;
            }

            _size = size;
            result = array[size];

            array[size] = default; // Free memory quicker.
            return true;
        }

        // Pushes an item to the top of the stack.
        public void Push(T item)
        {
            var size = _size;
            T[] array = _array;

            if ((uint)size < (uint)array.Length)
            {
                array[size] = item;
                _size = size + 1;
            }
            else
            {
                PushWithResize(item);
            }
        }

        // Non-inline from Stack.Push to improve its code quality as uncommon path
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void PushWithResize(T item)
        {
            Array.Resize(ref _array, _array.Length == 0 ? DefaultCapacity : 2 * _array.Length);
            _array[_size] = item;
            _size++;
        }

        // Copies the Stack to an array, in the same order Pop would return the items.
        public T[] ToArray()
        {
            if (_size == 0)
            {
                return Array.Empty<T>();
            }

            var objArray = new T[_size];
            var i = 0;
            while (i < _size)
            {
                objArray[i] = _array[_size - i - 1];
                i++;
            }

            return objArray;
        }

        [SuppressMessage("Microsoft.Performance", "CA1815:OverrideEqualsAndOperatorEqualsOnValueTypes", Justification = "not an expected scenario")]
        public struct Enumerator : IEnumerator<T>, IEnumerator
        {
            private readonly SimpleStack<T> _stack;
            private int _index;
            private T _currentElement;

            internal Enumerator(SimpleStack<T> stack)
            {
                _stack = stack;
                _index = -2;
                _currentElement = default;
            }

            public void Dispose() => _index = -1;

            public bool MoveNext()
            {
                bool retval;

                if (_index == -2)
                {
                    // First call to enumerator.
                    _index = _stack._size - 1;
                    retval = _index >= 0;
                    if (retval)
                    {
                        _currentElement = _stack._array[_index];
                    }

                    return retval;
                }

                if (_index == -1)
                {
                    // End of enumeration.
                    return false;
                }

                retval = --_index >= 0;
                if (retval)
                {
                    _currentElement = _stack._array[_index];
                }
                else
                {
                    _currentElement = default;
                }

                return retval;
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

            private void ThrowEnumerationNotStartedOrEnded() => throw new InvalidOperationException(_index == -2 ? "Enumeration is not started" : "Enumeration is ended");

            object IEnumerator.Current => Current;

            void IEnumerator.Reset()
            {
                _index = -2;
                _currentElement = default;
            }
        }
    }
}