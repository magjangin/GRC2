using System.Collections.Generic;

namespace GRC2.Parsers
{
    /// <summary>
    /// BMS 시간 계산 유틸리티
    /// </summary>
    public static class BmsTimeCalculator
    {
        /// <summary>
        /// Tick을 시간(초)으로 변환
        /// </summary>
        public static float CalculateTime(float tick, float baseBpm, float baseFreq, List<BpmChange> sortedBpmChanges)
        {
            // BPM 변화가 없는 경우
            if (sortedBpmChanges.Count == 0)
            {
                // tick은 measure 단위이므로 1 measure = 4 beats를 곱해야 함
                return tick * 4f * baseFreq;
            }

            // BPM 변화가 있는 경우
            float time = 0f;
            float lastTick = 0f;
            float lastFreq = baseFreq;

            // 정렬된 리스트에서 현재 tick보다 작거나 같은 BPM 변화만 찾기 (이미 정렬되어 있음)
            foreach (var change in sortedBpmChanges)
            {
                if (change.Tick > tick)
                    break; // 정렬되어 있으므로 더 이상 찾을 필요 없음

                // 이전 구간 계산
                if (change.Tick > lastTick)
                {
                    var offset = change.Tick - lastTick;
                    time += offset * 4f * lastFreq;  // 1 measure = 4 beats
                    lastTick = change.Tick;
                }
                lastFreq = change.Freq;
            }

            // 마지막 구간 (현재 tick까지)
            if (tick > lastTick)
            {
                var offset = tick - lastTick;
                time += offset * 4f * lastFreq;
            }

            return time;
        }
    }
}


























