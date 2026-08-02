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

    /// <summary>
    /// 원본 noticeChangedMusic 완료 후 커스텀 선택 상태와 프리뷰를 동기화합니다.
    /// </summary>
    [HarmonyPatch(typeof(cMusicSelectSceneUIUpdater), "noticeChangedMusic")]
    public static class AudioClipPatch
    {
        private static object _lastHandledMusicId;

        [HarmonyPostfix]
        public static void NoticeChangedMusicPostfix(
            cMusicSelectSceneUIUpdater __instance,
            object nextMusicID)
        {
            try
            {
                if (nextMusicID == null)
                {
                    return;
                }

                if (!AlbumManager.IsCustomChartMusicID(nextMusicID))
                {
                    HandleNormalSongSelection();
                    return;
                }

                // 원본 UI가 같은 MusicID를 여러 번 알리는 경우 대용량 에셋 요청을
                // 반복해서 시작하지 않습니다.
                if (CustomAssetManager.IsCustomChartSelected() &&
                    Equals(_lastHandledMusicId, nextMusicID))
                {
                    return;
                }

                if (!AlbumManager.SelectAlbumByMusicID(nextMusicID))
                {
                    return;
                }

                _lastHandledMusicId = nextMusicID;
                CustomAssetManager.SetCustomChartSelected(true);
                TextPatch.EnableTextReplacement(true);

                PreviewAudioManager.StopPreviewAndAmbient(__instance);
                ArtworkUpdater.UpdateArtwork(__instance);

                string bgmFile = AlbumManager.GetCurrentBgmFile();
                if (!string.IsNullOrEmpty(bgmFile) && File.Exists(bgmFile))
                {
                    MelonCoroutines.Start(
                        CustomBgmPlayer.InjectCustomBgm(bgmFile));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(
                    $"[AudioClipPatch] 곡 선택 동기화 실패: {ex.Message}");
            }
        }

        private static void HandleNormalSongSelection()
        {
            _lastHandledMusicId = null;
            TextPatch.EnableTextReplacement(false);

            if (!CustomAssetManager.IsCustomChartSelected())
            {
                return;
            }

            CustomAssetManager.SetCustomChartSelected(false);
            CustomBgmPlayer.CleanupAndRestore();
        }
    }
}
