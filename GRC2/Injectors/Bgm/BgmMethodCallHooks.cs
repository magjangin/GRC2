using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using GRC2.Core;
using GRC2.Helpers;

namespace GRC2.Injectors
{
    /// <summary>
    /// BGM 메서드 호출 후킹을 담당하는 클래스
    /// </summary>
    internal static class BgmMethodCallHooks
    {
        // 성능 최적화: HashSet 사용 (배열 Contains 대신 O(1) 검색)
        private static readonly HashSet<string> _excludedMethods = new HashSet<string>
        {
            "GetInstanceID", "GetHashCode", "ToString", "Equals", "GetType", "MemberwiseClone",
            "CompareTag", "SendMessage", "BroadcastMessage", "SendMessageUpwards",
            "Invoke", "InvokeRepeating", "CancelInvoke", "IsInvoking",
            "GetComponent", "GetComponents", "GetComponentInChildren", "GetComponentInParent",
            "GetComponentsInChildren", "GetComponentsInParent", "TryGetComponent",
            "StartCoroutine", "StopCoroutine", "StopAllCoroutines",
            "GetScriptClassName", "GetComponentFastPath",
            "getCurrentSample", "getSamplePerSec", "getAudioSorceCurrentTime",
            "setVolume", "setSpeed", "getAudioClip", "isReadyPlay"  // 고빈도 조회 메서드 로그 제외
        };
        
        private static readonly HashSet<string> _importantMethods = new HashSet<string>
        {
            "setClip", "requestLoadBGM", "requestPlayAudio", "setTime",
            "setIsLoop", "setBPM", "setSample"
        };

        public static void MethodCallPostfixVoid(object __instance, MethodBase __originalMethod, object[] __args)
        {
            try
            {
                var methodName = __originalMethod.Name;
                if (_excludedMethods.Contains(methodName)) return;

                LogAndHandleMethodCall(__instance, methodName, __args, resultStr: null);

                if (methodName == "requestCommonRythmGameEnd")
                    LogRythmGameEndTiming(__instance);
            }
            catch { }
        }

        public static void MethodCallPostfix(object __instance, MethodBase __originalMethod, object[] __args, object __result)
        {
            try
            {
                var methodName = __originalMethod.Name;
                if (_excludedMethods.Contains(methodName)) return;

                var resultStr = BgmFormattingUtils.FormatResult(__result);
                LogAndHandleMethodCall(__instance, methodName, __args, resultStr);
            }
            catch { }
        }

        private static void LogAndHandleMethodCall(object instance, string methodName, object[] args, string resultStr)
        {
            if (_importantMethods.Contains(methodName))
            {
                var argsStr = BgmFormattingUtils.FormatArguments(args);
                MelonLogger.Msg($"[BgmInjectorHooks] ⚡ {methodName} 호출됨: {methodName}({argsStr})");
                HandleImportantMethodCall(instance, methodName);
            }
            else if (methodName == "requestPause")
            {
                HandleImportantMethodCall(instance, methodName);
            }
            else if (methodName.Contains("Clear") || methodName.Contains("End") || methodName.Contains("Finish"))
            {
                var argsStr = BgmFormattingUtils.FormatArguments(args);
                MelonLogger.Msg($"[BgmInjectorHooks] ⚠ 게임 종료/클리어 메서드 호출: {methodName}({argsStr})");
            }
            else
            {
                var argsStr = BgmFormattingUtils.FormatArguments(args);
                var suffix = resultStr != null ? resultStr : string.Empty;
                MelonLogger.Msg($"[BgmInjectorHooks] 호출: {methodName}({argsStr}){suffix}");
            }
        }
        
        public static void SetTimePrefix(object __instance, ref float time)
        {
            var targetFinishTime = BgmFinishTimeManager.GetTargetFinishTime();
            if (targetFinishTime > 0f && time > targetFinishTime)
            {
                time = targetFinishTime;
            }
        }

