using System;
using System.IO;
using HarmonyLib;
using IntiCreates;
using MelonLoader;
using Steamworks;

namespace GRC2.Harmony
{
    /// <summary>
    /// Steamworks API 및 게임 내부 DLC 검증 메서드를 하이재킹하는 클래스
    /// </summary>
    public static class SteamApiHijacker
    {
        /// <summary>
        /// SteamAPI.Init의 결과. null이면 아직 호출되지 않은 것입니다. Init 실패를 true로 바꾸지 않습니다(A3).
        /// </summary>
        public static bool? SteamInitResult { get; private set; }

        #region Harmony Patches

        public static bool RestartAppIfNecessaryPrefix(ref bool __result)
        {
            __result = false; // 스팀 강제 재시작 방지
            return false;
        }

        public static void InitPostfix(ref bool __result)
        {
            // 원본 게임은 Init 실패를 로그만 남기고 계속 진행합니다. 여기서 true로 바꾸면 SteamManager.Initialized가 켜져
            // cDlcDirector 등이 초기화되지 않은 Steamworks를 호출해 예외가 납니다(알려진 문제 A3). 그래서 결과를 그대로 둡니다.
            SteamInitResult = __result;
            if (!__result)
            {
                MelonLogger.Msg("[SteamApiHijacker] SteamAPI.Init returned false. 결과는 그대로 두고, 콜백/Shutdown 호출만 건너뜁니다.");
            }
        }

        /// <summary>Init이 성공했을 때만 원본 콜백을 실행합니다. 초기화되지 않은 Steamworks 콜백은 예외를 냅니다.</summary>
        public static bool RunCallbacksPrefix()
        {
            return SteamInitResult == true;
        }

        /// <summary>Init이 성공했을 때만 원본 Shutdown을 실행합니다.</summary>
        public static bool ShutdownPrefix()
        {
            return SteamInitResult == true;
        }

        /// <summary>
        /// Init이 실패한 환경(스팀 미실행, Goldberg 없음)에서는 Steamworks가 언어를 읽을 수 없어 예외가 납니다.
        /// 그때만 OS 언어로 대신 답합니다. Init을 아직 호출하지 않았다면 원본을 그대로 씁니다.
        /// </summary>
        public static string FallbackGameLanguage()
        {
            return System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko" ? "korean" : "english";
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

        public static void InitializePostfix(cDlcDirector __instance)
        {
            try
            {
                var dlcList = __instance.DlcList;
                if (dlcList == null)
                {
                    MelonLogger.Warning("[SteamApiHijacker] cDlcDirector.DlcList가 null이라 DLC를 등록하지 못했습니다.");
                    return;
                }

                // 작업 폴더 기준(Path.GetFullPath)으로 찾으면 런처나 바로가기로 실행할 때 DataAddon을 못 찾습니다.
                // 게임 폴더(dataPath의 상위)를 기준으로 찾습니다(A6).
                string gameFolder = Path.GetDirectoryName(UnityEngine.Application.dataPath);
                string dataAddonPath = Path.Combine(gameFolder, "DataAddon");
                MelonLogger.Msg($"[SteamApiHijacker] cDlcDirector.Initialize Postfix - Scanning {dataAddonPath}...");

                if (!Directory.Exists(dataAddonPath))
                {
                    MelonLogger.Warning($"[SteamApiHijacker] DataAddon folder not found at: {dataAddonPath}");
                    return;
                }

                foreach (string dir in Directory.GetDirectories(dataAddonPath))
                {
                    // 폴더 하나가 실패해도 나머지 DLC 등록은 계속합니다(H6).
                    try
                    {
                        string dirName = Path.GetFileName(dir);
                        if (!int.TryParse(dirName, out int index))
                        {
                            MelonLogger.Msg($"[SteamApiHijacker]   -> 숫자가 아닌 폴더는 건너뜁니다: {dirName}");
                            continue;
                        }

                        string fullPath = Path.GetFullPath(dir);
                        dlcList[index] = new cDlcDirector.cDlcInfo { mMountPath = fullPath };
                        MelonLogger.Msg($"[SteamApiHijacker]   -> Detected & Populated DLC {index}: {fullPath}");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"[SteamApiHijacker] DLC 폴더 등록 실패 ({dir}): {ex.Message}");
                    }
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
            private static void Postfix(cDlcDirector __instance)
            {
                InitializePostfix(__instance);
            }
        }

        [HarmonyPatch(typeof(SteamApps), "GetCurrentGameLanguage")]
        private static class GetCurrentGameLanguagePatch
        {
            [HarmonyPrefix]
            private static bool Prefix(ref string __result)
            {
                // Init이 아직 호출되지 않았다면(null) 원본을 그대로 씁니다.
                if (SteamInitResult != false)
                    return true;

                __result = FallbackGameLanguage();
                return false;
            }
        }

        #endregion
    }
}
