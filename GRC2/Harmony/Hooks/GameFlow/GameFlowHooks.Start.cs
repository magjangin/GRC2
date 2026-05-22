using GRC2.Harmony.Handlers;
using MelonLoader;
using System;
using System.Diagnostics;
using System.Reflection;

namespace GRC2.Harmony.Hooks
{
    public static partial class GameFlowHooks
    {
        /// <summary>
        /// startRythmGame prefix - 리듬 게임 시작 요청
        /// </summary>
        public static void StartRythmGamePrefix(object __instance)
        {
            Stopwatch totalSw = Stopwatch.StartNew();
            Stopwatch stepSw = new Stopwatch();
            try
            {
                LogStartRythmGameInvocationHeader(__instance);
                LogStartRythmGameCallStackSample();
                LogStartRythmGameActiveSceneName();
                LogStartRythmGameImportantInstanceFields(__instance);
                RunStartRythmGameAudioCleanupAndRestore(ref stepSw);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] startRythmGame 오류: {ex.Message}");
                MelonLogger.Warning($"[GameFlowHooks] 스택 트레이스: {ex.StackTrace}");
            }
            finally
            {
                totalSw.Stop();
                MelonLogger.Msg($"[GameFlowHooks] ⏱️ 전체 처리 시간: {totalSw.ElapsedMilliseconds}ms");
                MelonLogger.Msg("===========================================");
            }
        }

        /// <summary>
        /// coStartRythmGame prefix - 리듬 게임 시작 코루틴
        /// </summary>
        public static void CoStartRythmGamePrefix(object __instance)
        {
            try
            {
                MelonLogger.Msg("===========================================");
                MelonLogger.Msg("[GameFlowHooks] 🎮 coStartRythmGame() 호출됨");
                MelonLogger.Msg($"[GameFlowHooks]   인스턴스: {__instance?.GetType().Name ?? "null"}");
                MelonLogger.Msg("===========================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] coStartRythmGame 오류: {ex.Message}");
            }
        }

        private static void LogStartRythmGameInvocationHeader(object __instance)
        {
            MelonLogger.Msg("===========================================");
            MelonLogger.Msg("[GameFlowHooks] 🎮 startRythmGame() 호출됨");
            MelonLogger.Msg($"[GameFlowHooks]   인스턴스: {__instance?.GetType().Name ?? "null"}");
            MelonLogger.Msg($"[GameFlowHooks]   시간: {DateTime.Now:HH:mm:ss.fff}");
            MelonLogger.Msg("[GameFlowHooks]   설명: 곡 선택 화면에서 게임 시작 버튼 클릭");
        }

        private static void LogStartRythmGameCallStackSample()
        {
            try
            {
                StackTrace stackTrace = new StackTrace(true);
                MelonLogger.Msg("[GameFlowHooks] 📚 호출 스택:");
                for (int i = 1; i < Math.Min(4, stackTrace.FrameCount); i++)
                {
                    StackFrame frame = stackTrace.GetFrame(i);
                    if (frame == null) continue;
                    string methodName = frame.GetMethod()?.Name ?? "unknown";
                    string className = frame.GetMethod()?.DeclaringType?.Name ?? "unknown";
                    MelonLogger.Msg($"[GameFlowHooks]   [{i}] {className}.{methodName}()");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[GameFlowHooks]   호출 스택 읽기 실패: {ex.Message}");
            }
        }

        private static void LogStartRythmGameActiveSceneName()
        {
            try
            {
                string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                MelonLogger.Msg($"[GameFlowHooks] 🎬 현재 씬: {sceneName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[GameFlowHooks]   씬 정보 읽기 실패: {ex.Message}");
            }
        }

        private static void LogStartRythmGameImportantInstanceFields(object __instance)
        {
            if (__instance == null) return;
            try
            {
                Type instanceType = __instance.GetType();
                MelonLogger.Msg($"[GameFlowHooks] 🔍 인스턴스 필드 정보:");
                foreach (string fieldName in StartRythmGameImportantFields)
                    LogOneStartRythmImportantField(instanceType, __instance, fieldName);
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[GameFlowHooks]   필드 정보 읽기 실패: {ex.Message}");
            }
        }

        private static void LogOneStartRythmImportantField(Type instanceType, object instance, string fieldName)
        {
            try
            {
                FieldInfo field = instanceType.GetField(fieldName, InstanceFieldFlags);
                if (field == null) return;
                object value = field.GetValue(instance);
                string valueStr = value?.ToString() ?? "null";
                if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                MelonLogger.Msg($"[GameFlowHooks]   ⭐ {fieldName}: {valueStr}");
                if (fieldName == "mCurrentDispData" && value != null)
                    LogMusicIdUnderCurrentDispData(value);
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[GameFlowHooks]   ⚠️ {fieldName} 읽기 실패: {ex.Message}");
            }
        }

        private static void LogMusicIdUnderCurrentDispData(object dispData)
        {
            try
            {
                Type dispDataType = dispData.GetType();
                FieldInfo musicSelectDataField = dispDataType.GetField("mMusicSelectData", InstanceFieldFlags);
                if (musicSelectDataField == null) return;
                object musicSelectData = musicSelectDataField.GetValue(dispData);
                if (musicSelectData == null) return;
                Type musicSelectDataType = musicSelectData.GetType();
                FieldInfo musicIDField = musicSelectDataType.GetField("musicID", InstanceFieldFlags);
                if (musicIDField == null) return;
                object musicID = musicIDField.GetValue(musicSelectData);
                MelonLogger.Msg($"[GameFlowHooks]     🎵 musicID: {musicID}");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[GameFlowHooks]     mCurrentDispData 내부 musicID 읽기 실패: {ex.Message}");
            }
        }

        private static void RunStartRythmGameAudioCleanupAndRestore(ref Stopwatch stepSw)
        {
            stepSw.Restart();
            CustomBgmPlayer.Cleanup();
            stepSw.Stop();
            MelonLogger.Msg($"[GameFlowHooks] ✅ 커스텀 프리뷰 BGM 중지됨 ({stepSw.ElapsedMilliseconds}ms)");
            stepSw.Restart();
            PreviewAudioManager.RestoreMutedAudioSources();
            stepSw.Stop();
            MelonLogger.Msg($"[GameFlowHooks] ✅ 원본 오디오 소스 복원됨 ({stepSw.ElapsedMilliseconds}ms)");
        }
    }
}
