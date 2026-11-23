using System;
using System.Collections.Generic;
using System.Linq;
using GRC2.Parsers;
using Xunit;

namespace GRC2.Tests
{
    /// <summary>BpmTimeline(이분 탐색)이 기준 구현 BmsParser.CalculateTime과 비트 단위로 같은 값을 내는지 확인합니다(P8).</summary>
    public class BpmTimelineTests
    {
        private static void AssertSameAsReference(float baseBpm, List<BpmChange> changes, IEnumerable<float> ticks)
        {
            float baseFreq = 60f / baseBpm;
            var sorted = changes.OrderBy(c => c.Tick).ToList();
            var timeline = new BpmTimeline(baseFreq, sorted);

            foreach (float tick in ticks)
            {
                float expected = BmsParser.CalculateTime(tick, baseBpm, baseFreq, sorted);
                float actual = timeline.ToSeconds(tick);
                // 덧셈 순서까지 같으므로 근사가 아니라 정확히 같아야 합니다.
                Assert.True(BitConverter.ToInt32(BitConverter.GetBytes(expected), 0) == BitConverter.ToInt32(BitConverter.GetBytes(actual), 0),
                    $"tick={tick}: 기준 {expected:R}, 타임라인 {actual:R}");
            }
        }

        private static BpmChange Change(float tick, float bpm) => new BpmChange { Tick = tick, Bpm = bpm, Freq = 60f / bpm };

        [Fact]
        public void NoChanges_MatchesReference()
        {
            AssertSameAsReference(150f, new List<BpmChange>(), new[] { -1f, 0f, 0.25f, 1f, 17.5f, 300f });
        }

        [Fact]
        public void ChangesAtZero_Duplicates_AndBoundaries_MatchReference()
        {
            var changes = new List<BpmChange>
            {
                Change(0f, 180f),     // 0번 마디 시작의 변속(change.Tick > lastTick 아님)
                Change(2f, 90f),
                Change(2f, 200f),     // 같은 Tick의 연속 변속: 마지막 값이 이어짐
                Change(3.5f, 60f),
                Change(10f, 240f),
            };
            var ticks = new List<float> { -0.5f, 0f, 1f, 2f, 2.0001f, 3.5f, 3.4999f, 9.99f, 10f, 12.75f };
            foreach (var c in changes) { ticks.Add(c.Tick); ticks.Add(c.Tick + 0.125f); }
            AssertSameAsReference(120f, changes, ticks);
        }

        [Fact]
        public void ManyRandomChanges_MatchReference()
        {
            var rng = new Random(20261124);
            var changes = Enumerable.Range(0, 400)
                .Select(_ => Change((float)(rng.NextDouble() * 200.0), (float)(60.0 + rng.NextDouble() * 240.0)))
                .ToList();
            var ticks = Enumerable.Range(0, 2000).Select(_ => (float)(rng.NextDouble() * 210.0 - 5.0)).ToList();
            AssertSameAsReference(133f, changes, ticks);
        }
    }
}
