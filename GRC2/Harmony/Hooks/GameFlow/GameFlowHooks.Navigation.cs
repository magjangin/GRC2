using GRC2.Core;
using GRC2.Harmony.Handlers;
using GRC2.Services;
using MelonLoader;
using System;


namespace GRC2.Harmony.Hooks
{
    public static partial class GameFlowHooks
    {
    

        public static void BackToPreScreenPrefix(object __instance)
        {
            try
            {
                MelonLogger.Msg("===========================================");
                MelonLogger.Msg("[GameFlowHooks] ⬅️ backToPreScreen() 호출됨");
                MelonLogger.Msg($"[GameFlowHooks]   인스턴스: {__instance?.GetType().Name ?? "null"}");
                MelonLogger.Msg($"[GameFlowHooks]   시간: {DateTime.Now:HH:mm:ss.fff}");
                MelonLogger.Msg("[GameFlowHooks]   설명: 이전 화면(곡 선택 화면 등)으로 돌아감");
                
                // 커스텀 프리뷰 BGM 즉시 중지
                CustomBgmPlayer.Cleanup();
                MelonLogger.Msg("[GameFlowHooks] ✅ 커스텀 프리뷰 BGM 중지됨");
                
                // 원래 음소거했던 프리뷰/환경음 복원 (볼륨 1.0으로 재생)
                PreviewAudioManager.RestoreMutedAudioSources();
                MelonLogger.Msg("[GameFlowHooks] ✅ 원본 오디오 소스 복원됨");
                
                MelonLogger.Msg("===========================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] backToPreScreen 오류: {ex.Message}");
            }
        }

        public static void CoOpenPrefix(object __instance)
        {
            try
            {
                // SoundPlayerScene / MoviePlayer_MovieSelect에서는 리스트 선택을 곡 선택 씬으로 착각하지 않음
                if (CustomAssetManager.IsSceneWhereInjectionDisallowed())
                    return;

                if (__instance != null)
                {
                    // 커스텀 차트 감지 및 아트워크/OGG 로드
                    CustomChartHandler.UpdateCustomChartTitle(__instance);
                    
                    // 커스텀 차트 감지 여부 확인하여 텍스트 훅 스위치 제어
                    // CustomChartHandler.UpdateCustomChartTitle에서 이미 처리했으므로 그 결과 확인
                    bool isCustomChart = CustomAssetManager.IsCustomChartSelected();
                    
                    // 텍스트 훅 스위치 제어 (커스텀 차트면 ON, 일반 곡이면 OFF)
                    GRC2.Harmony.Handlers.TextPatch.EnableTextReplacement(isCustomChart);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] coOpen 오류: {ex.Message}");
            }
        }

        public static void CoClosePrefix(object __instance)
        {
            try
            {
                MelonLogger.Msg("===========================================");
                MelonLogger.Msg("[GameFlowHooks] 🚪 coClose() 호출됨");
                MelonLogger.Msg($"[GameFlowHooks]   인스턴스: {__instance?.GetType().Name ?? "null"}");
                MelonLogger.Msg($"[GameFlowHooks]   시간: {DateTime.Now:HH:mm:ss.fff}");
                MelonLogger.Msg("[GameFlowHooks]   설명: 닫기 코루틴");
                MelonLogger.Msg("===========================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] coClose 오류: {ex.Message}");
            }
        }
}
}
