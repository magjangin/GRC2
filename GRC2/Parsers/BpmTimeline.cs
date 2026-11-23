using System.Collections.Generic;

namespace GRC2.Parsers
{
    /// <summary>
    /// 마디 단위 tick → 초 변환을 BPM 변화 수와 무관하게 O(log n)으로 합니다(알려진_문제 P8).
    ///
    /// <see cref="BmsParser.CalculateTime"/>은 노트마다 BPM 변화 목록을 처음부터 훑어 O(노트 수 × 변화 수)였습니다.
    /// 변속이 많은 차트(채널 03/08로 박자마다 BPM을 바꾸는 곡)에서는 노트 수천 × 변화 수백이 됩니다.
    /// 여기서는 같은 순서·같은 식으로 변화마다의 누적 시간을 한 번 미리 계산해 두고, 노트마다 이분 탐색으로 그 지점부터만 더합니다.
    /// 부동소수 덧셈 순서까지 <see cref="BmsParser.CalculateTime"/>과 같아서 결과가 비트 단위로 같습니다(<c>BpmTimelineTests</c>).
    /// </summary>
    public sealed class BpmTimeline
    {
        private readonly float _baseFreq;
        private readonly float[] _changeTicks;
        // i번째 변화까지 반영한 뒤의 상태(CalculateTime 루프의 time / lastTick / lastFreq).
        private readonly float[] _timeAfter;
        private readonly float[] _lastTickAfter;
        private readonly float[] _freqAfter;

        /// <param name="sortedBpmChanges">Tick 오름차순(같은 Tick은 원래 순서 유지)으로 정렬된 변화 목록.</param>
        public BpmTimeline(float baseFreq, IReadOnlyList<BpmChange> sortedBpmChanges)
        {
            _baseFreq = baseFreq;
            int count = sortedBpmChanges?.Count ?? 0;
            _changeTicks = new float[count];
            _timeAfter = new float[count];
            _lastTickAfter = new float[count];
            _freqAfter = new float[count];

            float time = 0f;
            float lastTick = 0f;
            float lastFreq = baseFreq;
            for (int i = 0; i < count; i++)
            {
                var change = sortedBpmChanges[i];
                if (change.Tick > lastTick)
                {
                    var offset = change.Tick - lastTick;
                    time += offset * 4f * lastFreq;
                    lastTick = change.Tick;
                }
                lastFreq = change.Freq;

                _changeTicks[i] = change.Tick;
                _timeAfter[i] = time;
                _lastTickAfter[i] = lastTick;
                _freqAfter[i] = lastFreq;
            }
        }

        /// <summary>마디 단위 tick을 초로 바꿉니다. <see cref="BmsParser.CalculateTime"/>과 같은 값입니다.</summary>
        public float ToSeconds(float tick)
        {
            if (_changeTicks.Length == 0)
                return tick * 4f * _baseFreq;

            // CalculateTime은 change.Tick > tick인 첫 변화에서 멈추므로, Tick <= tick인 마지막 변화까지 반영된 상태에서 이어 갑니다.
            int last = LastIndexAtOrBefore(tick);
            float time = last >= 0 ? _timeAfter[last] : 0f;
            float lastTick = last >= 0 ? _lastTickAfter[last] : 0f;
            float lastFreq = last >= 0 ? _freqAfter[last] : _baseFreq;

            if (tick > lastTick)
            {
                var offset = tick - lastTick;
                time += offset * 4f * lastFreq;
            }

            return time;
        }

        /// <summary>_changeTicks[i] &lt;= tick인 가장 큰 i. 없으면 -1.</summary>
        private int LastIndexAtOrBefore(float tick)
        {
            int lo = 0;
            int hi = _changeTicks.Length - 1;
            int found = -1;
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                if (_changeTicks[mid] <= tick)
                {
                    found = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            return found;
        }
    }
}
