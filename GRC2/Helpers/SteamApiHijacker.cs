using System;
using System.IO;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using GRC2.Core;

namespace GRC2.Helpers
{
    /// <summary>
    /// Steamworks API 및 게임 내부 DLC 검증 메서드를 하이재킹하는 클래스
    /// </summary>
    public static class SteamApiHijacker
    {
        private static HarmonyLib.Harmony _harmonyInstance;

        /// <summary>
        /// Steam API 초기화 실패 여부
        /// </summary>
        public static bool IsBypassed { get; private set; } = false;

        public static void ApplyPatches()
        {
            try
            {
                _harmonyInstance = new HarmonyLib.Harmony("GRC2.SteamApiHijacker");
                MelonLogger.Msg("[SteamApiHijacker] 하이재킹 패치 시작...");

                // 1. Steamworks.SteamAPI 패치
                PatchSteamAPI();

                // 2. Steamworks.SteamApps 패치 (DLC 구매 체크 우회)
                PatchSteamApps();

                // 3. 게임 내부(IntiCreates) DLC 검증 패치
                PatchGameDlcLogic();

                MelonLogger.Msg("[SteamApiHijacker] 모든 하이재킹 패치 프로세스 완료.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SteamApiHijacker] 패치 중 예기치 못한 치명적 오류: {ex.Message}");
            }
        }

        private static void PatchSteamAPI()
        {
            Type steamApiType = ReflectionHelper.FindType("Steamworks.SteamAPI");
            if (steamApiType == null)
            {
                MelonLogger.Warning("[SteamApiHijacker] Steamworks.SteamAPI 타입을 찾을 수 없습니다.");
                return;
            }

            // RestartAppIfNecessary 패치 (스팀 강제 재시작 방지)
            MethodInfo restartMethod = steamApiType.GetMethod("RestartAppIfNecessary", BindingFlags.Public | BindingFlags.Static);
            if (restartMethod != null)
            {
                MethodInfo prefixMethod = typeof(SteamApiHijacker).GetMethod(nameof(RestartAppIfNecessaryPrefix), BindingFlags.Public | BindingFlags.Static);
                _harmonyInstance.Patch(restartMethod, new HarmonyMethod(prefixMethod), null);
                MelonLogger.Msg("[SteamApiHijacker] ✅ SteamAPI.RestartAppIfNecessary 패치 성공 (재시작 우회)");
            }

            // Init 패치 (스팀 미실행 시에도 강제로 true 반환하여 게임 유지)
            MethodInfo initMethod = steamApiType.GetMethod("Init", BindingFlags.Public | BindingFlags.Static);
            if (initMethod != null)
            {
                MethodInfo postfixMethod = typeof(SteamApiHijacker).GetMethod(nameof(InitPostfix), BindingFlags.Public | BindingFlags.Static);
                _harmonyInstance.Patch(initMethod, null, new HarmonyMethod(postfixMethod));
                MelonLogger.Msg("[SteamApiHijacker] ✅ SteamAPI.Init 패치 성공 (실패 시 강제 우회)");
            }

            // RunCallbacks 패치 (우회 모드 시 무시)
            MethodInfo runCallbacksMethod = steamApiType.GetMethod("RunCallbacks", BindingFlags.Public | BindingFlags.Static);
            if (runCallbacksMethod != null)
            {
                MethodInfo prefixMethod = typeof(SteamApiHijacker).GetMethod(nameof(RunCallbacksPrefix), BindingFlags.Public | BindingFlags.Static);
                _harmonyInstance.Patch(runCallbacksMethod, new HarmonyMethod(prefixMethod), null);
                MelonLogger.Msg("[SteamApiHijacker] ✅ SteamAPI.RunCallbacks 패치 성공");
            }

            // Shutdown 패치 (우회 모드 시 무시)
            MethodInfo shutdownMethod = steamApiType.GetMethod("Shutdown", BindingFlags.Public | BindingFlags.Static);
            if (shutdownMethod != null)
            {
                MethodInfo prefixMethod = typeof(SteamApiHijacker).GetMethod(nameof(ShutdownPrefix), BindingFlags.Public | BindingFlags.Static);
                _harmonyInstance.Patch(shutdownMethod, new HarmonyMethod(prefixMethod), null);
                MelonLogger.Msg("[SteamApiHijacker] ✅ SteamAPI.Shutdown 패치 성공");
            }
        }

