using System;
using System.Collections.Generic;
using System.Linq;
using GRC2.Builders;
using GRC2.Parsers;
using GRC2.Helpers;
using IntiCreates;
using IntiCreates.RythmGame;
using IntiCreates.RythmGame.FairyMode;
using MelonLoader;

namespace GRC2.Processors
{
    using NoteCreateData = FairyNoteEditorLoader.NoteCreateData;

    public static class HoldNoteProcessor
    {
        private static int ToSampleIndex(float timeSeconds)
        {
            return NoteSampleTime.ToSamples(timeSeconds);
        }

        private static string BuildEndKey(int lane, bool isLeft, int endSample)
        {
            return $"{lane}_{isLeft}_{endSample}";
        }
        /// <summary>
        /// 홀드 노트 시작(02)과 끝(19)을 매칭하고 Duration을 계산합니다.
        /// </summary>
        public static void MatchHoldNotes(List<BmsNote> notes)
        {
            // 같은 레인에서 02(시작)와 19(끝)을 매칭
            // 노트 채널만 필터링 (11, 12, 14, 15, 16, 18)
            var validChannels = BmsNoteDataParser.ChannelToLaneMap.Keys.ToHashSet();
            var holdStarts = notes.Where(n => n.Type == NoteType.Hold && validChannels.Contains(n.Channel)).ToList();
            var holdEnds = notes.Where(n => n.Type == NoteType.HoldEnd && validChannels.Contains(n.Channel)).ToList();

            // 끝 노트 하나는 시작 노트 하나에만 짝지어야 합니다. 같은 끝을 두 홀드가 나눠 쓰면 두 홀드가 겹칩니다(E3).
            // 시작을 시각순으로 훑어, 아직 짝이 없는 가장 이른 끝을 붙입니다. 끝을 못 찾은 시작은 Duration이 0으로 남아
            // BmsNoteConverter의 끝 노트 검사에서 주입이 취소됩니다.
            var usedEnds = new HashSet<BmsNote>();
            foreach (var start in holdStarts.OrderBy(s => s.Tick))
            {
                // 같은 레인과 방향(IsLeft)에서 아직 짝이 없는 가장 가까운 19(Hold end) 찾기
                var end = holdEnds
                    .Where(e => !usedEnds.Contains(e) && e.Lane == start.Lane && e.IsLeft == start.IsLeft && e.Tick > start.Tick)
                    .OrderBy(e => e.Tick)
                    .FirstOrDefault();

                if (end != null)
                {
                    usedEnds.Add(end);
                    // Duration 계산 (Tick 단위로 저장, 나중에 Time으로 변환됨)
                    start.Duration = end.Tick - start.Tick;

                    // 상호 참조 설정 (직접 연결)
                    start.EndNote = end;
                    end.StartNote = start;

                    // 끝 노트는 제거하지 않고 유지 (connectNodeDataArray에 추가하기 위해)
                }
            }
        }

        private static HoldAttachResult AttachHoldEnd(
            BmsNote holdEnd,
            Dictionary<string, List<(NoteCreateData Note, BmsNote BmsNote)>> holdStartMap,
            Dictionary<(int Lane, bool IsLeft), List<(string Key, NoteCreateData Note, BmsNote BmsNote)>> holdStartByLane,
            Dictionary<string, NoteCreateData> processedHoldStartsByEndKey,
            float timeTolerance)
        {
            if (holdEnd.StartNote != null)
            {
                AttachExplicitStartReference(holdEnd, holdStartMap, processedHoldStartsByEndKey);
                return HoldAttachResult.Matched;
            }

            var fallbackSearchKey = BuildEndKey(holdEnd.Lane, holdEnd.IsLeft, ToSampleIndex(holdEnd.Time));
            if (processedHoldStartsByEndKey.TryGetValue(fallbackSearchKey, out _))
                return HoldAttachResult.Matched;

            MelonLogger.Msg($"[HoldNoteProcessor] 홀드 끝 노트 매칭 시도: Lane={holdEnd.Lane}, IsLeft={holdEnd.IsLeft}, Time={holdEnd.Time:F3}, SearchKey={fallbackSearchKey}");

            HoldAttachResult attachResult = TryAttachExactFallback(holdEnd, fallbackSearchKey, holdStartMap, processedHoldStartsByEndKey, timeTolerance);
            if (attachResult == HoldAttachResult.NotMatched)
            {
                attachResult = TryAttachNearestFallback(holdEnd, fallbackSearchKey, holdStartMap, holdStartByLane, processedHoldStartsByEndKey, timeTolerance);
            }

            return attachResult;
        }

