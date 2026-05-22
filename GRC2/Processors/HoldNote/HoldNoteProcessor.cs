using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GRC2.Parsers;
using GRC2.Helpers;
using MelonLoader;

namespace GRC2.Processors
{
    public static partial class HoldNoteProcessor
    {
        private const int SampleRate = 48000;

        private static int ToSampleIndex(float timeSeconds)
        {
            return (int)Math.Round(timeSeconds * SampleRate, MidpointRounding.AwayFromZero);
        }

        private static string BuildEndKey(int lane, bool isLeft, int endSample)
        {
            return $"{lane}_{isLeft}_{endSample}";
        }
        /// <summary>
        /// BPM 기반 시간 오차 허용 범위 계산 (동적)
        /// BPM이 높을수록 타이밍이 더 까다로워지므로 허용 범위를 축소
        /// BPM이 낮을수록 타이밍이 느슨해지므로 허용 범위를 확대
        /// </summary>
        private static float CalculateTimeTolerance(float bpm)
        {
            // 기준: BPM 120 = 0.05초
            // 공식: 0.05 * (120 / BPM)
            // 예: BPM 240 = 0.025초, BPM 60 = 0.10초
            const float BASE_TOLERANCE = 0.05f; // 기본 허용 범위 (초)
            const float BASE_BPM = 120f;        // 기본 BPM
            
            if (bpm <= 0) bpm = BASE_BPM;
            
            float tolerance = BASE_TOLERANCE * (BASE_BPM / bpm);
            
            // 최소/최대 범위 설정 (BPM 변동으로 인한 극단적 값 방지)
            return Math.Max(0.02f, Math.Min(0.15f, tolerance));
        }
        
        /// <summary>
        /// 홀드 노트 시작(02)과 끝(19)을 매칭하고 Duration을 계산합니다.
        /// </summary>
        public static void MatchHoldNotes(List<BmsNote> notes)
        {
            // 같은 레인에서 02(시작)와 19(끝)을 매칭
            // 노트 채널만 필터링 (11, 12, 14, 15, 16, 18)
            var validChannels = BmsNoteDataParser.ChannelToLaneMap.Keys.ToHashSet();
            var holdStarts = notes.Where(n => n.Type == NoteType.Hold && validChannels.Contains(n.Channel)).ToList();
            var holdEnds = notes.Where(n => n.Type == NoteType.HoldEnd && validChannels.Contains(n.Channel)).ToList();

            foreach (var start in holdStarts)
            {
                // 같은 레인과 방향(IsLeft)에서 가장 가까운 19(Hold end) 찾기
                var end = holdEnds
                    .Where(e => e.Lane == start.Lane && e.IsLeft == start.IsLeft && e.Tick > start.Tick)
                    .OrderBy(e => e.Tick)
                    .FirstOrDefault();

                if (end != null)
                {
                    // Duration 계산 (Tick 단위로 저장, 나중에 Time으로 변환됨)
                    start.Duration = end.Tick - start.Tick;
                    
                    // 상호 참조 설정 (직접 연결)
                    start.EndNote = end;
                    end.StartNote = start;
                    
                    // 끝 노트는 제거하지 않고 유지 (connectNodeDataArray에 추가하기 위해)
                }
            }
        }

        /// <summary>
        /// 홀드 끝 노트를 connectNodeDataArray에 추가합니다.
        /// 성능 최적화: 루프 외부에서 필드 정보 캐싱
        /// </summary>
    }
}

