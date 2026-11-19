using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GRC2.Helpers;
using IntiCreates;
using IntiCreates.RythmGame;
using MelonLoader;

namespace GRC2.Core
{
    using MusicID = soRythmGameMusicDataMap.MusicID;

    /// <summary>
    /// 커스텀 곡 MusicID 범위·배정·세이브 칸을 관리합니다.
    ///
    /// 외부 맵 파일을 두지 않고, 시작할 때 레거시 맵 파일(<c>UserData/GRC2/musicid_map.txt</c>,
    /// <c>savecustomkey/musicid_map.txt</c>)이나 <c>MelonPreferences.cfg</c>의 <c>[GRC2_MusicID]</c> 항목이 남아 있으면
    /// 모두 지우고 메모리에서만 이름순으로 배정합니다(<see cref="CustomMusicIdAllocator"/>).
    ///
    /// 범위: 원본 곡 다음 값(지금 54)부터 UNIQUE_TOP(8000) 직전까지, 게임이 이름 붙여 쓰는 값(SAVEABLE_ID_END=512 등)은 제외.
    /// 예전 상한은 SAVEABLE_ID_END 전인 511(458곡)이었습니다. 512 이상을 쓰려면 세이브 배열
    /// <c>playerMusicData</c>(기본 512칸)가 그 ID까지 있어야 하므로 <see cref="EnsureSaveSlots"/>로 늘립니다.
    /// 세이브는 JsonUtility JSON이라 배열 길이가 그대로 저장·로드되고, 게임 코드는 0~511만 돌거나(갤러리·업적)
    /// getIsUsableMusicID로 거르므로 늘어난 칸이 원본 기능에 끼어들지 않습니다(모드를 빼도 같음).
    /// 8000 이상은 튜토리얼·미리보기 전용 ID(id >= UNIQUE_TOP 판정)라 쓰지 않습니다.
    /// </summary>
    public static class CustomMusicIdRegistry
    {
        private static HashSet<int> _definedValues;
        private static int _start = -1;
        private static Dictionary<string, int> _sessionMap;

        /// <summary>예전에 MelonPreferences.cfg에서 잠깐 쓰던 섹션 이름(정리용).</summary>
        public const string LegacyPreferencesCategory = "GRC2_MusicID";

        /// <summary>0.6.0까지 맵 파일을 두던 UserData 아래 폴더 이름(정리용).</summary>
        private const string LegacyDataFolderName = "GRC2";

        /// <summary>
        /// UNIQUE_TOP(8000) 직전까지 씁니다. enum 값은 빌드 때 숫자로 박히므로 런타임에 이름으로 읽어
        /// 게임이 값을 옮겨도 따라가게 합니다(<see cref="GameCompatibility.RuntimeEnumValueOr"/>).
        /// </summary>
        public static int End => _end >= 0 ? _end : (_end = UniqueTop - 1);
        private static int _end = -1;

        private static int UniqueTop =>
            GameCompatibility.RuntimeEnumValueOr(typeof(MusicID), nameof(MusicID.UNIQUE_TOP), (int)MusicID.UNIQUE_TOP);

        private static int SaveableIdEnd =>
            GameCompatibility.RuntimeEnumValueOr(typeof(MusicID), nameof(MusicID.SAVEABLE_ID_END), (int)MusicID.SAVEABLE_ID_END);

        /// <summary>원본 곡 MusicID 중 SAVEABLE_ID_END 미만의 최댓값 + 1. 게임에 곡이 추가되면 자동으로 밀립니다(C4 (3)).</summary>
        public static int Start
        {
            get
            {
                if (_start < 0)
                {
                    int saveableEnd = SaveableIdEnd;
                    _start = DefinedValues.Where(v => v >= 0 && v < saveableEnd).DefaultIfEmpty(-1).Max() + 1;
                }
                return _start;
            }
        }

        private static HashSet<int> DefinedValues =>
            _definedValues ?? (_definedValues = new HashSet<int>(Enum.GetValues(typeof(MusicID)).Cast<MusicID>().Select(v => (int)v)));

        /// <summary>게임이 이름을 붙여 쓰는 값(원본 곡·SAVEABLE_ID_END 등)인지.</summary>
        public static bool IsReserved(int value) => DefinedValues.Contains(value);

        /// <summary>커스텀 곡에 배정될 수 있는 값인지(범위 안이고 예약 값이 아님).</summary>
        public static bool IsCustomRange(MusicID id)
        {
            int value = (int)id;
            return value >= Start && value <= End && !IsReserved(value);
        }

        /// <summary>
        /// 메모리 배정을 초기화하고, 예전에 만들어졌던 외부 맵 파일(<c>UserData/GRC2/musicid_map.txt</c>,
        /// <c>savecustomkey/musicid_map.txt</c>)과 <c>MelonPreferences.cfg</c>의 <c>[GRC2_MusicID]</c> 섹션이 남아 있으면 지웁니다.
        /// </summary>
        public static void Initialize(string gameFolder)
        {
            _sessionMap = null;

            var legacyFiles = new List<string>();
            string userData = null;
            try { userData = MelonLoader.Utils.MelonEnvironment.UserDataDirectory; }
            catch { /* 아래 대체 경로 사용 */ }
            if (string.IsNullOrEmpty(userData) && !string.IsNullOrEmpty(gameFolder))
                userData = Path.Combine(gameFolder, "UserData");
            if (!string.IsNullOrEmpty(userData))
                legacyFiles.Add(Path.Combine(Path.Combine(userData, LegacyDataFolderName), CustomMusicIdAllocator.LegacyMapFileName));
            if (!string.IsNullOrEmpty(gameFolder))
                legacyFiles.Add(Path.Combine(Path.Combine(gameFolder, "savecustomkey"), CustomMusicIdAllocator.LegacyMapFileName));

            CleanupLegacyMapFiles(legacyFiles);
            if (!string.IsNullOrEmpty(userData))
                CleanupLegacyPreferencesSection(Path.Combine(userData, "MelonPreferences.cfg"));
        }

