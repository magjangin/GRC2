using System;
using System.Reflection;
using HarmonyLib;
using GRC2.Core;
using GRC2.Harmony.Handlers;
using MelonLoader;

namespace GRC2.Harmony.Registration
{
    /// <summary>
    /// 텍스트 설정 관련 타입 패치
    /// </summary>
    internal static class TextPatcher
    {
        private static HarmonyLib.Harmony _harmonyInstance;

        public static void Initialize(HarmonyLib.Harmony harmonyInstance)
        {
            _harmonyInstance = harmonyInstance;
        }

        /// <summary>
        /// 텍스트 설정 관련 타입 찾기 및 후킹
        /// </summary>
        public static void Patch()
        {
            try
            {
                // UnityEngine.UI.Text.set_text 후킹
                PatchTextType(typeof(UnityEngine.UI.Text));
                
                // TMPro.TextMeshProUGUI.set_text 후킹
                Type tmpUGUIType = ReflectionHelper.FindType("TMPro.TextMeshProUGUI");
                if (tmpUGUIType != null)
                {
                    PatchTextType(tmpUGUIType);
                }
                
                // TMPro.TextMeshPro.set_text 후킹
                Type tmpType = ReflectionHelper.FindType("TMPro.TextMeshPro");
                if (tmpType != null)
                {
                    PatchTextType(tmpType);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[TextPatcher] 텍스트 타입 패치 중 오류: {ex.Message}");
            }
        }

        private static void PatchTextType(Type textType)
        {
            try
            {
                PropertyInfo textProperty = textType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                if (textProperty != null)
                {
                    MethodInfo setter = textProperty.GetSetMethod();
                    if (setter != null)
                    {
                        var prefixMethod = typeof(TextPatch).GetMethod("SetTextPrefix", BindingFlags.Static | BindingFlags.Public);
                        if (prefixMethod != null)
                        {
                            _harmonyInstance.Patch(setter, new HarmonyMethod(prefixMethod), null);
                            MelonLogger.Msg($"[TextPatcher] ✅ {textType.Name}.set_text 패치 성공");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[TextPatcher] ⚠️ {textType.Name} 패치 실패: {ex.Message}");
            }
        }
    }
}