        public static void RequestLoadBGMPrefix(object __instance)
        {
            try
            {
                // 커스텀 차트인지 확인
                if (CustomAssetManager.IsCustomChartSelected())
                {
                    MelonLogger.Msg("[BgmInjectorHooks] 🎵 커스텀 차트 감지됨 (RequestLoadBGMPrefix)");
                    var currentBgmFile = GRC2.Core.AlbumManager.GetCurrentBgmFile();
                    MelonLogger.Msg($"[BgmInjectorHooks] 🔍 현재 BGM 파일: {currentBgmFile}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[BgmInjectorHooks] ❌ requestLoadBGM prefix 오류: {ex.Message}");
                MelonLogger.Msg($"[BgmInjectorHooks] 스택 트레이스: {ex.StackTrace}");
            }
        }

        private static void LogRythmGameEndTiming(object instance)
        {
            try
            {
                var instanceType = instance.GetType();
                var bgmManagerType = instanceType.Assembly.GetType("IntiCreates.cBGMBeatManager");
                if (bgmManagerType == null)
                    return;

                var bgmManagers = UnityEngine.Object.FindObjectsOfType(bgmManagerType);
                if (bgmManagers == null || bgmManagers.Length == 0)
                    return;

                var getCurrentSampleMethod = bgmManagerType.GetMethod("getCurrentSample",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var getAudioClipMethod = bgmManagerType.GetMethod("getAudioClip",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (getCurrentSampleMethod == null || getAudioClipMethod == null)
                    return;

                var currentSample = getCurrentSampleMethod.Invoke(bgmManagers[0], null);
                var clip = getAudioClipMethod.Invoke(bgmManagers[0], null) as AudioClip;

                if (currentSample is int intCurrentSample && clip != null)
                {
                    float currentTime = intCurrentSample / 48000f;
                    MelonLogger.Msg($"[BgmInjectorHooks] 재생 시간: {currentTime:F3}초 / BGM 길이: {clip.length:F3}초");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[BgmMethodCallHooks] MethodCallPostfix", "재생 시간 로깅 중 예외");
            }
        }
        
        /// <summary>
        /// 중요한 메서드 호출 후 처리
        /// setClip에서 커스텀 BGM 교체 로직 삭제 - BgmInjector에서 처리함
        /// </summary>
        private static void HandleImportantMethodCall(object instance, string methodName)
        {
            // null 체크 강화
            if (instance == null)
            {
                MelonLogger.Warning($"[BgmMethodCallHooks] HandleImportantMethodCall: instance가 null입니다 (methodName: {methodName})");
                return;
            }
            
            Type instanceType = null;
            try
            {
                instanceType = instance.GetType();
                if (instanceType == null)
                {
                    MelonLogger.Warning($"[BgmMethodCallHooks] HandleImportantMethodCall: instanceType이 null입니다");
                    return;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BgmMethodCallHooks] HandleImportantMethodCall: 타입 가져오기 실패 - {ex.Message}");
                return;
            }
            
            try
            {
                // setClip 호출 후 실제 클립 확인만 수행 (커스텀 BGM 교체는 BgmInjector에서 처리)
                if (methodName == "setClip")
                {
                    BgmAudioStateChecker.CheckAudioClip(instance, instanceType, "setClip");
                    // 커스텀 BGM 교체 로직 삭제됨 - BgmInjector.InjectCustomBGM()에서 처리
                }
                // requestLoadBGM, requestPlayAudio 호출 후 상태 확인
                else if (methodName == "requestLoadBGM" || methodName == "requestPlayAudio")
                {
                    BgmAudioStateChecker.CheckAudioClip(instance, instanceType, methodName);
                    BgmAudioStateChecker.CheckIsReadyPlay(instance, instanceType, methodName);
                    BgmAudioStateChecker.CheckAudioSource(instance, instanceType);
                    
                    // requestPlayAudio 호출 시 주입된 BGM 길이 확인 및 게임 종료 시간 조정 (주입 금지 씬에서는 건너뜀)
                    if (methodName == "requestPlayAudio" && BgmInjector.IsInjected && CustomAssetManager.ShouldInjectCustomContent())
                    {
                        AudioClip clip = null;
                        try
                        {
                            clip = BgmAudioStateChecker.GetAudioClip(instance, instanceType);
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Warning($"[BgmMethodCallHooks] GetAudioClip 오류: {ex.Message}");
                        }
                        
                        if (clip != null)
                        {
                            bool isInjectedBgm = string.IsNullOrEmpty(clip.name) || 
                                                 (!clip.name.StartsWith("PCD_") && !clip.name.StartsWith("PCD_RHYTHM"));
                            
                            if (isInjectedBgm)
                            {
                                var clipName = string.IsNullOrEmpty(clip.name) ? "(이름 없음)" : clip.name;
                                MelonLogger.Msg($"[BgmMethodCallHooks] 주입된 BGM 감지: {clipName}, {clip.length:F3}초");
                                
                                try
                                {
                                    BgmFinishTimeManager.SetFinishTime(clip.length, instanceType);
                                    BgmMonitorCoroutine.StartBgmMonitorCoroutine(instanceType);
                                }
                                catch (Exception ex)
                                {
                                    MelonLogger.Warning($"[BgmMethodCallHooks] 게임 종료 시간 설정 오류: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BgmMethodCallHooks] HandleImportantMethodCall 오류 ({methodName}): {ex.Message}");
                MelonLogger.Warning($"[BgmMethodCallHooks] 스택: {ex.StackTrace}");
            }
        }
        
    }
}
