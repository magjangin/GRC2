using System;
using System.Reflection;
using HarmonyLib;
using GRC2.Core;
using GRC2.Harmony.Handlers;
using MelonLoader;

namespace GRC2.Harmony.Registration
{
    /// <summary>
    /// 커버 이미지 관련 타입 패치
    /// </summary>
    internal static class CoverImagePatcher
    {
        private static HarmonyLib.Harmony _harmonyInstance;

        public static void Initialize(HarmonyLib.Harmony harmonyInstance)
        {
            _harmonyInstance = harmonyInstance;
        }

        /// <summary>
        /// 커버 이미지 관련 타입 찾기 및 후킹
        /// </summary>
        public static void Patch()
        {
            try
            {
                // cMusicSelectArtWork 타입 찾기 및 후킹
                Type artWorkType = ReflectionHelper.FindType("IntiCreates.cMusicSelectArtWork");
                if (artWorkType != null)
                {
                    MelonLogger.Msg($"[CoverImagePatcher] ✅ cMusicSelectArtWork 타입 발견");
                    
                    // requestSetArtworkSprite 메서드 후킹
                    var setArtworkMethod = ReflectionHelper.FindMethod("IntiCreates.cMusicSelectArtWork", "requestSetArtworkSprite", 
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic, silent: true);
                    if (setArtworkMethod != null)
                    {
                        var prefixMethod = typeof(ArtWorkPatch).GetMethod("RequestSetArtworkSpritePrefix", BindingFlags.Static | BindingFlags.Public);
                        var postfixMethod = typeof(ArtWorkPatch).GetMethod("RequestSetArtworkSpritePostfix", BindingFlags.Static | BindingFlags.Public);
                        if (prefixMethod != null && postfixMethod != null)
                        {
                            _harmonyInstance.Patch(setArtworkMethod, new HarmonyMethod(prefixMethod), new HarmonyMethod(postfixMethod));
                            MelonLogger.Msg("[CoverImagePatcher] ✅ cMusicSelectArtWork.requestSetArtworkSprite 패치 성공");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[CoverImagePatcher] 커버 이미지 타입 패치 중 오류: {ex.Message}");
            }
        }
    }
}













