using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using GRC2.Core;
using GRC2.Harmony.Hooks;
using MelonLoader;

namespace GRC2.Harmony.Registration
{
    /// <summary>
    /// 곡 선택 UI 관련 타입 패치
    /// </summary>
    internal static class SelectingMusicUIPatcher
    {
        private static HarmonyLib.Harmony _harmonyInstance;

        public static void Initialize(HarmonyLib.Harmony harmonyInstance)
        {
            _harmonyInstance = harmonyInstance;
        }

        /// <summary>
        /// 곡 선택 UI 관련 타입 찾기 및 후킹
        /// </summary>
        public static void Patch()
        {
            try
            {
                // cMusicSelectSceneSelectingMusicUI 타입 찾기
                Type selectingMusicUIType = ReflectionHelper.FindType("IntiCreates.cMusicSelectSceneSelectingMusicUI");
                if (selectingMusicUIType != null)
                {
                    MelonLogger.Msg($"[SelectingMusicUIPatcher] ✅ cMusicSelectSceneSelectingMusicUI 타입 발견");
                    
                    MethodInfo[] methods = selectingMusicUIType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

                    // coOpen 메서드 후킹
                    PatchMethod(methods, "coOpen", typeof(GameFlowHooks), "CoOpenPrefix", null);
                    
                    // coClose 메서드 후킹
                    PatchMethod(methods, "coClose", typeof(GameFlowHooks), "CoClosePrefix", null);
                }
                else
                {
                    MelonLogger.Msg("[SelectingMusicUIPatcher] ❌ cMusicSelectSceneSelectingMusicUI 타입을 찾을 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[SelectingMusicUIPatcher] 곡 선택 UI 타입 패치 중 오류: {ex.Message}");
            }
        }

        private static void PatchMethod(MethodInfo[] methods, string methodName, Type patchType, 
            string prefixMethodName, string postfixMethodName)
        {
            try
            {
                MelonLogger.Msg($"[SelectingMusicUIPatcher] 🔍 {methodName} 메서드 검색 중...");
                
                var method = methods.FirstOrDefault(m => 
                    m.Name == methodName &&
                    !m.IsSpecialName &&
                    m.DeclaringType != typeof(UnityEngine.MonoBehaviour));
                
                if (method != null)
                {
                    MelonLogger.Msg($"[SelectingMusicUIPatcher] === {methodName} 메서드 발견 ===");
                    MelonLogger.Msg($"[SelectingMusicUIPatcher]   - {method.Name}({string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
                    
                    MethodInfo prefixMethod = null;
                    MethodInfo postfixMethod = null;
                    
                    if (!string.IsNullOrEmpty(prefixMethodName))
                    {
                        prefixMethod = patchType.GetMethod(prefixMethodName, BindingFlags.Static | BindingFlags.Public);
                    }
                    
                    if (!string.IsNullOrEmpty(postfixMethodName))
                    {
                        postfixMethod = patchType.GetMethod(postfixMethodName, BindingFlags.Static | BindingFlags.Public);
                    }
                    
                    if (prefixMethod != null || postfixMethod != null)
                    {
                        _harmonyInstance.Patch(method, 
                            prefixMethod != null ? new HarmonyMethod(prefixMethod) : null,
                            postfixMethod != null ? new HarmonyMethod(postfixMethod) : null);
                        MelonLogger.Msg($"[SelectingMusicUIPatcher] ✅ {methodName} 패치 성공!");
                    }
                }
                else
                {
                    MelonLogger.Msg($"[SelectingMusicUIPatcher] ⚠️ {methodName} 메서드를 찾을 수 없습니다!");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[SelectingMusicUIPatcher] ⚠️ {methodName} 패치 실패: {ex.Message}");
            }
        }
    }
}