        private static void AttachExplicitStartReference(
            BmsNote holdEnd,
            Dictionary<string, List<(NoteCreateData Note, BmsNote BmsNote)>> holdStartMap,
            Dictionary<string, NoteCreateData> processedHoldStartsByEndKey)
        {
            var holdStartBms = holdEnd.StartNote;
            var endSample = ToSampleIndex(holdStartBms.Time + holdStartBms.Duration);
            var mapKey = BuildEndKey(holdStartBms.Lane, holdStartBms.IsLeft, endSample);

            if (holdStartMap.TryGetValue(mapKey, out var list))
            {
                foreach (var (startNoteObj, bmsNote) in list)
                {
                    if (startNoteObj == null)
                    {
                        MelonLogger.Warning($"[HoldNoteProcessor] noteList에 없는 홀드 시작(고아 로직 없음) 스킵: Time={bmsNote.Time:F3}, Lane={bmsNote.Lane}");
                        continue;
                    }
                    AttachEndNote(startNoteObj, holdEnd);
                }
                holdStartMap.Remove(mapKey);
            }

            processedHoldStartsByEndKey[mapKey] = null;
        }

        private static HoldAttachResult TryAttachExactFallback(
            BmsNote holdEnd,
            string fallbackSearchKey,
            Dictionary<string, List<(NoteCreateData Note, BmsNote BmsNote)>> holdStartMap,
            Dictionary<string, NoteCreateData> processedHoldStartsByEndKey,
            float timeTolerance)
        {
            if (!holdStartMap.TryGetValue(fallbackSearchKey, out var fallbackList) || fallbackList.Count == 0)
            {
                MelonLogger.Msg($"[HoldNoteProcessor] 홀드 끝 노트 키 매칭 실패: SearchKey={fallbackSearchKey} (holdStartMap에 없음)");
                return HoldAttachResult.NotMatched;
            }

            var holdStart = fallbackList[0];
            fallbackList.RemoveAt(0);
            if (fallbackList.Count == 0) holdStartMap.Remove(fallbackSearchKey);
            if (holdStart.Note == null)
            {
                MelonLogger.Warning($"[HoldNoteProcessor] noteList에 없는 홀드 시작(고아 로직 없음) 스킵: Time={holdStart.BmsNote.Time:F3}, Lane={holdStart.BmsNote.Lane}");
                AddHoldStartAtFront(holdStartMap, fallbackSearchKey, null, holdStart.BmsNote);
                return HoldAttachResult.Skip;
            }

            var timeDiff = Math.Abs(holdStart.BmsNote.Time + holdStart.BmsNote.Duration - holdEnd.Time);
            if (timeDiff >= timeTolerance)
            {
                MelonLogger.Warning($"[HoldNoteProcessor] 홀드 끝 노트 시간 불일치: Expected={holdStart.BmsNote.Time + holdStart.BmsNote.Duration:F3}, Actual={holdEnd.Time:F3}, Diff={timeDiff:F3}");
                AddHoldStartAtFront(holdStartMap, fallbackSearchKey, holdStart.Note, holdStart.BmsNote);
                return HoldAttachResult.NotMatched;
            }

            AttachEndNote(holdStart.Note, holdEnd);
            processedHoldStartsByEndKey[fallbackSearchKey] = holdStart.Note;
            MelonLogger.Msg($"[HoldNoteProcessor] ✓ 홀드 끝 노트 매칭 성공: Time={holdEnd.Time:F3}, StartTime={holdStart.BmsNote.Time:F3}, Duration={holdStart.BmsNote.Duration:F3}");
            return HoldAttachResult.Matched;
        }

