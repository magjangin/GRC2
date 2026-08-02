using System;
using System.Collections;
using GRC2.Core;
using HarmonyLib;
using IntiCreates;
using MelonLoader;
using UnityEngine;

namespace GRC2.Injectors
{
    /// <summary>
    /// 게임 종료 시간 조정을 담당하는 클래스
    /// </summary>
    internal static class BgmFinishTimeManager
    {
        private const float SampleRate = 48000f;

        private static readonly AccessTools.FieldRef<cFairyModeNotesManager, FairyNoteEditorLoader.NoteCreateData[]> NoteArrayRef =
            AccessTools.FieldRefAccess<cFairyModeNotesManager, FairyNoteEditorLoader.NoteCreateData[]>("mFairyNoteCreateDataArray");

        private static float _targetFinishTime = 0f;

        public static float GetTargetFinishTime()
        {
            return _targetFinishTime;
        }

        public static void Reset()
        {
            _targetFinishTime = 0f;
        }

        public static void SetFinishTime(float newBgmLength)
        {
            try
            {
                float lastNoteTime = GetLastNoteTime();

                // 종료 시간은 BGM 길이와 마지막 노트 시간 중 더 큰 값 사용
                float finishTime = Math.Max(newBgmLength, lastNoteTime);
                _targetFinishTime = finishTime;

                MelonLogger.Msg($"[BgmFinishTimeManager] 게임 종료 시간 설정: {finishTime:F3}초 (BGM: {newBgmLength:F3}초, 마지막 노트: {lastNoteTime:F3}초)");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BgmFinishTimeManager] 게임 종료 시간 설정 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 노트 배열에서 마지막 노트 시간을 계산합니다.
        /// </summary>
        private static float GetLastNoteTime()
        {
            var notesManager = UnityEngine.Object.FindObjectOfType<cFairyModeNotesManager>();
            if (notesManager == null)
            {
                return 0f;
            }

            var noteArray = NoteArrayRef(notesManager);
            if (noteArray == null || noteArray.Length == 0)
            {
                return 0f;
            }

            int lastNoteSample = 0;
            foreach (var note in noteArray)
            {
                if (note != null && note.perfectSample > lastNoteSample)
                {
                    lastNoteSample = note.perfectSample;
                }
            }

            return lastNoteSample / SampleRate;
        }
    }

    /// <summary>
    /// 커스텀 BGM 길이가 준비된 뒤 원본 게임 종료 코루틴을 실행합니다.
    /// 원본 코루틴의 페이드, 점수 보정, 클리어 연출과 씬 전환은 그대로 유지됩니다.
    /// </summary>
    internal static class BgmGameEndMonitor
    {
        private const float SampleRate = 48000f;
        private const float TimingWaitTimeout = 15f;

        private static readonly AccessTools.FieldRef<cRythmGameManager, FairyNoteEditorLoader.MusicData> MusicDataRef =
            AccessTools.FieldRefAccess<cRythmGameManager, FairyNoteEditorLoader.MusicData>("mRythmGameMusicData");

        /// <summary>
        /// 새 플레이 씬 진입 시 이전 곡의 종료 시간을 초기화합니다.
        /// </summary>
        public static void AdjustMusicDataOnSceneLoad()
        {
            BgmFinishTimeManager.Reset();
        }

        /// <summary>
        /// IEnumerator 팩터리의 반환값을 감싸되 원본 자체는 건너뛰지 않습니다.
        /// </summary>
        public static void MonitorGameEndPostfix(cRythmGameManager __instance, ref IEnumerator __result)
        {
            if (__instance == null ||
                __result == null ||
                !CustomAssetManager.ShouldInjectCustomContent())
            {
                return;
            }

            __result = WaitForTimingAndRunOriginal(__instance, __result);
        }

        private static IEnumerator WaitForTimingAndRunOriginal(cRythmGameManager manager, IEnumerator original)
        {
            float remaining = TimingWaitTimeout;
            while (remaining > 0f &&
                   CustomAssetManager.ShouldInjectCustomContent() &&
                   BgmFinishTimeManager.GetTargetFinishTime() <= 0f)
            {
                float delta = Time.unscaledDeltaTime;
                remaining -= delta > 0f ? delta : 0.02f;
                yield return null;
            }

            float targetTime = BgmFinishTimeManager.GetTargetFinishTime();
            if (targetTime > 0f)
            {
                ApplyTargetTime(manager, targetTime);
                MelonLogger.Msg(
                    $"[BgmGameEndMonitor] 원본 종료 코루틴에 커스텀 종료 시간 적용: {targetTime:F3}초");
            }
            else
            {
                MelonLogger.Warning(
                    "[BgmGameEndMonitor] 커스텀 BGM 종료 시간이 준비되지 않아 원본 차트 시간을 사용합니다.");
            }

            try
            {
                while (original.MoveNext())
                    yield return original.Current;
            }
            finally
            {
                (original as IDisposable)?.Dispose();
            }
        }

        private static void ApplyTargetTime(cRythmGameManager manager, float targetTime)
        {
            try
            {
                var musicData = MusicDataRef(manager);
                if (musicData == null)
                    return;

                int endSample = ToSample(targetTime);

                musicData.musicFadeOutStartSample = ToSample(Math.Max(0f, targetTime - 1f));
                musicData.musicFadeOutEndSample = endSample;
                musicData.screenFadeOutStartSample = ToSample(Math.Max(0f, targetTime - 1.5f));
                musicData.screenFadeOutEndSample = endSample;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BgmGameEndMonitor] 종료 샘플 적용 실패: {ex.Message}");
            }
        }

        private static int ToSample(float seconds)
        {
            double samples = seconds * SampleRate;
            return samples >= int.MaxValue ? int.MaxValue : (int)samples;
        }
    }

    /// <summary>
    /// BGM 주입에 필요한 게임 종료 IEnumerator 래퍼를 자동 등록합니다.
    /// </summary>
    [HarmonyPatch(typeof(cRythmGameManager), "coMonitorGameEnd")]
    internal static class BgmInjectorHooks
    {
        [HarmonyPostfix]
        private static void Postfix(cRythmGameManager __instance, ref IEnumerator __result)
        {
            BgmGameEndMonitor.MonitorGameEndPostfix(__instance, ref __result);
        }
    }
}
