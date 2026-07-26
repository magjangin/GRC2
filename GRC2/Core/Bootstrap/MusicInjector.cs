using MelonLoader;
using System;
using HarmonyLib;

namespace GRC2.Core
{
    /// <summary>
    /// Harmony 패치 초기화 및 관리
    /// </summary>
    public static class MusicInjector
    {
        private static HarmonyLib.Harmony harmonyInstance = null;

        public static void Initialize()
        {
            if (harmonyInstance != null) return;
            
            MelonLogger.Msg("[MusicInjector] 초기화 중...");
            
            try
            {
                harmonyInstance = new HarmonyLib.Harmony("GRC2.MusicInjector");
                harmonyInstance.PatchAll(typeof(MusicInjector).Assembly);
                MelonLogger.Msg("[MusicInjector] Harmony 자동 패치 적용 완료");
            }
            catch (Exception ex)
            {
                harmonyInstance = null;
                MelonLogger.Msg($"[MusicInjector] Harmony 패치 적용 실패: {ex.Message}");
                MelonLogger.Msg($"[MusicInjector] 스택 트레이스: {ex.StackTrace}");
            }
        }
    }
}
