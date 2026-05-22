using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using GRC2.Helpers;

namespace GRC2.Injectors
{
    /// <summary>
    /// BGM 오디오 상태 확인을 담당하는 클래스
    /// </summary>
    internal static class BgmAudioStateChecker
    {
        private static readonly Dictionary<Type, FieldInfo[]> _audioSourceFieldsCache = new Dictionary<Type, FieldInfo[]>();

        /// <summary>
        /// 오디오 클립 상태 확인
        /// </summary>
        public static void CheckAudioClip(object instance, Type instanceType, string methodName)
        {
            var clip = GetAudioClip(instance, instanceType);
            if (clip != null)
            {
                var clipName = string.IsNullOrEmpty(clip.name) ? "(이름 없음)" : clip.name;
                MelonLogger.Msg($"[BgmInjectorHooks]   → {methodName} 후 getAudioClip(): {clipName}, {clip.length:F3}초");
            }
            else
            {
                MelonLogger.Warning($"[BgmInjectorHooks]   → {methodName} 후 getAudioClip(): null");
            }
        }
        
        /// <summary>
        /// 재생 준비 상태 확인
        /// </summary>
        public static void CheckIsReadyPlay(object instance, Type instanceType, string methodName)
        {
            var isReadyPlayMethod = instanceType.GetMethod("isReadyPlay", 
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (isReadyPlayMethod != null)
            {
                var isReady = isReadyPlayMethod.Invoke(instance, null);
                MelonLogger.Msg($"[BgmInjectorHooks]   → {methodName} 후 isReadyPlay(): {isReady}");
            }
        }
        
        /// <summary>
        /// AudioSource 상태 확인
        /// </summary>
        public static void CheckAudioSource(object instance, Type instanceType)
        {
            if (!_audioSourceFieldsCache.TryGetValue(instanceType, out var fields))
            {
                var allFields = instanceType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                var audioSourceFields = new List<FieldInfo>(allFields.Length);
                foreach (var field in allFields)
                {
                    if (field.FieldType == typeof(AudioSource))
                    {
                        audioSourceFields.Add(field);
                    }
                }

                fields = audioSourceFields.ToArray();
                _audioSourceFieldsCache[instanceType] = fields;
            }

            foreach (var field in fields)
            {
                try
                {
                    var audioSource = field.GetValue(instance) as AudioSource;
                    if (audioSource != null)
                    {
                        var sourceClipName = audioSource.clip != null
                            ? (string.IsNullOrEmpty(audioSource.clip.name) ? "(이름 없음)" : audioSource.clip.name)
                            : "null";
                        MelonLogger.Msg($"[BgmInjectorHooks]   → AudioSource({field.Name}): isPlaying={audioSource.isPlaying}, clip={sourceClipName}");
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogWarning(ex, "[BgmAudioStateChecker] LogAudioSourceFields", "AudioSource 필드 읽기 실패");
                }
            }
        }
        
        /// <summary>
        /// 오디오 클립 가져오기
        /// </summary>
        public static AudioClip GetAudioClip(object instance, Type instanceType)
        {
            var getAudioClipMethod = instanceType.GetMethod("getAudioClip", 
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (getAudioClipMethod != null)
            {
                return getAudioClipMethod.Invoke(instance, null) as AudioClip;
            }
            return null;
        }
    }
}





