using System;
using System.Collections;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace GRC2.Injectors
{
    /// <summary>
    /// BGM 모니터링 코루틴을 담당하는 클래스
    /// </summary>
    internal static class BgmMonitorCoroutine
    {
        private static object _bgmMonitorCoroutine = null;
        private static readonly WaitForSeconds _monitorInterval = new WaitForSeconds(0.1f);
        
        /// <summary>
        /// BGM 모니터링 코루틴 시작
        /// </summary>
        public static void StartBgmMonitorCoroutine(Type bgmBeatManagerType)
        {
            try
            {
                // 기존 코루틴이 있으면 중지
                if (_bgmMonitorCoroutine != null)
                {
                    MelonCoroutines.Stop(_bgmMonitorCoroutine);
                    _bgmMonitorCoroutine = null;
                }
                
                // 새 코루틴 시작
                _bgmMonitorCoroutine = MelonCoroutines.Start(MonitorBgmFinishCoroutine(bgmBeatManagerType));
                MelonLogger.Msg("[BgmInjectorHooks] BGM 모니터링 코루틴 시작");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BgmInjectorHooks] BGM 모니터링 코루틴 시작 실패: {ex.Message}");
            }
        }
        
        /// <summary>
        /// BGM 재생 시간을 모니터링하여 BGM 길이에 도달하면 강제로 결과 화면으로 전환
        /// coMonitorGameEnd가 차단되어 무한 플레이 모드이므로, 여기서 BGM 종료를 감지하여 결과 화면으로 전환
        /// </summary>
        private static IEnumerator MonitorBgmFinishCoroutine(Type bgmBeatManagerType)
        {
            // BGM 길이 가져오기 (우선순위: BgmInjector.GetBgmLength() > BgmFinishTimeManager > AudioClip.length)
            float targetFinishTime = BgmInjector.GetBgmLength();
            if (targetFinishTime <= 0f)
            {
                targetFinishTime = BgmFinishTimeManager.GetTargetFinishTime();
            }
            
            if (targetFinishTime <= 0f)
            {
                MelonLogger.Warning("[BgmMonitorCoroutine] BGM 길이가 설정되지 않았습니다. AudioClip에서 가져오기를 시도합니다.");
            }
            
            MelonLogger.Msg($"[BgmMonitorCoroutine] BGM 모니터링 시작 (목표 종료 시간: {targetFinishTime:F3}초)");
            
            var assembly = bgmBeatManagerType.Assembly;
            var rythmGameManagerType = assembly.GetType("IntiCreates.cRythmGameManager");
            
            float lastLogTime = 0f;
            object cachedBgmManager = null;
            object cachedRythmGameManager = null;
            
            // 리플렉션 메서드 캐싱 (루프 밖에서 한 번만 수행)
            MethodInfo getCurrentSampleMethod = null;
            MethodInfo getAudioClipMethod = null;
            MethodInfo requestEndMethod = null;
            
            // cBGMBeatManager 메서드 찾기
            try {
                getCurrentSampleMethod = bgmBeatManagerType.GetMethod("getCurrentSample",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                getAudioClipMethod = bgmBeatManagerType.GetMethod("getAudioClip",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            } catch (Exception ex) {
                MelonLogger.Warning($"[BgmMonitorCoroutine] cBGMBeatManager 메서드 찾기 실패: {ex.Message}");
            }
            
            // cRythmGameManager 메서드 찾기
            if (rythmGameManagerType != null) {
                try {
                    requestEndMethod = rythmGameManagerType.GetMethod("requestCommonRythmGameEnd",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                } catch (Exception ex) {
                    MelonLogger.Warning($"[BgmMonitorCoroutine] cRythmGameManager 메서드 찾기 실패: {ex.Message}");
                }
            }

            if (getCurrentSampleMethod == null || getAudioClipMethod == null)
            {
                MelonLogger.Error("[BgmMonitorCoroutine] 필수 메서드를 찾을 수 없어 코루틴을 종료합니다.");
                yield break;
            }

            while (true)
            {
                yield return _monitorInterval; // 0.1초마다 체크
                
                try
                {
                    // cBGMBeatManager에서 현재 재생 시간 확인
                    if (cachedBgmManager == null || ReferenceEquals(cachedBgmManager, null))
                    {
                        var bgmManagers = UnityEngine.Object.FindObjectsOfType(bgmBeatManagerType);
                        if (bgmManagers == null || bgmManagers.Length == 0)
                        {
                            cachedBgmManager = null;
                            continue;
                        }

                        cachedBgmManager = bgmManagers[0];
                    }
                    
                    var bgmInstance = cachedBgmManager;
                    
                    var currentSample = getCurrentSampleMethod.Invoke(bgmInstance, null);
                    if (!(currentSample is int intCurrentSample))
                    {
                        continue;
                    }
                    
                    float currentPlayTime = intCurrentSample / 48000f;
                    
                    // BGM 길이 확인 (AudioClip에서 가져오기)
                    var clip = getAudioClipMethod.Invoke(bgmInstance, null) as AudioClip;
                    if (clip == null)
                    {
                        continue;
                    }
                    
                    float bgmLength = clip.length;
                    
                    // BGM 길이가 설정되지 않았으면 AudioClip에서 가져온 값 사용
                    if (targetFinishTime <= 0f)
                    {
                        targetFinishTime = bgmLength;
                        MelonLogger.Msg($"[BgmMonitorCoroutine] BGM 길이 설정: {targetFinishTime:F3}초 (AudioClip에서 가져옴)");
                    }
                    
                    // 종료 조건: 현재 재생 시간이 BGM 길이에 도달 (0.2초 여유)
                    if (currentPlayTime >= targetFinishTime - 0.2f)
                    {
                        MelonLogger.Msg($"[BgmMonitorCoroutine] 재생 시간: {currentPlayTime:F3}초 / BGM 길이: {targetFinishTime:F3}초");
                        
                        // 게임 종료 요청 (결과 화면으로 전환)
                        if (requestEndMethod != null)
                        {
                            if (cachedRythmGameManager == null || ReferenceEquals(cachedRythmGameManager, null))
                            {
                                var rythmGameManagers = UnityEngine.Object.FindObjectsOfType(rythmGameManagerType);
                                if (rythmGameManagers != null && rythmGameManagers.Length > 0)
                                {
                                    cachedRythmGameManager = rythmGameManagers[0];
                                }
                            }

                            if (cachedRythmGameManager != null && !ReferenceEquals(cachedRythmGameManager, null))
                            {
                                requestEndMethod.Invoke(cachedRythmGameManager, null);
                                break; // 코루틴 종료
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    cachedBgmManager = null;
                    cachedRythmGameManager = null;
                    // 오류 발생 시 로그 출력 (너무 자주 출력되지 않도록)
                    if (Time.time - lastLogTime >= 5f)
                    {
                        MelonLogger.Warning($"[BgmMonitorCoroutine] 모니터링 오류: {ex.Message}");
                        lastLogTime = Time.time;
                    }
                }
            }
            
            MelonLogger.Msg("[BgmMonitorCoroutine] BGM 모니터링 코루틴 종료");
            _bgmMonitorCoroutine = null;
        }
    }
}

