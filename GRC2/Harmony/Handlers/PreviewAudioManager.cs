using HarmonyLib;
using IntiCreates;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GRC2.Harmony.Handlers
{
    /// <summary>
    /// 곡 선택 화면의 프리뷰/환경음만 음소거합니다.
    /// 씬 전체 AudioSource 검색은 선택 전환 프레임을 멈추게 하므로 사용하지 않습니다.
    /// </summary>
    public static class PreviewAudioManager
    {
        private readonly struct MutedAudioState
        {
            public readonly float Volume;
            public readonly AudioClip Clip;

            public MutedAudioState(float volume, AudioClip clip)
            {
                Volume = volume;
                Clip = clip;
            }
        }

        private static readonly Dictionary<AudioSource, MutedAudioState> MutedSources =
            new Dictionary<AudioSource, MutedAudioState>();

        private static readonly AccessTools.FieldRef<cMusicSelectSceneUIUpdater, AudioSource> PreviewSourceRef =
            AccessTools.FieldRefAccess<cMusicSelectSceneUIUpdater, AudioSource>("mPreviewAudioSorce");
        private static readonly AccessTools.FieldRef<cMusicSelectSceneUIUpdater, AudioSource> AmbientSourceRef =
            AccessTools.FieldRefAccess<cMusicSelectSceneUIUpdater, AudioSource>("mAmbientAudioSorce");
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
                foreach (KeyValuePair<AudioSource, MutedAudioState> entry in MutedSources)
                {
                    AudioSource source = entry.Key;
                    if (source == null)
                    {
                        continue;
                    }

                    // 뮤트 시 반납했던 clip을 되돌려야 재생이 재개될 수 있습니다.
                    source.clip = entry.Value.Clip;
                    source.mute = false;
                    source.volume = entry.Value.Volume;
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

                foreach (KeyValuePair<AudioSource, MutedAudioState> entry in MutedSources)
                {
                    AudioSource source = entry.Key;
                    if (source == null)
                    {
                        continue;
                    }

                    // clip을 비운 뒤로 이 슬롯을 sSoundManager2D 풀의 다른 사운드가
                    // 가져갔을 수 있습니다(clip이 우리가 비운 null이 아닌 값으로 바뀜).
                    // 그런 경우 계속 건드리면 그 무관한 사운드를 우리가 끊어버리게 됩니다.
                    if (source.clip != null && source.clip != entry.Value.Clip)
                    {
                        continue;
                    }

                    MuteSourceWithoutRecording(source);
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
                MutedSources[source] = new MutedAudioState(source.volume, source.clip);
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

            // sSoundManager2D는 BGM/환경음/판정 SE가 같은 AudioSource 풀을 공유하고,
            // getPlayableData()는 clip == null인 슬롯만 재사용 가능한 것으로 봅니다. clip을
            // 비우지 않으면 이 슬롯이 계속 "사용 중"으로 남아 풀에서 빠집니다.
            //
            // mute는 절대 true로 설정하지 않습니다. 원본 어셈블리 어디에도 이 풀의
            // AudioSource.mute를 다시 false로 되돌리는 코드가 없어서, 여기서 true를
            // 걸어두면 이 슬롯을 나중에 넘겨받는 무관한 사운드(판정 SE 포함)가
            // volume/clip은 정상인데 영구 무음이 됩니다. Stop() + volume=0 + clip=null
            // 만으로 우리 쪽 무음 처리는 충분합니다.
            source.clip = null;
        }

        private static AudioSource[] GetUpdaterSources(
            cMusicSelectSceneUIUpdater updater)
        {
            if (updater == null)
            {
                return Array.Empty<AudioSource>();
            }

            AudioSource preview = PreviewSourceRef(updater);
            AudioSource ambient = AmbientSourceRef(updater);

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
