using GRC2.Core;
using HarmonyLib;
using IntiCreates;
using IntiCreates.RythmGame;

namespace GRC2.Harmony.Handlers
{
    /// <summary>
    /// cNotecWorkBase.onJudgeMent를 후킹해 모든 판정을 PERFECT로 강제합니다.
    /// savecustomkey/config.txt의 AllPerfect 값으로만 결정되며, 게임 중 토글은 없습니다.
    /// 각 노트 워크(cFairyTouchNoteWork 등)의 override는 전부 base.onJudgeMent(judgeParam)를
    /// 먼저 호출하므로, 베이스 메서드 하나만 패치해도 판정 반영/이펙트/사운드에 모두 적용됩니다.
    /// </summary>
    [HarmonyPatch(typeof(cNotecWorkBase), "onJudgeMent")]
    public static class JudgePerfectPatch
    {
        public static bool IsEnabled { get; private set; }

        public static void Initialize()
        {
            IsEnabled = CustomKeySettings.AllPerfect;
        }

        [HarmonyPrefix]
        private static void Prefix(cNotecWorkBase.OnJudgeParam judgeParam)
        {
            if (IsEnabled && judgeParam != null)
                judgeParam.judgeType = JudgeType.PERFECT;
        }
    }
}
