using System;
using System.Reflection;
using HarmonyLib;
using GRC2.Core;
using GRC2.Harmony.Handlers;
using MelonLoader;

namespace GRC2.Harmony.Registration
{
    /// <summary>
    /// cRythmGameResultSceneUpdater.initializePreFade 후킹 등록
    /// </summary>
    internal static class ResultSceneUpdaterPatcher
    {
        private static HarmonyLib.Harmony _harmonyInstance;

        public static void Initialize(HarmonyLib.Harmony harmonyInstance)
        {
            _harmonyInstance = harmonyInstance;
        }

        public static void Patch()
        {
            try
            {
                var method = ReflectionHelper.FindMethod(
                    "IntiCreates.cRythmGameResultSceneUpdater",
                    "initializePreFade",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    silent: true);

                if (method == null)
                {
                    MelonLogger.Warning("[ResultSceneUpdaterPatcher] cRythmGameResultSceneUpdater.initializePreFade 메서드를 찾을 수 없습니다.");
                    return;
                }

                var postfixMethod = typeof(ResultSceneUpdaterPatch).GetMethod(
                    nameof(ResultSceneUpdaterPatch.InitializePreFadePostfix),
                    BindingFlags.Static | BindingFlags.Public);

                if (postfixMethod == null)
                {
                    MelonLogger.Warning("[ResultSceneUpdaterPatcher] InitializePreFadePostfix 메서드를 찾을 수 없습니다.");
                    return;
                }

                _harmonyInstance.Patch(method, null, new HarmonyMethod(postfixMethod));
                MelonLogger.Msg("[ResultSceneUpdaterPatcher] ✅ cRythmGameResultSceneUpdater.initializePreFade 패치 성공");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ResultSceneUpdaterPatcher] 패치 중 오류: {ex.Message}");
            }
        }
    }
}
