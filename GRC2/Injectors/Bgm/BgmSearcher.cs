using IntiCreates;
using MelonLoader;
using UnityEngine;

namespace GRC2.Injectors
{
    internal static class BgmSearcher
    {
        public static bool TryFindBeatManager(out cBGMBeatManager instance)
        {
            instance = UnityEngine.Object.FindObjectOfType<cBGMBeatManager>();
            return instance != null;
        }

        /// <summary>
        /// FindObjectOfType이 놓치는 비활성 컴포넌트를 위해 AudioSource 쪽에서 역으로 찾습니다.
        /// cBGMBeatManager는 [RequireComponent(typeof(AudioSource))]이므로 항상 같은 GameObject에 있습니다.
        /// </summary>
        public static bool TryFindBeatManagerFromAudioSource(out cBGMBeatManager instance)
        {
            instance = null;

            var audioSources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
            if (audioSources == null || audioSources.Length == 0)
            {
                return false;
            }

            foreach (var audioSource in audioSources)
            {
                var beatManager = audioSource.GetComponent<cBGMBeatManager>();
                if (beatManager != null)
                {
                    instance = beatManager;
                    return true;
                }
            }

            return false;
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
