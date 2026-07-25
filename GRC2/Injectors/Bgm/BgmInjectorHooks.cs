using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace GRC2.Injectors
{
    /// <summary>
    /// BGM 주입에 필요한 유일한 Harmony 패치인 게임 종료 IEnumerator 래퍼를 등록합니다.
    /// </summary>
    internal static class BgmInjectorHooks
    {
        private static HarmonyLib.Harmony _harmony;

        public static void Initialize()
        {
            if (_harmony != null)
                return;

            try
            {
                Assembly gameAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(assembly => assembly.GetName().Name == "Assembly-CSharp");
                Type managerType = gameAssembly?.GetType("IntiCreates.cRythmGameManager");
                MethodInfo target = managerType?.GetMethod(
                    "coMonitorGameEnd",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo postfix = typeof(BgmGameEndMonitor).GetMethod(
                    nameof(BgmGameEndMonitor.MonitorGameEndPostfix),
                    BindingFlags.Public | BindingFlags.Static);

                if (target == null || postfix == null)
                {
                    MelonLogger.Warning("[BgmInjectorHooks] coMonitorGameEnd 패치 대상을 찾지 못했습니다.");
                    return;
                }

                _harmony = new HarmonyLib.Harmony("GUNVOLT_RECORDS_Cychronicle.BgmInjector");
                _harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                MelonLogger.Msg("[BgmInjectorHooks] coMonitorGameEnd 원본 보존 래퍼 패치 완료");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BgmInjectorHooks] 초기화 실패: {ex.Message}");
                _harmony = null;
            }
        }
    }
}
