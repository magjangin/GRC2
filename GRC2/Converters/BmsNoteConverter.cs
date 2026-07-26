using System;
using System.Collections.Generic;
using System.Linq;
using GRC2.Parsers;
using GRC2.Processors;
using MelonLoader;
using System.Reflection;

namespace GRC2.Converters
{
    /// <summary>
    /// BMS 노트를 게임 노트로 변환하는 메인 클래스
    /// </summary>
    public static partial class BmsNoteConverter
    {
        // 상세 로깅 플래그 (성능 최적화용, 기본 비활성화)
        private static readonly bool EnableDetailedHoldNoteLogging = false;

        /// <summary>
        /// BMS 노트를 게임의 NoteCreateData 배열로 변환합니다.
        /// </summary>
        /// <param name="bmsNotes">변환할 BMS 노트 리스트</param>
        /// <returns>NoteCreateData 타입의 배열 (타입: NoteCreateData[]). 변환 실패 시 null 반환</returns>
        public static Array ConvertBmsNotesToNoteCreateData(List<BmsNote> bmsNotes)
        {
            // 타입 안전성: 초기화 검증
            if (Loaders.GameTypeLoader.NoteCreateDataType == null)
            {
                MelonLogger.Error("[BmsNoteConverter] NoteCreateData 타입을 확인할 수 없습니다.");
                return null;
            }

            // 타입 안전성: 입력 검증
            if (bmsNotes == null)
            {
                MelonLogger.Error("[BmsNoteConverter] bmsNotes가 null입니다.");
                return null;
            }

            try
            {
                MelonLogger.Msg($"[BmsNoteConverter] BMS 노트 변환 시작: {bmsNotes.Count}개");

                // 성능 최적화: 캐시 초기화
                Builders.NoteCreateDataBuilder.ClearCache();

                var noteList = new List<object>();
                var holdEndNotes = new List<BmsNote>(); // 홀드 끝 노트 저장
                var fairyEndNotes = new List<BmsNote>(); // 페어리 끝 노트 저장

                // 성능 최적화: 한 번만 정렬 (Time 기준)
                var sortedBmsNotes = bmsNotes.OrderBy(n => n.Time).ToList();
                int convertedCount = 0;
                int skippedCount = 0;
                
                foreach (var bmsNote in sortedBmsNotes)
                {
                    try
                    {
                        // 홀드/페어리 끝 노트는 별도 처리
                        if (bmsNote.Type == NoteType.HoldEnd)
                        {
                            holdEndNotes.Add(bmsNote);
                            skippedCount++;
                            continue;
                        }
                        if (bmsNote.Type == NoteType.FairyEnd)
                        {
                            fairyEndNotes.Add(bmsNote);
                            skippedCount++;
                            continue;
                        }

                        var noteCreateData = Builders.NoteCreateDataBuilder.CreateNoteCreateData(bmsNote);
                        if (noteCreateData != null)
                        {
                            noteList.Add(noteCreateData);
                            convertedCount++;
                        }
                        else
                        {
                            MelonLogger.Warning($"[BmsNoteConverter] 노트 변환 실패: Time={bmsNote.Time}, Lane={bmsNote.Lane}, Type={bmsNote.Type}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Helpers.ErrorLogger.LogException(ex, "[BmsNoteConverter]", $"노트 변환 중 오류 (Time={bmsNote?.Time}, Type={bmsNote?.Type})");
                    }
                }

                // 홀드 끝 노트 체크: 홀드 시작 노트가 있는데 끝 노트가 없는 경우 주입 금지
                var missingHoldEnds = CheckMissingEndNotes(
                    bmsNotes.Where(n => n.Type == NoteType.Hold),
                    holdEndNotes,
                    "홀드",
                    "홀드 시작 노트(02 채널)",
                    "홀드 끝 노트(19 채널)");
                
                if (missingHoldEnds != null)
                {
                    return null; // 주입 금지
                }

                ProcessHoldEndNotes(noteList, holdEndNotes, bmsNotes);

                // 페어리 끝 노트 체크: 매칭된 페어리 시작(Duration>0)에 대해 끝이 있는지만 검사
                var missingFairyEnds = CheckMissingEndNotes(
                    bmsNotes.Where(n => n.Type == NoteType.Fairy && n.Duration > 0),
                    fairyEndNotes,
                    "페어리",
                    "페어리 시작 노트(11-18 채널)",
                    "페어리 끝 노트(1A-1B 채널)");
                
                if (missingFairyEnds != null)
                {
                    return null; // 주입 금지
                }

                ProcessFairyEndNotes(noteList, fairyEndNotes, bmsNotes);

                // 0초 더미 노트 필터링
                try
                {
                    FilterZeroTimeNotes(noteList);
                }
                catch (Exception ex)
                {
                    Helpers.ErrorLogger.LogWarning(ex, "[BmsNoteConverter]", "0초 노트 필터링 중 오류");
                }

                // 마지막 노트 찾기 및 isLast 설정
                try
                {
                    SetLastNoteFlag(noteList);
                }
                catch (Exception ex)
                {
                    Helpers.ErrorLogger.LogWarning(ex, "[BmsNoteConverter]", "마지막 노트 설정 중 오류");
                }

                return CreateTypedNoteArray(noteList, bmsNotes);
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, "[BmsNoteConverter]", "변환 오류");
                return null;
            }
        }
    }

