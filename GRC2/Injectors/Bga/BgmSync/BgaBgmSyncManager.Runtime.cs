using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using UnityEngine.Video;


namespace GRC2.Injectors
{
    internal static partial class BgaBgmSyncManager
    {
    

        private static MethodInfo _getCurrentSampleMethod = null;

        private static MethodInfo _getTimeMethod = null;

        private static Assembly _cachedGameAssembly = null;
        private static Type _cachedBgmBeatManagerType = null;
        private static object _cachedBgmManagerInstance = null;

        private static bool _methodsCached = false;

        private static void CacheMethods(Type bgmBeatManagerType)
        {
            if (_methodsCached || bgmBeatManagerType == null) return;

            try
            {
                _getCurrentSampleMethod = bgmBeatManagerType.GetMethod("getCurrentSample",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                _getTimeMethod = bgmBeatManagerType.GetMethod("getAudioSorceCurrentTime",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                _methodsCached = true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BGAPlayerHook] 메서드 캐싱 실패: {ex.Message}");
            }
        }

        private static float GetBgmTimeFromManager()
        {
            try
            {
                if (_cachedGameAssembly == null)
                {
                    var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                    _cachedGameAssembly = loadedAssemblies.FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                }

                if (_cachedGameAssembly == null)
                {
                    return 0f;
                }

                if (_cachedBgmBeatManagerType == null)
                {
                    _cachedBgmBeatManagerType = _cachedGameAssembly.GetType("IntiCreates.cBGMBeatManager");
                }

                var bgmBeatManagerType = _cachedBgmBeatManagerType;
                if (bgmBeatManagerType == null)
                {
                    return 0f;
                }

                // 메서드 캐싱 시도
                if (!_methodsCached)
                {
                    CacheMethods(bgmBeatManagerType);
                }

                object bgmManager = _cachedBgmManagerInstance;
                if (bgmManager == null || ReferenceEquals(bgmManager, null))
                {
                    var bgmManagers = UnityEngine.Object.FindObjectsOfType(bgmBeatManagerType);
                    if (bgmManagers == null || bgmManagers.Length == 0)
                    {
                        _cachedBgmManagerInstance = null;
                        return 0f;
                    }

                    bgmManager = bgmManagers[0];
                    _cachedBgmManagerInstance = bgmManager;
                }

                // getCurrentSample 메서드로 샘플 가져오기
                if (_getCurrentSampleMethod != null)
                {
                    var currentSample = _getCurrentSampleMethod.Invoke(bgmManager, null);
                    
                    if (currentSample is int intSample && intSample > 0)
                    {
                        return intSample / 48000f;
                    }
                    else if (currentSample is long longSample && longSample > 0)
                    {
                        return longSample / 48000f;
                    }
                }

                // getAudioSorceCurrentTime 메서드로 시간 가져오기
                if (_getTimeMethod != null)
                {
                    var time = _getTimeMethod.Invoke(bgmManager, null);
                    
                    if (time is float floatTime && floatTime > 0f)
                    {
                        return floatTime;
                    }
                    else if (time is double doubleTime && doubleTime > 0.0)
                    {
                        return (float)doubleTime;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BGAPlayerHook] GetBgmTimeFromManager 오류: {ex.Message}");
            }

            return 0f;
        }

        private static IEnumerator SyncCoroutine()
        {
            float lastSyncTime = 0f;
            float syncInterval = 0.1f; // 0.1초마다 동기화 확인
            bool hasSyncedOnce = false; // 최소 한 번은 동기화했는지 여부
            bool wasPlaying = false; // 이전 프레임에서 재생 중이었는지

            while (_isSyncing)
            {
                yield return new WaitForSeconds(syncInterval);

                if (_bgmAudioSource == null || _videoPlayers == null)
                {
                    _bgmAudioSource = GetCurrentAudioSource();
                    if (_bgmAudioSource == null)
                    {
                        continue;
                    }
                }

                try
                {
                    bool isPlaying = _bgmAudioSource.isPlaying;
                    float bgmTime = 0f;

                    // BGM이 방금 재생되기 시작했는지 확인
                    if (isPlaying && !wasPlaying)
                    {
                        hasSyncedOnce = false; // 재생이 시작되었으므로 다시 동기화 필요
                    }
                    wasPlaying = isPlaying;

                    // BGM이 재생 중이면 시간 가져오기
                    if (isPlaying)
                    {
                        bgmTime = _bgmAudioSource.time;
                        if (bgmTime <= 0f)
                        {
                            bgmTime = GetBgmTimeFromManager();
                        }
                    }
                    else
                    {
                        // 재생 중이 아니어도 cBGMBeatManager에서 시간 가져오기 시도
                        bgmTime = GetBgmTimeFromManager();
                    }

                    // BGM이 재생되기 시작했고 아직 동기화하지 않았으면 즉시 동기화
                    if (isPlaying && !hasSyncedOnce && bgmTime > 0f)
                    {
                        SyncBgaToBgm();
                        hasSyncedOnce = true;
                        lastSyncTime = bgmTime;
                        continue;
                    }

                    if (bgmTime <= 0f)
                    {
                        continue;
                    }

                    // BGA와 BGM 시간 차이 확인
                    bool needsSync = false;
                    float maxTimeDiff = 0f;
                    float bgaTime = 0f;

                    foreach (var videoPlayer in _videoPlayers)
                    {
                        if (videoPlayer == null || !videoPlayer.isPlaying || !videoPlayer.isPrepared)
                        {
                            continue;
                        }
                        float currentBgaTime = (float)videoPlayer.time;
                        if (bgaTime == 0f)
                        {
                            bgaTime = currentBgaTime;
                        }

                        float expectedVideoTime = (float)(bgmTime % videoPlayer.length);
                        float timeDiff = Mathf.Abs((float)(videoPlayer.time - expectedVideoTime));
                        maxTimeDiff = Mathf.Max(maxTimeDiff, timeDiff);
                        
                        // 0.1초 이상 차이나면 동기화
                        if (timeDiff > 0.1f)
                        {
                            needsSync = true;
                        }
                    }

                    // 동기화가 필요한 경우에만 수행 (재동기화 시에만 로그)
                    if (needsSync && Mathf.Abs(bgmTime - lastSyncTime) > 1f)
                    {
                        MelonLogger.Msg($"[BGABGMSyncHook] 재동기화 완료: BGA {bgaTime:F3}초 -> BGM {bgmTime:F3}초 (차이 {maxTimeDiff:F3}초)");
                        SyncBgaToBgm();
                        lastSyncTime = bgmTime;
                        hasSyncedOnce = true;
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[BGAPlayerHook] 동기화 모니터링 오류: {ex.Message}");
                }
            }
        }

        public static void StopSync()
        {
            if (_syncCoroutine != null)
            {
                MelonCoroutines.Stop(_syncCoroutine);
                _syncCoroutine = null;
            }

            _isSyncing = false;
            _bgmAudioSource = null;
            _videoPlayers = null;
            _cachedBgmManagerInstance = null;

            MelonLogger.Msg("[BGAPlayerHook] 오디오 동기화 모니터링 중지");
        }

        public static void Reset()
        {
            StopSync();
        }
}
}
