using System;
using System.Collections;
using IntiCreates;
using MelonLoader;
using GRC2.Helpers;

namespace GRC2.Injectors
{
    /// <summary>
    /// BGM 주입 메인 클래스 - 코루틴 및 상태 관리
    /// </summary>
    internal static class BgmInjector
    {
        private const int MaxAttemptCount = 10;

        private static bool _bgmInjected = false;
        private static int _bgmAttemptCount = 0;
        private static bool _bgmLogShown = false;
        private static cBGMBeatManager _bgmBeatManagerInstance = null;

        public static bool IsInjected => _bgmInjected;
        public static int AttemptCount => _bgmAttemptCount;
        public static bool LogShown => _bgmLogShown;

        public static void Reset()
        {
            _bgmInjected = false;
            _bgmAttemptCount = 0;
            _bgmLogShown = false;
            _bgmBeatManagerInstance = null;
        }

        public static IEnumerator TryInjectBgmCoroutine(string bgmFilePath)
        {
            MelonLogger.Msg("[BgmInjector] TryInjectBgmCoroutine 시작");

            // 시도 횟수 제한
            _bgmAttemptCount++;
            if (_bgmAttemptCount > MaxAttemptCount)
            {
                if (!_bgmLogShown)
                {
                    MelonLogger.Warning("[BgmInjector] BGM 주입 시도 횟수 초과. cBGMBeatManager를 찾을 수 없습니다.");
                    _bgmLogShown = true;
                }
                yield break;
            }

            MelonLogger.Msg($"[BgmInjector] BGM 주입 시도 횟수: {_bgmAttemptCount}/{MaxAttemptCount}");

            // ⚡ 성능 최적화: 캐시된 인스턴스 확인 (이미 발견되었으면 재검색 생략)
            if (_bgmBeatManagerInstance == null)
            {
                FindBeatManager();
            }

            // cBGMBeatManager 메서드로 BGM 주입 시도 (이미 주입되지 않은 경우만)
            if (!_bgmInjected && _bgmBeatManagerInstance != null)
            {
                yield return BgmLoader.LoadAndInjectAudioClip(
                    bgmFilePath,
                    _bgmBeatManagerInstance,
                    (injected) => _bgmInjected = injected);
            }
        }

        private static void FindBeatManager()
        {
            try
            {
                if (BgmSearcher.TryFindBeatManager(out var beatManager))
                {
                    _bgmBeatManagerInstance = beatManager;
                    MelonLogger.Msg("[BgmInjector] cBGMBeatManager 인스턴스 발견");
                    BgmSearcher.LogOriginalAudioInfo(_bgmBeatManagerInstance, "BgmInjector");
                    return;
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[BgmInjector] FindBeatManager", "FindObjectOfType(cBGMBeatManager) 실패");
            }

            // 활성 컴포넌트에서 찾지 못한 경우 AudioSource 쪽에서 역으로 찾기
            try
            {
                if (BgmSearcher.TryFindBeatManagerFromAudioSource(out var beatManagerFromAudio))
                {
                    _bgmBeatManagerInstance = beatManagerFromAudio;
                    MelonLogger.Msg("[BgmInjector] AudioSource에서 cBGMBeatManager 발견");
                    BgmSearcher.LogOriginalAudioInfo(_bgmBeatManagerInstance, "BgmInjector");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[BgmInjector] FindBeatManager", "AudioSource 경로 검색 실패");
            }
        }
    }
}
