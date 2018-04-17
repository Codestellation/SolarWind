using System;

namespace Codestellation.SolarWind.Internals
{
    public class StackBasedPool<T>
        where T : class
    {
        private readonly Func<T> _generator;
        private readonly SimpleStack<T> _stack;
        private readonly int _size;

        public StackBasedPool(Func<T> generator)
        {
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            _stack = new SimpleStack<T>();
            _size = 4;
            AddObjects(_size);
        }

        public T Rent()
        {
            lock (_stack)
            {
                if (_stack.TryPop(out T result))
                {
                    return result;
                }
            }

            return _generator();
        }

        public void Return(T value)
        {
            lock (_stack)
            {
                _stack.Push(value);
            }
        }

        private void AddObjects(int size)
        {
            for (var i = 0; i < size; i++)
            {
                _stack.Push(_generator());
            }
        }
    }
}