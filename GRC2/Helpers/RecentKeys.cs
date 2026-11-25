using System;
using System.Collections.Generic;

namespace GRC2.Helpers
{
    /// <summary>
    /// 최근에 쓴 키를 최대 <see cref="Capacity"/>개까지 기억하고, 넘친 가장 오래된 키를 알려 줍니다(LRU).
    /// 값은 들고 있지 않아 기존 사전 캐시 옆에 붙여 쓸 수 있습니다. Unity 없이 테스트됩니다.
    ///
    /// 쓰는 곳: <c>SceneDetector</c>의 BMS 파싱 캐시(2026-11-25, 알려진_문제 P17). 곡 상한이 7900여 곡이라
    /// 한 실행에서 많은 곡을 플레이하면 쓰지 않는 노트 목록이 계속 쌓였습니다.
    /// </summary>
    public sealed class RecentKeys
    {
        private readonly LinkedList<string> _order = new LinkedList<string>();
        private readonly Dictionary<string, LinkedListNode<string>> _nodes;

        public int Capacity { get; }

        public int Count => _order.Count;

        public RecentKeys(int capacity, StringComparer comparer)
        {
            if (capacity < 1)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
            _nodes = new Dictionary<string, LinkedListNode<string>>(comparer ?? StringComparer.Ordinal);
        }

        /// <summary>키를 가장 최근으로 올립니다. 용량을 넘으면 밀려난 키를 돌려주고, 아니면 null.</summary>
        public string Touch(string key)
        {
            if (key == null)
                return null;

            if (_nodes.TryGetValue(key, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                return null;
            }

            _nodes[key] = _order.AddFirst(key);
            if (_order.Count <= Capacity)
                return null;

            var oldest = _order.Last;
            _order.RemoveLast();
            _nodes.Remove(oldest.Value);
            return oldest.Value;
        }

        public void Clear()
        {
            _order.Clear();
            _nodes.Clear();
        }
    }
}
