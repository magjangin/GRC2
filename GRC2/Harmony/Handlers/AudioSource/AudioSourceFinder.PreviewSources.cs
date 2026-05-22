using MelonLoader;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GRC2.Harmony.Handlers
{
    public static partial class AudioSourceFinder
    {
        /// <summary>
        /// 프리뷰 AudioSource 목록을 찾습니다.
        /// </summary>
        private static List<AudioSource> FindPreviewAudioSources(Type soundManagerType, object soundManagerInstance)
        {
            List<AudioSource> previewAudioSources = new List<AudioSource>();

            FieldInfo[] allFields = soundManagerType.GetFields(MemberLookupFlags);
            PropertyInfo[] allProperties = soundManagerType.GetProperties(MemberLookupFlags);

            MelonLogger.Msg($"[AudioSourceFinder] 🔍 sSoundManager2D 필드 개수: {allFields.Length}, 프로퍼티 개수: {allProperties.Length}");

            AddFieldAudioSources(previewAudioSources, allFields, soundManagerInstance);
            AddPropertyAudioSources(previewAudioSources, allProperties, soundManagerInstance);
            return previewAudioSources;
        }

        private static void AddFieldAudioSources(List<AudioSource> previewAudioSources, FieldInfo[] fields, object soundManagerInstance)
        {
            foreach (var field in fields)
            {
                try
                {
                    if (!IsAudioSourceType(field.FieldType))
                        continue;

                    object value = field.GetValue(soundManagerInstance);
                    if (value is AudioSource audioSource && audioSource != null)
                    {
                        AddPreviewCandidate(previewAudioSources, audioSource, field.Name);
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"[AudioSourceFinder]   ⚠️ sSoundManager2D.{field.Name} 읽기 실패: {ex.Message}");
                }
            }
        }

        private static void AddPropertyAudioSources(List<AudioSource> previewAudioSources, PropertyInfo[] properties, object soundManagerInstance)
        {
            foreach (var prop in properties)
            {
                try
                {
                    if (!IsAudioSourceType(prop.PropertyType) || !prop.CanRead)
                        continue;

                    object value = prop.GetValue(soundManagerInstance);
                    if (value is AudioSource audioSource && audioSource != null)
                    {
                        AddPreviewCandidate(previewAudioSources, audioSource, prop.Name);
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Msg($"[AudioSourceFinder]   ⚠️ sSoundManager2D.{prop.Name} 읽기 실패: {ex.Message}");
                }
            }
        }

        private static bool IsAudioSourceType(Type type)
        {
            return type == typeof(AudioSource) || type.IsSubclassOf(typeof(AudioSource));
        }

        private static void AddPreviewCandidate(List<AudioSource> previewAudioSources, AudioSource audioSource, string memberName)
        {
            string clipName = GetClipNameForLog(audioSource);
            MelonLogger.Msg($"[AudioSourceFinder]   🔊 sSoundManager2D.{memberName}: isPlaying={audioSource.isPlaying}, clip='{clipName}', volume={audioSource.volume}");

            if (IsPreviewCandidate(audioSource, clipName, memberName) && !previewAudioSources.Contains(audioSource))
                previewAudioSources.Add(audioSource);
        }

        private static string GetClipNameForLog(AudioSource audioSource)
        {
            if (audioSource.clip == null)
                return "null";

            return string.IsNullOrEmpty(audioSource.clip.name) ? "이름 없음" : audioSource.clip.name;
        }

        /// <summary>
        /// 프리뷰 AudioSource 후보인지 확인합니다.
        /// </summary>
        private static bool IsPreviewCandidate(AudioSource audioSource, string clipName, string memberName)
        {
            if (audioSource.isPlaying)
            {
                MelonLogger.Msg($"[AudioSourceFinder]     ✅ 재생 중 - 프리뷰 AudioSource 후보: {memberName}");
                return true;
            }

            if (audioSource.clip != null && (clipName.Contains("PCD_PREVIEW") || clipName.Contains("PREVIEW")))
            {
                MelonLogger.Msg($"[AudioSourceFinder]     ✅ 프리뷰 클립 포함 - 프리뷰 AudioSource 후보: {memberName}");
                return true;
            }

            return false;
        }
    }
}
