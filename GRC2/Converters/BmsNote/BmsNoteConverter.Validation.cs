using System;
using System.Collections.Generic;
using System.Linq;
using GRC2.Parsers;
using MelonLoader;


namespace GRC2.Converters
{
    public static partial class BmsNoteConverter
    {
    

        private static void FilterZeroTimeNotes(List<object> noteList)
        {
            if (noteList == null || noteList.Count == 0) return;

            try
            {
                int removedCount = 0;
                for (int i = noteList.Count - 1; i >= 0; i--)
                {
                    if (noteList[i] == null || IsNoteAtZeroTime(noteList[i]))
                    {
                        noteList.RemoveAt(i);
                        removedCount++;
                    }
                }

                if (removedCount > 0)
                    MelonLogger.Warning($"[BmsNoteConverter] 0초 더미 노트 {removedCount}개 제거됨 (남은 노트: {noteList.Count}개)");
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, "[BmsNoteConverter]", "FilterZeroTimeNotes 오류");
            }
        }

        private static bool IsNoteAtZeroTime(object note)
        {
            // perfectSample이 없거나 0이면 제로 타임 노트
            var rawPerfectSample = Helpers.FieldAccessHelper.GetFieldValue(note, Helpers.FieldAccessHelper.FIELD_PERFECT_SAMPLE);
            if (rawPerfectSample == null) return true;
            if (TryGetIntField(note, Helpers.FieldAccessHelper.FIELD_PERFECT_SAMPLE, out int perfectSample) && perfectSample == 0)
                return true;

            if (TryGetIntField(note, "mSample", out int mSample) && mSample == 0)
                return true;

            if (TryGetFloatField(note, "Time", out float time) && Math.Abs(time) < 0.0001f)
                return true;

            return false;
        }

        private static bool TryGetIntField(object obj, string fieldName, out int value)
        {
            value = 0;
            var raw = Helpers.FieldAccessHelper.GetFieldValue(obj, fieldName);
            if (raw == null) return false;
            try { value = Convert.ToInt32(raw); return true; }
            catch { return false; }
        }

        private static bool TryGetFloatField(object obj, string fieldName, out float value)
        {
            value = 0f;
            var raw = Helpers.FieldAccessHelper.GetFieldValue(obj, fieldName);
            if (raw == null) return false;
            try { value = Convert.ToSingle(raw); return true; }
            catch { return false; }
        }

        private static List<BmsNote> CheckMissingEndNotes(
            IEnumerable<BmsNote> startNotes,
            List<BmsNote> endNotes,
            string noteTypeName,
            string startNoteDescription,
            string endNoteDescription)
        {
            var missingEnds = new List<BmsNote>();
            const float TIME_TOLERANCE = 0.01f; // 시간 오차 허용 범위
            
            foreach (var startNote in startNotes)
            {
                var expectedEndTime = startNote.Time + startNote.Duration;
                var hasEndNote = endNotes.Any(end => 
                    Math.Abs(end.Time - expectedEndTime) < TIME_TOLERANCE &&
                    end.Lane == startNote.Lane &&
                    end.IsLeft == startNote.IsLeft);
                
                if (!hasEndNote)
                {
                    missingEnds.Add(startNote);
                }
            }
            
            if (missingEnds.Count > 0)
            {
                MelonLogger.Error("");
                MelonLogger.Error("═══════════════════════════════════════════════════════════════");
                MelonLogger.Error($"❌❌❌ BMS 노트 주입 실패: {noteTypeName} 끝 노트가 없습니다! ❌❌❌");
                MelonLogger.Error("═══════════════════════════════════════════════════════════════");
                MelonLogger.Error($"{noteTypeName} 시작 노트 {missingEnds.Count}개에 대해 끝 노트가 BMS 파일에 없습니다!");
                MelonLogger.Error("");
                MelonLogger.Error("🔍 BMS 파일을 다시 확인해보세요!");
                MelonLogger.Error($"   - {startNoteDescription}에 대응하는");
                MelonLogger.Error($"   - {endNoteDescription}가 있는지 확인하세요.");
                MelonLogger.Error("");
                foreach (var startNote in missingEnds)
                {
                    MelonLogger.Error($"   {noteTypeName} 시작: Time={startNote.Time:F3}초, Lane={startNote.Lane}, IsLeft={startNote.IsLeft}, Duration={startNote.Duration:F3}초");
                    MelonLogger.Error($"   예상 끝 시간: {startNote.Time + startNote.Duration:F3}초");
                }
                MelonLogger.Error("═══════════════════════════════════════════════════════════════");
                MelonLogger.Error("");
                return missingEnds; // 주입 금지
            }
            
            return null; // 모든 끝 노트가 있음
        }

        private static void SetLastNoteFlag(List<object> noteList)
        {
            if (noteList == null || noteList.Count == 0)
            {
                return;
            }

            try
            {
                int maxPerfectSample = int.MinValue;
                object lastNote = null;

                // 모든 노트와 connectNodeDataArray의 끝 노트를 확인하여 가장 큰 perfectSample 찾기
                foreach (var noteObj in noteList)
                {
                    var perfectSample = Helpers.FieldAccessHelper.GetFieldValue(noteObj, Helpers.FieldAccessHelper.FIELD_PERFECT_SAMPLE);
                    if (perfectSample != null)
                    {
                        int sample = (int)perfectSample;
                        if (sample > maxPerfectSample)
                        {
                            maxPerfectSample = sample;
                            lastNote = noteObj;
                        }
                    }

                    // connectNodeDataArray 확인
                    var connectNodeArray = Helpers.FieldAccessHelper.GetFieldValue(noteObj, Helpers.FieldAccessHelper.FIELD_CONNECT_NODE_DATA_ARRAY);
                    if (connectNodeArray != null && connectNodeArray is Array connectArray)
                    {
                        foreach (var connectNode in connectArray)
                        {
                            var connectPerfectSample = Helpers.FieldAccessHelper.GetFieldValue(connectNode, Helpers.FieldAccessHelper.FIELD_PERFECT_SAMPLE);
                            if (connectPerfectSample != null)
                            {
                                int connectSample = (int)connectPerfectSample;
                                if (connectSample > maxPerfectSample)
                                {
                                    maxPerfectSample = connectSample;
                                    lastNote = connectNode;
                                }
                            }
                        }
                    }
                }

                // 마지막 노트에 isLast 설정
                if (lastNote != null)
                {
                    Helpers.FieldAccessHelper.SetFieldValue(lastNote, Helpers.FieldAccessHelper.FIELD_IS_LAST, true);
                }
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, "[BmsNoteConverter]", "SetLastNoteFlag 오류");
            }
        }
}
}
