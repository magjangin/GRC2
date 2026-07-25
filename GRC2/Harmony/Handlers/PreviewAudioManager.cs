using GRC2.Core;
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

        private static Type _soundManagerType;
        private static FieldInfo _previewSourceField;
        private static FieldInfo _ambientSourceField;
        private static object _soundManagerInstance;
        private static bool _isMonitoring;

        public static bool IsMonitoring => _isMonitoring;
        public static int MutedCount => MutedSources.Count;

        public static void StopPreviewAndAmbient()
        {
            try
            {
                AudioSource[] sources = GetSoundManagerSources();
                for (int i = 0; i < sources.Length; i++)
                {
                    MuteSource(sources[i]);
                }

                if (!_isMonitoring && MutedSources.Count > 0)
                {
                    _isMonitoring = true;
                    MelonCoroutines.Start(KeepMutedCoroutine());
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

        public static void Reset()
        {
            _isMonitoring = false;
            MutedSources.Clear();
            _soundManagerInstance = null;
        }

        private static IEnumerator KeepMutedCoroutine()
        {
            const float duration = 2f;
            const float interval = 0.1f;
            float elapsed = 0f;

            while (_isMonitoring &&
                   elapsed < duration &&
                   Core.CustomAssetManager.IsCustomChartSelected())
            {
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

        private static AudioSource[] GetSoundManagerSources()
        {
            EnsureSoundManagerMetadata();
            object manager = GetSoundManagerInstance();
            if (manager == null)
            {
                return Array.Empty<AudioSource>();
            }

            AudioSource preview = _previewSourceField?.GetValue(manager) as AudioSource;
            AudioSource ambient = _ambientSourceField?.GetValue(manager) as AudioSource;

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

        private static void EnsureSoundManagerMetadata()
        {
            if (_soundManagerType != null)
            {
                return;
            }

            _soundManagerType = ReflectionHelper.FindType("IntiCreates.cSoundManager");
            if (_soundManagerType == null)
            {
                return;
            }

            const BindingFlags flags =
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            _previewSourceField = _soundManagerType.GetField("mPreviewAudioSorce", flags);
            _ambientSourceField = _soundManagerType.GetField("mAmbientAudioSorce", flags);
        }

        private static object GetSoundManagerInstance()
        {
            UnityEngine.Object cachedUnityObject = _soundManagerInstance as UnityEngine.Object;
            if (cachedUnityObject != null)
            {
                return _soundManagerInstance;
            }

            _soundManagerInstance = null;
            if (_soundManagerType == null)
            {
                return null;
            }

            UnityEngine.Object[] managers = UnityEngine.Object.FindObjectsOfType(_soundManagerType);
            if (managers != null && managers.Length > 0)
            {
                _soundManagerInstance = managers[0];
            }

            return _soundManagerInstance;
        }
    }
}
