using GRC2.Helpers;
using Xunit;

namespace GRC2.Tests
{
    public class BgaSyncPolicyTests
    {
        private const double Long = 1000; // 탐색한 지 오래됨

        private static BgaSyncDecision Decide(
            double bgm, double? prev, double video,
            double length = 200, bool playing = true, double sinceSeek = Long, bool canSetSpeed = true)
            => BgaSyncPolicy.Decide(bgm, prev, video, length, playing, sinceSeek, canSetSpeed);

        [Fact]
        public void FirstTick_DoesNothing()
        {
            Assert.Equal(BgaSyncAction.None, Decide(0.011, null, 2.0).Action);
        }

        [Fact]
        public void BgmNotAdvancingYet_HoldsVideo()
        {
            // 2026-11-24 로그: BGM은 isPlaying인데 시각이 0.011초에 2초 동안 머물렀고, 영상은 먼저 2초를 재생했다.
            var d = Decide(0.011, 0.011, 1.5);
            Assert.Equal(BgaSyncAction.Hold, d.Action);
            Assert.Equal(0.011, d.SeekTime, 6);
        }

        [Fact]
        public void BgmStarts_WhileVideoHeld_SeeksAndResumes()
        {
            var d = Decide(0.12, 0.011, 0.011, playing: false);
            Assert.Equal(BgaSyncAction.Seek, d.Action);
            Assert.Equal(0.12, d.SeekTime, 6);
        }

        [Theory]
        [InlineData(0.108)]
        [InlineData(0.150)]
        [InlineData(0.179)]
        [InlineData(-0.150)]
        public void SmallDrift_AdjustsSpeedInsteadOfSeeking(double drift)
        {
            // 예전에는 이 정도 차이(로그의 0.11~0.18초)마다 1초 간격으로 되감아 영상이 끊겼다.
            var d = Decide(10.0, 9.9, 10.0 + drift);
            Assert.Equal(BgaSyncAction.AdjustSpeed, d.Action);
            if (drift > 0) Assert.True(d.PlaybackSpeed < 1f); // 영상이 앞서면 느리게
            else Assert.True(d.PlaybackSpeed > 1f);
            Assert.InRange(d.PlaybackSpeed, BgaSyncPolicy.MinSpeed, BgaSyncPolicy.MaxSpeed);
        }

        [Fact]
        public void TinyDrift_Ignored()
        {
            Assert.Equal(BgaSyncAction.None, Decide(10.0, 9.9, 10.02).Action);
        }

        [Fact]
        public void LargeDrift_Seeks()
        {
            var d = Decide(7.488, 7.388, 5.75);
            Assert.Equal(BgaSyncAction.Seek, d.Action);
            Assert.Equal(7.488, d.SeekTime, 6);
        }

        [Fact]
        public void LargeDrift_RightAfterSeek_WaitsForDecoder()
        {
            Assert.Equal(BgaSyncAction.None, Decide(7.6, 7.5, 5.75, sinceSeek: 0.3).Action);
        }

        [Fact]
        public void SpeedIsClamped()
        {
            var d = Decide(10.0, 9.9, 10.45);
            Assert.Equal(BgaSyncAction.AdjustSpeed, d.Action);
            Assert.Equal(BgaSyncPolicy.MinSpeed, d.PlaybackSpeed);
        }

        [Fact]
        public void CannotSetSpeed_IgnoresSmallDrift_SeeksMediumDrift()
        {
            Assert.Equal(BgaSyncAction.None, Decide(10.0, 9.9, 10.15, canSetSpeed: false).Action);
            Assert.Equal(BgaSyncAction.Seek, Decide(10.0, 9.9, 10.3, canSetSpeed: false).Action);
        }

        [Fact]
        public void LoopBoundary_DriftIsWrapped()
        {
            // 영상 길이 10초: BGM 20.05초 → 기대 0.05초. 영상이 9.98초면 0.07초 뒤처진 것이지 9.93초 앞선 게 아니다.
            var d = Decide(20.05, 19.95, 9.98, length: 10);
            Assert.Equal(-0.07, d.Drift, 6);
            Assert.Equal(BgaSyncAction.AdjustSpeed, d.Action);
        }

        [Fact]
        public void ExpectedVideoTime_LoopsAndClamps()
        {
            Assert.Equal(3.0, BgaSyncPolicy.ExpectedVideoTime(23.0, 10.0), 6);
            Assert.Equal(0.0, BgaSyncPolicy.ExpectedVideoTime(-1.0, 10.0), 6);
            Assert.Equal(23.0, BgaSyncPolicy.ExpectedVideoTime(23.0, 0), 6);
        }
    }
}
