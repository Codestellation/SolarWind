using System;
using System.Collections.Generic;

namespace Codestellation.SolarWind.Internals
{
    public class PreemptiveHashSet<T> where T : IEquatable<T>
    {
        private readonly int _size;
        private readonly T[] _ringBuffer;
        private readonly HashSet<T> _index;
        private int _current;

        public PreemptiveHashSet(int size)
        {
            _size = size;
            _ringBuffer = new T[_size];
            _index = new HashSet<T>();
            _current = 0;
        }

        public bool Add(T value)
        {
            if (!_index.Add(value))
            {
                return false;
            }

            _index.Remove(_ringBuffer[_current]);
            _ringBuffer[_current] = value;

            _current = ++_current % _size;


            return true;
        }

        public bool Contains(T value) => _index.Contains(value);

        public void Reset()
        {
            _index.Clear();
            Array.Clear(_ringBuffer, 0, _ringBuffer.Length);
        }
    }
}