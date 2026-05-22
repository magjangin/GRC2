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
            if (noteList == null || noteList.Count == 0)
            {
                return;
            }

            try
            {
                int removedCount = 0;
                
                for (int i = noteList.Count - 1; i >= 0; i--)
                {
                    var note = noteList[i];
                    
                    if (note == null)
                    {
                        noteList.RemoveAt(i);
                        removedCount++;
                        continue;
                    }

                    bool isZeroTime = false;
                    
                    // perfectSample 체크
                    var perfectSample = Helpers.FieldAccessHelper.GetFieldValue(note, Helpers.FieldAccessHelper.FIELD_PERFECT_SAMPLE);
                    if (perfectSample == null)
                    {
                        isZeroTime = true;
                    }
                    else
                    {
                        try
                        {
                            int sample = Convert.ToInt32(perfectSample);
                            if (sample == 0)
                            {
                                isZeroTime = true;
                            }
                        }
                        catch (Exception)
                        {
                            // perfectSample을 int로 변환 불가 시 0이 아님으로 간주하지 않음
                        }
                    }
                    
                    // mSample 체크
                    if (!isZeroTime)
                    {
                        var mSample = Helpers.FieldAccessHelper.GetFieldValue(note, "mSample");
                        if (mSample != null)
                        {
                            try
                            {
                                int mSampleValue = Convert.ToInt32(mSample);
                                if (mSampleValue == 0)
                                {
                                    isZeroTime = true;
                                }
                            }
                            catch (Exception)
                            {
                                // mSample을 int로 변환 불가 시 스킵
                            }
                        }
                    }
                    
                    // Time 체크
                    if (!isZeroTime)
                    {
                        var time = Helpers.FieldAccessHelper.GetFieldValue(note, "Time");
                        if (time != null)
                        {
                            try
                            {
                                float timeValue = Convert.ToSingle(time);
                                if (Math.Abs(timeValue) < 0.0001f)
                                {
                                    isZeroTime = true;
                                }
                            }
                            catch (Exception)
                            {
                                // Time을 float로 변환 불가 시 스킵
                            }
                        }
                    }
                    
                    if (isZeroTime)
                    {
                        noteList.RemoveAt(i);
                        removedCount++;
                    }
                }

                if (removedCount > 0)
                {
                    MelonLogger.Warning($"[BmsNoteConverter] 0초 더미 노트 {removedCount}개 제거됨 (남은 노트: {noteList.Count}개)");
                }
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, "[BmsNoteConverter]", "FilterZeroTimeNotes 오류");
            }
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
