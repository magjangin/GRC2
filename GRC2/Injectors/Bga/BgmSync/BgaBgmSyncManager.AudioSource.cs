using System;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace GRC2.Injectors
{
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
}
