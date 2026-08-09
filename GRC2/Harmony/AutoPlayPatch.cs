using GRC2.Core;
using HarmonyLib;
using IntiCreates;

namespace GRC2.Harmony
{
    /// <summary>
    /// cFairyModeNotesManager의 오토플레이 상태를 강제합니다.
    /// savecustomkey/config.txt의 AutoPlay 값으로만 결정되며, 게임 중 토글 키는 없습니다.
    /// mIsCurrentAutoPlay는 씬 초기화 시 InitializeParam.isAutoPlay로 직접 대입되어
    /// setIsAutoPlay를 거치지 않으므로, createAllNote 진입 시점에도 함께 강제합니다.
    ///
    /// 값을 캐시하지 않고 매번 CustomKeySettings에서 읽습니다. 설정은 씬 로드 때만 다시 읽히므로
    /// 곡이 진행되는 동안에는 고정이고, 파일을 고치면 다음 플레이부터(리트라이 포함) 반영됩니다.
    /// </summary>
    public static class AutoPlayPatch
    {
        public static bool IsEnabled => CustomKeySettings.AutoPlay;

        private static readonly AccessTools.FieldRef<cFairyModeNotesManager, bool> AutoPlayFieldRef =
            AccessTools.FieldRefAccess<cFairyModeNotesManager, bool>("mIsCurrentAutoPlay");

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
