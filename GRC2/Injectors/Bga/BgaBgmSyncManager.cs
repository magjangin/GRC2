using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.Video;
using System.Linq;
using System.Reflection;
using System.Collections;

namespace GRC2.Injectors
{
    /// <summary>
    /// BGA와 BGM 동기화를 담당하는 클래스
    /// </summary>
    internal static partial class BgaBgmSyncManager
    {
        private static object _syncCoroutine = null;
        private static bool _isSyncing = false;
        private static AudioSource _bgmAudioSource = null;
        private static VideoPlayer[] _videoPlayers = null;

        /// <summary>
        /// BGA 재생 시작 시 BGM과 동기화
        /// </summary>
        public static void StartSync(VideoPlayer[] videoPlayers)
        {
            if (_isSyncing)
            {
                return;
            }

            _videoPlayers = videoPlayers;
            _bgmAudioSource = GetCurrentAudioSource();

            if (_bgmAudioSource == null)
            {
                MelonLogger.Warning("[BGAPlayerHook] BGM 오디오 소스를 찾을 수 없어 동기화를 시작할 수 없습니다.");
                return;
            }

            if (_videoPlayers == null || _videoPlayers.Length == 0)
            {
                MelonLogger.Warning("[BGAPlayerHook] VideoPlayer를 찾을 수 없어 동기화를 시작할 수 없습니다.");
                return;
            }

            // BGA 재생 시작 시 BGM 시간에 맞춰 동기화
            SyncBgaToBgm();

            // 지속적인 동기화 모니터링 시작
            _isSyncing = true;
            _syncCoroutine = MelonCoroutines.Start(SyncCoroutine());

            MelonLogger.Msg($"[BGAPlayerHook] BGA-BGM 동기화 시작: {_videoPlayers.Length}개 VideoPlayer");
        }

    }

    internal static partial class BgaBgmSyncManager
    {
        /// <summary>
        /// BGM 오디오 소스 찾기 (cBGMBeatManager 우선, 그 다음 일반 검색)
        /// </summary>
        public static AudioSource GetCurrentAudioSource()
        {
            try
            {
                AudioSource bgmAudioSource = GetBgmAudioSourceFromManager();
                if (bgmAudioSource != null)
                {
                    return bgmAudioSource;
                }

                return FindBestAudioSource(UnityEngine.Object.FindObjectsOfType<AudioSource>());
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BGMPlayerHook] GetCurrentAudioSource 오류: {ex.Message}");
                return null;
            }
        }

        private static AudioSource FindBestAudioSource(AudioSource[] allAudioSources)
        {
            if (allAudioSources == null || allAudioSources.Length == 0)
            {
                return null;
            }

            AudioSource bestAudioSource = null;
            int bestScore = -1;
            foreach (var audioSource in allAudioSources)
            {
                if (audioSource == null) continue;

                int score = ScoreAudioSourceCandidate(audioSource);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestAudioSource = audioSource;
                }
            }

