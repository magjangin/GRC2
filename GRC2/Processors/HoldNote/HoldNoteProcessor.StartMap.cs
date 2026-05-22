using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GRC2.Helpers;
using GRC2.Parsers;
using MelonLoader;

namespace GRC2.Processors
{
    public static partial class HoldNoteProcessor
    {
        private static Dictionary<int, List<object>> BuildNoteListBySample(List<object> noteList, FieldInfo perfectSampleField)
        {
            var noteListBySample = new Dictionary<int, List<object>>();

            foreach (var noteObj in noteList)
            {
                var perfectSample = perfectSampleField?.GetValue(noteObj);
                if (perfectSample == null)
                    continue;

                int sampleValue = (int)perfectSample;
                if (!noteListBySample.ContainsKey(sampleValue))
                    noteListBySample[sampleValue] = new List<object>();
                noteListBySample[sampleValue].Add(noteObj);
            }

            MelonLogger.Msg($"[HoldNoteProcessor] noteList 인덱싱 완료: {noteListBySample.Count}개 샘플 값");
            return noteListBySample;
        }

        private static Dictionary<string, List<(object Note, BmsNote BmsNote)>> BuildHoldStartMap(
            Dictionary<int, List<object>> noteListBySample,
            List<BmsNote> allBmsNotes,
            HoldNoteFieldCache fields,
            Func<object, List<BmsNote>, BmsNote> getBmsNoteFromNoteCreateData,
            out int matchedCount,
            out int unmatchedCount)
        {
            var holdStartMap = new Dictionary<string, List<(object Note, BmsNote BmsNote)>>();
            matchedCount = 0;
            unmatchedCount = 0;

            foreach (var holdStartBms in allBmsNotes.Where(n => n.Type == NoteType.Hold && n.Duration > 0))
            {
                var endSample = ToSampleIndex(holdStartBms.Time + holdStartBms.Duration);
                var key = BuildEndKey(holdStartBms.Lane, holdStartBms.IsLeft, endSample);
                object matchedNote = FindMatchingHoldStartNote(noteListBySample, holdStartBms, allBmsNotes, fields, getBmsNoteFromNoteCreateData);

                if (matchedNote != null)
                    matchedCount++;
                else
                    unmatchedCount++;

                AddHoldStart(holdStartMap, key, matchedNote, holdStartBms);
            }

            return holdStartMap;
        }

        private static object FindMatchingHoldStartNote(
            Dictionary<int, List<object>> noteListBySample,
            BmsNote holdStartBms,
            List<BmsNote> allBmsNotes,
            HoldNoteFieldCache fields,
            Func<object, List<BmsNote>, BmsNote> getBmsNoteFromNoteCreateData)
        {
            var expectedPerfectSample = ToSampleIndex(holdStartBms.Time);

            for (int offset = -2; offset <= 2; offset++)
            {
                int searchSample = expectedPerfectSample + offset;
                if (!noteListBySample.TryGetValue(searchSample, out var candidates))
                    continue;

                foreach (var noteObj in candidates)
                {
                    if (!IsHoldNoteCreateData(noteObj, allBmsNotes, fields.NoteTypeIdField, getBmsNoteFromNoteCreateData))
                        continue;

                    if (MatchesHoldLane(noteObj, holdStartBms, fields))
                        return noteObj;
                }
            }

            return null;
        }

        private static bool IsHoldNoteCreateData(
            object noteObj,
            List<BmsNote> allBmsNotes,
            FieldInfo noteTypeIdField,
            Func<object, List<BmsNote>, BmsNote> getBmsNoteFromNoteCreateData)
        {
            var noteTypeId = noteTypeIdField?.GetValue(noteObj);
            if (noteTypeId == null)
            {
                var bmsNote = getBmsNoteFromNoteCreateData(noteObj, allBmsNotes);
                return bmsNote != null && bmsNote.Type == NoteType.Hold;
            }

            var noteTypeIdStr = noteTypeId.ToString();
            return noteTypeIdStr != null && (noteTypeIdStr.Contains("Hold") || noteTypeIdStr == "Hold");
        }

        private static bool MatchesHoldLane(object noteObj, BmsNote holdStartBms, HoldNoteFieldCache fields)
        {
            var laneLeftRight = fields.LaneLeftRightField?.GetValue(noteObj);
            var subLane = fields.SubLaneField?.GetValue(noteObj);
            var expectedLaneLeftRight = EnumValueHelper.GetEnumValue(
                Loaders.GameTypeLoader.NoteLaneLeftRightEnum,
                holdStartBms.IsLeft ? EnumValueHelper.GetEnumLeft() : EnumValueHelper.GetEnumRight());
            var expectedSubLane = EnumValueHelper.GetSubLaneType(holdStartBms.Lane);

            return laneLeftRight != null &&
                expectedLaneLeftRight != null &&
                laneLeftRight.Equals(expectedLaneLeftRight) &&
                subLane != null &&
                expectedSubLane != null &&
                subLane.Equals(expectedSubLane);
        }

        private static Dictionary<(int Lane, bool IsLeft), List<(string Key, object Note, BmsNote BmsNote, int EndSample)>> BuildHoldStartByLane(
            Dictionary<string, List<(object Note, BmsNote BmsNote)>> holdStartMap)
        {
            var holdStartByLane = new Dictionary<(int Lane, bool IsLeft), List<(string Key, object Note, BmsNote BmsNote, int EndSample)>>();
            foreach (var kvp in holdStartMap)
            {
                foreach (var (noteObj, bmsNote) in kvp.Value)
                {
                    var laneKey = (bmsNote.Lane, bmsNote.IsLeft);
                    if (!holdStartByLane.ContainsKey(laneKey))
                        holdStartByLane[laneKey] = new List<(string, object, BmsNote, int)>();
                    holdStartByLane[laneKey].Add((kvp.Key, noteObj, bmsNote, ToSampleIndex(bmsNote.Time + bmsNote.Duration)));
                }
            }

            return holdStartByLane;
        }

        private static void AddHoldStart(Dictionary<string, List<(object Note, BmsNote BmsNote)>> holdStartMap, string key, object noteObj, BmsNote bmsNote)
        {
            if (!holdStartMap.ContainsKey(key))
                holdStartMap[key] = new List<(object, BmsNote)>();
            holdStartMap[key].Add((noteObj, bmsNote));
        }

        private static void AddHoldStartAtFront(Dictionary<string, List<(object Note, BmsNote BmsNote)>> holdStartMap, string key, object noteObj, BmsNote bmsNote)
        {
            if (!holdStartMap.ContainsKey(key))
                holdStartMap[key] = new List<(object, BmsNote)>();
            holdStartMap[key].Insert(0, (noteObj, bmsNote));
        }
    }
}
