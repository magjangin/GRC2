using System;
using HarmonyLib;
using IntiCreates;
using MelonLoader;

namespace GRC2.Injectors
{
    /// <summary>
    /// 게임 종료 시간 조정을 담당하는 클래스
    /// </summary>
    internal static class BgmFinishTimeManager
    {
        private const float SampleRate = 48000f;

        private static readonly AccessTools.FieldRef<cFairyModeNotesManager, FairyNoteEditorLoader.NoteCreateData[]> NoteArrayRef =
            AccessTools.FieldRefAccess<cFairyModeNotesManager, FairyNoteEditorLoader.NoteCreateData[]>("mFairyNoteCreateDataArray");

        private static float _targetFinishTime = 0f;

        public static float GetTargetFinishTime()
        {
            return _targetFinishTime;
        }

        public static void Reset()
        {
            _targetFinishTime = 0f;
        }

        public static void SetFinishTime(float newBgmLength)
        {
            try
            {
                float lastNoteTime = GetLastNoteTime();

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

        /// <summary>
        /// 노트 배열에서 마지막 노트 시간을 계산합니다.
        /// </summary>
        private static float GetLastNoteTime()
        {
            var notesManager = UnityEngine.Object.FindObjectOfType<cFairyModeNotesManager>();
            if (notesManager == null)
            {
                return 0f;
            }

            var noteArray = NoteArrayRef(notesManager);
            if (noteArray == null || noteArray.Length == 0)
            {
                return 0f;
            }

            int lastNoteSample = 0;
            foreach (var note in noteArray)
            {
                if (note != null && note.perfectSample > lastNoteSample)
                {
                    lastNoteSample = note.perfectSample;
                }
            }

            return lastNoteSample / SampleRate;
        }
    }
}