    public static partial class BmsNoteConverter
    {
        private static Array CreateTypedNoteArray(List<object> noteList, List<BmsNote> bmsNotes)
        {
            try
            {
                if (Loaders.GameTypeLoader.NoteCreateDataType == null)
                {
                    MelonLogger.Error("[BmsNoteConverter] NoteCreateDataType이 null입니다. 배열을 생성할 수 없습니다.");
                    return null;
                }

                MelonLogger.Msg($"[BmsNoteConverter] 배열 생성: noteList.Count={noteList.Count}");
                var array = Array.CreateInstance(Loaders.GameTypeLoader.NoteCreateDataType, noteList.Count);
                int holdNoteCount = PopulateTypedNoteArray(array, noteList, bmsNotes);

                if (EnableDetailedHoldNoteLogging)
                {
                    MelonLogger.Msg($"[BmsNoteConverter] 배열에 포함된 홀드 노트: {holdNoteCount}개 (전체 noteList: {noteList.Count}개)");
                }

                if (array.GetType().GetElementType() != Loaders.GameTypeLoader.NoteCreateDataType)
                {
                    MelonLogger.Error($"[BmsNoteConverter] 생성된 배열의 요소 타입이 일치하지 않습니다. 예상: {Loaders.GameTypeLoader.NoteCreateDataType.Name}, 실제: {array.GetType().GetElementType()?.Name ?? "null"}");
                    return null;
                }

                MelonLogger.Msg($"[BmsNoteConverter] 변환 완료: {noteList.Count}개 노트 (타입: {Loaders.GameTypeLoader.NoteCreateDataType.Name}[])");
                return array;
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, "[BmsNoteConverter]", "배열 변환 중 오류");
                return null;
            }
        }

        private static int PopulateTypedNoteArray(Array array, List<object> noteList, List<BmsNote> bmsNotes)
        {
            int holdNoteCount = 0;
            for (int i = 0; i < noteList.Count; i++)
            {
                var noteObj = noteList[i];
                if (noteObj == null)
                {
                    MelonLogger.Warning($"[BmsNoteConverter] 인덱스 {i}의 노트가 null입니다. 건너뜁니다.");
                    continue;
                }

                if (!Loaders.GameTypeLoader.NoteCreateDataType.IsInstanceOfType(noteObj))
                {
                    MelonLogger.Error($"[BmsNoteConverter] 인덱스 {i}의 노트 타입이 일치하지 않습니다. 예상: {Loaders.GameTypeLoader.NoteCreateDataType.Name}, 실제: {noteObj.GetType().Name}");
                    continue;
                }

                array.SetValue(noteObj, i);
                if (EnableDetailedHoldNoteLogging && IsHoldNoteForDebug(noteObj, bmsNotes))
                {
                    holdNoteCount++;
                    LogHoldNoteConnectionForDebug(noteObj, holdNoteCount);
                }
            }

            return holdNoteCount;
        }

