using System;
using System.IO;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using IntiCreates;
using MelonLoader;
using Steamworks;

namespace GRC2.Helpers
{
    /// <summary>
    /// Steamworks API 및 게임 내부 DLC 검증 메서드를 하이재킹하는 클래스
    /// </summary>
    public static class SteamApiHijacker
    {
        /// <summary>
        /// Steam API 초기화 실패 여부
        /// </summary>
        public static bool IsBypassed { get; private set; } = false;

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

        [HarmonyPatch(typeof(SteamAPI), "RestartAppIfNecessary")]
        private static class RestartAppIfNecessaryPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(ref bool __result)
            {
                return RestartAppIfNecessaryPrefix(ref __result);
            }
        }

        [HarmonyPatch(typeof(SteamAPI), "Init")]
        private static class SteamApiInitPatch
        {
            [HarmonyPostfix]
            private static void Postfix(ref bool __result)
            {
                InitPostfix(ref __result);
            }
        }

        [HarmonyPatch(typeof(SteamAPI), "RunCallbacks")]
        private static class RunCallbacksPatch
        {
            [HarmonyPrefix]
            private static bool Prefix()
            {
                return RunCallbacksPrefix();
            }
        }

        [HarmonyPatch(typeof(SteamAPI), "Shutdown")]
        private static class ShutdownPatch
        {
            [HarmonyPrefix]
            private static bool Prefix()
            {
                return ShutdownPrefix();
            }
        }

        [HarmonyPatch(typeof(SteamApps), "BIsDlcInstalled")]
        private static class IsDlcInstalledPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(ref bool __result)
            {
                return BIsDlcInstalledPrefix(ref __result);
            }
        }

        [HarmonyPatch(typeof(IntiCreates.Application), "isDLCEnable")]
        private static class IsDlcEnablePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(ref bool __result)
            {
                return IsDlcEnablePrefix(ref __result);
            }
        }

        [HarmonyPatch(typeof(sAddressableDirector), "isUsableDlc")]
        private static class IsUsableDlcPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(ref bool __result)
            {
                return IsUsableDlcPrefix(ref __result);
            }
        }

        [HarmonyPatch(typeof(sAddressableDirector), "isNotYetPurchased")]
        private static class AddressableIsNotYetPurchasedPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(ref bool __result)
            {
                return IsNotYetPurchasedPrefix(ref __result);
            }
        }

        [HarmonyPatch(typeof(sAddressableDirector), "coCheckDLC")]
        private static class CheckDlcPatch
        {
            [HarmonyPostfix]
            private static void Postfix()
            {
                CoCheckDLCPostfix();
            }
        }

        [HarmonyPatch(typeof(cDlcDirector), "IsNotYetPurchased")]
        private static class DlcDirectorIsNotYetPurchasedPatch
        {
            [HarmonyPrefix]
            private static bool Prefix(ref bool __result)
            {
                return IsNotYetPurchasedPrefix(ref __result);
            }
        }

        [HarmonyPatch(typeof(cDlcDirector), "Initialize")]
        private static class DlcDirectorInitializePatch
        {
            [HarmonyPostfix]
            private static void Postfix(object __instance)
            {
                InitializePostfix(__instance);
            }
        }

        #endregion
    }
}
