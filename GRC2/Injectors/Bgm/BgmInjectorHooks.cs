using System.Collections;
using HarmonyLib;
using IntiCreates;

namespace GRC2.Injectors
{
    /// <summary>
    /// BGM 주입에 필요한 게임 종료 IEnumerator 래퍼를 자동 등록합니다.
    /// </summary>
    [HarmonyPatch(typeof(cRythmGameManager), "coMonitorGameEnd")]
    internal static class BgmInjectorHooks
    {
        [HarmonyPostfix]
        private static void Postfix(object __instance, ref IEnumerator __result)
        {
            BgmGameEndMonitor.MonitorGameEndPostfix(__instance, ref __result);
        }
    }
}