        private static void PatchSteamApps()
        {
            Type steamAppsType = ReflectionHelper.FindType("Steamworks.SteamApps");
            if (steamAppsType == null)
            {
                MelonLogger.Warning("[SteamApiHijacker] Steamworks.SteamApps 타입을 찾을 수 없습니다.");
                return;
            }

            // BIsDlcInstalled 패치 (항상 true 반환)
            MethodInfo isDlcInstalledMethod = steamAppsType.GetMethod("BIsDlcInstalled", BindingFlags.Public | BindingFlags.Static);
            if (isDlcInstalledMethod != null)
            {
                MethodInfo prefixMethod = typeof(SteamApiHijacker).GetMethod(nameof(BIsDlcInstalledPrefix), BindingFlags.Public | BindingFlags.Static);
                _harmonyInstance.Patch(isDlcInstalledMethod, new HarmonyMethod(prefixMethod), null);
                MelonLogger.Msg("[SteamApiHijacker] ✅ SteamApps.BIsDlcInstalled 패치 성공 (항상 true)");
            }
        }

        private static void PatchGameDlcLogic()
        {
            // 1. IntiCreates.Application.isDLCEnable 패치 (항상 true 반환)
            Type appType = ReflectionHelper.FindType("IntiCreates.Application");
            if (appType != null)
            {
                MethodInfo isDlcEnableMethod = appType.GetMethod("isDLCEnable", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                if (isDlcEnableMethod != null)
                {
                    MethodInfo prefixMethod = typeof(SteamApiHijacker).GetMethod(nameof(IsDlcEnablePrefix), BindingFlags.Public | BindingFlags.Static);
                    _harmonyInstance.Patch(isDlcEnableMethod, new HarmonyMethod(prefixMethod), null);
                    MelonLogger.Msg("[SteamApiHijacker] ✅ IntiCreates.Application.isDLCEnable 패치 성공 (항상 true)");
                }
            }

            // 2. IntiCreates.sAddressableDirector 패치
            Type addressableDirectorType = ReflectionHelper.FindType("IntiCreates.sAddressableDirector");
            if (addressableDirectorType != null)
            {
                // isUsableDlc 패치 (항상 true 반환)
                MethodInfo isUsableDlcMethod = addressableDirectorType.GetMethod("isUsableDlc", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (isUsableDlcMethod != null)
                {
                    MethodInfo prefixMethod = typeof(SteamApiHijacker).GetMethod(nameof(IsUsableDlcPrefix), BindingFlags.Public | BindingFlags.Static);
                    _harmonyInstance.Patch(isUsableDlcMethod, new HarmonyMethod(prefixMethod), null);
                    MelonLogger.Msg("[SteamApiHijacker] ✅ sAddressableDirector.isUsableDlc 패치 성공 (항상 true)");
                }

                // isNotYetPurchased 패치 (항상 false 반환)
                MethodInfo isNotYetPurchasedMethod = addressableDirectorType.GetMethod("isNotYetPurchased", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (isNotYetPurchasedMethod != null)
                {
                    MethodInfo prefixMethod = typeof(SteamApiHijacker).GetMethod(nameof(IsNotYetPurchasedPrefix), BindingFlags.Public | BindingFlags.Static);
                    _harmonyInstance.Patch(isNotYetPurchasedMethod, new HarmonyMethod(prefixMethod), null);
                    MelonLogger.Msg("[SteamApiHijacker] ✅ sAddressableDirector.isNotYetPurchased 패치 성공 (항상 false)");
                }

                // coCheckDLC 패치 (실행 확인 로깅)
                MethodInfo coCheckDLCMethod = addressableDirectorType.GetMethod("coCheckDLC", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (coCheckDLCMethod != null)
                {
                    MethodInfo postfixMethod = typeof(SteamApiHijacker).GetMethod(nameof(CoCheckDLCPostfix), BindingFlags.Public | BindingFlags.Static);
                    _harmonyInstance.Patch(coCheckDLCMethod, null, new HarmonyMethod(postfixMethod));
                    MelonLogger.Msg("[SteamApiHijacker] ✅ sAddressableDirector.coCheckDLC 로깅 패치 성공");
                }
            }

            // 3. IntiCreates.cDlcDirector 패치
            Type dlcDirectorType = ReflectionHelper.FindType("IntiCreates.cDlcDirector");
            if (dlcDirectorType != null)
            {
                // IsNotYetPurchased 패치 (항상 false 반환)
                MethodInfo isNotYetPurchasedMethod = dlcDirectorType.GetMethod("IsNotYetPurchased", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (isNotYetPurchasedMethod != null)
                {
                    MethodInfo prefixMethod = typeof(SteamApiHijacker).GetMethod(nameof(IsNotYetPurchasedPrefix), BindingFlags.Public | BindingFlags.Static);
                    _harmonyInstance.Patch(isNotYetPurchasedMethod, new HarmonyMethod(prefixMethod), null);
                    MelonLogger.Msg("[SteamApiHijacker] ✅ cDlcDirector.IsNotYetPurchased 패치 성공 (항상 false)");
                }

                // Initialize 패치 (로컬 DataAddon 폴더 감지 및 동적 mDlcList 주입)
                MethodInfo initializeMethod = dlcDirectorType.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (initializeMethod != null)
                {
                    MethodInfo postfixMethod = typeof(SteamApiHijacker).GetMethod(nameof(InitializePostfix), BindingFlags.Public | BindingFlags.Static);
                    _harmonyInstance.Patch(initializeMethod, null, new HarmonyMethod(postfixMethod));
                    MelonLogger.Msg("[SteamApiHijacker] ✅ cDlcDirector.Initialize 패치 성공 (동적 폴더 마운트 준비)");
                }
            }
        }

        #region Harmony Patches

        public static bool RestartAppIfNecessaryPrefix(ref bool __result)
        {
            __result = false; // 스팀 강제 재시작 방지
            return false;
        }

        public static void InitPostfix(ref bool __result)
        {
            if (!__result)
            {
                MelonLogger.Msg("[SteamApiHijacker] SteamAPI.Init returned false. Bypassing and forcing to true.");
                IsBypassed = true;
                __result = true;
            }
        }

        public static bool RunCallbacksPrefix()
        {
            if (IsBypassed) return false; // 우회 상태면 본래 콜백 무시
            return true;
        }

        public static bool ShutdownPrefix()
        {
            if (IsBypassed) return false; // 우회 상태면 본래 셧다운 무시
            return true;
        }

        public static bool BIsDlcInstalledPrefix(ref bool __result)
        {
            __result = true; // 스팀웍스 레벨 DLC 항상 설치됨으로 설정
            return false;
        }

        public static bool IsDlcEnablePrefix(ref bool __result)
        {
            __result = true; // 게임 로직 레벨 DLC 항상 활성화
            return false;
        }

        public static bool IsUsableDlcPrefix(ref bool __result)
        {
            __result = true; // DLC 에셋 사용 가능하도록 우회
            return false;
        }

        public static bool IsNotYetPurchasedPrefix(ref bool __result)
        {
            __result = false; // 미구매 상태가 아니도록(구매완료됨) 우회
            return false;
        }

        public static void CoCheckDLCPostfix()
        {
            MelonLogger.Msg("[SteamApiHijacker] sAddressableDirector.coCheckDLC 실행 완료됨.");
        }

        public static void InitializePostfix(object __instance)
        {
            try
            {
                Type dlcDirectorType = __instance.GetType();
                var field = AccessTools.Field(dlcDirectorType, "mDlcList");
                var dlcList = field.GetValue(__instance) as IDictionary;
                if (dlcList == null)
                {
                    dlcList = Activator.CreateInstance(field.FieldType) as IDictionary;
                    field.SetValue(__instance, dlcList);
                }

                string dataAddonPath = Path.GetFullPath("DataAddon");
                MelonLogger.Msg($"[SteamApiHijacker] cDlcDirector.Initialize Postfix - Scanning {dataAddonPath}...");

                if (Directory.Exists(dataAddonPath))
                {
                    Type dlcInfoType = dlcDirectorType.GetNestedType("cDlcInfo", BindingFlags.Public | BindingFlags.NonPublic);
                    foreach (string dir in Directory.GetDirectories(dataAddonPath))
                    {
                        string dirName = Path.GetFileName(dir);
                        if (int.TryParse(dirName, out int index))
                        {
                            object dlcInfoInstance = Activator.CreateInstance(dlcInfoType);
                            string fullPath = Path.GetFullPath(dir);
                            AccessTools.Field(dlcInfoType, "mMountPath").SetValue(dlcInfoInstance, fullPath);
                            dlcList[index] = dlcInfoInstance;
                            MelonLogger.Msg($"[SteamApiHijacker]   -> Detected & Populated DLC {index}: {fullPath}");
                        }
                    }
                }
                else
                {
                    MelonLogger.Warning($"[SteamApiHijacker] DataAddon folder not found at: {dataAddonPath}");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SteamApiHijacker] cDlcDirector.Initialize Postfix error: {ex}");
            }
        }

        #endregion
    }
}
