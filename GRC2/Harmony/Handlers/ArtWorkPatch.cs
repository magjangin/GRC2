using UnityEngine;
using System;
using GRC2.Core;
using HarmonyLib;
using IntiCreates;

namespace GRC2.Harmony.Handlers
{
    /// <summary>
    /// cMusicSelectArtWork.requestSetArtworkSprite 메서드 후킹 - 커버 이미지 교체
    /// </summary>
    [HarmonyPatch(typeof(cMusicSelectArtWork), "requestSetArtworkSprite")]
    public static class ArtWorkPatch
    {
        [HarmonyPrefix]
        public static void RequestSetArtworkSpritePrefix(object __instance, ref Sprite useSprite, bool isInstant)
        {
            try
            {
                // 현재 선택된 곡이 커스텀 차트인지 확인
                if (!CustomAssetManager.IsCustomChartSelected())
                {
                    return;
                }

                string imageFile = AlbumManager.GetCurrentImageFile();
                if (string.IsNullOrEmpty(imageFile))
                {
                    return;
                }

                // Prefix 안에서 File.ReadAllBytes/Texture2D.LoadImage를 실행하면
                // 곡 전환 프레임이 멈춥니다. 캐시가 없으면 비동기 준비만 시작하고,
                // 완료 후 ArtworkUpdater가 현재 UI에 반영합니다.
                if (CustomAssetManager.TryGetCustomArtwork(
                    imageFile,
                    out Sprite customSprite))
                {
                    useSprite = customSprite;
                }
                else
                {
                    CustomAssetManager.RequestCustomArtwork(imageFile);
                }
            }
            catch (Exception ex)
            {
                MelonLoader.MelonLogger.Warning(
                    $"[ArtWorkPatch] 커버 이미지 교체 실패: {ex.Message}");
            }
        }
    }
}




