using System;
using System.IO;
using Microsoft.Win32;
using MelonLoader;

namespace GRC2.Helpers
{
    public static class SteamManifestLocker
    {
        private const string GameAppId = "2585040"; // GUNVOLT RECORDS Cychronicle의 Steam App ID
        private const string ManifestFileName = "appmanifest_{0}.acf";

        /// <summary>
        /// Steam 설치 경로를 레지스트리에서 찾습니다.
        /// </summary>
        private static string FindSteamPath()
        {
            try
            {
                // 64비트 레지스트리 경로 시도
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam"))
                {
                    if (key != null)
                    {
                        var steamPath = key.GetValue("InstallPath") as string;
                        if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
                        {
                            return steamPath;
                        }
                    }
                }

                // 32비트 레지스트리 경로 시도
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                {
                    if (key != null)
                    {
                        var steamPath = key.GetValue("InstallPath") as string;
                        if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
                        {
                            return steamPath;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GRC2] [SteamManifestLock] 레지스트리에서 Steam 경로 찾기 실패: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 게임 실행 파일 경로에서 Steam 경로를 역으로 찾습니다.
        /// </summary>
        private static string FindSteamPathFromGame()
        {
            try
            {
                var gameDataPath = UnityEngine.Application.dataPath;
                if (string.IsNullOrEmpty(gameDataPath))
                    return null;

                var currentPath = Path.GetDirectoryName(gameDataPath);
                
                // steamapps 폴더를 찾을 때까지 상위 디렉토리로 이동
                while (!string.IsNullOrEmpty(currentPath))
                {
                    var steamappsPath = Path.Combine(currentPath, "steamapps");
                    if (Directory.Exists(steamappsPath))
                    {
                        return currentPath; // Steam 설치 경로 반환
                    }

                    var parent = Directory.GetParent(currentPath);
                    if (parent == null)
                        break;

                    currentPath = parent.FullName;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GRC2] [SteamManifestLock] 게임 경로에서 Steam 경로 찾기 실패: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 매니페스트 파일 경로를 찾습니다.
        /// </summary>
        private static string FindManifestPath()
        {
            // 1. 레지스트리에서 Steam 경로 찾기
            var steamPath = FindSteamPath();
            if (!string.IsNullOrEmpty(steamPath))
            {
                var manifestPath = Path.Combine(steamPath, "steamapps", string.Format(ManifestFileName, GameAppId));
                if (File.Exists(manifestPath))
                {
                    MelonLogger.Msg($"[GRC2] [SteamManifestLock] 레지스트리에서 Steam 경로 찾음: {steamPath}");
                    return manifestPath;
                }
            }

            // 2. 게임 실행 파일 경로에서 Steam 경로 찾기
            steamPath = FindSteamPathFromGame();
            if (!string.IsNullOrEmpty(steamPath))
            {
                var manifestPath = Path.Combine(steamPath, "steamapps", string.Format(ManifestFileName, GameAppId));
                if (File.Exists(manifestPath))
                {
                    MelonLogger.Msg($"[GRC2] [SteamManifestLock] 게임 경로에서 Steam 경로 찾음: {steamPath}");
                    return manifestPath;
                }
            }

            return null;
        }

        public static void LockManifest()
        {
            try
            {
                // 매니페스트 파일 경로 찾기
                var manifestPath = FindManifestPath();
                
                if (string.IsNullOrEmpty(manifestPath))
                {
                    MelonLogger.Warning($"[GRC2] [SteamManifestLock] 매니페스트 파일을 찾을 수 없습니다. (App ID: {GameAppId})");
                    MelonLogger.Warning("[GRC2] [SteamManifestLock] Steam 설치 경로를 찾지 못했거나 게임이 Steam 라이브러리에 설치되어 있지 않을 수 있습니다.");
                    return;
                }

                MelonLogger.Msg($"[GRC2] [SteamManifestLock] 매니페스트 파일 발견: {manifestPath}");

                // 파일 속성 확인 및 읽기 전용 설정
                FileAttributes attributes = File.GetAttributes(manifestPath);
                if ((attributes & FileAttributes.ReadOnly) != FileAttributes.ReadOnly)
                {
                    File.SetAttributes(manifestPath, attributes | FileAttributes.ReadOnly);
                    MelonLogger.Msg($"[GRC2] [SteamManifestLock] 매니페스트 파일을 읽기 전용으로 설정했습니다: {manifestPath}");
                }
                else
                {
                    MelonLogger.Msg("[GRC2] [SteamManifestLock] 매니페스트 파일이 이미 읽기 전용입니다.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[GRC2] [SteamManifestLock] 매니페스트 파일 잠금 실패: {ex.Message}");
            }
        }
    }
}
