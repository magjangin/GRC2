using GRC2.Helpers;
using Xunit;

namespace GRC2.Tests
{
    public class MarqueeTimelineTests
    {
        private static MarqueeTimeline Make() => new MarqueeTimeline(startDelay: 1f, endHold: 1f, speed: 10f);

        [Fact]
        public void NoDistance_NeverMoves()
        {
            var t = Make();
            t.Reset(0f);
            for (int i = 0; i < 100; i++)
                Assert.Equal(0f, t.Advance(0.1f));
        }

        [Fact]
        public void WaitsAtStart_ThenScrollsAtSpeed()
        {
            var t = Make();
            t.Reset(20f);
            Assert.Equal(0f, t.Advance(0.9f));
            // 0.1초 지나 시작, 남은 0.4초만큼 이동
            Assert.Equal(4f, t.Advance(0.5f), 3);
        }

        [Fact]
        public void StopsAtEnd_HoldsThenRestarts()
        {
            var t = Make();
            t.Reset(20f);
            t.Advance(1f);
            Assert.Equal(20f, t.Advance(5f));   // 끝을 넘지 않음
            Assert.Equal(20f, t.Advance(0.5f)); // 끝에서 대기
            Assert.Equal(0f, t.Advance(0.6f));  // 처음으로
            Assert.Equal(0f, t.Advance(0.5f));  // 다시 시작 대기
        }

        [Fact]
        public void Reset_StartsOver()
        {
            var t = Make();
            t.Reset(20f);
            t.Advance(1.5f);
            t.Reset(-5f);
            Assert.Equal(0f, t.Distance);
            Assert.Equal(0f, t.Offset);
        }
    }
}
