using GRC2.Core;
using HarmonyLib;
using IntiCreates;
using IntiCreates.RythmGame;

namespace GRC2.Harmony
{
    /// <summary>
    /// cNotecWorkBase.onJudgeMent를 후킹해 모든 판정을 PERFECT로 강제합니다.
    /// savecustomkey/config.txt의 AllPerfect 값으로만 결정되며, 게임 중 토글 키는 없습니다.
    /// 각 노트 워크(cFairyTouchNoteWork 등)의 override는 전부 base.onJudgeMent(judgeParam)를
    /// 먼저 호출하므로, 베이스 메서드 하나만 패치해도 판정 반영/이펙트/사운드에 모두 적용됩니다.
    ///
    /// 값을 캐시하지 않고 매번 CustomKeySettings에서 읽습니다. 설정은 씬 로드 때만 다시 읽히므로
    /// 한 곡 안에서는 판정 규칙이 절대 바뀌지 않고, 파일을 고치면 다음 플레이부터 반영됩니다.
    /// </summary>
    [HarmonyPatch(typeof(cNotecWorkBase), "onJudgeMent")]
    public static class JudgePerfectPatch
    {
        public static bool IsEnabled => CustomKeySettings.AllPerfect;

        [HarmonyPrefix]
        private static void Prefix(cNotecWorkBase.OnJudgeParam judgeParam)
        {
            if (IsEnabled && judgeParam != null)
                judgeParam.judgeType = JudgeType.PERFECT;
        }
    }
}
