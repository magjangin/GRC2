using GRC2.Core;
using GRC2.Core.Hud;
using HarmonyLib;
using IntiCreates;
using MelonLoader;

namespace GRC2.Harmony.Handlers
{
    /// <summary>
    /// cNotecWorkBase.onJudgeMent를 후킹해 판정 결과(등급/시간오차)를 GameHud의 판정바로 전달합니다.
    /// JudgePerfectPatch와 같은 대상 메서드를 각자 독립적인 Prefix/Postfix로 패치하므로 서로 간섭하지
    /// 않습니다(Harmony는 같은 메서드에 여러 패치를 허용).
    /// </summary>
    [HarmonyPatch(typeof(cNotecWorkBase), "onJudgeMent")]
    public static class JudgmentBarPatch
    {
        private const float SAMPLE_RATE = 48000f;

        [HarmonyPostfix]
        private static void Postfix(cNotecWorkBase.OnJudgeParam judgeParam)
        {
            if (!CustomKeySettings.EnableJudgmentBar || judgeParam == null)
                return;

            try
            {
                GameHud.ReportJudgment(judgeParam.judgeType, judgeParam.subSample, SAMPLE_RATE);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning("[JudgmentBarPatch] 판정바 갱신 오류: " + ex.Message);
            }
        }
    }
}
