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
        private static string _mapFilePath;
        private static Dictionary<string, int> _lastSavedMap;

        /// <summary>UNIQUE_TOP(8000) 직전까지 씁니다.</summary>
        public static int End => (int)MusicID.UNIQUE_TOP - 1;

        /// <summary>원본 곡 MusicID 중 SAVEABLE_ID_END 미만의 최댓값 + 1. 게임에 곡이 추가되면 자동으로 밀립니다(C4 (3)).</summary>
        public static int Start
        {
            get
            {
                if (_start < 0)
                {
                    int saveableEnd = (int)MusicID.SAVEABLE_ID_END;
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
        /// 맵 파일 위치를 정합니다: MelonLoader의 모드 데이터 폴더 <c>UserData/GRC2/musicid_map.txt</c>.
        /// savecustomkey는 사용자가 고치는 설정 파일만 두는 폴더이고, hwa는 루트도 앨범으로 스캔돼 txt가 곡 정보로 읽히므로 둘 다 쓰지 않습니다.
        /// 0.6.0 개발 중 잠깐 쓰던 savecustomkey/musicid_map.txt가 있으면 새 위치로 옮깁니다(ID 유지).
        /// </summary>
        public static void Initialize(string gameFolder)
        {
            string userData = null;
            try { userData = MelonLoader.Utils.MelonEnvironment.UserDataDirectory; }
            catch { /* 아래 대체 경로 사용 */ }
            if (string.IsNullOrEmpty(userData) && !string.IsNullOrEmpty(gameFolder))
                userData = Path.Combine(gameFolder, "UserData");
            if (string.IsNullOrEmpty(userData))
                return;

            _mapFilePath = Path.Combine(Path.Combine(userData, DataFolderName), CustomMusicIdAllocator.MapFileName);

            if (!string.IsNullOrEmpty(gameFolder))
                MigrateLegacyMapFile(Path.Combine(Path.Combine(gameFolder, "savecustomkey"), CustomMusicIdAllocator.MapFileName));
        }

        /// <summary>UserData 아래 이 모드의 폴더 이름.</summary>
        public const string DataFolderName = "GRC2";

        private static void MigrateLegacyMapFile(string legacyPath)
        {
            try
            {
                if (!File.Exists(legacyPath))
                    return;

                if (File.Exists(_mapFilePath))
                {
                    // 새 위치가 이미 정본이므로 옛 파일만 지웁니다.
                    File.Delete(legacyPath);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_mapFilePath));
                    File.Move(legacyPath, _mapFilePath);
                }
                MelonLogger.Msg($"[MusicID] 맵 파일을 savecustomkey에서 {_mapFilePath}로 옮겼습니다.");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MusicID] 옛 맵 파일 이동 실패({legacyPath}): {ex.Message}");
            }
        }

        /// <summary>
        /// 앨범 키마다 ID를 정합니다. 맵이 바뀌었을 때만 파일을 씁니다. 곡 선택 씬 한 번에 여러 번 불려도
        /// 같은 결과면 디스크를 건드리지 않습니다.
        /// </summary>
        public static CustomMusicIdAllocator.Result Allocate(IEnumerable<string> albumKeys)
        {
            var existing = _lastSavedMap ?? (_mapFilePath != null
                ? CustomMusicIdAllocator.Load(_mapFilePath)
                : new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase));

            var result = CustomMusicIdAllocator.Allocate(albumKeys, existing, Start, End, IsReserved);

            if (result.Changed && _mapFilePath != null)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_mapFilePath));
                    CustomMusicIdAllocator.Save(_mapFilePath, result.FullMap);
                    MelonLogger.Msg($"[MusicID] 맵 저장: {result.FullMap.Count}개 ({_mapFilePath})");
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[MusicID] 맵 파일을 쓰지 못했습니다(이번 실행에서는 메모리 값으로 진행): {ex.Message}");
                }
            }

            _lastSavedMap = new Dictionary<string, int>(result.FullMap, StringComparer.OrdinalIgnoreCase);

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
