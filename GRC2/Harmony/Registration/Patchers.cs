using System;
using System.Collections.Generic;
using System.Reflection;
using GRC2.Core;
using GRC2.Harmony.Handlers;
using GRC2.Harmony.Hooks;
using HarmonyLib;
using MelonLoader;

namespace GRC2.Harmony.Registration
{
    internal static class AudioClipPatcher
    {
        private static HarmonyLib.Harmony _harmony;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            _harmony = harmony;
        }

        public static void Patch()
        {
            Type type = ReflectionHelper.FindType("IntiCreates.cMusicSelectSceneUIUpdater");
            if (type == null)
            {
                MelonLogger.Warning("[AudioClipPatcher] cMusicSelectSceneUIUpdater 타입을 찾지 못했습니다.");
                return;
            }

            PatchMethod(
                type,
                "noticeChangedMusic",
                prefix: null,
                postfix: GetPatchMethod(typeof(AudioClipPatch), nameof(AudioClipPatch.NoticeChangedMusicPostfix)));

            PatchMethod(
                type,
                "startRythmGame",
                prefix: GetPatchMethod(typeof(GameFlowHooks), nameof(GameFlowHooks.StartRythmGamePrefix)),
                postfix: null);

            PatchMethod(
                type,
                "coOpenPreMusicStartWindow",
                prefix: GetPatchMethod(typeof(GameFlowHooks), nameof(GameFlowHooks.CoOpenPreMusicStartWindowPrefix)),
                postfix: null);

            PatchMethod(
                type,
                "backToPreScreen",
                prefix: GetPatchMethod(typeof(GameFlowHooks), nameof(GameFlowHooks.BackToPreScreenPrefix)),
                postfix: null);
        }

        private static MethodInfo GetPatchMethod(Type type, string name)
        {
            return type.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
        }

        private static void PatchMethod(
            Type targetType,
            string targetName,
            MethodInfo prefix,
            MethodInfo postfix)
        {
            try
            {
                MethodInfo target = targetType.GetMethod(
                    targetName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (target == null)
                {
                    MelonLogger.Warning($"[AudioClipPatcher] {targetType.Name}.{targetName}을 찾지 못했습니다.");
                    return;
                }

                _harmony.Patch(
                    target,
                    prefix == null ? null : new HarmonyMethod(prefix),
                    postfix == null ? null : new HarmonyMethod(postfix));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(
                    $"[AudioClipPatcher] {targetType.Name}.{targetName} 패치 실패: {ex.Message}");
            }
        }
    }

    internal static class PreMusicStartWindowPatcher
    {
        private static HarmonyLib.Harmony _harmony;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            _harmony = harmony;
        }

        public static void Patch()
        {
            try
            {
                MethodInfo target = ReflectionHelper.FindMethod(
                    "IntiCreates.cMusicSelectPreMusicStartWindowManager",
                    "requestOpenWindow",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    silent: true);
                MethodInfo postfix = typeof(GameFlowHooks).GetMethod(
                    nameof(GameFlowHooks.PreMusicStartWindowOpenedPostfix),
                    BindingFlags.Public | BindingFlags.Static);

                if (target == null || postfix == null)
                {
                    MelonLogger.Warning("[PreMusicStartWindowPatcher] 시작 전 팝업 패치 대상을 찾지 못했습니다.");
                    return;
                }

                _harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[PreMusicStartWindowPatcher] 패치 실패: {ex.Message}");
            }
        }
    }

    internal static class CoverImagePatcher
    {
        private static HarmonyLib.Harmony _harmony;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            _harmony = harmony;
        }

        public static void Patch()
        {
            try
            {
                MethodInfo target = ReflectionHelper.FindMethod(
                    "IntiCreates.cMusicSelectArtWork",
                    "requestSetArtworkSprite",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    silent: true);
                MethodInfo prefix = typeof(ArtWorkPatch).GetMethod(
                    nameof(ArtWorkPatch.RequestSetArtworkSpritePrefix),
                    BindingFlags.Public | BindingFlags.Static);

                if (target == null || prefix == null)
                {
                    MelonLogger.Warning("[CoverImagePatcher] 아트워크 패치 대상을 찾지 못했습니다.");
                    return;
                }

                _harmony.Patch(target, prefix: new HarmonyMethod(prefix));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CoverImagePatcher] 패치 실패: {ex.Message}");
            }
        }
    }

    internal static class ResultSceneUpdaterPatcher
    {
        private static HarmonyLib.Harmony _harmony;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            _harmony = harmony;
        }

        public static void Patch()
        {
            try
            {
                MethodInfo target = ReflectionHelper.FindMethod(
                    "IntiCreates.cRythmGameResultSceneUpdater",
                    "initializePreFade",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    silent: true);
                MethodInfo postfix = typeof(ResultSceneUpdaterPatch).GetMethod(
                    nameof(ResultSceneUpdaterPatch.InitializePreFadePostfix),
                    BindingFlags.Public | BindingFlags.Static);

                if (target == null || postfix == null)
                {
                    MelonLogger.Warning("[ResultSceneUpdaterPatcher] 결과 화면 패치 대상을 찾지 못했습니다.");
                    return;
                }

                _harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ResultSceneUpdaterPatcher] 패치 실패: {ex.Message}");
            }
        }
    }

    internal static class TextPatcher
    {
        private static readonly HashSet<MethodInfo> PatchedSetters = new HashSet<MethodInfo>();
        private static HarmonyLib.Harmony _harmony;

        public static void Initialize(HarmonyLib.Harmony harmony)
        {
            _harmony = harmony;
        }

        public static void Patch()
        {
            PatchTextType(typeof(UnityEngine.UI.Text));
            PatchTextType(ReflectionHelper.FindType("TMPro.TextMeshProUGUI"));
            PatchTextType(ReflectionHelper.FindType("TMPro.TextMeshPro"));
        }

        private static void PatchTextType(Type textType)
        {
            if (textType == null)
                return;

            try
            {
                MethodInfo setter = textType
                    .GetProperty("text", BindingFlags.Public | BindingFlags.Instance)?
                    .GetSetMethod();
                if (setter == null || !PatchedSetters.Add(setter))
                    return;

                MethodInfo prefix = typeof(TextPatch).GetMethod(
                    nameof(TextPatch.SetTextPrefix),
                    BindingFlags.Public | BindingFlags.Static);
                if (prefix != null)
                    _harmony.Patch(setter, prefix: new HarmonyMethod(prefix));
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[TextPatcher] {textType.Name}.set_text 패치 실패: {ex.Message}");
            }
        }
    }
}
