using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace GRC2.Injectors
{
    internal static class GameTypeSearcher
    {
        private static Type _bgmBeatManagerType;
        private static Type _rythmGameResultSceneUpdaterType;

        public static Type BgmBeatManagerType => _bgmBeatManagerType;
        public static Type RythmGameResultSceneUpdaterType => _rythmGameResultSceneUpdaterType;

        public static void SearchGameTypes()
        {
            try
            {
                var assembly = LoadAssembly();
                if (assembly == null)
                {
                    MelonLogger.Error("[GameTypeSearcher] Assembly-CSharp를 찾을 수 없습니다.");
                    return;
                }

                SearchBgmBeatManager(assembly);
                SearchRythmGameResultSceneUpdater(assembly);
                
                LogInitializationStatus();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[GameTypeSearcher] 게임 타입 탐색 오류: {ex.Message}");
            }
        }

        private static Assembly LoadAssembly()
        {
            var assemblyPath = Path.Combine(
                Path.GetDirectoryName(Application.dataPath),
                "GUNVOLT_RECORDS_Cychronicle_Data",
                "Managed",
                "Assembly-CSharp.dll"
            );

            if (File.Exists(assemblyPath))
            {
                return Assembly.LoadFrom(assemblyPath);
            }

            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
        }

        private static void SearchBgmBeatManager(Assembly assembly)
        {
            _bgmBeatManagerType = assembly.GetType("IntiCreates.cBGMBeatManager");
            if (_bgmBeatManagerType == null)
            {
                _bgmBeatManagerType = assembly.GetTypes().FirstOrDefault(t =>
                    t.Name == "cBGMBeatManager" ||
                    (t.Name.Contains("BGM") && t.Name.Contains("Beat") && t.Name.Contains("Manager")));
            }
        }

        private static void SearchRythmGameResultSceneUpdater(Assembly assembly)
        {
            _rythmGameResultSceneUpdaterType = assembly.GetType("IntiCreates.cRythmGameResultSceneUpdater");
            if (_rythmGameResultSceneUpdaterType == null)
            {
                _rythmGameResultSceneUpdaterType = assembly.GetTypes().FirstOrDefault(t =>
                    t.Name == "cRythmGameResultSceneUpdater" ||
                    (t.Name.Contains("Rythm") && t.Name.Contains("Game") && t.Name.Contains("Result") && t.Name.Contains("Scene") && t.Name.Contains("Updater")));
            }
        }

        /// <summary>
        /// 초기화 상태를 로그로 출력합니다.
        /// </summary>
        private static void LogInitializationStatus()
        {
            MelonLogger.Msg("[GameTypeSearcher] 초기화 완료 - 로드된 타입:");
            
            if (_bgmBeatManagerType != null)
                MelonLogger.Msg($"  - {_bgmBeatManagerType.Name} ({_bgmBeatManagerType.Namespace})");
            
            if (_rythmGameResultSceneUpdaterType != null)
                MelonLogger.Msg($"  - {_rythmGameResultSceneUpdaterType.Name} ({_rythmGameResultSceneUpdaterType.Namespace})");
        }
    }
}