            return bestAudioSource;
        }

        private static int ScoreAudioSourceCandidate(AudioSource audioSource)
        {
            int score = 0;
            string name = audioSource.name ?? "";
            string clipName = audioSource.clip?.name ?? "";

            if (clipName.Contains("SE_") || clipName.Contains("_SE") || name.Contains("SFX") || name.Contains("Effect") || name.Contains("UI"))
            {
                score -= 10;
            }

            if (name.Contains("Audio") || name.Contains("BGM") || name.Contains("Music"))
            {
                score += 5;
            }

            if (audioSource.isPlaying)
            {
                score += 4;
            }

            if (audioSource.clip != null)
            {
                score += 2;
            }

            return score;
        }

        /// <summary>
        /// cBGMBeatManager에서 AudioSource 가져오기
        /// </summary>
        private static AudioSource GetBgmAudioSourceFromManager()
        {
            try
            {
                var gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                var bgmBeatManagerType = gameAssembly?.GetType("IntiCreates.cBGMBeatManager");
                if (bgmBeatManagerType == null)
                {
                    return null;
                }

                var bgmManagers = UnityEngine.Object.FindObjectsOfType(bgmBeatManagerType);
                if (bgmManagers == null || bgmManagers.Length == 0)
                {
                    return null;
                }

                var bgmManager = bgmManagers[0];
                return FindAudioSourceField(bgmBeatManagerType, bgmManager) ??
                    FindAudioSourceComponent(bgmManager as MonoBehaviour);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BGMPlayerHook] GetBgmAudioSourceFromManager 오류: {ex.Message}");
            }

            return null;
        }

        private static AudioSource FindAudioSourceField(Type bgmBeatManagerType, object bgmManager)
        {
            var fields = bgmBeatManagerType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(AudioSource))
                {
                    var audioSource = field.GetValue(bgmManager) as AudioSource;
                    if (audioSource != null)
                    {
                        return audioSource;
                    }
                }
            }

            return null;
        }

        private static AudioSource FindAudioSourceComponent(MonoBehaviour bgmManager)
        {
            var components = bgmManager?.GetComponents<Component>();
            if (components == null)
            {
                return null;
            }

            foreach (var component in components)
            {
                if (component is AudioSource audioSource)
                {
                    return audioSource;
                }
            }

            return null;
        }
    }

    internal static partial class BgaBgmSyncManager
    {
    

        private static MethodInfo _getCurrentSampleMethod = null;

        private static MethodInfo _getTimeMethod = null;

        private static Assembly _cachedGameAssembly = null;
        private static Type _cachedBgmBeatManagerType = null;
        private static object _cachedBgmManagerInstance = null;

        private static bool _methodsCached = false;

        private static int _consecutiveInvokeFailures = 0;
        private const int MaxInvokeFailuresBeforeDisable = 3;
        private static bool _invokeDisabledThisSession = false;

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
            if (_invokeDisabledThisSession) return 0f;

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
                    _consecutiveInvokeFailures = 0;

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
                    _consecutiveInvokeFailures = 0;

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
                _consecutiveInvokeFailures++;
                if (_consecutiveInvokeFailures <= MaxInvokeFailuresBeforeDisable)
                {
                    MelonLogger.Warning($"[BGAPlayerHook] GetBgmTimeFromManager 오류 ({_consecutiveInvokeFailures}/{MaxInvokeFailuresBeforeDisable}): {ex.Message}");
                }

                if (_consecutiveInvokeFailures == MaxInvokeFailuresBeforeDisable)
                {
                    _invokeDisabledThisSession = true;
                    MelonLogger.Warning("[BGAPlayerHook] GetBgmTimeFromManager이 반복 실패하여 이번 곡에서는 더 이상 시도하지 않습니다 (BGA 없는 곡일 수 있음).");
                }
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
            _consecutiveInvokeFailures = 0;
            _invokeDisabledThisSession = false;

            MelonLogger.Msg("[BGAPlayerHook] 오디오 동기화 모니터링 중지");
        }

        public static void Reset()
        {
            StopSync();
        }
}

    internal static partial class BgaBgmSyncManager
    {
        /// <summary>
        /// BGA를 BGM 시간에 맞춰 동기화
        /// </summary>
        private static void SyncBgaToBgm()
        {
            if (_bgmAudioSource == null || _videoPlayers == null)
            {
                MelonLogger.Warning($"[BGAPlayerHook] SyncBgaToBgm: 오디오 소스 또는 VideoPlayer가 null입니다. (AudioSource: {_bgmAudioSource != null}, VideoPlayers: {_videoPlayers != null})");
                return;
            }

            try
            {
                float bgmTime = ResolveBgmTimeForSync();
                if (bgmTime <= 0f)
                {
                    MelonLogger.Msg($"[BGAPlayerHook] SyncBgaToBgm: BGM 시간이 0이어서 동기화 건너뜀 (AudioSource.isPlaying={_bgmAudioSource?.isPlaying}, clip={_bgmAudioSource?.clip?.name})");
                    return;
                }

                int syncedCount = SyncPreparedVideoPlayers(bgmTime);
                LogSyncResult(bgmTime, syncedCount);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BGAPlayerHook] BGA-BGM 동기화 오류: {ex.Message}");
            }
        }

        private static float ResolveBgmTimeForSync()
        {
            if (_bgmAudioSource.isPlaying && _bgmAudioSource.clip != null)
            {
                return _bgmAudioSource.time;
            }

            if (_bgmAudioSource.clip != null && _bgmAudioSource.time > 0f)
            {
                return _bgmAudioSource.time;
            }

            return GetBgmTimeFromManager();
        }

        private static int SyncPreparedVideoPlayers(float bgmTime)
        {
            int syncedCount = 0;
            foreach (var videoPlayer in _videoPlayers)
            {
                if (videoPlayer == null || !videoPlayer.isPrepared)
                {
                    continue;
                }

                if (TrySyncVideoPlayer(videoPlayer, bgmTime))
                {
                    syncedCount++;
                }
            }

            return syncedCount;
        }

        private static bool TrySyncVideoPlayer(VideoPlayer videoPlayer, float bgmTime)
        {
            try
            {
                float syncTime = (float)(bgmTime % videoPlayer.length);
                float oldTime = (float)videoPlayer.time;
                videoPlayer.time = syncTime;
                MelonLogger.Msg($"[BGAPlayerHook] 동기화: {videoPlayer.gameObject.name} - BGM={bgmTime:F3}초, BGA={oldTime:F3}초 → {syncTime:F3}초");
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BGAPlayerHook] {videoPlayer.gameObject.name} 동기화 실패: {ex.Message}");
                return false;
            }
        }

        private static void LogSyncResult(float bgmTime, int syncedCount)
        {
            if (syncedCount > 0)
            {
                MelonLogger.Msg($"[BGAPlayerHook] ✅ BGA-BGM 동기화 완료: {syncedCount}개 VideoPlayer (BGM 시간: {bgmTime:F3}초)");
                return;
            }

            int preparedCount = _videoPlayers.Count(vp => vp != null && vp.isPrepared);
            MelonLogger.Warning($"[BGAPlayerHook] ⚠️ 동기화된 VideoPlayer가 없습니다 (준비된 VideoPlayer: {preparedCount}/{_videoPlayers.Length})");
        }
    }
}
