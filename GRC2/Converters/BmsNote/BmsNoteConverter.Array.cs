using System;
using System.Collections.Generic;
using System.Reflection;
using GRC2.Parsers;
using MelonLoader;

namespace GRC2.Converters
{
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
}
