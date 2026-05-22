using System;
using System.Collections.Generic;
using System.Linq;
using GRC2.Parsers;
using MelonLoader;


namespace GRC2.Processors
{
    public static partial class FairyNoteProcessor
    {
    

        private static Dictionary<(int Lane, bool IsLeft), List<FairyStartLookupEntry>> BuildFairyStartLookup(
            List<object> noteList,
            List<BmsNote> allBmsNotes,
            Func<object, List<BmsNote>, BmsNote> getBmsNoteFromNoteCreateData)
        {
            var lookup = new Dictionary<(int Lane, bool IsLeft), List<FairyStartLookupEntry>>();

            foreach (var noteObj in noteList)
            {
                if (noteObj == null) continue;

                var bmsNote = getBmsNoteFromNoteCreateData(noteObj, allBmsNotes);
                if (bmsNote == null || bmsNote.Type != NoteType.Fairy) continue;

                var key = (bmsNote.Lane, bmsNote.IsLeft);
                if (!lookup.TryGetValue(key, out var entries))
                {
                    entries = new List<FairyStartLookupEntry>();
                    lookup[key] = entries;
                }

                entries.Add(new FairyStartLookupEntry
                {
                    NoteObject = noteObj,
                    BmsNote = bmsNote,
                    Sample = ToSampleIndex(bmsNote.Time)
                });
            }

            foreach (var entries in lookup.Values)
            {
                entries.Sort((left, right) => left.Sample.CompareTo(right.Sample));
            }

            return lookup;
        }

        public static void ProcessFairyEndNotes(
            List<object> noteList,
            List<BmsNote> fairyEndNotes,
            List<BmsNote> allBmsNotes,
            Type noteCreateDataType,
            Type noteDirectionIndexEnum,
            Type noteSizeEnum,
            Func<object, List<BmsNote>, BmsNote> getBmsNoteFromNoteCreateData,
            Func<BmsNote, object> createNoteCreateData,
            Func<Type, string, object> getEnumValue,
            Action<object, string, object> setFieldValue)
        {
            if (noteList == null || fairyEndNotes == null || allBmsNotes == null) return;

            MelonLogger.Msg($"[FairyNoteProcessor] 페어리 끝 노트 처리 시작: {fairyEndNotes.Count}개");
            MelonLogger.Msg($"[FairyNoteProcessor] noteList 개수: {noteList.Count}개");

            float bpm = fairyEndNotes.FirstOrDefault(n => n?.BaseBpm > 0f)?.BaseBpm ?? BaseBpm;
            float timeTolerance = CalculateTimeTolerance(bpm);

            if (noteList.Count > 0)
            {
                try { getBmsNoteFromNoteCreateData(noteList[0], allBmsNotes); } catch { /* ignore */ }
            }

            var startNoteMap = BuildFairyStartLookup(noteList, allBmsNotes, getBmsNoteFromNoteCreateData);

            int success = 0;
            int fail = 0;

            foreach (var fairyEnd in fairyEndNotes)
            {
                try
                {
                    var startBms = fairyEnd?.StartNote;
                    if (startBms == null || startBms.Duration <= 0f)
                    {
                        startBms = FindFallbackStartForEnd(fairyEnd, allBmsNotes, timeTolerance);
                        if (startBms != null)
                        {
                            LinkFairyPair(startBms, fairyEnd, useTimeDuration: true, timeTolerance);
                        }
                    }

                    if (startBms == null)
                    {
                        fail++;
                        continue;
                    }

                    object startNoteObj = FindStartNoteObject(startBms, startNoteMap, timeTolerance);

                    if (startNoteObj == null)
                    {
                        fail++;
                        MelonLogger.Warning($"[FairyNoteProcessor] 시작 노트 역매핑 실패: Time={startBms.Time:F3}, Lane={startBms.Lane}, IsLeft={startBms.IsLeft}");
                        continue;
                    }

                    NoteProcessorHelper.AddEndNoteToConnectNodeArray(
                        startNoteObj,
                        fairyEnd,
                        noteCreateDataType,
                        noteDirectionIndexEnum,
                        noteSizeEnum,
                        createNoteCreateData,
                        getEnumValue,
                        setFieldValue,
                        endDirection: "CENTER_TOP",
                        processorName: "FairyNoteProcessor",
                        copyTurnDirection: true);

                    success++;
                }
                catch (Exception ex)
                {
                    fail++;
                    Helpers.ErrorLogger.LogWarning(ex, "[FairyNoteProcessor]", "ProcessFairyEndNotes 처리 중 오류");
                }
            }

            MelonLogger.Msg($"[FairyNoteProcessor] 페어리 끝 노트 처리 완료: 성공={success}개, 실패={fail}개");
        }

        private static object FindStartNoteObject(
            BmsNote startBms,
            Dictionary<(int Lane, bool IsLeft), List<FairyStartLookupEntry>> startNoteMap,
            float timeTolerance)
        {
            if (startBms == null || startNoteMap == null) return null;

            var key = (startBms.Lane, startBms.IsLeft);
            if (!startNoteMap.TryGetValue(key, out var entries) || entries.Count == 0) return null;

            var exactMatch = entries.FirstOrDefault(entry => ReferenceEquals(entry.BmsNote, startBms));
            if (exactMatch != null) return exactMatch.NoteObject;

            int targetSample = ToSampleIndex(startBms.Time);
            int maxDelta = Math.Max(StartLookupSampleSlack, ToSampleIndex(timeTolerance));

            return entries
                .Where(entry => Math.Abs(entry.Sample - targetSample) <= maxDelta)
                .OrderBy(entry => Math.Abs(entry.Sample - targetSample))
                .ThenBy(entry => Math.Abs(entry.BmsNote.Duration - startBms.Duration))
                .Select(entry => entry.NoteObject)
                .FirstOrDefault();
        }

        private static BmsNote FindFallbackStartForEnd(BmsNote fairyEnd, List<BmsNote> allBmsNotes, float timeTolerance)
        {
            if (fairyEnd == null || allBmsNotes == null) return null;

            return allBmsNotes
                .Where(note => note != null
                    && note.Type == NoteType.Fairy
                    && note.Lane == fairyEnd.Lane
                    && note.IsLeft == fairyEnd.IsLeft
                    && note.EndNote == null
                    && fairyEnd.Time >= note.Time - timeTolerance)
                .OrderBy(note => Math.Abs(fairyEnd.Time - note.Time))
                .ThenBy(note => note.Tick)
                .FirstOrDefault();
        }

        private static int ToSampleIndex(float timeSeconds)
        {
            return (int)Math.Round(timeSeconds * SampleRate, MidpointRounding.AwayFromZero);
        }
}
}
