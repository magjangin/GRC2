using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using GRC2.Helpers;

namespace GRC2.Injectors
{
    /// <summary>
    /// BGM Injector의 Harmony 후킹 로직을 담당하는 메인 클래스
    /// 초기화 및 후킹 설정만 담당하며, 실제 후킹 로직은 분리된 클래스들에서 처리
    /// </summary>
    internal static class BgmInjectorHooks
    {
        private static readonly string[] BgmBeatMethodsToHook =
        {
            "setClip",
            "requestLoadBGM",
            "requestPlayAudio",
            "requestPause",
            "setBPM",
            "setSample",
            "getAudioClip",
            "isReadyPlay"
        };

        private static HarmonyLib.Harmony _harmony = null;

        public static void Initialize()
        {
            try
            {
                Assembly assembly = LoadGameAssembly();
                if (assembly == null)
                {
                    MelonLogger.Warning("[BgmInjectorHooks] Assembly-CSharp를 찾을 수 없습니다.");
                    return;
                }
                
                var bgmBeatManagerType = assembly.GetType("IntiCreates.cBGMBeatManager");
                if (bgmBeatManagerType == null)
                {
                    MelonLogger.Warning("[BgmInjectorHooks] cBGMBeatManager 타입을 찾을 수 없습니다.");
                    return;
                }
                
                _harmony = new HarmonyLib.Harmony("GUNVOLT_RECORDS_Cychronicle.BgmInjector");

                var postfix = new HarmonyLib.HarmonyMethod(
                    typeof(BgmMethodCallHooks).GetMethod(nameof(BgmMethodCallHooks.MethodCallPostfix), 
                        BindingFlags.Public | BindingFlags.Static));
                var postfixVoid = new HarmonyLib.HarmonyMethod(
                    typeof(BgmMethodCallHooks).GetMethod(nameof(BgmMethodCallHooks.MethodCallPostfixVoid), 
                        BindingFlags.Public | BindingFlags.Static));
                var allMethods = GetDeclaredBgmBeatMethods(bgmBeatManagerType);
                int hookedCount = PatchBgmBeatManagerMethods(allMethods, bgmBeatManagerType, postfix, postfixVoid);
                
                // setTime은 prefix로도 후킹 (시간 제한용)
                PatchSetTimeMethod(allMethods);
                
                MelonLogger.Msg($"[BgmInjectorHooks] cBGMBeatManager 메서드 {hookedCount}개 후킹 완료");
                PatchRythmGameManagerMethods(assembly, postfixVoid);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BgmInjectorHooks] 초기화 실패: {ex.Message}");
            }
        }

        private static Assembly LoadGameAssembly()
        {
            var assemblyPath = Path.Combine(
                Path.GetDirectoryName(Application.dataPath),
                "GUNVOLT_RECORDS_Cychronicle_Data",
                "Managed",
                "Assembly-CSharp.dll"
            );

            if (File.Exists(assemblyPath))
                return Assembly.LoadFrom(assemblyPath);

            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
        }

        private static System.Collections.Generic.List<MethodInfo> GetDeclaredBgmBeatMethods(Type bgmBeatManagerType)
        {
            var instanceMethods = bgmBeatManagerType.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var staticMethods = bgmBeatManagerType.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var allMethods = instanceMethods.Concat(staticMethods).ToList();

            MelonLogger.Msg($"[BgmInjectorHooks] cBGMBeatManager 전체 메서드 수: {allMethods.Count} (인스턴스: {instanceMethods.Length}, 정적: {staticMethods.Length})");
            return allMethods;
        }

