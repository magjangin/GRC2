using IntiCreates;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace GRC2.Harmony.Handlers
{
    /// <summary>
    /// 곡 선택 화면의 프리뷰/환경음만 음소거합니다.
    /// 씬 전체 AudioSource 검색은 선택 전환 프레임을 멈추게 하므로 사용하지 않습니다.
    /// </summary>
    public static class PreviewAudioManager
    {
        private static readonly Dictionary<AudioSource, float> MutedSources =
            new Dictionary<AudioSource, float>();

        private const BindingFlags InstanceFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly FieldInfo PreviewSourceField =
            typeof(cMusicSelectSceneUIUpdater).GetField(
                "mPreviewAudioSorce",
                InstanceFlags);
        private static readonly FieldInfo AmbientSourceField =
            typeof(cMusicSelectSceneUIUpdater).GetField(
                "mAmbientAudioSorce",
                InstanceFlags);
        private static bool _isMonitoring;

        public static void StopPreviewAndAmbient(
            cMusicSelectSceneUIUpdater updater)
        {
            try
            {
                AudioSource[] sources = GetUpdaterSources(updater);
                for (int i = 0; i < sources.Length; i++)
                {
                    MuteSource(sources[i]);
                }

                if (!_isMonitoring && updater != null)
                {
                    _isMonitoring = true;
                    MelonCoroutines.Start(KeepMutedCoroutine(updater));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PreviewAudioManager] 프리뷰 음소거 실패: {ex.Message}");
            }
        }

        public static void RestoreMutedAudioSources()
        {
            _isMonitoring = false;

            try
            {
                foreach (KeyValuePair<AudioSource, float> entry in MutedSources)
                {
                    AudioSource source = entry.Key;
                    if (source == null)
                    {
                        continue;
                    }

                    source.mute = false;
                    source.volume = entry.Value;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PreviewAudioManager] 프리뷰 복원 실패: {ex.Message}");
            }
            finally
            {
                MutedSources.Clear();
            }
        }

        public static void DiscardMutedSourceState()
        {
            _isMonitoring = false;
            MutedSources.Clear();
        }

        private static IEnumerator KeepMutedCoroutine(
            cMusicSelectSceneUIUpdater updater)
        {
            const float duration = 2f;
            const float interval = 0.1f;
            float elapsed = 0f;

            while (_isMonitoring &&
                   elapsed < duration &&
                   Core.CustomAssetManager.IsCustomChartSelected())
            {
                AudioSource[] currentSources = GetUpdaterSources(updater);
                for (int i = 0; i < currentSources.Length; i++)
                {
                    MuteSource(currentSources[i]);
                }

                foreach (KeyValuePair<AudioSource, float> entry in MutedSources)
                {
                    MuteSourceWithoutRecording(entry.Key);
                }

                yield return new WaitForSeconds(interval);
                elapsed += interval;
            }

            _isMonitoring = false;
        }

        private static void MuteSource(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            if (!MutedSources.ContainsKey(source))
            {
                MutedSources[source] = source.volume;
            }

            MuteSourceWithoutRecording(source);
        }

        private static void MuteSourceWithoutRecording(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            if (source.isPlaying)
            {
                source.Stop();
            }

            source.volume = 0f;
            source.mute = true;
        }

        private static AudioSource[] GetUpdaterSources(
            cMusicSelectSceneUIUpdater updater)
        {
            if (updater == null)
            {
                return Array.Empty<AudioSource>();
            }

            AudioSource preview =
                PreviewSourceField?.GetValue(updater) as AudioSource;
            AudioSource ambient =
                AmbientSourceField?.GetValue(updater) as AudioSource;

            if (preview == null)
            {
                return ambient == null
                    ? Array.Empty<AudioSource>()
                    : new[] { ambient };
            }

            if (ambient == null || ambient == preview)
            {
                return new[] { preview };
            }

            return new[] { preview, ambient };
        }
    }
}
