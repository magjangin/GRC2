using GRC2.Core;
using HarmonyLib;
using IntiCreates;

namespace GRC2.Harmony.Handlers
{
    /// <summary>
    /// cFairyModeNotesManager의 오토플레이 상태를 강제합니다.
    /// savecustomkey/config.txt의 autoplay_enabled 값으로만 결정되며, 게임 중 토글은 없습니다.
    /// mIsCurrentAutoPlay는 씬 초기화 시 InitializeParam.isAutoPlay로 직접 대입되어
    /// setIsAutoPlay를 거치지 않으므로, createAllNote 진입 시점에도 함께 강제합니다.
    /// </summary>
    public static class AutoPlayPatch
    {
        public static bool IsEnabled { get; private set; }

        private static readonly AccessTools.FieldRef<cFairyModeNotesManager, bool> AutoPlayFieldRef =
            AccessTools.FieldRefAccess<cFairyModeNotesManager, bool>("mIsCurrentAutoPlay");

        public static void Initialize()
        {
            IsEnabled = CustomKeySettings.AutoPlayEnabled;
        }

        [HarmonyPatch(typeof(cFairyModeNotesManager), "createAllNote")]
        private static class ForceOnNoteCreatePatch
        {
            [HarmonyPrefix]
            private static void Prefix(cFairyModeNotesManager __instance)
            {
                if (IsEnabled)
                    AutoPlayFieldRef(__instance) = true;
            }
        }

        [HarmonyPatch(typeof(cFairyModeNotesManager), "setIsAutoPlay")]
        private static class SetterPatch
        {
            [HarmonyPrefix]
            private static void Prefix(ref bool isAuto)
            {
                if (IsEnabled)
                    isAuto = true;
            }
        }
    }
}
