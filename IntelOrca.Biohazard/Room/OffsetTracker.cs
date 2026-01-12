using System.Collections;
using System.Collections.Generic;

namespace IntelOrca.Biohazard.Room
{
    internal class OffsetTracker : IEnumerable<int>
    {
        private readonly List<int> _offsets = new List<int>();

        public int GetOrAdd(int offset)
        {
            if (offset == 0)
                return -1;

            var index = _offsets.IndexOf(offset);
            if (index == -1)
            {
                index = _offsets.Count;
                _offsets.Add(offset);
            }
            return index;
        }

        public IEnumerator<int> GetEnumerator() => _offsets.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
