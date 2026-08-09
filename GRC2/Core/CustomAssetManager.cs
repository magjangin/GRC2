using GRC2.Helpers;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace GRC2.Core
{
    /// <summary>
    /// 커스텀 아트워크를 캐시하고 선택 상태를 관리합니다.
    /// </summary>
    public static class CustomAssetManager
    {
        private const int MaxCachedArtwork = 12;
        private const float ArtworkSelectionDebounceSeconds = 0.08f;

        private static readonly Dictionary<string, Sprite> ArtworkCache =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly Queue<string> ArtworkCacheOrder = new Queue<string>();
        private static readonly Dictionary<string, List<Action<Sprite>>> PendingArtworkCallbacks =
            new Dictionary<string, List<Action<Sprite>>>(StringComparer.OrdinalIgnoreCase);

        private static Sprite customArtworkSprite;
        private static bool isCustomChartSelected;
        private static string requestedArtworkPath;

        /// <summary>
        /// 즉시 아트워크가 필요한 씬 전환용 동기 경로입니다.
        /// 곡 선택 화면에서는 RequestCustomArtwork를 사용합니다.
        /// </summary>
        public static void LoadCustomArtwork(string imagePath)
        {
            string normalizedPath = NormalizeExistingPath(imagePath);
            if (normalizedPath == null)
            {
                return;
            }

            requestedArtworkPath = normalizedPath;
            if (TryGetCustomArtwork(normalizedPath, out _))
            {
                return;
            }

            try
            {
                byte[] imageData = File.ReadAllBytes(normalizedPath);
                Texture2D texture = new Texture2D(2, 2);
                if (!texture.LoadImage(imageData))
                {
                    UnityEngine.Object.Destroy(texture);
                    return;
                }

                Sprite sprite = CreateArtworkSprite(normalizedPath, texture);
                CacheArtwork(normalizedPath, sprite);
                SelectArtwork(sprite);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CustomAssetManager] 아트워크 로드 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 파일 읽기와 이미지 디코딩을 UnityWebRequest의 작업 경로로 넘깁니다.
        /// 같은 이미지에 대한 중복 요청은 하나로 합칩니다.
        /// </summary>
        public static void RequestCustomArtwork(
            string imagePath,
            Action<Sprite> onLoaded = null)
        {
            string normalizedPath = NormalizeExistingPath(imagePath);
            if (normalizedPath == null)
            {
                return;
            }

            requestedArtworkPath = normalizedPath;
            if (TryGetCustomArtwork(normalizedPath, out Sprite cachedSprite))
            {
                onLoaded?.Invoke(cachedSprite);
                return;
            }

            if (PendingArtworkCallbacks.TryGetValue(
                normalizedPath,
                out List<Action<Sprite>> callbacks))
            {
                if (onLoaded != null)
                {
                    callbacks.Add(onLoaded);
                }

                return;
            }

            callbacks = new List<Action<Sprite>>();
            if (onLoaded != null)
            {
                callbacks.Add(onLoaded);
            }

            PendingArtworkCallbacks[normalizedPath] = callbacks;
            MelonCoroutines.Start(LoadArtworkCoroutine(normalizedPath));
        }

        public static bool TryGetCustomArtwork(string imagePath, out Sprite sprite)
        {
            sprite = null;
            string normalizedPath = NormalizePath(imagePath);
            if (normalizedPath == null ||
                !ArtworkCache.TryGetValue(normalizedPath, out sprite) ||
                sprite == null)
            {
                if (normalizedPath != null)
                {
                    ArtworkCache.Remove(normalizedPath);
                }

                sprite = null;
                return false;
            }

            SelectArtwork(sprite);
            return true;
        }

        private static IEnumerator LoadArtworkCoroutine(string imagePath)
        {
            Sprite loadedSprite = null;

            // 연속 스크롤 중 지나간 앨범 이미지는 디스크에서 읽지 않습니다.
            yield return new WaitForSecondsRealtime(ArtworkSelectionDebounceSeconds);
            if (!string.Equals(
                requestedArtworkPath,
                imagePath,
                StringComparison.OrdinalIgnoreCase))
            {
                CompleteArtworkRequest(imagePath, null);
                yield break;
            }

            using (UnityWebRequest request =
                UnityWebRequestTexture.GetTexture(new Uri(imagePath).AbsoluteUri, true))
            {
                yield return request.SendWebRequest();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    if (!ArtworkCache.TryGetValue(imagePath, out loadedSprite) ||
                        loadedSprite == null)
                    {
                        Texture2D texture = DownloadHandlerTexture.GetContent(request);
                        if (texture != null)
                        {
                            loadedSprite = CreateArtworkSprite(imagePath, texture);
                            CacheArtwork(imagePath, loadedSprite);
                        }
                    }
                }
                else
                {
                    MelonLogger.Warning(
                        $"[CustomAssetManager] 비동기 아트워크 로드 실패: {request.error}");
                }
            }

            if (loadedSprite != null &&
                string.Equals(
                    requestedArtworkPath,
                    imagePath,
                    StringComparison.OrdinalIgnoreCase))
            {
                SelectArtwork(loadedSprite);
            }

            CompleteArtworkRequest(imagePath, loadedSprite);
        }

        private static Sprite CreateArtworkSprite(string imagePath, Texture2D texture)
        {
            string assetName = Path.GetFileNameWithoutExtension(imagePath);
            texture.name = assetName;

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                51.2f);
            sprite.name = assetName;
            return sprite;
        }

        private static void CacheArtwork(string path, Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            if (!ArtworkCache.ContainsKey(path))
            {
                ArtworkCacheOrder.Enqueue(path);
            }

            ArtworkCache[path] = sprite;
            TrimArtworkCache();
        }

        private static void TrimArtworkCache()
        {
            int attempts = ArtworkCacheOrder.Count;
            while (ArtworkCache.Count > MaxCachedArtwork &&
                   ArtworkCacheOrder.Count > 0 &&
                   attempts-- > 0)
            {
                string oldestPath = ArtworkCacheOrder.Dequeue();
                if (string.Equals(
                    oldestPath,
                    requestedArtworkPath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    ArtworkCacheOrder.Enqueue(oldestPath);
                    continue;
                }

                if (!ArtworkCache.TryGetValue(oldestPath, out Sprite oldestSprite))
                {
                    continue;
                }

                ArtworkCache.Remove(oldestPath);
                if (oldestSprite != null)
                {
                    Texture2D texture = oldestSprite.texture;
                    UnityEngine.Object.Destroy(oldestSprite);
                    if (texture != null)
                    {
                        UnityEngine.Object.Destroy(texture);
                    }
                }
            }
        }

        private static void SelectArtwork(Sprite sprite)
        {
            customArtworkSprite = sprite;
        }

        private static void CompleteArtworkRequest(string path, Sprite sprite)
        {
            if (!PendingArtworkCallbacks.TryGetValue(
                path,
                out List<Action<Sprite>> callbacks))
            {
                return;
            }

            PendingArtworkCallbacks.Remove(path);
            if (sprite == null)
            {
                return;
            }

            for (int i = 0; i < callbacks.Count; i++)
            {
                try
                {
                    callbacks[i]?.Invoke(sprite);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning(
                        $"[CustomAssetManager] 아트워크 완료 콜백 실패: {ex.Message}");
                }
            }
        }

        public static Sprite GetCustomArtwork() => customArtworkSprite;
        public static void SetCustomChartSelected(bool selected) =>
            isCustomChartSelected = selected;
        public static bool IsCustomChartSelected() => isCustomChartSelected;

        public static bool IsSceneWhereInjectionDisallowed()
        {
            try
            {
                UnityEngine.SceneManagement.Scene scene =
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (!scene.IsValid())
                {
                    return false;
                }

                string name = scene.name ?? string.Empty;
                return name == "SoundPlayerScene" ||
                       name == "MoviePlayer_MovieSelect";
            }
            catch
            {
                return false;
            }
        }

        public static bool ShouldInjectCustomContent()
        {
            return !IsSceneWhereInjectionDisallowed() && IsCustomChartSelected();
        }

        private static string NormalizeExistingPath(string path)
        {
            string normalizedPath = NormalizePath(path);
            return normalizedPath != null && File.Exists(normalizedPath)
                ? normalizedPath
                : null;
        }

        private static string NormalizePath(string path)
        {
            try
            {
                return string.IsNullOrWhiteSpace(path)
                    ? null
                    : Path.GetFullPath(path);
            }
            catch
            {
                return null;
            }
        }
    }
}
