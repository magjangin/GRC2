using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using IntiCreates;
using IntiCreates.RythmGame;
using IntiCreates.RythmGame.FairyMode;
using MelonLoader;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GRC2.Core
{
    /// <summary>
    /// 게임 업데이트 내성 점검. 이 모드는 게임 내부 필드·메서드를 이름으로 건드립니다. 게임이 업데이트되어 이름이 바뀌면
    /// 지금까지는 어디가 왜 깨졌는지 알 수 없었습니다. 이 클래스는 시작할 때 (1) 설치된 게임 빌드가 검증된 빌드와 같은지,
    /// (2) 이름으로 접근하는 필드가 아직 있고 타입이 같은지를 확인해 로그 한 곳에 모아 남깁니다.
    /// 점검은 읽기 전용이며, 결과가 나빠도 모드 동작은 막지 않습니다(진단 전용).
    /// </summary>
    public static class GameCompatibility
    {
        /// <summary>마지막으로 인게임 검증한 Steam Build ID (AGENTS.md 1절과 맞출 것).</summary>
        public const string TestedBuildId = "13530823";

        /// <summary>마지막으로 검증한 Assembly-CSharp.dll의 MVID. Steam이 아닌 사본에서도 업데이트 여부를 알 수 있습니다.</summary>
        public const string TestedAssemblyMvid = "90b8351d-201c-4b67-8d98-87031a628c8e";

        /// <summary>검증 빌드에서의 커스텀 MusicID 시작 번호(원본 곡 마지막 ID 53 + 1). 다르면 게임에 원본 곡이 추가된 것입니다.</summary>
        public const int TestedCustomMusicIdStart = 54;

        /// <summary>
        /// 마지막으로 검증한 MelonLoader 버전(주.부.수). 게임이 그대로여도 MelonLoader를 올리면 Harmony 동작
        /// (예: <c>HarmonyDontPatchAll</c>, 자동 패치 규칙)과 Unity 바인딩이 바뀔 수 있어 따로 봅니다(2026-11-24).
        /// </summary>
        public const string TestedMelonLoaderVersion = "0.7.3";

        /// <summary>마지막으로 검증한 Unity 엔진 버전(<c>Application.unityVersion</c>). 바뀌면 TMP·VideoPlayer·오디오 동작이 달라질 수 있습니다.</summary>
        public const string TestedUnityVersion = "2021.3.31f1";

        /// <summary>어셈블리 버전을 "주.부.수"로 줄입니다(0.7.3.0 → 0.7.3). null이면 null.</summary>
        public static string ToThreePartVersion(Version version)
        {
            if (version == null)
                return null;
            int build = version.Build < 0 ? 0 : version.Build;
            return $"{version.Major}.{version.Minor}.{build}";
        }

        /// <summary>지금 로드된 MelonLoader 어셈블리의 버전(주.부.수). const인 BuildInfo.Version은 빌드 때 박히므로 쓰지 않습니다.</summary>
        public static string RuntimeMelonLoaderVersion()
        {
            try { return ToThreePartVersion(typeof(MelonLogger).Assembly.GetName().Version); }
            catch { return null; }
        }

        /// <summary>
        /// 모드가 직접 기대하는 게임 enum 상수·const 값 계약. 게임 업데이트로 값이 바뀌면 시작 로그와 테스트가 잡습니다.
        ///
        /// 주의: C#은 enum 멤버와 <c>const</c> 필드를 <b>모드 DLL을 빌드할 때 숫자로 박아 넣습니다</b>(인라인).
        /// 그래서 <c>(int)MusicID.UNIQUE_TOP</c>처럼 코드로 읽으면 게임이 값을 바꿔도 예전 빌드의 숫자가 나와
        /// 점검이 영원히 통과합니다(2026-11-20까지의 버그). 실제 값은 반드시 이름으로 런타임에 읽습니다
        /// (<see cref="RuntimeEnumValue"/>, <see cref="RuntimeConstValue"/>).
        /// </summary>
        public struct ConstantContract
        {
            /// <summary>소유 타입 이름(enum 이름 또는 클래스 이름). 소스 커버리지 테스트가 씁니다.</summary>
            public readonly string OwnerName;
            /// <summary>enum 멤버 또는 const 필드 이름.</summary>
            public readonly string MemberName;
            public readonly int Expected;
            private readonly Func<int?> _readActual;

            public ConstantContract(string ownerName, string memberName, int expected, Func<int?> readActual)
            {
                OwnerName = ownerName;
                MemberName = memberName;
                Expected = expected;
                _readActual = readActual;
            }

            public string Description => $"{OwnerName}.{MemberName}";

            /// <summary>지금 로드된 게임 어셈블리의 값. 이름이 없으면 null.</summary>
            public int? Actual => _readActual?.Invoke();

            public bool IsBroken => Actual != Expected;

            public override string ToString()
            {
                var actual = Actual;
                return actual.HasValue
                    ? $"{Description}: 기대 {Expected}, 현재 {actual.Value}"
                    : $"{Description}: 기대 {Expected}, 현재 없음(이름이 바뀌었거나 삭제됨)";
            }
        }

        private static ConstantContract EnumContract<TEnum>(string memberName, int expected) where TEnum : struct
            => new ConstantContract(typeof(TEnum).Name, memberName, expected, () => RuntimeEnumValue(typeof(TEnum), memberName));

        private static ConstantContract ConstContract(Type owner, string fieldName, int expected)
            => new ConstantContract(owner.Name, fieldName, expected, () => RuntimeConstValue(owner, fieldName));

        /// <summary>
        /// 모드 코드가 이름으로 쓰는 게임 enum 멤버·const 전부. 소스에서 새 게임 enum 멤버를 쓰면
        /// <c>ConstantContracts_CoverEveryGameEnumMemberInSource</c> 테스트가 여기 한 줄 넣으라고 실패합니다.
        /// </summary>
        public static readonly ConstantContract[] ConstantContracts =
        {
            // MusicID 범위(CustomMusicIdRegistry): 512 = 세이브 경계, 8000 이상 = 튜토리얼·미리보기 전용.
            EnumContract<soRythmGameMusicDataMap.MusicID>("SAVEABLE_ID_END", 512),
            EnumContract<soRythmGameMusicDataMap.MusicID>("UNIQUE_TOP", 8000),
            // BGM 48kHz 전제(알려진_문제 B1).
            ConstContract(typeof(cBGMBeatManager), "defaultFrequency", 48000),
            // 노트 변환(EnumValueHelper·BmsNoteConverter): 값이 바뀌면 노트 종류·방향·레인이 엉뚱하게 주입됩니다.
            EnumContract<NoteTypeId>("Touch", 0),
            EnumContract<NoteTypeId>("Flick", 1),
            EnumContract<NoteTypeId>("Fairy", 2),
            EnumContract<NoteTypeId>("Hold", 3),
            EnumContract<NoteTypeId>("Hold_Middle", 5),
            EnumContract<NoteDirectionIndex>("LEFT_BOTTOM", 0),
            EnumContract<NoteDirectionIndex>("CENTER_BOTTOM", 1),
            EnumContract<NoteDirectionIndex>("RIGHT_BOTTOM", 2),
            EnumContract<NoteDirectionIndex>("LEFT_MIDDLE", 3),
            EnumContract<NoteDirectionIndex>("CENTER_MIDDLE", 4),
            EnumContract<NoteDirectionIndex>("RIGHT_MIDDLE", 5),
            EnumContract<NoteDirectionIndex>("LEFT_TOP", 6),
            EnumContract<NoteDirectionIndex>("CENTER_TOP", 7),
            EnumContract<NoteDirectionIndex>("RIGHT_TOP", 8),
            EnumContract<NoteDirectionIndex>("NUM", 9), // slideEndFlickDirection "없음" 표시(NoteCreateDataBuilder)
            EnumContract<NoteSubLaneType>("Lane_1", 0),
            EnumContract<NoteSubLaneType>("Lane_2", 1),
            EnumContract<NoteSubLaneType>("Lane_3", 2),
            EnumContract<NoteLaneLeftRight>("Left", 0),
            EnumContract<NoteLaneLeftRight>("Right", 1),
            // 판정(JudgePerfectPatch·AutoPlayPatch 등).
            EnumContract<JudgeType>("PERFECT", 0),
            EnumContract<JudgeType>("MISS", 4),
            EnumContract<JudgeType>("INVALID", 5),
        };

        /// <summary>enum 멤버의 값을 지금 로드된 게임 어셈블리에서 이름으로 읽습니다. 이름이 없으면 null.</summary>
        public static int? RuntimeEnumValue(Type enumType, string memberName)
        {
            try
            {
                if (enumType == null || !enumType.IsEnum || string.IsNullOrEmpty(memberName))
                    return null;
                if (!Enum.GetNames(enumType).Contains(memberName))
                    return null;
                return Convert.ToInt32(Enum.Parse(enumType, memberName));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>정수 const/static 필드 값을 지금 로드된 게임 어셈블리에서 이름으로 읽습니다. 없으면 null.</summary>
        public static int? RuntimeConstValue(Type owner, string fieldName)
        {
            try
            {
                var field = owner?.GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null)
                    return null;
                return Convert.ToInt32(field.IsLiteral ? field.GetRawConstantValue() : field.GetValue(null));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 범위 계산처럼 값 자체가 필요한 곳에서 씁니다. 런타임 값을 우선하고, 이름이 없어졌으면 빌드 당시 값으로 대체합니다.
        /// </summary>
        public static int RuntimeEnumValueOr(Type enumType, string memberName, int fallback)
            => RuntimeEnumValue(enumType, memberName) ?? fallback;

        /// <summary>상수 계약 중 깨진 것을 설명 문자열로 돌려줍니다.</summary>
        public static List<string> FindBrokenConstantContracts()
        {
            return ConstantContracts.Where(c => c.IsBroken).Select(c => c.ToString()).ToList();
        }
        public struct FieldContract
        {
            public readonly Type Owner;
            public readonly string Name;
            public readonly Type FieldType;

            public FieldContract(Type owner, string name, Type fieldType)
            {
                Owner = owner;
                Name = name;
                FieldType = fieldType;
            }

            public override string ToString() => $"{Owner.Name}.{Name} ({FieldType.Name})";
        }

        public static readonly FieldContract[] FieldContracts =
        {
            new FieldContract(typeof(cRythmGameManager), "mPauseMenuWork", typeof(cRythmGamePauseMenuHud)),
            new FieldContract(typeof(cRythmGameManager), "mRythmGameMusicData", typeof(FairyNoteEditorLoader.MusicData)),
            new FieldContract(typeof(cMusicSelectSceneUIUpdater), "mArtWorkAndMusicDetail", typeof(cMusicSelectArtWorkManager)),
            new FieldContract(typeof(cMusicSelectSceneUIUpdater), "mPreviewAudioSorce", typeof(AudioSource)),
            new FieldContract(typeof(cMusicSelectSceneUIUpdater), "mAmbientAudioSorce", typeof(AudioSource)),
            new FieldContract(typeof(cMusicSelectArtWorkManager), "mArtWork", typeof(cMusicSelectArtWork)),
            new FieldContract(typeof(cMusicSelectPreMusicStartWindowManager), "mArtworkImage", typeof(Image)),
            new FieldContract(typeof(cMusicSelectScrollView), "mCellHaviableMusicDataList", typeof(List<MusicSelectScrollItemData>)),
            new FieldContract(typeof(cFairyModeNotesManager), "mIsCurrentAutoPlay", typeof(bool)),
            new FieldContract(typeof(cFairyModeNotesManager), "mFairyNoteCreateDataArray", typeof(FairyNoteEditorLoader.NoteCreateData[])),
            new FieldContract(typeof(cFairyModeNotesManager), "tap_perfectJudgeRange", typeof(float)),
            new FieldContract(typeof(cFairyModeNotesManager), "tap_greatJudgeRange", typeof(int)),
            new FieldContract(typeof(cFairyModeNotesManager), "tap_goodJudgeRange", typeof(int)),
            new FieldContract(typeof(cFairyModeNotesManager), "tap_badJudgeRange", typeof(int)),
            new FieldContract(typeof(cFairyHoldNoteWork), "mIsJudgedFirst", typeof(bool)),
            new FieldContract(typeof(cFairySlideNoteWork), "mIsJudgedFirstNote", typeof(bool)),
            new FieldContract(typeof(cPlayMovieSceneManager), "mUpdateSwapCoroutine", typeof(Coroutine)),
            new FieldContract(typeof(cRythmGameResultSceneUpdater), "mOldPlayHighScore", typeof(int)),
            new FieldContract(typeof(cRythmGameResultSceneUpdater), "mSceneInitializeParam", typeof(cRythmGameResultSceneManageObject.InitializeParam)),
            new FieldContract(typeof(cRythmGameResultSceneUpdater), "mMusicLVUI", typeof(cUIMusicLVLoopImage)),
            new FieldContract(typeof(cRythmGameResultSceneUpdater), "mMusicNameText", typeof(TextMeshProUGUI)),
            new FieldContract(typeof(cRythmGameResultSceneUpdater), "mArtWorkImage", typeof(Image)),
            new FieldContract(typeof(cMusicSelectSceneSelectingMusicUI), "mMusicNameText", typeof(TextMeshProUGUI)),
        };

        /// <summary>필드 계약 중 지금 게임 어셈블리에서 깨진 것(없거나 타입이 다름)을 설명 문자열로 돌려줍니다. 부작용이 없어 테스트에서도 씁니다.</summary>
        public static List<string> FindBrokenFieldContracts()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

            var broken = new List<string>();
            foreach (var contract in FieldContracts)
            {
                FieldInfo field = null;
                for (var type = contract.Owner; type != null && field == null; type = type.BaseType)
                    field = type.GetField(contract.Name, flags | BindingFlags.DeclaredOnly);

                if (field == null)
                    broken.Add($"{contract} - 필드 없음(이름이 바뀌었거나 삭제됨)");
                else if (field.FieldType != contract.FieldType)
                    broken.Add($"{contract} - 타입 변경: 현재 {field.FieldType.Name}");
            }

            return broken;
        }

        private static readonly List<string> _hookFailures = new List<string>();

        /// <summary>Harmony 적용 중 빠진 훅(실패한 패치 클래스, 찾지 못한 대상 메서드). 시작 점검이 문제로 셉니다.</summary>
        public static IReadOnlyList<string> HookFailures => _hookFailures;

        /// <summary>
        /// 적용하지 못한 훅을 기록합니다. Harmony 적용은 시작 점검보다 먼저 돌므로(SceneDetector.OnInitializeMelon)
        /// 여기 쌓인 것이 <see cref="LogStartupReport"/>에 나옵니다. 예전에는 필드·상수만 맞으면 훅 대상 메서드가
        /// 사라졌어도 "[호환성] 점검 통과"가 찍혔습니다.
        /// </summary>
        public static void RecordHookFailure(string description)
        {
            // PatchAll이 실패하면 클래스별로 다시 적용하며 TargetMethods가 두 번 불리므로 같은 항목은 한 번만 남깁니다.
            if (!string.IsNullOrEmpty(description) && !_hookFailures.Contains(description))
                _hookFailures.Add(description);
        }

        /// <summary>빌드 설정(Build Settings)에 들어 있는 씬 이름 목록. 읽지 못하면 빈 목록.</summary>
        private static List<string> ReadBuildSceneNames()
        {
            var names = new List<string>();
            try
            {
                int count = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
                for (int i = 0; i < count; i++)
                {
                    string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
                    if (!string.IsNullOrEmpty(path))
                        names.Add(System.IO.Path.GetFileNameWithoutExtension(path));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[호환성] 씬 목록 읽기 실패: {ex.Message}");
            }
            return names;
        }

        /// <summary>시작 점검 결과를 로그로 남깁니다. 어떤 예외도 밖으로 내보내지 않습니다.</summary>
        public static void LogStartupReport(string gameFolder)
        {
            try
            {
                int problems = 0;

                foreach (var failure in _hookFailures)
                {
                    problems++;
                    MelonLogger.Error($"[호환성] 훅 적용 실패: {failure} - 이 기능이 꺼졌습니다(대상 메서드 이름·시그니처 변경 의심, HOOK_MAP.md).");
                }

                var buildId = SteamBuildInfo.ReadInstalledBuildId(gameFolder);
                if (buildId == null)
                {
                    MelonLogger.Msg("[호환성] Steam Build ID를 읽지 못했습니다(비 Steam 설치이거나 .acf 없음). 어셈블리 MVID로만 점검합니다.");
                }
                else if (buildId != TestedBuildId)
                {
                    problems++;
                    MelonLogger.Warning($"[호환성] 게임이 업데이트되었을 수 있습니다: 설치 Build ID {buildId} (검증 {TestedBuildId}).");
                }
                else
                {
                    MelonLogger.Msg($"[호환성] Steam Build ID {buildId} - 검증된 빌드와 같습니다.");
                }

                // 게임 파일이 그대로여도 MelonLoader·Unity가 바뀌면 훅·TMP·영상 동작이 달라질 수 있습니다(2026-11-24).
                string melonVersion = RuntimeMelonLoaderVersion();
                if (melonVersion != null && melonVersion != TestedMelonLoaderVersion)
                {
                    problems++;
                    MelonLogger.Warning($"[호환성] MelonLoader {melonVersion}가 검증 버전 {TestedMelonLoaderVersion}과 다릅니다. 훅이 두 번 걸리거나 빠지는지 로그로 확인하세요.");
                }

                string unityVersion = UnityEngine.Application.unityVersion;
                if (!string.IsNullOrEmpty(unityVersion) && unityVersion != TestedUnityVersion)
                {
                    problems++;
                    MelonLogger.Warning($"[호환성] Unity {unityVersion}가 검증 버전 {TestedUnityVersion}과 다릅니다(게임 엔진 업데이트).");
                }

                var mvid = typeof(cRythmGameManager).Assembly.ManifestModule.ModuleVersionId.ToString();
                if (!string.Equals(mvid, TestedAssemblyMvid, StringComparison.OrdinalIgnoreCase))
                {
                    problems++;
                    MelonLogger.Warning($"[호환성] Assembly-CSharp가 검증본과 다릅니다: MVID {mvid} (검증 {TestedAssemblyMvid}).");
                }

                var broken = FindBrokenFieldContracts();
                foreach (var message in broken)
                {
                    problems++;
                    MelonLogger.Error($"[호환성] 게임 필드 불일치: {message}");
                }

                var brokenConstants = FindBrokenConstantContracts();
                foreach (var message in brokenConstants)
                {
                    problems++;
                    MelonLogger.Error($"[호환성] 게임 상수 불일치: {message}");
                }

                // 커스텀 ID 시작 번호는 원본 곡 수에 따라 런타임에 정해집니다. 게임에 곡이 추가되면 모든 커스텀 곡 ID가 밀리고,
                // 예전 커스텀 곡 기록(BlockSave=0)이 남은 세이브 칸을 새 원본 곡이 물려받습니다(알려진_문제 C4 남은 한계).
                int customStart = CustomMusicIdRegistry.Start;
                if (customStart != TestedCustomMusicIdStart)
                {
                    problems++;
                    MelonLogger.Warning($"[호환성] 커스텀 MusicID 시작 번호가 {TestedCustomMusicIdStart} → {customStart}로 바뀌었습니다(원본 곡 추가). " +
                        $"세이브 칸 {TestedCustomMusicIdStart}~{customStart - 1}에 예전 커스텀 곡 기록이 남아 있으면 새 원본 곡 기록처럼 보일 수 있습니다.");
                }

                // 씬 이름은 분기 조건이라 바뀌면 BGM/BGA 주입·기록 차단·제목 치환이 조용히 꺼집니다(2026-11-25, R6).
                var buildScenes = ReadBuildSceneNames();
                if (buildScenes.Count == 0)
                {
                    MelonLogger.Msg("[호환성] 빌드 설정의 씬 목록을 읽지 못해 씬 이름 점검을 건너뜁니다.");
                }
                else
                {
                    foreach (var missing in GameSceneNames.FindMissing(buildScenes))
                    {
                        problems++;
                        MelonLogger.Error($"[호환성] 게임 씬 '{missing}'이(가) 없습니다(이름 변경 의심). 이 씬에 걸린 분기가 꺼집니다. 현재 씬: {string.Join(", ", buildScenes)}");
                    }
                }

                if (problems == 0)
                    MelonLogger.Msg($"[호환성] 점검 통과 (필드 {FieldContracts.Length}개, 상수 {ConstantContracts.Length}개, 씬 {GameSceneNames.ExactNames.Length + 1}개, 훅 클래스 전부 적용).");
                else
                    MelonLogger.Warning($"[호환성] 문제 {problems}건. 곡 선택·결과 화면 등 일부 기능이 동작하지 않을 수 있습니다. " +
                        "HOOK_MAP.md와 AGENTS.md의 '업데이트 대응 절차'를 따라 훅 시그니처부터 다시 확인하세요.");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[호환성] 점검 중 예외(무시하고 계속): {ex.Message}");
            }
        }
    }
}
