using System;
using System.Linq;
using MelonLoader;
using UnityEngine.Video;

namespace GRC2.Injectors
{
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
