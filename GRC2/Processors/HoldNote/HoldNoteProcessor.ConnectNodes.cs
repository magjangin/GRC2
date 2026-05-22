using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GRC2.Parsers;
using MelonLoader;

namespace GRC2.Processors
{
    public static partial class HoldNoteProcessor
    {
        private sealed class HoldNoteFieldCache
        {
            public readonly FieldInfo PerfectSampleField = Helpers.FieldAccessHelper.GetCachedField("perfectSample");
            public readonly FieldInfo LaneLeftRightField = Helpers.FieldAccessHelper.GetCachedField("laneLeftRightID");
            public readonly FieldInfo SubLaneField = Helpers.FieldAccessHelper.GetCachedField("subLaneID");
            public readonly FieldInfo NoteTypeIdField = Helpers.FieldAccessHelper.GetCachedField("noteTypeID");
        }

        private sealed class HoldEndAttachContext
        {
            public Type NoteCreateDataType;
            public Type NoteDirectionIndexEnum;
            public Type NoteSizeEnum;
            public Func<BmsNote, object> CreateNoteCreateData;
            public Func<Type, string, object> GetEnumValue;
            public Action<object, string, object> SetFieldValue;
        }

        private enum HoldAttachResult
        {
            NotMatched,
            Matched,
            Skip
        }

        public static void ProcessHoldEndNotes(
            List<object> noteList,
            List<BmsNote> holdEndNotes,
            List<BmsNote> allBmsNotes,
            Type noteCreateDataType,
            Type noteDirectionIndexEnum,
            Type noteSizeEnum,
            Func<object, List<BmsNote>, BmsNote> getBmsNoteFromNoteCreateData,
            Func<BmsNote, object> createNoteCreateData,
            Func<Type, string, object> getEnumValue,
            Action<object, string, object> setFieldValue)
        {
            MelonLogger.Msg($"[HoldNoteProcessor] 홀드 끝 노트 처리 시작: {holdEndNotes.Count}개");
            MelonLogger.Msg($"[HoldNoteProcessor] noteList 개수: {noteList.Count}개");

            var fields = new HoldNoteFieldCache();
            var attachContext = new HoldEndAttachContext
            {
                NoteCreateDataType = noteCreateDataType,
                NoteDirectionIndexEnum = noteDirectionIndexEnum,
                NoteSizeEnum = noteSizeEnum,
                CreateNoteCreateData = createNoteCreateData,
                GetEnumValue = getEnumValue,
                SetFieldValue = setFieldValue
            };

            if (noteList.Count > 0)
                getBmsNoteFromNoteCreateData(noteList[0], allBmsNotes);

            var noteListBySample = BuildNoteListBySample(noteList, fields.PerfectSampleField);
            var holdStartMap = BuildHoldStartMap(
                noteListBySample,
                allBmsNotes,
                fields,
                getBmsNoteFromNoteCreateData,
                out int matchedCount,
                out int unmatchedCount);

            int totalStartCount = holdStartMap.Values.Sum(list => list.Count);
            MelonLogger.Msg($"[HoldNoteProcessor] 홀드 시작 노트 맵 생성 완료: {totalStartCount}개 (키 {holdStartMap.Count}개, noteList 매칭: {matchedCount}개, 매칭 실패: {unmatchedCount}개)");

            float baseBpm = holdEndNotes.FirstOrDefault()?.BaseBpm ?? 120f;
            float timeTolerance = CalculateTimeTolerance(baseBpm);
            MelonLogger.Msg($"[HoldNoteProcessor] BPM: {baseBpm}, 시간 오차 허용 범위: {timeTolerance:F4}초");

            var holdStartByLane = BuildHoldStartByLane(holdStartMap);
            var processedHoldStartsByEndKey = new Dictionary<string, object>();
            int successCount = 0;
            int failCount = 0;

            foreach (var holdEnd in holdEndNotes)
            {
                HoldAttachResult attachResult = AttachHoldEnd(
                    holdEnd,
                    holdStartMap,
                    holdStartByLane,
                    processedHoldStartsByEndKey,
                    timeTolerance,
                    attachContext);

                if (attachResult == HoldAttachResult.Skip)
                    continue;

                if (attachResult == HoldAttachResult.NotMatched)
                {
                    failCount++;
                    LogHoldEndMatchFailure(holdEnd, allBmsNotes, holdStartMap);
                }
                else
                {
                    successCount++;
                }
            }

            MelonLogger.Msg($"[HoldNoteProcessor] 홀드 끝 노트 매칭 완료: 성공={successCount}개, 실패={failCount}개");
            LogRemainingHoldStarts(holdStartMap);
        }
    }
}
