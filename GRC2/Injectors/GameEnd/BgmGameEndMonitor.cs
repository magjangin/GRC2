using System;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using GRC2.Helpers;

namespace GRC2.Injectors
{
    /// <summary>
    /// 게임 종료 모니터링 관련 후킹을 담당하는 클래스
    /// </summary>
    internal static partial class BgmGameEndMonitor
    {
        private static int _monitorGameEndCallCount = 0;

        // Reflection 필드 정보 캐싱 (성능 최적화)
        private static FieldInfo _cachedMusicDataField = null;
        private static FieldInfo _cachedFadeOutEndSampleField = null;
        private static FieldInfo _cachedFadeOutStartSampleField = null;
        private static FieldInfo _cachedScreenFadeOutStartSampleField = null;
        private static FieldInfo _cachedScreenFadeOutEndSampleField = null;
        private static FieldInfo _cachedKudosBoostEndSampleField = null;
        private static Type _cachedMusicDataType = null;
        private static Type _cachedInstanceType = null;
        
        // 마지막 조정된 값 캐싱 (불필요한 SetValue 방지)
        private static int _lastTargetSample = -1;
        private static int _lastFadeOutStartSample = -1;
        private static int _lastScreenFadeOutStartSample = -1;
        
        // 인스턴스 캐싱 (성능 최적화)
        private static object _cachedBgmManagerInstance = null;
        private static Type _cachedBgmManagerType = null;
        
        // 메서드 정보 캐싱 (성능 최적화)
        private static MethodInfo _cachedGetAudioClipMethod = null;
        private static MethodInfo _cachedGetCurrentSampleMethod = null;
        