        /// <summary>예전 외부 맵 파일과 빈 <c>UserData/GRC2</c> 폴더를 지웁니다.</summary>
        internal static void CleanupLegacyMapFiles(IEnumerable<string> legacyPaths)
        {
            if (legacyPaths == null)
                return;

            foreach (var legacyPath in legacyPaths)
            {
                try
                {
                    if (!string.IsNullOrEmpty(legacyPath) && File.Exists(legacyPath))
                    {
                        File.Delete(legacyPath);
                        MelonLogger.Msg($"[MusicID] 레거시 맵 파일을 삭제했습니다: {legacyPath}");
                    }

                    var dir = string.IsNullOrEmpty(legacyPath) ? null : Path.GetDirectoryName(legacyPath);
                    if (!string.IsNullOrEmpty(dir) &&
                        string.Equals(Path.GetFileName(dir), LegacyDataFolderName, StringComparison.OrdinalIgnoreCase) &&
                        Directory.Exists(dir) && !Directory.GetFileSystemEntries(dir).Any())
                    {
                        Directory.Delete(dir);
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[MusicID] 레거시 맵 파일 삭제 실패({legacyPath}): {ex.Message}");
                }
            }
        }

        /// <summary>MelonPreferences.cfg에 남아 있는 [GRC2_MusicID] 섹션을 지웁니다.</summary>
        internal static void CleanupLegacyPreferencesSection(string cfgPath)
        {
            try
            {
                if (string.IsNullOrEmpty(cfgPath) || !File.Exists(cfgPath))
                    return;

                var lines = File.ReadAllLines(cfgPath);
                var cleaned = CustomMusicIdAllocator.StripIniSection(lines, LegacyPreferencesCategory, out bool removed);
                if (!removed)
                    return;

                File.WriteAllLines(cfgPath, cleaned);
                MelonLogger.Msg($"[MusicID] MelonPreferences.cfg에서 레거시 [{LegacyPreferencesCategory}] 섹션을 정리했습니다.");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MusicID] MelonPreferences.cfg 정리 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 앨범 키마다 메모리에서 ID를 정합니다. 외부 파일을 읽거나 쓰지 않습니다.
        /// 한 실행 안에서는 이미 받은 ID를 유지해 게임을 켠 채 곡을 추가해도 방금 선택·플레이하던 곡의 ID가 흔들리지 않습니다.
        /// </summary>
        public static CustomMusicIdAllocator.Result Allocate(IEnumerable<string> albumKeys)
        {
            var existing = _sessionMap ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var result = CustomMusicIdAllocator.Allocate(albumKeys, existing, Start, End, IsReserved);
            _sessionMap = new Dictionary<string, int>(result.FullMap, StringComparer.OrdinalIgnoreCase);

            if (result.Skipped.Count > 0)
            {
                MelonLogger.Warning($"[MusicID] 커스텀 MusicID({Start}~{End})가 모자라 {result.Skipped.Count}곡을 건너뜁니다: " +
                    string.Join(", ", result.Skipped.Take(10)) + (result.Skipped.Count > 10 ? " …" : ""));
            }

            return result;
        }

        /// <summary>
        /// 세이브 배열이 maxId까지 들어가도록 늘립니다. 결과 화면이 playerMusicData[커스텀 ID]를 바로 인덱싱하므로
        /// 512 이상 ID의 곡을 플레이하기 전에 반드시 있어야 합니다. 이미 충분하면 아무것도 하지 않습니다.
        /// </summary>
        public static void EnsureSaveSlots(int maxId)
        {
            try
            {
                var gameData = SingletonMonoBehaviour<sSaveDataDirector>.Instance?.getCurrentActiveGameData();
                var current = gameData?.playerMusicData;
                if (current == null || maxId < current.Length)
                    return;

                var expanded = new sSaveDataDirector.PlayerMusicPlayData[maxId + 1];
                Array.Copy(current, expanded, current.Length);
                for (int i = current.Length; i < expanded.Length; i++)
                {
                    // 커스텀 곡은 목록에서 NEW 표시를 쓰지 않으므로(isNeedDispNew=false) 세이브 값도 맞춰 둡니다.
                    expanded[i] = new sSaveDataDirector.PlayerMusicPlayData { isNew = false };
                }

                gameData.playerMusicData = expanded;
                MelonLogger.Msg($"[MusicID] 세이브 곡 칸을 {current.Length} → {expanded.Length}개로 늘렸습니다(커스텀 MusicID {maxId}까지).");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MusicID] 세이브 곡 칸 확장 실패: {ex.Message}");
            }
        }
    }
}