        private static HoldAttachResult TryAttachNearestFallback(
            BmsNote holdEnd,
            string fallbackSearchKey,
            Dictionary<string, List<(NoteCreateData Note, BmsNote BmsNote)>> holdStartMap,
            Dictionary<(int Lane, bool IsLeft), List<(string Key, NoteCreateData Note, BmsNote BmsNote)>> holdStartByLane,
            Dictionary<string, NoteCreateData> processedHoldStartsByEndKey,
            float timeTolerance)
        {
            var laneKey = (holdEnd.Lane, holdEnd.IsLeft);
            if (!holdStartByLane.TryGetValue(laneKey, out var laneNotes))
                return HoldAttachResult.NotMatched;

            string bestMatchKey = null;
            (NoteCreateData Note, BmsNote BmsNote)? bestMatch = null;
            float bestTimeDiff = float.MaxValue;

            foreach (var (key, noteObj, bmsNote) in laneNotes)
            {
                if (!holdStartMap.TryGetValue(key, out var keyList) || keyList.Count == 0) continue;
                var expectedEndTime = bmsNote.Time + bmsNote.Duration;
                var timeDiff = Math.Abs(expectedEndTime - holdEnd.Time);
                if (timeDiff < timeTolerance && timeDiff < bestTimeDiff)
                {
                    bestMatchKey = key;
                    bestMatch = (noteObj, bmsNote);
                    bestTimeDiff = timeDiff;
                }
            }

            if (!bestMatch.HasValue || bestMatchKey == null || !holdStartMap.TryGetValue(bestMatchKey, out var bestList) || bestList.Count == 0)
                return HoldAttachResult.NotMatched;

            var (_, matchedBmsNote) = bestMatch.Value;
            var idx = bestList.FindIndex(p => p.BmsNote == matchedBmsNote);
            if (idx < 0) idx = 0;
            var pair = bestList[idx];
            bestList.RemoveAt(idx);
            if (bestList.Count == 0) holdStartMap.Remove(bestMatchKey);

            NoteCreateData finalNote = pair.Note;
            if (finalNote == null)
            {
                MelonLogger.Warning($"[HoldNoteProcessor] noteList에 없는 홀드 시작(고아 로직 없음) 스킵(범위검색): Time={matchedBmsNote.Time:F3}, Lane={matchedBmsNote.Lane}");
                AddHoldStart(holdStartMap, bestMatchKey, null, matchedBmsNote);
                return HoldAttachResult.Skip;
            }

            AttachEndNote(finalNote, holdEnd);
            processedHoldStartsByEndKey[bestMatchKey] = finalNote;
            processedHoldStartsByEndKey[fallbackSearchKey] = finalNote;
            MelonLogger.Msg($"[HoldNoteProcessor] ✓ 홀드 끝 노트 매칭 성공 (범위 검색): Time={holdEnd.Time:F3}, StartTime={matchedBmsNote.Time:F3}, Duration={matchedBmsNote.Duration:F3}, Diff={bestTimeDiff:F3}");
            return HoldAttachResult.Matched;
        }

        private static void AttachEndNote(NoteCreateData startNoteObj, BmsNote holdEnd)
        {
            NoteProcessorHelper.AddEndNoteToConnectNodeArray(
                startNoteObj,
                holdEnd,
                endDirection: NoteDirectionIndex.CENTER_MIDDLE,
                processorName: "HoldNoteProcessor",
                copyTurnDirection: false);
        }

        private enum HoldAttachResult
        {
            NotMatched,
            Matched,
            Skip
        }

