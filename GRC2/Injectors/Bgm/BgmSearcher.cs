using System;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace GRC2.Injectors
{
    internal static class BgmSearcher
    {
        public static bool TryFindBeatManager(Type bgmBeatManagerType, out object instance)
        {
            instance = null;
            if (bgmBeatManagerType == null)
            {
                return false;
            }

            var singleManager = UnityEngine.Object.FindObjectOfType(bgmBeatManagerType);
            if (singleManager != null)
            {
                instance = singleManager;
                return true;
            }

            var bgmManagers = UnityEngine.Object.FindObjectsOfType(bgmBeatManagerType);
            if (bgmManagers != null && bgmManagers.Length > 0)
            {
                instance = bgmManagers[0];
                return true;
            }

            return false;
        }

        public static bool TryFindBeatManagerFromAudioSource(Type bgmBeatManagerType, out object instance)
        {
            instance = null;
            if (bgmBeatManagerType == null)
            {
                return false;
            }

            var audioSources = UnityEngine.Object.FindObjectsOfType<AudioSource>();
            if (audioSources == null || audioSources.Length == 0)
            {
                return false;
            }

            foreach (var audioSource in audioSources)
            {
                var components = audioSource.GetComponents<Component>();
                foreach (var component in components)
                {
                    if (component != null && component.GetType() == bgmBeatManagerType)
                    {
                        instance = component;
                        return true;
                    }
                }
            }

            return false;
        }

        public static void LogOriginalAudioInfo(Type bgmBeatManagerType, object instance, string logPrefix)
        {
            if (bgmBeatManagerType == null || instance == null)
            {
                return;
            }

            var getAudioClipMethod = bgmBeatManagerType.GetMethod("getAudioClip", 
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (getAudioClipMethod != null)
            {
                try
                {
                    var originalClip = getAudioClipMethod.Invoke(instance, null) as AudioClip;
                    if (originalClip != null)
                    {
                        MelonLogger.Msg($"[{logPrefix}] 원본 AudioClip: {originalClip.name}, 길이: {originalClip.length:F3}초 ({originalClip.samples} 샘플)");
                    }
                    else
                    {
                        MelonLogger.Msg($"[{logPrefix}] 원본 AudioClip: null");
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[{logPrefix}] 원본 AudioClip 확인 실패: {ex.Message}");
                }
            }

            var fields = bgmBeatManagerType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(AudioSource))
                {
                    var audioSource = field.GetValue(instance) as AudioSource;
                    if (audioSource != null && audioSource.clip != null)
                    {
                        MelonLogger.Msg($"[{logPrefix}] AudioSource ({field.Name}) 원본 클립: {audioSource.clip.name}, 길이: {audioSource.clip.length:F3}초 ({audioSource.clip.samples} 샘플)");
                    }
                }
            }
        }
    }
}