        private static int PatchBgmBeatManagerMethods(
            System.Collections.Generic.List<MethodInfo> allMethods,
            Type bgmBeatManagerType,
            HarmonyLib.HarmonyMethod postfix,
            HarmonyLib.HarmonyMethod postfixVoid)
        {
            int hookedCount = 0;
            foreach (var methodName in BgmBeatMethodsToHook)
            {
                var method = allMethods.FirstOrDefault(m =>
                    m.Name == methodName &&
                    m.DeclaringType == bgmBeatManagerType);

                if (method == null)
                    continue;

                try
                {
                    HarmonyLib.HarmonyMethod prefix = methodName == "requestLoadBGM"
                        ? new HarmonyLib.HarmonyMethod(typeof(BgmMethodCallHooks).GetMethod(nameof(BgmMethodCallHooks.RequestLoadBGMPrefix),
                            BindingFlags.Public | BindingFlags.Static))
                        : null;

                    PatchWithReturnAwarePostfix(method, prefix, postfix, postfixVoid);
                    hookedCount++;
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[BgmInjectorHooks] {methodName} 후킹 실패: {ex.Message}");
                }
            }

            return hookedCount;
        }

        private static void PatchWithReturnAwarePostfix(MethodInfo method, HarmonyLib.HarmonyMethod prefix, HarmonyLib.HarmonyMethod postfix, HarmonyLib.HarmonyMethod postfixVoid)
        {
            _harmony.Patch(
                method,
                prefix: prefix,
                postfix: method.ReturnType == typeof(void) ? postfixVoid : postfix);
        }

        private static void PatchSetTimeMethod(System.Collections.Generic.List<MethodInfo> allMethods)
        {
            var setTimeMethod = allMethods.FirstOrDefault(m =>
                m.Name == "setTime" &&
                m.GetParameters().Length == 1 &&
                m.GetParameters()[0].ParameterType == typeof(float));
            if (setTimeMethod == null)
                return;

            try
            {
                var prefix = new HarmonyLib.HarmonyMethod(
                    typeof(BgmMethodCallHooks).GetMethod(nameof(BgmMethodCallHooks.SetTimePrefix),
                        BindingFlags.Public | BindingFlags.Static));
                _harmony.Patch(setTimeMethod, prefix: prefix);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[BgmInjectorHooks] Initialize", "setTime Harmony 패치 실패");
            }
        }

        private static void PatchRythmGameManagerMethods(Assembly assembly, HarmonyLib.HarmonyMethod postfixVoid)
        {
            var rythmGameManagerType = assembly.GetType("IntiCreates.cRythmGameManager");
            if (rythmGameManagerType == null)
                return;

            PatchRythmGameMethod(rythmGameManagerType, "requestCommonRythmGameEnd", null, postfixVoid, "cRythmGameManager.requestCommonRythmGameEnd 후킹 완료");

            var monitorPrefix = new HarmonyLib.HarmonyMethod(
                typeof(BgmGameEndMonitor).GetMethod(nameof(BgmGameEndMonitor.MonitorGameEndPrefix),
                    BindingFlags.Public | BindingFlags.Static));
            var monitorPostfix = new HarmonyLib.HarmonyMethod(
                typeof(BgmGameEndMonitor).GetMethod(nameof(BgmGameEndMonitor.MonitorGameEndPostfix),
                    BindingFlags.Public | BindingFlags.Static));
            PatchRythmGameMethod(rythmGameManagerType, "coMonitorGameEnd", monitorPrefix, monitorPostfix, "cRythmGameManager.coMonitorGameEnd 후킹 완료 (prefix + postfix)");

            var clearPrefix = new HarmonyLib.HarmonyMethod(
                typeof(BgmGameEndMonitor).GetMethod(nameof(BgmGameEndMonitor.ClearGameEndPrefix),
                    BindingFlags.Public | BindingFlags.Static));
            PatchRythmGameMethod(rythmGameManagerType, "coClearRythmGameEnd", clearPrefix, null, "cRythmGameManager.coClearRythmGameEnd 후킹 완료");
        }

        private static void PatchRythmGameMethod(Type type, string methodName, HarmonyLib.HarmonyMethod prefix, HarmonyLib.HarmonyMethod postfix, string successMessage)
        {
            var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
                return;

            try
            {
                _harmony.Patch(method, prefix: prefix, postfix: postfix);
                MelonLogger.Msg($"[BgmInjectorHooks] {successMessage}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BgmInjectorHooks] {methodName} 후킹 실패: {ex.Message}");
            }
        }
    }
}
