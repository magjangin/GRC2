using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using IntiCreates;
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

        /// <summary>모드가 직접 기대하는 게임 enum 상수·기본값 계약. 게임 업데이트로 값이 바뀌면 시작 로그와 테스트가 잡습니다.</summary>
        public struct ConstantContract
        {
            public readonly string Description;
            public readonly int Expected;
            public readonly int Actual;

            public ConstantContract(string description, int expected, int actual)
            {
                Description = description;
                Expected = expected;
                Actual = actual;
            }

            public bool IsBroken => Expected != Actual;

            public override string ToString() => $"{Description}: 기대 {Expected}, 현재 {Actual}";
        }

        public static readonly ConstantContract[] ConstantContracts =
        {
            new ConstantContract("MusicID.SAVEABLE_ID_END", 512, (int)soRythmGameMusicDataMap.MusicID.SAVEABLE_ID_END),
            new ConstantContract("MusicID.UNIQUE_TOP", 8000, (int)soRythmGameMusicDataMap.MusicID.UNIQUE_TOP),
            new ConstantContract("cBGMBeatManager.defaultFrequency", 48000, cBGMBeatManager.defaultFrequency),
        };

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

        /// <summary>시작 점검 결과를 로그로 남깁니다. 어떤 예외도 밖으로 내보내지 않습니다.</summary>
        public static void LogStartupReport(string gameFolder)
        {
            try
            {
                int problems = 0;

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

                if (problems == 0)
                    MelonLogger.Msg($"[호환성] 점검 통과 (필드 {FieldContracts.Length}개, 상수 {ConstantContracts.Length}개 확인).");
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
