using GRC2.Helpers;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace GRC2.Harmony
{
    /// <summary>
    /// 커스텀 곡 프리뷰를 지연 로드하고 최근 클립을 재사용합니다.
    /// 빠르게 스크롤하는 동안에는 마지막으로 멈춘 곡만 실제로 로드합니다.
    /// </summary>
    public static class CustomBgmPlayer
    {
        private const int MaxCachedClips = 3;
        private const float SelectionDebounceSeconds = 0.15f;

        private static readonly Dictionary<string, AudioClip> ClipCache =
            new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        private static readonly Helpers.RecentKeys CacheOrder = new Helpers.RecentKeys(MaxCachedClips, StringComparer.OrdinalIgnoreCase);

        private static AudioSource _customBgmAudioSource;
        private static GameObject _customBgmGameObject;
        private static UnityWebRequest _activeRequest;
        private static string _currentPath;
        private static int _requestVersion;

        public static bool IsPlaying =>
            _customBgmAudioSource != null && _customBgmAudioSource.isPlaying;

        public static IEnumerator InjectCustomBgm(string bgmFilePath)
        {
            string normalizedPath = PathHelper.GetExistingFullPathOrNull(bgmFilePath);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                yield break;
            }

            if (string.Equals(_currentPath, normalizedPath, StringComparison.OrdinalIgnoreCase) &&
                IsPlaying)
            {
                yield break;
            }

            int requestVersion = ++_requestVersion;
            CancelActiveRequest();
            StopPlayback();

            // 휠/키 연속 입력 중 매 곡의 대용량 오디오를 디코딩하지 않습니다.
            yield return new WaitForSecondsRealtime(SelectionDebounceSeconds);
            if (requestVersion != _requestVersion || !Core.CustomAssetManager.IsCustomChartSelected())
            {
                yield break;
            }

            EnsureAudioSource();

            if (TryGetCachedClip(normalizedPath, out AudioClip cachedClip))
            {
                PlayClip(normalizedPath, cachedClip);
                yield break;
            }

            yield return DownloadAndPlayBgmCoroutine(normalizedPath, requestVersion);
        }

        private static IEnumerator DownloadAndPlayBgmCoroutine(string normalizedPath, int requestVersion)
        {
            string fileUri = new Uri(normalizedPath).AbsoluteUri;
            using (UnityWebRequest request =
                UnityWebRequestMultimedia.GetAudioClip(fileUri, AudioType.UNKNOWN))
            {
                _activeRequest = request;
                DownloadHandlerAudioClip handler =
                    request.downloadHandler as DownloadHandlerAudioClip;
                if (handler != null)
                {
                    // 전체 파일을 한 번에 PCM으로 풀지 않아 선택 순간의 프레임 멈춤을 줄입니다.
                    handler.streamAudio = true;
                }

                yield return request.SendWebRequest();
                if (ReferenceEquals(_activeRequest, request))
                    _activeRequest = null;

                if (requestVersion != _requestVersion || !Core.CustomAssetManager.IsCustomChartSelected())
                    yield break;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    MelonLogger.Warning($"[CustomBgmPlayer] 프리뷰 로드 실패: {request.error}");
                    yield break;
                }

                AudioClip loadedClip = TryExtractAudioClip(request);
                if (loadedClip == null)
                    yield break;

                loadedClip.name = Path.GetFileNameWithoutExtension(normalizedPath);
                AddToCache(normalizedPath, loadedClip);
                PlayClip(normalizedPath, loadedClip);
            }
        }

        private static AudioClip TryExtractAudioClip(UnityWebRequest request)
        {
            try
            {
                return DownloadHandlerAudioClip.GetContent(request);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CustomBgmPlayer] 프리뷰 클립 생성 실패: {ex.Message}");
                return null;
            }
        }

        public static void Cleanup()
        {
            ++_requestVersion;
            CancelActiveRequest();
            StopPlayback();
        }

        public static void CleanupAndRestore()
        {
            Cleanup();
            PreviewAudioManager.RestoreMutedAudioSources();
        }

        private static void EnsureAudioSource()
        {
            if (_customBgmAudioSource != null)
            {
                return;
            }

            _customBgmGameObject = new GameObject("CustomPreviewBGM");
            UnityEngine.Object.DontDestroyOnLoad(_customBgmGameObject);

            _customBgmAudioSource = _customBgmGameObject.AddComponent<AudioSource>();
            _customBgmAudioSource.playOnAwake = false;
            _customBgmAudioSource.loop = true;
            _customBgmAudioSource.volume = 1f;
            _customBgmAudioSource.priority = 0;
        }

        private static void PlayClip(string path, AudioClip clip)
        {
            if (_customBgmAudioSource == null || clip == null)
            {
                return;
            }

            _customBgmAudioSource.volume = PreviewAudioManager.LastPreviewVolume;
            _customBgmAudioSource.clip = clip;
            _customBgmAudioSource.time = 0f;
            _customBgmAudioSource.Play();
            _currentPath = path;
        }

        private static void StopPlayback()
        {
            _currentPath = null;
            if (_customBgmAudioSource == null)
            {
                return;
            }

            if (_customBgmAudioSource.isPlaying)
            {
                _customBgmAudioSource.Stop();
            }

            _customBgmAudioSource.clip = null;
        }

        private static void CancelActiveRequest()
        {
            if (_activeRequest == null)
            {
                return;
            }

            try
            {
                _activeRequest.Abort();
            }
            catch
            {
                // 이미 완료/폐기 중인 요청은 다음 코루틴 프레임에서 정리됩니다.
            }

            _activeRequest = null;
        }

        private static bool TryGetCachedClip(string path, out AudioClip clip)
        {
            if (ClipCache.TryGetValue(path, out clip) && clip != null)
            {
                CacheOrder.Touch(path); // 다시 고른 곡은 가장 최근으로(예전에는 먼저 넣은 순서로만 버림)
                return true;
            }

            // 클립이 밖에서 파괴됐으면 항목을 지웁니다. 순서 목록에는 남겨 두어도 다시 넣을 때 Touch가 위치만 옮깁니다.
            ClipCache.Remove(path);
            clip = null;
            return false;
        }

        /// <summary>
        /// 예전 Queue 방식은 파괴된 클립을 다시 넣으면 경로가 큐에 두 번 들어가, 나중에 옛 항목을 꺼낼 때
        /// 방금 넣은 클립을 캐시에서 빼 버렸습니다(2026-11-26, 알려진_문제 P19). RecentKeys는 키당 한 칸만 씁니다.
        /// </summary>
        private static void AddToCache(string path, AudioClip clip)
        {
            if (ClipCache.ContainsKey(path))
            {
                return;
            }

            ClipCache[path] = clip;
            string evictedPath = CacheOrder.Touch(path);
            if (evictedPath == null || !ClipCache.TryGetValue(evictedPath, out AudioClip evictedClip))
            {
                return;
            }

            ClipCache.Remove(evictedPath);
            if (evictedClip != null && evictedClip != clip)
            {
                UnityEngine.Object.Destroy(evictedClip);
            }
        }

    }
}
