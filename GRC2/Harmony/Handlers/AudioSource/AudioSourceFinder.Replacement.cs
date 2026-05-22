using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GRC2.Helpers;
using UnityEngine;
using UnityEngine.Networking;

namespace GRC2.Harmony.Handlers
{
    public static partial class AudioSourceFinder
    {
        /// <summary>
        /// AudioSource 목록에 BGM을 로드하고 교체합니다.
        /// </summary>
        private static IEnumerator LoadAndReplaceBGMForAudioSources(string musicOggPath, List<AudioSource> audioSources)
        {
            MelonLogger.Msg($"[AudioSourceFinder] 🔄 sSoundManager2D용 music.ogg 로드 시작: {System.IO.Path.GetFileName(musicOggPath)}");

            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(
                "file://" + musicOggPath, AudioType.UNKNOWN))
            {
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    MelonLogger.Msg($"[AudioSourceFinder] ❌ sSoundManager2D용 music.ogg 로드 실패: {www.error}");
                    yield break;
                }

                AudioClip musicClip = CreateLoadedAudioClip(www, musicOggPath);
                if (musicClip != null)
                {
                    ReplaceAudioSources(audioSources, musicClip);
                }
            }
        }

        private static AudioClip CreateLoadedAudioClip(UnityWebRequest www, string musicOggPath)
        {
            try
            {
                AudioClip musicClip = DownloadHandlerAudioClip.GetContent(www);
                if (musicClip == null)
                    return null;

                var fileName = System.IO.Path.GetFileNameWithoutExtension(musicOggPath);
                SetAudioClipName(musicClip, fileName);

                var clipNameForLog = string.IsNullOrEmpty(musicClip.name) ? fileName : musicClip.name;
                MelonLogger.Msg($"[AudioSourceFinder] ✅ sSoundManager2D용 music.ogg 로드 완료 (길이: {musicClip.length:F2}초, 이름: '{clipNameForLog}')");
                return musicClip;
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[AudioSourceFinder] ❌ sSoundManager2D용 music.ogg 처리 오류: {ex.Message}");
                return null;
            }
        }

        private static void ReplaceAudioSources(List<AudioSource> audioSources, AudioClip musicClip)
        {
            int successCount = 0;
            foreach (var audioSource in audioSources)
            {
                if (audioSource == null)
                    continue;

                if (TryReplaceAudioSource(audioSource, musicClip))
                    successCount++;
            }

            MelonLogger.Msg($"[AudioSourceFinder] ✅ 총 {successCount}개의 AudioSource에 BGM 교체 완료");
        }

        private static bool TryReplaceAudioSource(AudioSource audioSource, AudioClip musicClip)
        {
            try
            {
                if (audioSource.isPlaying)
                    audioSource.Stop();

                audioSource.clip = musicClip;
                audioSource.time = 0f;
                if (audioSource.volume <= 0f)
                    audioSource.volume = 1f;

                audioSource.Play();
                MelonLogger.Msg($"[AudioSourceFinder] ✅ AudioSource 교체 완료");
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[AudioSourceFinder] ⚠️ AudioSource 교체 실패: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// AudioClip의 이름을 설정합니다.
        /// </summary>
        private static void SetAudioClipName(AudioClip clip, string name)
        {
            if (!string.IsNullOrEmpty(clip.name))
            {
                return;
            }

            try
            {
                var nameField = typeof(AudioClip).GetField("m_Name",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (nameField != null)
                {
                    nameField.SetValue(clip, name);
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[AudioSourceFinder] TrySetClipName", "AudioClip.m_Name 설정 실패(무시)");
            }
        }
    }
}
