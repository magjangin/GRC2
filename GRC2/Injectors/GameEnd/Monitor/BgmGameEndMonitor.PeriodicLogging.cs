using System;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using GRC2.Helpers;

namespace GRC2.Injectors
{
    internal static partial class BgmGameEndMonitor
    {
        private static void LogPeriodicBgmEndState(object instance, Type instanceType)
        {
            if (_monitorGameEndCallCount % 300 != 0)
                return;

            try
            {
                var bgmInstance = GetBgmManagerInstance(instanceType);
                if (bgmInstance == null || _cachedBgmManagerType == null)
                    return;

                if (!TryReadCurrentSample(bgmInstance, out int currentSample))
                    return;

                if (!TryReadBgmClip(bgmInstance, out AudioClip clip))
                    return;

                float currentPlayTime = currentSample / SampleRate;
                float bgmLength = clip.length;
                string endSampleStr = FormatKudosBoostEndSample(instance);

                MelonLogger.Msg($"[BgmInjectorHooks]   → 재생 시간: {currentPlayTime:F3}초 / BGM 길이: {bgmLength:F3}초 / 종료 샘플: {endSampleStr}");
                if (currentPlayTime >= bgmLength - 1f || currentPlayTime >= 128f)
                {
                    MelonLogger.Warning("[BgmInjectorHooks]   ⚠ 종료 조건 근처! 재생 시간이 BGM 길이 또는 128초에 도달했습니다.");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[BgmGameEndMonitor] MonitorGameEndPostfix", "BGM 상태 확인 중 예외");
            }
        }

        private static bool TryReadCurrentSample(object bgmInstance, out int currentSample)
        {
            currentSample = 0;
            var getCurrentSampleMethod = GetCachedMethod(_cachedBgmManagerType, "getCurrentSample",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var currentSampleValue = getCurrentSampleMethod?.Invoke(bgmInstance, null);
            if (currentSampleValue is int intCurrentSample)
            {
                currentSample = intCurrentSample;
                return true;
            }

            return false;
        }

        private static bool TryReadBgmClip(object bgmInstance, out AudioClip clip)
        {
            clip = null;
            var getAudioClipMethod = GetCachedMethod(_cachedBgmManagerType, "getAudioClip",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            clip = getAudioClipMethod?.Invoke(bgmInstance, null) as AudioClip;
            return clip != null;
        }

        private static string FormatKudosBoostEndSample(object instance)
        {
            FieldInfo cachedEndSampleField = _cachedKudosBoostEndSampleField;
            if (cachedEndSampleField == null)
                return "N/A";

            var endSampleValue = cachedEndSampleField.GetValue(instance);
            if (endSampleValue is int endSample)
            {
                float endTime = endSample / SampleRate;
                return $"{endSample} 샘플 ({endTime:F3}초)";
            }

            return "N/A";
        }
    }
}
