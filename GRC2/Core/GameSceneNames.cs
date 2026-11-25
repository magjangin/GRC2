using System;
using System.Collections.Generic;
using System.Linq;

namespace GRC2.Core
{
    /// <summary>
    /// 모드가 분기에 쓰는 게임 씬 이름 전부(2026-11-25, 4차 감사).
    ///
    /// 예전에는 씬 이름 문자열이 SceneDetector·TextPatch·RecordBlockPatch·CustomAssetManager에 흩어져 있었고,
    /// 게임 업데이트로 씬 이름이 바뀌면 해당 분기(BGM/BGA 주입, 결과 화면 기록 차단, 제목 치환)가 아무 로그 없이 꺼졌습니다.
    /// 이제 이름은 여기에만 두고, 시작할 때 빌드 설정의 씬 목록과 대조해 <c>[호환성]</c> 문제로 셉니다
    /// (<see cref="GameCompatibility.LogStartupReport"/>). 다른 소스에 씬 이름 문자열을 쓰면
    /// <c>SourceRulesTests.ModSource_SceneNamesLiveInGameSceneNames</c>가 실패합니다.
    /// </summary>
    public static class GameSceneNames
    {
        public const string FairyMode = "FairyModeScene";
        public const string PlayMovie = "PlayMovieScene";
        public const string RenderCutin = "RenderCutinScene";
        public const string Result = "RythmGameResultScene";
        public const string SoundPlayer = "SoundPlayerScene";
        public const string MoviePlayerSelect = "MoviePlayer_MovieSelect";

        /// <summary>
        /// 곡 선택 씬 이름의 앞부분. 실제 이름은 <c>MusicSelectScene_Hasegawa</c>라 정확 일치로 비교하면
        /// 분기가 한 번도 실행되지 않습니다(AGENTS.md 5절). 그래서 접두사로 비교합니다.
        /// </summary>
        public const string MusicSelectPrefix = "MusicSelectScene";

        /// <summary>정확한 이름으로 비교하는 씬들. 시작 점검이 빌드 설정에 모두 있는지 봅니다.</summary>
        public static readonly string[] ExactNames =
        {
            FairyMode, PlayMovie, RenderCutin, Result, SoundPlayer, MoviePlayerSelect,
        };

        public static bool IsMusicSelect(string sceneName)
            => sceneName != null && sceneName.StartsWith(MusicSelectPrefix, StringComparison.Ordinal);

        public static bool IsInjectionDisallowed(string sceneName)
            => sceneName == SoundPlayer || sceneName == MoviePlayerSelect;

        /// <summary>
        /// 빌드 설정에 있는 씬 이름 목록과 비교해, 모드가 기대하지만 없는 이름을 돌려줍니다.
        /// 목록이 비어 있으면(읽기 실패) 판단할 수 없으므로 빈 목록을 돌려줍니다.
        /// </summary>
        public static List<string> FindMissing(IEnumerable<string> buildSceneNames)
        {
            var present = new HashSet<string>(
                (buildSceneNames ?? Enumerable.Empty<string>()).Where(n => !string.IsNullOrEmpty(n)),
                StringComparer.Ordinal);
            var missing = new List<string>();
            if (present.Count == 0)
                return missing;

            foreach (var name in ExactNames)
            {
                if (!present.Contains(name))
                    missing.Add(name);
            }

            if (!present.Any(IsMusicSelect))
                missing.Add(MusicSelectPrefix + "*");

            return missing;
        }
    }
}
