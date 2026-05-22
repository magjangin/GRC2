using MelonLoader;
using System;
using System.Reflection;


namespace GRC2.Harmony.Hooks
{
    public static partial class GameFlowHooks
    {
    

        public static void SetIsAutoPlayPrefix(object __instance, bool isAuto)
        {
            try
            {
                MelonLogger.Msg("===========================================");
                MelonLogger.Msg($"[GameFlowHooks] 🎮 setIsAutoPlay() 호출됨");
                MelonLogger.Msg($"[GameFlowHooks]   인스턴스: {__instance?.GetType().Name ?? "null"}");
                MelonLogger.Msg($"[GameFlowHooks]   isAuto: {isAuto}");
                
                if (__instance != null)
                {
                    Type instanceType = __instance.GetType();
                    
                    // mIsCurrentAutoPlay 필드 확인
                    FieldInfo autoPlayField = instanceType.GetField("mIsCurrentAutoPlay", 
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    if (autoPlayField != null)
                    {
                        object currentValue = autoPlayField.GetValue(__instance);
                        MelonLogger.Msg($"[GameFlowHooks]   현재 mIsCurrentAutoPlay 값: {currentValue}");
                    }
                }
                
                MelonLogger.Msg("===========================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] setIsAutoPlay 오류: {ex.Message}");
            }
        }

        public static void SetIsAutoPlayPostfix(object __instance, bool isAuto)
        {
            try
            {
                if (__instance != null)
                {
                    Type instanceType = __instance.GetType();
                    
                    // mIsCurrentAutoPlay 필드 확인
                    FieldInfo autoPlayField = instanceType.GetField("mIsCurrentAutoPlay", 
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    if (autoPlayField != null)
                    {
                        object newValue = autoPlayField.GetValue(__instance);
                        MelonLogger.Msg($"[GameFlowHooks]   설정 후 mIsCurrentAutoPlay 값: {newValue}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] setIsAutoPlay Postfix 오류: {ex.Message}");
            }
        }

        public static void PushButtonPrefix(object __instance, object __0)
        {
            try
            {
                MelonLogger.Msg("===========================================");
                MelonLogger.Msg("[GameFlowHooks] 🔘 pushButton() 호출됨");
                MelonLogger.Msg($"[GameFlowHooks]   인스턴스: {__instance?.GetType().Name ?? "null"}");
                MelonLogger.Msg($"[GameFlowHooks]   시간: {DateTime.Now:HH:mm:ss.fff}");
                MelonLogger.Msg("[GameFlowHooks]   설명: 메인 메뉴 버튼 클릭");
                MelonLogger.Msg($"[GameFlowHooks]   매개변수: id = {__0} (타입: {__0?.GetType().Name ?? "null"})");
                MelonLogger.Msg("===========================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] pushButton 오류: {ex.Message}");
            }
        }

        
        public static void PushButtonPostfix(object __instance)
        {
            try
            {
                MelonLogger.Msg("[GameFlowHooks] 🔘 pushButton() 완료");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] pushButton Postfix 오류: {ex.Message}");
            }
        }

        public static void SetSceneStatePrefix(object __instance, object __0)
        {
            try
            {
                MelonLogger.Msg("===========================================");
                MelonLogger.Msg("[GameFlowHooks] 🔄 setSceneState() 호출됨");
                MelonLogger.Msg($"[GameFlowHooks]   인스턴스: {__instance?.GetType().Name ?? "null"}");
                MelonLogger.Msg($"[GameFlowHooks]   시간: {DateTime.Now:HH:mm:ss.fff}");
                MelonLogger.Msg("[GameFlowHooks]   설명: 씬 상태 설정");
                MelonLogger.Msg($"[GameFlowHooks]   매개변수: state = {__0} (타입: {__0?.GetType().Name ?? "null"})");
                MelonLogger.Msg("===========================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] setSceneState 오류: {ex.Message}");
            }
        }

        
        public static void SetSceneStatePostfix(object __instance)
        {
            try
            {
                MelonLogger.Msg("[GameFlowHooks] 🔄 setSceneState() 완료");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] setSceneState Postfix 오류: {ex.Message}");
            }
        }

        public static void OpenSortWindowPrefix(object __instance)
        {
            try
            {
                MelonLogger.Msg("===========================================");
                MelonLogger.Msg("[GameFlowHooks] 🔀 openSortWindow() 호출됨");
                MelonLogger.Msg($"[GameFlowHooks]   인스턴스: {__instance?.GetType().Name ?? "null"}");
                MelonLogger.Msg($"[GameFlowHooks]   시간: {DateTime.Now:HH:mm:ss.fff}");
                MelonLogger.Msg("[GameFlowHooks]   설명: 정렬 창 열기");
                MelonLogger.Msg("===========================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] openSortWindow 오류: {ex.Message}");
            }
        }

        public static void OpenFilterWindowPrefix(object __instance)
        {
            try
            {
                MelonLogger.Msg("===========================================");
                MelonLogger.Msg("[GameFlowHooks] 🔍 openFilterWindow() 호출됨");
                MelonLogger.Msg($"[GameFlowHooks]   인스턴스: {__instance?.GetType().Name ?? "null"}");
                MelonLogger.Msg($"[GameFlowHooks]   시간: {DateTime.Now:HH:mm:ss.fff}");
                MelonLogger.Msg("[GameFlowHooks]   설명: 필터 창 열기");
                MelonLogger.Msg("===========================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] openFilterWindow 오류: {ex.Message}");
            }
        }
}
}
