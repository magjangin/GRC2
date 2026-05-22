using System;
using System.Reflection;
using MelonLoader;

namespace GRC2.Injectors
{
    internal static partial class BgmGameEndMonitor
    {
        private const float SampleRate = 48000f;

        private sealed class MusicEndSamples
        {
            public int TargetSample;
            public int FadeOutStartSample;
            public int ScreenFadeOutStartSample;
        }

        private static void ApplyMusicDataSampleAdjustments(object musicData, float targetTime, bool logAdjustment)
        {
            var samples = BuildMusicEndSamples(targetTime);

            ApplyIntSampleField(
                musicData,
                _cachedFadeOutEndSampleField,
                samples.TargetSample,
                targetTime,
                "musicFadeOutEndSample",
                logAdjustment,
                value => _lastTargetSample = value);

            ApplyIntSampleField(
                musicData,
                _cachedFadeOutStartSampleField,
                samples.FadeOutStartSample,
                targetTime - 1.0f,
                "musicFadeOutStartSample",
                logAdjustment,
                value => _lastFadeOutStartSample = value);

            ApplyIntSampleField(
                musicData,
                _cachedScreenFadeOutStartSampleField,
                samples.ScreenFadeOutStartSample,
                targetTime - 1.5f,
                "screenFadeOutStartSample",
                logAdjustment,
                value => _lastScreenFadeOutStartSample = value);

            ApplyIntSampleField(
                musicData,
                _cachedScreenFadeOutEndSampleField,
                samples.TargetSample,
                targetTime,
                "screenFadeOutEndSample",
                logAdjustment,
                value => { });
        }

        private static MusicEndSamples BuildMusicEndSamples(float targetTime)
        {
            return new MusicEndSamples
            {
                TargetSample = (int)(targetTime * SampleRate),
                FadeOutStartSample = Math.Max(0, (int)((targetTime - 1.0f) * SampleRate)),
                ScreenFadeOutStartSample = Math.Max(0, (int)((targetTime - 1.5f) * SampleRate))
            };
        }

        private static void ApplyIntSampleField(
            object target,
            FieldInfo field,
            int targetSample,
            float targetTime,
            string fieldName,
            bool logAdjustment,
            Action<int> rememberValue)
        {
            if (field == null)
                return;

            var currentValue = field.GetValue(target);
            if (currentValue is int currentSample && currentSample != targetSample)
            {
                if (logAdjustment)
                {
                    MelonLogger.Msg($"[BgmGameEndMonitor] {fieldName}: {currentSample} → {targetSample} ({targetTime:F3}초)");
                }
                field.SetValue(target, targetSample);
                rememberValue(targetSample);
            }
            else if (logAdjustment && currentValue is int)
            {
                MelonLogger.Msg($"[BgmGameEndMonitor] {fieldName}: {currentValue} (이미 조정됨, {targetTime:F3}초)");
            }
        }

        private static void UpdateKudosBoostEndSample(object instance, Type instanceType, float targetTime)
        {
            if (targetTime <= 0f)
                return;

            int targetSample = (int)(targetTime * SampleRate);
            FieldInfo endSampleField = ResolveKudosBoostEndSampleField(instanceType);
            if (endSampleField == null)
                return;

            var currentValue = endSampleField.GetValue(instance);
            if (currentValue is int currentSample)
            {
                float currentTime = currentSample / SampleRate;
                if (currentSample == -1 || Math.Abs(currentTime - targetTime) > 0.1f)
                {
                    endSampleField.SetValue(instance, targetSample);
                }
            }
        }

        private static FieldInfo ResolveKudosBoostEndSampleField(Type instanceType)
        {
            var endSampleField = _cachedKudosBoostEndSampleField;
            if (endSampleField == null || _cachedInstanceType != instanceType)
            {
                endSampleField = instanceType.GetField("mKudosBoostEndSample",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                _cachedKudosBoostEndSampleField = endSampleField;
            }

            return endSampleField;
        }
    }
}