        public static void ProcessHoldEndNotes(
            List<NoteCreateData> noteList,
            List<BmsNote> holdEndNotes,
            List<BmsNote> allBmsNotes)
        {
            MelonLogger.Msg($"[HoldNoteProcessor] 홀드 끝 노트 처리 시작: {holdEndNotes.Count}개");
            MelonLogger.Msg($"[HoldNoteProcessor] noteList 개수: {noteList.Count}개");

            // 역매핑 캐시를 미리 구성해 둡니다.
            if (noteList.Count > 0)
                NoteCreateDataBuilder.GetBmsNoteFromNoteCreateData(noteList[0], allBmsNotes);

            var noteListBySample = BuildNoteListBySample(noteList);
            var holdStartMap = BuildHoldStartMap(
                noteListBySample,
                allBmsNotes,
                out int matchedCount,
                out int unmatchedCount);

            int totalStartCount = holdStartMap.Values.Sum(list => list.Count);
            MelonLogger.Msg($"[HoldNoteProcessor] 홀드 시작 노트 맵 생성 완료: {totalStartCount}개 (키 {holdStartMap.Count}개, noteList 매칭: {matchedCount}개, 매칭 실패: {unmatchedCount}개)");

            float baseBpm = holdEndNotes.FirstOrDefault()?.BaseBpm ?? NoteProcessorHelper.DefaultBpm;
            float timeTolerance = NoteProcessorHelper.CalculateTimeTolerance(baseBpm);
            MelonLogger.Msg($"[HoldNoteProcessor] BPM: {baseBpm}, 시간 오차 허용 범위: {timeTolerance:F4}초");

            var holdStartByLane = BuildHoldStartByLane(holdStartMap);
            var processedHoldStartsByEndKey = new Dictionary<string, NoteCreateData>();
            int successCount = 0;
            int failCount = 0;

            foreach (var holdEnd in holdEndNotes)
            {
                HoldAttachResult attachResult = AttachHoldEnd(
                    holdEnd,
                    holdStartMap,
                    holdStartByLane,
                    processedHoldStartsByEndKey,
                    timeTolerance);

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

        private static void LogHoldEndMatchFailure(BmsNote holdEnd, List<BmsNote> allBmsNotes, Dictionary<string, List<(NoteCreateData Note, BmsNote BmsNote)>> holdStartMap)
        {
            MelonLogger.Warning($"[HoldNoteProcessor] 홀드 끝 노트 매칭 실패: Lane={holdEnd.Lane}, IsLeft={holdEnd.IsLeft}, Time={holdEnd.Time:F3}");

            var matchingStarts = allBmsNotes.Where(n =>
                n.Type == NoteType.Hold &&
                n.Lane == holdEnd.Lane &&
                n.IsLeft == holdEnd.IsLeft &&
                n.Duration > 0).ToList();

            MelonLogger.Warning($"[HoldNoteProcessor] allBmsNotes에서 찾은 홀드 시작 노트: {matchingStarts.Count}개");
            foreach (var start in matchingStarts)
            {
                var expectedEndTime = start.Time + start.Duration;
                var timeDiff = Math.Abs(expectedEndTime - holdEnd.Time);
                MelonLogger.Warning($"[HoldNoteProcessor]   - Time={start.Time:F3}, Duration={start.Duration:F3}, ExpectedEnd={expectedEndTime:F3}, Diff={timeDiff:F3}");
            }

            MelonLogger.Warning($"[HoldNoteProcessor] holdStartMap에 있는 홀드 시작 노트:");
            bool foundInMap = false;
            foreach (var kvp in holdStartMap)
            {
                foreach (var (noteObj, bmsNote) in kvp.Value)
                {
                    if (bmsNote.Lane == holdEnd.Lane && bmsNote.IsLeft == holdEnd.IsLeft)
                    {
                        foundInMap = true;
                        var expectedEndTime = bmsNote.Time + bmsNote.Duration;
                        var timeDiff = Math.Abs(expectedEndTime - holdEnd.Time);
                        MelonLogger.Warning($"[HoldNoteProcessor]   - Time={bmsNote.Time:F3}, Duration={bmsNote.Duration:F3}, ExpectedEnd={expectedEndTime:F3}, Diff={timeDiff:F3}");
                    }
                }
            }
            if (!foundInMap)
                MelonLogger.Warning("[HoldNoteProcessor]   (해당 레인의 홀드 시작 노트가 holdStartMap에 없습니다)");
        }

        private static void LogRemainingHoldStarts(Dictionary<string, List<(NoteCreateData Note, BmsNote BmsNote)>> holdStartMap)
        {
            var remainingStartCount = holdStartMap.Values.Sum(list => list.Count);
            if (remainingStartCount <= 0)
                return;

            MelonLogger.Error($"[HoldNoteProcessor] ⚠️ 경고: 홀드 끝 노트가 매칭되지 않은 홀드 시작 노트 {remainingStartCount}개가 있습니다.");
            MelonLogger.Error($"[HoldNoteProcessor] 이는 BmsNoteConverter에서 이미 체크되었지만, 매칭 과정에서 문제가 발생했을 수 있습니다.");
            foreach (var list in holdStartMap.Values)
            {
                foreach (var (noteObj, holdStartBms) in list)
                {
                    MelonLogger.Error($"[HoldNoteProcessor]   홀드 시작: Time={holdStartBms.Time:F3}초, Lane={holdStartBms.Lane}, IsLeft={holdStartBms.IsLeft}, Duration={holdStartBms.Duration:F3}초");
                }
            }
        }

        private static Dictionary<int, List<NoteCreateData>> BuildNoteListBySample(List<NoteCreateData> noteList)
        {
            var noteListBySample = new Dictionary<int, List<NoteCreateData>>();

            foreach (var noteObj in noteList)
            {
                if (noteObj == null)
                    continue;

                int sampleValue = noteObj.perfectSample;
                if (!noteListBySample.ContainsKey(sampleValue))
                    noteListBySample[sampleValue] = new List<NoteCreateData>();
                noteListBySample[sampleValue].Add(noteObj);
            }

            MelonLogger.Msg($"[HoldNoteProcessor] noteList 인덱싱 완료: {noteListBySample.Count}개 샘플 값");
            return noteListBySample;
        }

        private static Dictionary<string, List<(NoteCreateData Note, BmsNote BmsNote)>> BuildHoldStartMap(
            Dictionary<int, List<NoteCreateData>> noteListBySample,
            List<BmsNote> allBmsNotes,
            out int matchedCount,
            out int unmatchedCount)
        {
            var holdStartMap = new Dictionary<string, List<(NoteCreateData Note, BmsNote BmsNote)>>();
            matchedCount = 0;
            unmatchedCount = 0;

            foreach (var holdStartBms in allBmsNotes.Where(n => n.Type == NoteType.Hold && n.Duration > 0))
            {
                var endSample = ToSampleIndex(holdStartBms.Time + holdStartBms.Duration);
                var key = BuildEndKey(holdStartBms.Lane, holdStartBms.IsLeft, endSample);
                NoteCreateData matchedNote = FindMatchingHoldStartNote(noteListBySample, holdStartBms);

                if (matchedNote != null)
                    matchedCount++;
                else
                    unmatchedCount++;

                AddHoldStart(holdStartMap, key, matchedNote, holdStartBms);
            }

            return holdStartMap;
        }

        private static NoteCreateData FindMatchingHoldStartNote(
            Dictionary<int, List<NoteCreateData>> noteListBySample,
            BmsNote holdStartBms)
        {
            var expectedPerfectSample = ToSampleIndex(holdStartBms.Time);

            for (int offset = -2; offset <= 2; offset++)
            {
                int searchSample = expectedPerfectSample + offset;
                if (!noteListBySample.TryGetValue(searchSample, out var candidates))
                    continue;

                foreach (var noteObj in candidates)
                {
                    if (!IsHoldNoteCreateData(noteObj))
                        continue;

                    if (MatchesHoldLane(noteObj, holdStartBms))
                        return noteObj;
                }
            }

            return null;
        }

        private static bool IsHoldNoteCreateData(NoteCreateData noteObj)
        {
            return noteObj.noteTypeID == NoteTypeId.Hold || noteObj.noteTypeID == NoteTypeId.Hold_Middle;
        }

        private static bool MatchesHoldLane(NoteCreateData noteObj, BmsNote holdStartBms)
        {
            return noteObj.laneLeftRightID == EnumValueHelper.GetLaneLeftRight(holdStartBms.IsLeft) &&
                noteObj.subLaneID == EnumValueHelper.GetSubLaneType(holdStartBms.Lane);
        }

        private static Dictionary<(int Lane, bool IsLeft), List<(string Key, NoteCreateData Note, BmsNote BmsNote)>> BuildHoldStartByLane(
            Dictionary<string, List<(NoteCreateData Note, BmsNote BmsNote)>> holdStartMap)
        {
            var holdStartByLane = new Dictionary<(int Lane, bool IsLeft), List<(string Key, NoteCreateData Note, BmsNote BmsNote)>>();
            foreach (var kvp in holdStartMap)
            {
                foreach (var (noteObj, bmsNote) in kvp.Value)
                {
                    var laneKey = (bmsNote.Lane, bmsNote.IsLeft);
                    if (!holdStartByLane.ContainsKey(laneKey))
                        holdStartByLane[laneKey] = new List<(string, NoteCreateData, BmsNote)>();
                    holdStartByLane[laneKey].Add((kvp.Key, noteObj, bmsNote));
                }
            }

            return holdStartByLane;
        }

        private static void AddHoldStart(Dictionary<string, List<(NoteCreateData Note, BmsNote BmsNote)>> holdStartMap, string key, NoteCreateData noteObj, BmsNote bmsNote)
        {
            if (!holdStartMap.ContainsKey(key))
                holdStartMap[key] = new List<(NoteCreateData, BmsNote)>();
            holdStartMap[key].Add((noteObj, bmsNote));
        }

        private static void AddHoldStartAtFront(Dictionary<string, List<(NoteCreateData Note, BmsNote BmsNote)>> holdStartMap, string key, NoteCreateData noteObj, BmsNote bmsNote)
        {
            if (!holdStartMap.ContainsKey(key))
                holdStartMap[key] = new List<(NoteCreateData, BmsNote)>();
            holdStartMap[key].Insert(0, (noteObj, bmsNote));
        }
    }
}
