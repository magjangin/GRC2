using System;
using System.Collections;
using System.Reflection;
using GRC2.Core;
using MelonLoader;
using UnityEngine;

namespace GRC2.Injectors
{
    /// <summary>
    /// 커스텀 BGM 길이가 준비된 뒤 원본 게임 종료 코루틴을 실행합니다.
    /// 원본 코루틴의 페이드, 점수 보정, 클리어 연출과 씬 전환은 그대로 유지됩니다.
    /// </summary>
    internal static class BgmGameEndMonitor
    {
        private const BindingFlags InstanceFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const float SampleRate = 48000f;
        private const float TimingWaitTimeout = 15f;

        private static Type _cachedManagerType;
        private static Type _cachedMusicDataType;
        private static FieldInfo _musicDataField;
        private static FieldInfo _musicFadeStartField;
        private static FieldInfo _musicFadeEndField;
        private static FieldInfo _screenFadeStartField;
        private static FieldInfo _screenFadeEndField;

        /// <summary>
        /// 새 플레이 씬 진입 시 이전 곡의 종료 시간과 리플렉션 캐시를 초기화합니다.
        /// </summary>
        public static void AdjustMusicDataOnSceneLoad()
        {
            BgmFinishTimeManager.Reset();
            ClearCache();
        }

        /// <summary>
        /// IEnumerator 팩터리의 반환값을 감싸되 원본 자체는 건너뛰지 않습니다.
        /// </summary>
        public static void MonitorGameEndPostfix(object __instance, ref IEnumerator __result)
        {
            if (__instance == null ||
                __result == null ||
                !CustomAssetManager.ShouldInjectCustomContent())
            {
                return;
            }

            __result = WaitForTimingAndRunOriginal(__instance, __result);
        }

        private static IEnumerator WaitForTimingAndRunOriginal(object manager, IEnumerator original)
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

        private static void ApplyTargetTime(object manager, float targetTime)
        {
            try
            {
                CacheFields(manager);
                object musicData = _musicDataField?.GetValue(manager);
                if (musicData == null)
                    return;

                CacheMusicDataFields(musicData.GetType());

                int endSample = ToSample(targetTime);
                int musicFadeStart = ToSample(Math.Max(0f, targetTime - 1f));
                int screenFadeStart = ToSample(Math.Max(0f, targetTime - 1.5f));

                SetIntField(_musicFadeStartField, musicData, musicFadeStart);
                SetIntField(_musicFadeEndField, musicData, endSample);
                SetIntField(_screenFadeStartField, musicData, screenFadeStart);
                SetIntField(_screenFadeEndField, musicData, endSample);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BgmGameEndMonitor] 종료 샘플 적용 실패: {ex.Message}");
            }
        }

        private static void CacheFields(object manager)
        {
            Type managerType = manager.GetType();
            if (_cachedManagerType == managerType && _musicDataField != null)
                return;

            _cachedManagerType = managerType;
            _musicDataField = managerType.GetField("mRythmGameMusicData", InstanceFlags);
        }

        private static void CacheMusicDataFields(Type musicDataType)
        {
            if (_cachedMusicDataType == musicDataType &&
                _musicFadeEndField != null &&
                _screenFadeEndField != null)
            {
                return;
            }

            _cachedMusicDataType = musicDataType;
            _musicFadeStartField = musicDataType.GetField("musicFadeOutStartSample", InstanceFlags);
            _musicFadeEndField = musicDataType.GetField("musicFadeOutEndSample", InstanceFlags);
            _screenFadeStartField = musicDataType.GetField("screenFadeOutStartSample", InstanceFlags);
            _screenFadeEndField = musicDataType.GetField("screenFadeOutEndSample", InstanceFlags);
        }

        private static int ToSample(float seconds)
        {
            double samples = seconds * SampleRate;
            return samples >= int.MaxValue ? int.MaxValue : (int)samples;
        }

        private static void SetIntField(FieldInfo field, object target, int value)
        {
            if (field != null && field.FieldType == typeof(int))
                field.SetValue(target, value);
        }

        private static void ClearCache()
        {
            _cachedManagerType = null;
            _cachedMusicDataType = null;
            _musicDataField = null;
            _musicFadeStartField = null;
            _musicFadeEndField = null;
            _screenFadeStartField = null;
            _screenFadeEndField = null;
        }
    }
}
