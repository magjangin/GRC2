using System;
using System.Collections.Generic;
using System.Linq;
using GRC2.Builders;
using GRC2.Parsers;
using IntiCreates;
using IntiCreates.RythmGame;
using MelonLoader;

namespace GRC2.Processors
{
    using NoteCreateData = FairyNoteEditorLoader.NoteCreateData;

    public static partial class FairyNoteProcessor
    {
        private const int SampleRate = 48000;
        private const int StartLookupSampleSlack = 8;
        private const float BaseToleranceSeconds = 0.05f;
        private const float BaseBpm = 120f;
        private const float MinToleranceSeconds = 0.02f;
        private const float MaxToleranceSeconds = 0.15f;
        private const float PreferredMatchWindowSeconds = 4.0f;
        private const float MinFairyDurationSeconds = 0.005f;

        public static void MatchFairyNotes(List<BmsNote> notes)
        {
            if (notes == null || notes.Count == 0) return;

            ResetFairyLinks(notes);
            RunPrimaryFairyMatch(notes);
            LogFairyMatchSummary(notes, "1차 FIFO");
        }

        public static void ReconcileFairyNotes(List<BmsNote> notes)
        {
            if (notes == null || notes.Count == 0) return;

            var fairyNotes = notes
                .Where(n => n != null && (n.Type == NoteType.Fairy || n.Type == NoteType.FairyEnd))
                .ToList();
            if (fairyNotes.Count == 0) return;

            float bpm = fairyNotes.FirstOrDefault(n => n.BaseBpm > 0f)?.BaseBpm ?? BaseBpm;
            float timeTolerance = CalculateTimeTolerance(bpm);

            int invalidated = ClearInvalidFairyLinks(fairyNotes, timeTolerance);
            int recovered = 0;
            int forced = 0;

            foreach (var group in fairyNotes.GroupBy(n => (n.Lane, n.IsLeft)))
            {
                var starts = group
                    .Where(n => n.Type == NoteType.Fairy)
                    .OrderBy(n => n.Time)
                    .ThenBy(n => n.Tick)
                    .ToList();
                var ends = group
                    .Where(n => n.Type == NoteType.FairyEnd)
                    .OrderBy(n => n.Time)
                    .ThenBy(n => n.Tick)
                    .ToList();

                var unmatchedEnds = ends.Where(n => n.StartNote == null).ToList();
                if (unmatchedEnds.Count == 0) continue;

                foreach (var start in starts.Where(n => n.EndNote == null))
                {
                    var end = FindBestEndCandidate(start, unmatchedEnds, timeTolerance, PreferredMatchWindowSeconds, preferWindow: true)
                        ?? FindBestEndCandidate(start, unmatchedEnds, timeTolerance, float.MaxValue, preferWindow: false);

                    if (end == null) continue;

                    bool usedForcedMatch = !IsWithinPreferredWindow(start, end, timeTolerance, PreferredMatchWindowSeconds);
                    LinkFairyPair(start, end, useTimeDuration: true, timeTolerance);
                    unmatchedEnds.Remove(end);

                    if (usedForcedMatch) forced++;
                    else recovered++;
                }
            }

            MelonLogger.Msg($"[FairyNoteProcessor] 2차 보정 완료: invalidated={invalidated}개, recovered={recovered}개, forced={forced}개, tolerance={timeTolerance:F4}s");
            LogFairyMatchSummary(notes, "2차 보정");
        }
        private static void ResetFairyLinks(IEnumerable<BmsNote> notes)
        {
            foreach (var note in notes)
            {
                if (note == null) continue;
                if (note.Type != NoteType.Fairy && note.Type != NoteType.FairyEnd) continue;

                note.StartNote = null;
                note.EndNote = null;
                if (note.Type == NoteType.Fairy)
                {
                    note.Duration = 0f;
                }
            }
        }