        private static bool IsHoldNoteForDebug(object noteObj, List<BmsNote> bmsNotes)
        {
            var bmsNote = Builders.NoteCreateDataBuilder.GetBmsNoteFromNoteCreateData(noteObj, bmsNotes);
            if (bmsNote != null && bmsNote.Type == NoteType.Hold)
            {
                return true;
            }

            var noteTypeIdField = Loaders.GameTypeLoader.NoteCreateDataType.GetField("noteTypeID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var noteTypeId = noteTypeIdField?.GetValue(noteObj);
            var noteTypeIdStr = noteTypeId?.ToString();
            return noteTypeIdStr != null && (noteTypeIdStr.Contains("Hold") || noteTypeIdStr == "Hold");
        }

        private static void LogHoldNoteConnectionForDebug(object noteObj, int holdNoteCount)
        {
            var connectArrayField = Loaders.GameTypeLoader.NoteCreateDataType.GetField("connectNodeDataArray",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (connectArrayField == null)
                return;

            var connectArray = connectArrayField.GetValue(noteObj) as Array;
            var perfectSampleField = Loaders.GameTypeLoader.NoteCreateDataType.GetField("perfectSample",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var startPerfectSample = perfectSampleField?.GetValue(noteObj);

            if (connectArray != null && connectArray.Length > 0)
            {
                var endNote = connectArray.GetValue(0);
                var endPerfectSample = perfectSampleField?.GetValue(endNote);
                MelonLogger.Msg($"[BmsNoteConverter] 홀드 노트[{holdNoteCount}]: perfectSample={startPerfectSample}, connectNodeDataArray.Length={connectArray.Length}, 끝 노트 perfectSample={endPerfectSample}");
            }
            else
            {
                MelonLogger.Warning($"[BmsNoteConverter] ⚠️ 홀드 노트[{holdNoteCount}]: perfectSample={startPerfectSample}, connectNodeDataArray가 비어있거나 null입니다!");
            }
        }
    }

    public static partial class BmsNoteConverter
    {
        private static void ProcessHoldEndNotes(List<object> noteList, List<BmsNote> holdEndNotes, List<BmsNote> bmsNotes)
        {
            try
            {
                HoldNoteProcessor.ProcessHoldEndNotes(
                    noteList,
                    holdEndNotes,
                    bmsNotes,
                    Loaders.GameTypeLoader.NoteCreateDataType,
                    Loaders.GameTypeLoader.NoteDirectionIndexEnum,
                    Loaders.GameTypeLoader.NoteSizeEnum,
                    Builders.NoteCreateDataBuilder.GetBmsNoteFromNoteCreateData,
                    Builders.NoteCreateDataBuilder.CreateNoteCreateData,
                    Helpers.EnumValueHelper.GetEnumValue,
                    Helpers.FieldAccessHelper.SetFieldValue);
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, "[BmsNoteConverter]", "홀드 끝 노트 처리 중 오류");
            }
        }

        private static void ProcessFairyEndNotes(List<object> noteList, List<BmsNote> fairyEndNotes, List<BmsNote> bmsNotes)
        {
            try
            {
                FairyNoteProcessor.ProcessFairyEndNotes(
                    noteList,
                    fairyEndNotes,
                    bmsNotes,
                    Loaders.GameTypeLoader.NoteCreateDataType,
                    Loaders.GameTypeLoader.NoteDirectionIndexEnum,
                    Loaders.GameTypeLoader.NoteSizeEnum,
                    Builders.NoteCreateDataBuilder.GetBmsNoteFromNoteCreateData,
                    Builders.NoteCreateDataBuilder.CreateNoteCreateData,
                    Helpers.EnumValueHelper.GetEnumValue,
                    Helpers.FieldAccessHelper.SetFieldValue);
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, "[BmsNoteConverter]", "페어리 끝 노트 처리 중 오류");
            }
        }
    }

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
