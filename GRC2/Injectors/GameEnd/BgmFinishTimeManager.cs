using System;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace GRC2.Injectors
{
    /// <summary>
    /// 게임 종료 시간 조정을 담당하는 클래스
    /// </summary>
    internal static class BgmFinishTimeManager
    {
        private static float _targetFinishTime = 0f;

        public static float GetTargetFinishTime()
        {
            return _targetFinishTime;
        }

        public static void SetFinishTime(float newBgmLength, Type bgmBeatManagerType)
        {
            try
            {
                // 노트 배열에서 마지막 노트 시간 계산
                float lastNoteTime = 0f;
                var assembly = bgmBeatManagerType.Assembly;
                var notesManagerType = assembly.GetType("IntiCreates.cFairyModeNotesManager");
                if (notesManagerType != null)
                {
                    var notesManagers = UnityEngine.Object.FindObjectsOfType(notesManagerType);
                    if (notesManagers != null && notesManagers.Length > 0)
                    {
                        var notesManagerInstance = notesManagers[0];
                        var noteArrayField = notesManagerType.GetField("mFairyNoteCreateDataArray", 
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (noteArrayField != null)
                        {
                            var noteArray = noteArrayField.GetValue(notesManagerInstance) as Array;
                            if (noteArray != null && noteArray.Length > 0)
                            {
                                int lastNoteSample = 0;
                                foreach (var noteObj in noteArray)
                                {
                                    if (noteObj != null)
                                    {
                                        var perfectSampleField = noteObj.GetType().GetField("perfectSample", 
                                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                                        if (perfectSampleField != null)
                                        {
                                            var sample = perfectSampleField.GetValue(noteObj);
                                            if (sample is int intSample && intSample > lastNoteSample)
                                            {
                                                lastNoteSample = intSample;
                                            }
                                        }
                                    }
                                }
                                lastNoteTime = lastNoteSample / 48000f;
                            }
                        }
                    }
                }
                
                // 종료 시간은 BGM 길이와 마지막 노트 시간 중 더 큰 값 사용
                float finishTime = Math.Max(newBgmLength, lastNoteTime);
                _targetFinishTime = finishTime;
                
                MelonLogger.Msg($"[BgmFinishTimeManager] 게임 종료 시간 설정: {finishTime:F3}초 (BGM: {newBgmLength:F3}초, 마지막 노트: {lastNoteTime:F3}초)");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BgmFinishTimeManager] 게임 종료 시간 설정 오류: {ex.Message}");
            }
        }

        public static void Reset()
        {
            _targetFinishTime = 0f;
        }
    }
}