        private static void RunPrimaryFairyMatch(List<BmsNote> notes)
        {
            var items = notes
                .Where(n => n != null && (n.Type == NoteType.Fairy || n.Type == NoteType.FairyEnd))
                .OrderBy(n => n.Tick)
                .ThenByDescending(n => n.Type == NoteType.Fairy)
                .ToList();

            foreach (var group in items.GroupBy(n => (n.Lane, n.IsLeft)))
            {
                var openStarts = new Queue<BmsNote>();

                foreach (var note in group)
                {
                    if (note.Type == NoteType.Fairy)
                    {
                        openStarts.Enqueue(note);
                        continue;
                    }

                    if (openStarts.Count == 0) continue;

                    var start = openStarts.Dequeue();
                    if (note.Tick <= start.Tick) continue;

                    LinkFairyPair(start, note, useTimeDuration: false, timeTolerance: 0f);
                }
            }
        }

        private static int ClearInvalidFairyLinks(IEnumerable<BmsNote> fairyNotes, float timeTolerance)
        {
            int invalidated = 0;

            foreach (var start in fairyNotes.Where(n => n.Type == NoteType.Fairy && n.EndNote != null).ToList())
            {
                var end = start.EndNote;
                if (end == null || end.Type != NoteType.FairyEnd)
                {
                    UnlinkFairyPair(start, end);
                    invalidated++;
                    continue;
                }

                if (end.StartNote != start || start.Lane != end.Lane || start.IsLeft != end.IsLeft)
                {
                    UnlinkFairyPair(start, end);
                    invalidated++;
                    continue;
                }

                float timeDelta = end.Time - start.Time;
                if (timeDelta < -timeTolerance)
                {
                    UnlinkFairyPair(start, end);
                    invalidated++;
                    continue;
                }

                start.Duration = Math.Max(MinFairyDurationSeconds, timeDelta);
            }

            return invalidated;
        }

        private static BmsNote FindBestEndCandidate(BmsNote start, List<BmsNote> unmatchedEnds, float timeTolerance, float windowSeconds, bool preferWindow)
        {
            if (start == null || unmatchedEnds == null || unmatchedEnds.Count == 0) return null;

            var candidates = unmatchedEnds
                .Where(end => end != null && end.StartNote == null && end.Lane == start.Lane && end.IsLeft == start.IsLeft)
                .Select(end => new
                {
                    End = end,
                    Delta = end.Time - start.Time
                })
                .Where(x => x.Delta >= -timeTolerance);

            if (preferWindow)
            {
                candidates = candidates.Where(x => x.Delta <= windowSeconds);
            }

            return candidates
                .OrderBy(x => x.Delta < 0f ? 1 : 0)
                .ThenBy(x => Math.Abs(x.Delta))
                .ThenBy(x => x.End.Tick)
                .Select(x => x.End)
                .FirstOrDefault();
        }

        private static bool IsWithinPreferredWindow(BmsNote start, BmsNote end, float timeTolerance, float preferredWindow)
        {
            if (start == null || end == null) return false;
            float delta = end.Time - start.Time;
            return delta >= -timeTolerance && delta <= preferredWindow;
        }

        private static void LinkFairyPair(BmsNote start, BmsNote end, bool useTimeDuration, float timeTolerance)
        {
            if (start == null || end == null) return;

            start.EndNote = end;
            end.StartNote = start;

            if (useTimeDuration)
            {
                start.Duration = Math.Max(MinFairyDurationSeconds, end.Time - start.Time);
                if (end.Time < start.Time && end.Time >= start.Time - timeTolerance)
                {
                    start.Duration = MinFairyDurationSeconds;
                }
                return;
            }

            start.Duration = end.Tick - start.Tick;
        }

        private static void UnlinkFairyPair(BmsNote start, BmsNote end)
        {
            if (start != null)
            {
                start.EndNote = null;
                if (start.Type == NoteType.Fairy)
                {
                    start.Duration = 0f;
                }
            }

            if (end != null)
            {
                end.StartNote = null;
            }
        }

