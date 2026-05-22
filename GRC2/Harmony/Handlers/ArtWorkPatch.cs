using MelonLoader;
using UnityEngine;
using System;
using GRC2.Core;

namespace GRC2.Harmony.Handlers
{
    /// <summary>
    /// cMusicSelectArtWork.requestSetArtworkSprite 메서드 후킹 - 커버 이미지 교체
    /// </summary>
    public static class ArtWorkPatch
    {
        public static void RequestSetArtworkSpritePrefix(object __instance, ref Sprite useSprite, bool isInstant)
        {
            try
            {
                // 현재 선택된 곡이 커스텀 차트인지 확인
                if (!CustomAssetManager.IsCustomChartSelected())
                {
                    return;
                }

                // 현재 선택된 앨범의 이미지 사용 (플레이 씬에서도 최신 이미지 사용)
                var imageFile = Core.AlbumManager.GetCurrentImageFile();
                if (!string.IsNullOrEmpty(imageFile))
                {
                    // LoadCustomArtwork는 내부에서 경로 캐싱을 통해 중복 로드를 방지함
                    CustomAssetManager.LoadCustomArtwork(imageFile);
                }
                
                Sprite customSprite = CustomAssetManager.GetCustomArtwork();
                if (customSprite != null)
                {
                    useSprite = customSprite;
                    MelonLogger.Msg($"[ArtWorkPatch] ✅ 커스텀 아트워크로 교체: '{customSprite.name}'");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[ArtWorkPatch] ❌ 커버 이미지 교체 중 오류: {ex.Message}");
            }
        }

        public static void RequestSetArtworkSpritePostfix(object __instance, Sprite useSprite, bool isInstant)
        {
            // Postfix는 로그 없이 조용히 실행
        }
    }
}




