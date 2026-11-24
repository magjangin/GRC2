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

            // 여기서 바로 탐색하지 않습니다. BGM은 isPlaying인 채로 약 2초 늦게 흐르기 시작하므로(2026-11-24 로그)
            // 첫 틱에서 BGM이 흐르는지 본 뒤 멈춰 두거나 그 시각에서 재생합니다(BgaSyncPolicy).
            _lastSeekRealtime = new float[_videoPlayers.Length];
            for (int i = 0; i < _lastSeekRealtime.Length; i++)
                _lastSeekRealtime[i] = float.NegativeInfinity;

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

        /// <summary>
        /// cBGMBeatManager를 못 찾았을 때 다시 찾기까지의 간격(초). 동기화 루프가 0.1초마다 시간을 읽으므로
        /// 매니저가 없는 씬(무비 씬 등)에서는 0.1초마다 씬 전체를 훑었습니다(2026-11-23 감사).
        /// </summary>
        private const float ManagerSearchRetrySeconds = 1f;
        private static float _nextManagerSearchTime;

        private static cBGMBeatManager GetBgmBeatManager()
        {
            if (_cachedBgmManager != null)
            {
                return _cachedBgmManager;
            }

            if (Time.unscaledTime < _nextManagerSearchTime)
            {
                return null;
            }
            _nextManagerSearchTime = Time.unscaledTime + ManagerSearchRetrySeconds;

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

        // 동기화 판단은 BgaSyncPolicy(단위 테스트 있음)가 하고, 여기서는 VideoPlayer에 적용만 합니다.
        private const float SyncIntervalSeconds = 0.1f;
        private static float[] _lastSeekRealtime;

        private static IEnumerator SyncCoroutine()
        {
            var wait = new WaitForSeconds(SyncIntervalSeconds); // 재사용: 0.1초마다 할당하지 않도록
            float nextSourceSearchTime = 0f;
            double? previousBgmTime = null;
            bool holding = false;

            while (_isSyncing)
            {
                yield return wait;

                if (_bgmAudioSource == null || _videoPlayers == null)
                {
                    // 오디오 소스를 잃었을 때 0.1초마다 씬 전체 AudioSource를 훑지 않도록 1초 간격으로만 다시 찾습니다.
                    if (Time.unscaledTime < nextSourceSearchTime)
                        continue;
                    nextSourceSearchTime = Time.unscaledTime + ManagerSearchRetrySeconds;

                    _bgmAudioSource = GetCurrentAudioSource();
                    if (_bgmAudioSource == null)
                        continue;
                }

                try
                {
                    double bgmTime = ReadCurrentBgmTime(out _);
                    bool anyHeld = false;

                    for (int i = 0; i < _videoPlayers.Length; i++)
                    {
                        var videoPlayer = _videoPlayers[i];
                        if (videoPlayer == null || !videoPlayer.isPrepared)
                            continue;

                        var decision = BgaSyncPolicy.Decide(
                            bgmTime,
                            previousBgmTime,
                            videoPlayer.time,
                            videoPlayer.length,
                            videoPlayer.isPlaying,
                            Time.realtimeSinceStartup - _lastSeekRealtime[i],
                            videoPlayer.canSetPlaybackSpeed);

                        if (decision.Action == BgaSyncAction.Hold)
                            anyHeld = true;
                        Apply(videoPlayer, i, decision, bgmTime);
                    }

                    if (anyHeld != holding)
                    {
                        holding = anyHeld;
                        MelonLogger.Msg(holding
                            ? $"[BGAPlayerHook] BGM이 흐르지 않아 BGA를 멈춰 둡니다 (BGM {bgmTime:F3}초)"
                            : $"[BGAPlayerHook] BGM이 흐르기 시작해 BGA를 이어서 재생합니다 (BGM {bgmTime:F3}초)");
                    }

                    previousBgmTime = bgmTime;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[BGAPlayerHook] 동기화 모니터링 오류: {ex.Message}");
                }
            }
        }

        private static void Apply(VideoPlayer videoPlayer, int index, BgaSyncDecision decision, double bgmTime)
        {
            switch (decision.Action)
            {
                case BgaSyncAction.Hold:
                    if (videoPlayer.isPlaying)
                        videoPlayer.Pause();
                    SetSpeed(videoPlayer, 1f);
                    break;

                case BgaSyncAction.Seek:
                    bool wasPlaying = videoPlayer.isPlaying;
                    SetSpeed(videoPlayer, 1f);
                    videoPlayer.time = decision.SeekTime;
                    if (!wasPlaying)
                        videoPlayer.Play();
                    _lastSeekRealtime[index] = Time.realtimeSinceStartup;
                    // 영상이 계속 재생 중이었다면 큰 차이를 바로잡은 것이니 남기고, 멈췄다 다시 시작한 것은 위의 상태 로그로 충분합니다.
                    if (wasPlaying)
                        MelonLogger.Msg($"[BGABGMSyncHook] 재동기화(탐색): {videoPlayer.gameObject.name} BGA가 {decision.Drift:+0.000;-0.000}초 어긋나 {decision.SeekTime:F3}초로 이동 (BGM {bgmTime:F3}초)");
                    break;

                case BgaSyncAction.AdjustSpeed:
                    SetSpeed(videoPlayer, decision.PlaybackSpeed);
                    ModLog.Verbose($"[BGABGMSyncHook] 배속 보정: {videoPlayer.gameObject.name} 차이 {decision.Drift:+0.000;-0.000}초 → 배속 {decision.PlaybackSpeed:F3}");
                    break;

                default:
                    SetSpeed(videoPlayer, 1f);
                    break;
            }
        }

        private static void SetSpeed(VideoPlayer videoPlayer, float speed)
        {
            if (videoPlayer.canSetPlaybackSpeed && Math.Abs(videoPlayer.playbackSpeed - speed) > 0.0005f)
                videoPlayer.playbackSpeed = speed;
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

        private static void StopSync()
        {
            if (_syncCoroutine != null)
            {
                MelonCoroutines.Stop(_syncCoroutine);
                _syncCoroutine = null;
            }

            _isSyncing = false;
            RestoreNormalSpeed();
            _bgmAudioSource = null;
            _videoPlayers = null;
            _lastSeekRealtime = null;
            _cachedBgmManager = null;
            _nextManagerSearchTime = 0f;

            MelonLogger.Msg("[BGAPlayerHook] 오디오 동기화 모니터링 중지");
        }

        public static void Reset()
        {
            StopSync();
        }

        /// <summary>배속 보정 중에 멈췄다면 VideoPlayer를 1배속으로 되돌립니다(씬에 남아 다시 쓰일 수 있음).</summary>
        private static void RestoreNormalSpeed()
        {
            if (_videoPlayers == null)
                return;

            foreach (var videoPlayer in _videoPlayers)
            {
                try
                {
                    if (videoPlayer != null)
                        SetSpeed(videoPlayer, 1f);
                }
                catch (Exception ex)
                {
                    ModLog.Verbose($"[BGAPlayerHook] 배속 복원 실패: {ex.Message}");
                }
            }
        }
    }
}
