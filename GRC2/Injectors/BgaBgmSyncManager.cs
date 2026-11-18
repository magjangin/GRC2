using System;
using IntiCreates;
using MelonLoader;
using GRC2.Helpers;
using UnityEngine;
using UnityEngine.Video;
using System.Collections;

namespace GRC2.Injectors
{
    /// <summary>
    /// BGA와 BGM 동기화를 담당하는 클래스
    /// </summary>
    internal static class BgaBgmSyncManager
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

        /// <summary>
        /// BGM 오디오 소스 찾기 (cBGMBeatManager 우선, 그 다음 일반 검색)
        /// </summary>
        private static AudioSource GetCurrentAudioSource()
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
                cBGMBeatManager manager = GetBgmBeatManager();
                return manager?.getAudioSorce();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BGMPlayerHook] GetBgmAudioSourceFromManager 오류: {ex.Message}");
            }

            return null;
        }

        private static cBGMBeatManager _cachedBgmManager;

        private static cBGMBeatManager GetBgmBeatManager()
        {
            if (_cachedBgmManager != null)
            {
                return _cachedBgmManager;
            }

            cBGMBeatManager[] managers =
                UnityEngine.Object.FindObjectsOfType<cBGMBeatManager>();
            _cachedBgmManager =
                managers != null && managers.Length > 0 ? managers[0] : null;
            return _cachedBgmManager;
        }

        private static float GetBgmTimeFromManager()
        {
            try
            {
                cBGMBeatManager manager = GetBgmBeatManager();
                return manager == null
                    ? 0f
                    : manager.getAudioSorceCurrentTime();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(
                    $"[BGAPlayerHook] GetBgmTimeFromManager 오류: {ex.Message}");
            }

            return 0f;
        }

        private static IEnumerator SyncCoroutine()
        {
            float lastSyncTime = 0f;
            const float syncInterval = 0.1f;
            bool hasSyncedOnce = false;
            bool wasPlaying = false;

            while (_isSyncing)
            {
                yield return new WaitForSeconds(syncInterval);

                if (_bgmAudioSource == null || _videoPlayers == null)
                {
                    _bgmAudioSource = GetCurrentAudioSource();
                    if (_bgmAudioSource == null)
                        continue;
                }

                try
                {
                    float bgmTime = ReadCurrentBgmTime(out bool isPlaying);

                    if (isPlaying && !wasPlaying)
                        hasSyncedOnce = false;
                    wasPlaying = isPlaying;

                    if (isPlaying && !hasSyncedOnce && bgmTime > 0f)
                    {
                        SyncBgaToBgm();
                        hasSyncedOnce = true;
                        lastSyncTime = bgmTime;
                        continue;
                    }

                    if (bgmTime <= 0f)
                        continue;

                    if (TryFindResyncNeed(bgmTime, out float bgaTime, out float maxTimeDiff) &&
                        Mathf.Abs(bgmTime - lastSyncTime) > 1f)
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

        private static float ReadCurrentBgmTime(out bool isPlaying)
        {
            isPlaying = _bgmAudioSource.isPlaying;
            if (isPlaying)
            {
                float time = _bgmAudioSource.time;
                return time > 0f ? time : GetBgmTimeFromManager();
            }

            return GetBgmTimeFromManager();
        }

        private static bool TryFindResyncNeed(float bgmTime, out float bgaTime, out float maxTimeDiff)
        {
            bool needsSync = false;
            maxTimeDiff = 0f;
            bgaTime = 0f;

            foreach (var videoPlayer in _videoPlayers)
            {
                if (videoPlayer == null || !videoPlayer.isPlaying || !videoPlayer.isPrepared)
                    continue;

                float currentBgaTime = (float)videoPlayer.time;
                if (bgaTime == 0f)
                    bgaTime = currentBgaTime;

                float expectedVideoTime = (float)(bgmTime % videoPlayer.length);
                float timeDiff = Mathf.Abs((float)(videoPlayer.time - expectedVideoTime));
                maxTimeDiff = Mathf.Max(maxTimeDiff, timeDiff);

                if (timeDiff > 0.1f)
                    needsSync = true;
            }

            return needsSync;
        }

        private static void StopSync()
        {
            if (_syncCoroutine != null)
            {
                MelonCoroutines.Stop(_syncCoroutine);
                _syncCoroutine = null;
            }

            _isSyncing = false;
            _bgmAudioSource = null;
            _videoPlayers = null;
            _cachedBgmManager = null;

            MelonLogger.Msg("[BGAPlayerHook] 오디오 동기화 모니터링 중지");
        }

        public static void Reset()
        {
            StopSync();
        }

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
                    ModLog.Verbose($"[BGAPlayerHook] SyncBgaToBgm: BGM 시간이 0이어서 동기화 건너뜀 (AudioSource.isPlaying={_bgmAudioSource?.isPlaying}, clip={_bgmAudioSource?.clip?.name})");
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
                ModLog.Verbose($"[BGAPlayerHook] 동기화: {videoPlayer.gameObject.name} - BGM={bgmTime:F3}초, BGA={oldTime:F3}초 → {syncTime:F3}초");
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

            int preparedCount = 0;
            for (int i = 0; i < _videoPlayers.Length; i++)
            {
                VideoPlayer videoPlayer = _videoPlayers[i];
                if (videoPlayer != null && videoPlayer.isPrepared)
                {
                    preparedCount++;
                }
            }
            MelonLogger.Warning($"[BGAPlayerHook] ⚠️ 동기화된 VideoPlayer가 없습니다 (준비된 VideoPlayer: {preparedCount}/{_videoPlayers.Length})");
        }
    }
}
