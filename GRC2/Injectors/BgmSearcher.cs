using IntiCreates;
using MelonLoader;
using UnityEngine;

namespace GRC2.Injectors
{
    /// <summary>
    /// 플레이 씬에서 cBGMBeatManager를 찾고 원본 오디오 정보를 로그로 남기는 도우미입니다.
    /// </summary>
    internal static class BgmSearcher
    {
        public static bool TryFindBeatManager(out cBGMBeatManager instance)
        {
            instance = UnityEngine.Object.FindObjectOfType<cBGMBeatManager>();
            return instance != null;
        }

        public static void LogOriginalAudioInfo(cBGMBeatManager instance, string logPrefix)
        {
            if (instance == null)
            {
                return;
            }

            var originalClip = instance.getAudioClip();
            if (originalClip != null)
            {
                MelonLogger.Msg($"[{logPrefix}] 원본 AudioClip: {originalClip.name}, 길이: {originalClip.length:F3}초 ({originalClip.samples} 샘플)");
            }
            else
            {
                MelonLogger.Msg($"[{logPrefix}] 원본 AudioClip: null");
            }

            var audioSource = instance.getAudioSorce();
            if (audioSource != null && audioSource.clip != null)
            {
                MelonLogger.Msg($"[{logPrefix}] AudioSource 원본 클립: {audioSource.clip.name}, 길이: {audioSource.clip.length:F3}초 ({audioSource.clip.samples} 샘플)");
            }
        }
    }
}
