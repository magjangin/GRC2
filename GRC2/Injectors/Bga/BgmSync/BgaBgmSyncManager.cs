using System;
using MelonLoader;
using UnityEngine;
using UnityEngine.Video;

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
}