        private static float CalculateTimeTolerance(float bpm)
        {
            if (bpm <= 0f) bpm = BaseBpm;

            float tolerance = BaseToleranceSeconds * (BaseBpm / bpm);
            return Math.Max(MinToleranceSeconds, Math.Min(MaxToleranceSeconds, tolerance));
        }

        private static void LogFairyMatchSummary(IEnumerable<BmsNote> notes, string stage)
        {
            var fairyNotes = notes
                .Where(n => n != null && (n.Type == NoteType.Fairy || n.Type == NoteType.FairyEnd))
                .ToList();
            if (fairyNotes.Count == 0) return;

            int startCount = fairyNotes.Count(n => n.Type == NoteType.Fairy);
            int endCount = fairyNotes.Count(n => n.Type == NoteType.FairyEnd);
            int matchedStarts = fairyNotes.Count(n => n.Type == NoteType.Fairy && n.EndNote != null);
            int matchedEnds = fairyNotes.Count(n => n.Type == NoteType.FairyEnd && n.StartNote != null);
            int unmatchedStarts = startCount - matchedStarts;
            int unmatchedEnds = endCount - matchedEnds;

            MelonLogger.Msg($"[FairyNoteProcessor] {stage} 요약: start={startCount}개, end={endCount}개, matchedStart={matchedStarts}개, matchedEnd={matchedEnds}개, unmatchedStart={unmatchedStarts}개, unmatchedEnd={unmatchedEnds}개");
        }
        private sealed class FairyStartLookupEntry
        {
            public NoteCreateData NoteObject { get; set; }
            public BmsNote BmsNote { get; set; }
            public int Sample { get; set; }
        }
    }

    public static partial class FairyNoteProcessor
    {
    

        private static Dictionary<(int Lane, bool IsLeft), List<FairyStartLookupEntry>> BuildFairyStartLookup(
            List<NoteCreateData> noteList,
            List<BmsNote> allBmsNotes)
        {
            var lookup = new Dictionary<(int Lane, bool IsLeft), List<FairyStartLookupEntry>>();

            foreach (var noteObj in noteList)
            {
                if (noteObj == null) continue;

                var bmsNote = NoteCreateDataBuilder.GetBmsNoteFromNoteCreateData(noteObj, allBmsNotes);
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
            List<NoteCreateData> noteList,
            List<BmsNote> fairyEndNotes,
            List<BmsNote> allBmsNotes)
        {
            if (noteList == null || fairyEndNotes == null || allBmsNotes == null) return;

            MelonLogger.Msg($"[FairyNoteProcessor] 페어리 끝 노트 처리 시작: {fairyEndNotes.Count}개");
            MelonLogger.Msg($"[FairyNoteProcessor] noteList 개수: {noteList.Count}개");

            float bpm = fairyEndNotes.FirstOrDefault(n => n?.BaseBpm > 0f)?.BaseBpm ?? BaseBpm;
            float timeTolerance = CalculateTimeTolerance(bpm);

            if (noteList.Count > 0)
            {
                // 역매핑 캐시를 미리 구성해 둡니다.
                try { NoteCreateDataBuilder.GetBmsNoteFromNoteCreateData(noteList[0], allBmsNotes); } catch { /* ignore */ }
            }

            var startNoteMap = BuildFairyStartLookup(noteList, allBmsNotes);

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

                    NoteCreateData startNoteObj = FindStartNoteObject(startBms, startNoteMap, timeTolerance);

                    if (startNoteObj == null)
                    {
                        fail++;
                        MelonLogger.Warning($"[FairyNoteProcessor] 시작 노트 역매핑 실패: Time={startBms.Time:F3}, Lane={startBms.Lane}, IsLeft={startBms.IsLeft}");
                        continue;
                    }

                    NoteProcessorHelper.AddEndNoteToConnectNodeArray(
                        startNoteObj,
                        fairyEnd,
                        endDirection: NoteDirectionIndex.CENTER_TOP,
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

        private static NoteCreateData FindStartNoteObject(
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
