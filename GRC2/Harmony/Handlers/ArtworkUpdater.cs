using GRC2.Core;
using HarmonyLib;
using IntiCreates;
using MelonLoader;
using System;
using System.IO;
using UnityEngine;

namespace GRC2.Harmony.Handlers
{
    /// <summary>
    /// 비동기로 준비된 커스텀 아트워크를 현재 곡 선택 UI에 반영합니다.
    /// </summary>
    public static class ArtworkUpdater
    {
        private static readonly AccessTools.FieldRef<cMusicSelectSceneUIUpdater, cMusicSelectArtWorkManager> ArtworkManagerRef =
            AccessTools.FieldRefAccess<cMusicSelectSceneUIUpdater, cMusicSelectArtWorkManager>("mArtWorkAndMusicDetail");

        private static readonly AccessTools.FieldRef<cMusicSelectArtWorkManager, cMusicSelectArtWork> ArtWorkRef =
            AccessTools.FieldRefAccess<cMusicSelectArtWorkManager, cMusicSelectArtWork>("mArtWork");

        public static void UpdateArtwork(
            cMusicSelectSceneUIUpdater instance)
        {
            if (instance == null ||
                !CustomAssetManager.IsCustomChartSelected())
            {
                return;
            }

            string imagePath = AlbumManager.GetCurrentImageFile();
            if (string.IsNullOrEmpty(imagePath))
            {
                return;
            }

            if (CustomAssetManager.TryGetCustomArtwork(
                imagePath,
                out Sprite cachedSprite))
            {
                ApplyArtwork(instance, cachedSprite);
                return;
            }

            string requestedPath = Path.GetFullPath(imagePath);
            CustomAssetManager.RequestCustomArtwork(
                requestedPath,
                sprite =>
                {
                    string currentPath = AlbumManager.GetCurrentImageFile();
                    if (!CustomAssetManager.IsCustomChartSelected() ||
                        string.IsNullOrEmpty(currentPath) ||
                        !string.Equals(
                            Path.GetFullPath(currentPath),
                            requestedPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    ApplyArtwork(instance, sprite);
                });
        }

        private static void ApplyArtwork(
            cMusicSelectSceneUIUpdater sceneUpdater,
            Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            try
            {
                cMusicSelectArtWork artwork = FindArtworkInstance(sceneUpdater);
                if (artwork == null)
                {
                    return;
                }

                artwork.requestSetArtworkSprite(sprite, true);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(
                    $"[ArtworkUpdater] 아트워크 UI 반영 실패: {ex.Message}");
            }
        }

        private static cMusicSelectArtWork FindArtworkInstance(
            cMusicSelectSceneUIUpdater sceneUpdater)
        {
            cMusicSelectArtWorkManager artworkManager = ArtworkManagerRef(sceneUpdater);
            if (artworkManager != null)
            {
                cMusicSelectArtWork artwork = ArtWorkRef(artworkManager);
                if (artwork != null)
                {
                    return artwork;
                }
            }

            // 아직 UI 계층이 연결되지 않은 경우에만 씬에서 직접 찾습니다.
            return UnityEngine.Object.FindObjectOfType<cMusicSelectArtWork>();
        }
    }
}
