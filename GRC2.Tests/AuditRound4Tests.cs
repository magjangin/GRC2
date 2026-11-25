using System;
using System.Collections.Generic;
using GRC2.Core;
using GRC2.Helpers;
using Xunit;

namespace GRC2.Tests
{
    /// <summary>2026-11-25 4차 감사에서 추가한 도우미(씬 이름 대조, LRU 키, 경고 횟수 제한) 테스트.</summary>
    public class GameSceneNamesTests
    {
        // 2026-11-24 게임 로그에서 실측한 빌드 설정 씬 목록(BuildIndex 0~8).
        private static readonly string[] MeasuredBuildScenes =
        {
            "BootScene", "LogoScene", "TitleScene", "FairyModeScene", "PlayMovieScene",
            "RythmGameResultScene", "MainMenuScene", "MusicSelectScene_Hasegawa", "RenderCutinScene",
        };

        [Fact]
        public void FindMissing_MeasuredBuild_ReportsOnlyScenesNotInBuild()
        {
            // SoundPlayerScene·MoviePlayer_MovieSelect는 실측 로그에 없었지만 빌드 설정 9개 밖일 수도 있어 목록으로 확인합니다.
            var missing = GameSceneNames.FindMissing(MeasuredBuildScenes);
            Assert.DoesNotContain(GameSceneNames.FairyMode, missing);
            Assert.DoesNotContain(GameSceneNames.Result, missing);
            Assert.DoesNotContain(GameSceneNames.MusicSelectPrefix + "*", missing);
        }

        [Fact]
        public void FindMissing_RenamedScene_IsReported()
        {
            var renamed = new List<string>(MeasuredBuildScenes);
            renamed.Remove("RythmGameResultScene");
            renamed.Add("RhythmGameResultScene");
            renamed.Remove("MusicSelectScene_Hasegawa");
            Assert.Contains(GameSceneNames.Result, GameSceneNames.FindMissing(renamed));
            Assert.Contains(GameSceneNames.MusicSelectPrefix + "*", GameSceneNames.FindMissing(renamed));
        }

        [Fact]
        public void FindMissing_EmptyList_CannotJudge()
        {
            Assert.Empty(GameSceneNames.FindMissing(Array.Empty<string>()));
            Assert.Empty(GameSceneNames.FindMissing(null));
        }

        [Fact]
        public void Predicates_MatchRouting()
        {
            Assert.True(GameSceneNames.IsMusicSelect("MusicSelectScene_Hasegawa"));
            Assert.False(GameSceneNames.IsMusicSelect(null));
            Assert.True(GameSceneNames.IsInjectionDisallowed("SoundPlayerScene"));
            Assert.False(GameSceneNames.IsInjectionDisallowed("FairyModeScene"));
        }
    }

    public class RecentKeysTests
    {
        [Fact]
        public void Touch_EvictsLeastRecentlyUsed()
        {
            var keys = new RecentKeys(2, StringComparer.OrdinalIgnoreCase);
            Assert.Null(keys.Touch("a"));
            Assert.Null(keys.Touch("b"));
            Assert.Null(keys.Touch("A"));      // 대소문자 무시: a를 최근으로 올림
            Assert.Equal("b", keys.Touch("c")); // 가장 오래 안 쓴 b가 밀림
            Assert.Equal(2, keys.Count);
        }

        [Fact]
        public void Clear_ForgetsEverything()
        {
            var keys = new RecentKeys(1, null);
            keys.Touch("a");
            keys.Clear();
            Assert.Null(keys.Touch("b"));
        }

        [Fact]
        public void Capacity_MustBePositive()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RecentKeys(0, null));
        }
    }

    [Collection("ModLog 전역 상태")]
    public class ModLogThrottleTests
    {
        [Fact]
        public void ShouldLogThrottled_LogsFirstAndPowersOfTen()
        {
            string key = "test-" + Guid.NewGuid();
            var logged = new List<int>();
            for (int i = 0; i < 1000; i++)
            {
                if (ModLog.ShouldLogThrottled(key, out int count))
                    logged.Add(count);
            }
            Assert.Equal(new[] { 1, 10, 100, 1000 }, logged);
        }

        [Fact]
        public void ShouldLogThrottled_KeysAreIndependent()
        {
            string a = "a-" + Guid.NewGuid(), b = "b-" + Guid.NewGuid();
            Assert.True(ModLog.ShouldLogThrottled(a, out _));
            Assert.False(ModLog.ShouldLogThrottled(a, out _));
            Assert.True(ModLog.ShouldLogThrottled(b, out _));
        }
    }
}