        /// <summary>
        /// Reflection 필드 정보를 캐싱 (성능 최적화)
        /// </summary>
        private static void CacheFieldInfo(Type instanceType, object musicData)
        {
            if (_cachedInstanceType == instanceType && _cachedMusicDataType == musicData?.GetType())
            {
                return; // 이미 캐싱됨
            }
            
            _cachedInstanceType = instanceType;
            _cachedMusicDataField = instanceType.GetField("mRythmGameMusicData",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (musicData != null)
            {
                _cachedMusicDataType = musicData.GetType();
                _cachedFadeOutEndSampleField = _cachedMusicDataType.GetField("musicFadeOutEndSample",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                _cachedFadeOutStartSampleField = _cachedMusicDataType.GetField("musicFadeOutStartSample",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                _cachedScreenFadeOutStartSampleField = _cachedMusicDataType.GetField("screenFadeOutStartSample",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                _cachedScreenFadeOutEndSampleField = _cachedMusicDataType.GetField("screenFadeOutEndSample",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            }
        }
        
        /// <summary>
        /// 캐시 초기화 (씬 변경 시 호출)
        /// </summary>
        public static void ClearCache()
        {
            _cachedMusicDataField = null;
            _cachedFadeOutEndSampleField = null;
            _cachedFadeOutStartSampleField = null;
            _cachedScreenFadeOutStartSampleField = null;
            _cachedScreenFadeOutEndSampleField = null;
            _cachedKudosBoostEndSampleField = null;
            _cachedMusicDataType = null;
            _cachedInstanceType = null;
            _lastTargetSample = -1;
            _lastFadeOutStartSample = -1;
            _lastScreenFadeOutStartSample = -1;
            
            // 인스턴스 및 메서드 캐시 초기화
            _cachedBgmManagerInstance = null;
            _cachedBgmManagerType = null;
            _cachedGetAudioClipMethod = null;
            _cachedGetCurrentSampleMethod = null;
        }
        
        /// <summary>
        /// BGM Manager 인스턴스 가져오기 (캐싱)
        /// </summary>
        private static object GetBgmManagerInstance(Type instanceType)
        {
            // 캐시된 인스턴스가 유효한지 확인
            if (_cachedBgmManagerInstance != null && _cachedBgmManagerType != null)
            {
                // 인스턴스가 여전히 유효한지 확인 (null 체크)
                if (!ReferenceEquals(_cachedBgmManagerInstance, null))
                {
                    return _cachedBgmManagerInstance;
                }
            }
            
            // 캐시가 없거나 무효한 경우 새로 찾기
            var assembly = instanceType.Assembly;
            var bgmManagerType = assembly.GetType("IntiCreates.cBGMBeatManager");
            if (bgmManagerType == null)
            {
                return null;
            }
            
            var bgmManagers = UnityEngine.Object.FindObjectsOfType(bgmManagerType);
            if (bgmManagers != null && bgmManagers.Length > 0)
            {
                _cachedBgmManagerInstance = bgmManagers[0];
                _cachedBgmManagerType = bgmManagerType;
                return _cachedBgmManagerInstance;
            }
            
            return null;
        }
        
        /// <summary>
        /// 메서드 정보 가져오기 (캐싱)
        /// </summary>
        private static MethodInfo GetCachedMethod(Type type, string methodName, BindingFlags flags)
        {
            // getAudioClip 메서드 캐싱
            if (methodName == "getAudioClip" && _cachedGetAudioClipMethod != null && _cachedBgmManagerType == type)
            {
                return _cachedGetAudioClipMethod;
            }
            
            // getCurrentSample 메서드 캐싱
            if (methodName == "getCurrentSample" && _cachedGetCurrentSampleMethod != null && _cachedBgmManagerType == type)
            {
                return _cachedGetCurrentSampleMethod;
            }
            
            // 메서드 찾기
            var method = type.GetMethod(methodName, flags);
            
            // 캐시에 저장
            if (methodName == "getAudioClip")
            {
                _cachedGetAudioClipMethod = method;
                _cachedBgmManagerType = type;
            }
            else if (methodName == "getCurrentSample")
            {
                _cachedGetCurrentSampleMethod = method;
                _cachedBgmManagerType = type;
            }
            
            return method;
        }
        
        public static bool MonitorGameEndPrefix(object __instance)
        {
            try
            {
                _monitorGameEndCallCount++;
                
                // 고빈도 코루틴 훅에서 과도한 로그/포맷팅을 피하기 위해 300회마다만 로그
                if (_monitorGameEndCallCount % 300 == 1)
                {
                    MelonLogger.Msg("[BgmGameEndMonitor] coMonitorGameEnd() 호출 감지 - 무한 플레이 모드 유지");
                }
                
                // mRythmGameMusicData를 BGM 길이에 맞게 조정 (필드 값은 유지)
                AdjustMusicDataForBgmLength(__instance, logAdjustment: false);

                // 원본 coMonitorGameEnd 코루틴을 차단 (무한 플레이 모드).
                // BgmMonitorCoroutine이 BGM 재생 시간을 감시하다가 직접 requestCommonRythmGameEnd()를 호출한다.
                // 여기서 true를 반환하면 원본도 자체적으로 게임 종료를 요청해 결과 씬 전환이 중복 발생한다.
                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BgmGameEndMonitor] MonitorGameEndPrefix 오류: {ex.Message}");
                // 오류 발생 시에는 원본 실행 허용 (모니터링 실패보다 게임 진행이 우선)
                return true;
            }
        }
            }

    internal static partial class BgmGameEndMonitor
    {
        public static void AdjustMusicDataOnSceneLoad()
        {
            try
            {
                // 씬 로드 시 캐시 초기화 (새 인스턴스일 수 있음)
                ClearCache();
                
                // cRythmGameManager 인스턴스 찾기
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                
                if (assembly == null)
                {
                    return;
                }
                
                var rythmGameManagerType = assembly.GetType("IntiCreates.cRythmGameManager");
                if (rythmGameManagerType == null)
                {
                    return;
                }
                
                var rythmGameManagers = UnityEngine.Object.FindObjectsOfType(rythmGameManagerType);
                if (rythmGameManagers == null || rythmGameManagers.Length == 0)
                {
                    return;
                }
                
                var rythmGameManager = rythmGameManagers[0];
                AdjustMusicDataForBgmLength(rythmGameManager, logAdjustment: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BgmGameEndMonitor] AdjustMusicDataOnSceneLoad 오류: {ex.Message}");
            }
        }

        private static void AdjustMusicDataForBgmLength(object instance, bool logAdjustment = false)
        {
            try
            {
                var instanceType = instance.GetType();
                float targetTime = ResolveTargetFinishTime(instanceType);
                if (targetTime <= 0f)
                {
                    return;
                }

                var musicDataField = _cachedMusicDataField ?? instanceType.GetField("mRythmGameMusicData",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (musicDataField == null)
                {
                    return;
                }

                var musicData = musicDataField.GetValue(instance);
                if (musicData == null)
                {
                    return;
                }

                CacheFieldInfo(instanceType, musicData);
                ApplyMusicDataSampleAdjustments(musicData, targetTime, logAdjustment);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BgmGameEndMonitor] AdjustMusicDataForBgmLength 오류: {ex.Message}");
                MelonLogger.Error($"[BgmGameEndMonitor] 스택 트레이스: {ex.StackTrace}");
            }
        }

        private static float ResolveTargetFinishTime(Type instanceType)
        {
            float targetTime = BgmFinishTimeManager.GetTargetFinishTime();
            if (targetTime > 0f)
            {
                return targetTime;
            }

            var bgmInstance = GetBgmManagerInstance(instanceType);
            if (bgmInstance == null || _cachedBgmManagerType == null)
            {
                return 0f;
            }

            var getAudioClipMethod = GetCachedMethod(_cachedBgmManagerType, "getAudioClip",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var clip = getAudioClipMethod?.Invoke(bgmInstance, null) as AudioClip;
            return clip?.length ?? 0f;
        }

        public static void MonitorGameEndPostfix(object __instance)
        {
            try
            {
                AdjustMusicDataForBgmLength(__instance);

                var instanceType = __instance.GetType();
                float targetTime = BgmFinishTimeManager.GetTargetFinishTime();
                UpdateKudosBoostEndSample(__instance, instanceType, targetTime);
                LogPeriodicBgmEndState(__instance, instanceType);
            }
            catch
            {
                // 조용히 실패 (너무 많은 로그 방지)
            }
        }

        
        public static void ClearGameEndPrefix(object __instance)
        {
            // 로그 제거됨
        }

        public static void GenericGameEndPrefix(object __instance, MethodBase __originalMethod)
        {
            try
            {
                MelonLogger.Msg($"[BgmInjectorHooks] ⚠ 게임 종료/클리어 코루틴 호출: {__originalMethod.Name}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BgmInjectorHooks] GenericGameEndPrefix 오류: {ex.Message}");
            }
        }
    }

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
