using System;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;


namespace GRC2.Injectors
{
    internal static partial class BgmGameEndMonitor
    {
        public static void AdjustMusicDataOnSceneLoad()
        {
            try
            {
                // 씬 로드 시 캐시 초기화 (새 인스턴스일 수 있음)
                ClearCache();
                
                // cRythmGameManager 인스턴스 찾기
                var assembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
                
                if (assembly == null)
                {
                    return;
                }
                
                var rythmGameManagerType = assembly.GetType("IntiCreates.cRythmGameManager");
                if (rythmGameManagerType == null)
                {
                    return;
                }
                
                var rythmGameManagers = UnityEngine.Object.FindObjectsOfType(rythmGameManagerType);
                if (rythmGameManagers == null || rythmGameManagers.Length == 0)
                {
                    return;
                }
                
                var rythmGameManager = rythmGameManagers[0];
                AdjustMusicDataForBgmLength(rythmGameManager, logAdjustment: false);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BgmGameEndMonitor] AdjustMusicDataOnSceneLoad 오류: {ex.Message}");
            }
        }

        private static void AdjustMusicDataForBgmLength(object instance, bool logAdjustment = false)
        {
            try
            {
                var instanceType = instance.GetType();
                float targetTime = ResolveTargetFinishTime(instanceType);
                if (targetTime <= 0f)
                {
                    return;
                }

                var musicDataField = _cachedMusicDataField ?? instanceType.GetField("mRythmGameMusicData",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (musicDataField == null)
                {
                    return;
                }

                var musicData = musicDataField.GetValue(instance);
                if (musicData == null)
                {
                    return;
                }

                CacheFieldInfo(instanceType, musicData);
                ApplyMusicDataSampleAdjustments(musicData, targetTime, logAdjustment);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BgmGameEndMonitor] AdjustMusicDataForBgmLength 오류: {ex.Message}");
                MelonLogger.Error($"[BgmGameEndMonitor] 스택 트레이스: {ex.StackTrace}");
            }
        }

        private static float ResolveTargetFinishTime(Type instanceType)
        {
            float targetTime = BgmFinishTimeManager.GetTargetFinishTime();
            if (targetTime > 0f)
            {
                return targetTime;
            }

            var bgmInstance = GetBgmManagerInstance(instanceType);
            if (bgmInstance == null || _cachedBgmManagerType == null)
            {
                return 0f;
            }

            var getAudioClipMethod = GetCachedMethod(_cachedBgmManagerType, "getAudioClip",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var clip = getAudioClipMethod?.Invoke(bgmInstance, null) as AudioClip;
            return clip?.length ?? 0f;
        }

        public static void MonitorGameEndPostfix(object __instance)
        {
            try
            {
                AdjustMusicDataForBgmLength(__instance);

                var instanceType = __instance.GetType();
                float targetTime = BgmFinishTimeManager.GetTargetFinishTime();
                UpdateKudosBoostEndSample(__instance, instanceType, targetTime);
                LogPeriodicBgmEndState(__instance, instanceType);
            }
            catch
            {
                // 조용히 실패 (너무 많은 로그 방지)
            }
        }

        
        public static void ClearGameEndPrefix(object __instance)
        {
            // 로그 제거됨
        }

        public static void GenericGameEndPrefix(object __instance, MethodBase __originalMethod)
        {
            try
            {
                MelonLogger.Msg($"[BgmInjectorHooks] ⚠ 게임 종료/클리어 코루틴 호출: {__originalMethod.Name}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BgmInjectorHooks] GenericGameEndPrefix 오류: {ex.Message}");
            }
        }
    }
}
