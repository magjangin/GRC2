using System;
using System.Reflection;
using GRC2.Core;
using GRC2.Harmony.Handlers;
using GRC2.Injectors;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace GRC2.Harmony.Hooks
{
    /// <summary>
    /// 곡 선택 화면에서 실제로 상태를 보정해야 하는 흐름만 처리합니다.
    /// </summary>
    public static class GameFlowHooks
    {
        private const BindingFlags InstanceFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static void BackToPreScreenPrefix()
        {
            try
            {
                // 곧 씬이 폐기되므로 원본 AudioSource를 전역 복원하지 않고 캐시만 비웁니다.
                CustomBgmPlayer.Cleanup();
                PreviewAudioManager.Reset();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] backToPreScreen 정리 오류: {ex.Message}");
            }
        }

        public static void StartRythmGamePrefix()
        {
            try
            {
                CustomBgmPlayer.Cleanup();
                PreviewAudioManager.Reset();

                // 직전 커스텀 곡의 종료 시간이 다음 플레이에 적용되지 않도록 초기화합니다.
                BgmFinishTimeManager.Reset();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] startRythmGame 정리 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 원본 팝업은 MusicDataMap에 존재하는 MusicID가 필요합니다.
        /// 커스텀 선택일 때만 같은 캐릭터의 실제 첫 곡 ID로 임시 치환합니다.
        /// </summary>
        public static void CoOpenPreMusicStartWindowPrefix(object __instance)
        {
            try
            {
                if (__instance == null || !CustomAssetManager.ShouldInjectCustomContent())
                    return;

                AlbumInfo album = AlbumManager.GetCurrentAlbum();
                string artistId = album?.SongInfo?.Character;
                if (string.IsNullOrWhiteSpace(artistId))
                    artistId = album?.SongInfo?.Artist;
                if (album == null)
                    return;

                FieldInfo currentMusicIdField = __instance.GetType().GetField(
                    "mCurentMusicId",
                    InstanceFlags);
                if (currentMusicIdField == null)
                    return;

                object previousMusicId = currentMusicIdField.GetValue(__instance);
                var firstSong = string.IsNullOrWhiteSpace(artistId)
                    ? null
                    : AlbumManager.GetArtistFirstSong(artistId);
                object firstMusicId = firstSong?.musicId;
                string firstTitle = firstSong?.title;

                if (firstMusicId == null)
                {
                    MusicScrollViewHooks.TryGetTemplateSong(
                        previousMusicId,
                        out firstMusicId,
                        out firstTitle);
                }

                if (firstMusicId == null ||
                    !currentMusicIdField.FieldType.IsInstanceOfType(firstMusicId))
                {
                    MelonLogger.Warning(
                        $"[GameFlowHooks] '{artistId ?? "unknown"}'에 대응하는 원본 기준 곡을 찾지 못했습니다.");
                    return;
                }

                if (!Equals(previousMusicId, firstMusicId))
                    currentMusicIdField.SetValue(__instance, firstMusicId);

                if (!string.IsNullOrWhiteSpace(artistId))
                    AlbumManager.SetCurrentArtistId(artistId);
                AlbumManager.RegisterOriginalTitle(firstMusicId, firstTitle);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(
                    $"[GameFlowHooks] coOpenPreMusicStartWindow MusicID 보정 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 팝업의 원본 artwork 로딩이 끝난 직후 실제 Image 필드에 커스텀 이미지를 적용합니다.
        /// </summary>
        public static void PreMusicStartWindowOpenedPostfix(object __instance)
        {
            try
            {
                if (__instance == null || !CustomAssetManager.ShouldInjectCustomContent())
                    return;

                string imageFile = AlbumManager.GetCurrentImageFile();
                if (string.IsNullOrWhiteSpace(imageFile))
                    return;

                FieldInfo artworkField =
                    __instance.GetType().GetField("mArtworkImage", InstanceFlags);
                if (!(artworkField?.GetValue(__instance) is Image artworkImage))
                    return;

                if (CustomAssetManager.TryGetCustomArtwork(imageFile, out Sprite sprite))
                {
                    artworkImage.sprite = sprite;
                    return;
                }

                string requestedImage = System.IO.Path.GetFullPath(imageFile);
                CustomAssetManager.RequestCustomArtwork(
                    requestedImage,
                    loadedSprite =>
                    {
                        string currentImage = AlbumManager.GetCurrentImageFile();
                        if (artworkImage == null ||
                            !CustomAssetManager.ShouldInjectCustomContent() ||
                            string.IsNullOrWhiteSpace(currentImage) ||
                            !string.Equals(
                                System.IO.Path.GetFullPath(currentImage),
                                requestedImage,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        artworkImage.sprite = loadedSprite;
                    });
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] 시작 전 아트워크 적용 오류: {ex.Message}");
            }
        }
    }
}
