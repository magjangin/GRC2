using System;
using System.Collections.Generic;
using GRC2.Parsers;
using MelonLoader;

namespace GRC2.Processors
{
    public static partial class HoldNoteProcessor
    {
        private static HoldAttachResult AttachHoldEnd(
            BmsNote holdEnd,
            Dictionary<string, List<(object Note, BmsNote BmsNote)>> holdStartMap,
            Dictionary<(int Lane, bool IsLeft), List<(string Key, object Note, BmsNote BmsNote, int EndSample)>> holdStartByLane,
            Dictionary<string, object> processedHoldStartsByEndKey,
            float timeTolerance,
            HoldEndAttachContext attachContext)
        {
            if (holdEnd.StartNote != null)
            {
                AttachExplicitStartReference(holdEnd, holdStartMap, processedHoldStartsByEndKey, attachContext);
                return HoldAttachResult.Matched;
            }

            var fallbackSearchKey = BuildEndKey(holdEnd.Lane, holdEnd.IsLeft, ToSampleIndex(holdEnd.Time));
            if (processedHoldStartsByEndKey.TryGetValue(fallbackSearchKey, out _))
                return HoldAttachResult.Matched;

            MelonLogger.Msg($"[HoldNoteProcessor] 홀드 끝 노트 매칭 시도: Lane={holdEnd.Lane}, IsLeft={holdEnd.IsLeft}, Time={holdEnd.Time:F3}, SearchKey={fallbackSearchKey}");

            HoldAttachResult attachResult = TryAttachExactFallback(holdEnd, fallbackSearchKey, holdStartMap, processedHoldStartsByEndKey, timeTolerance, attachContext);
            if (attachResult == HoldAttachResult.NotMatched)
            {
                attachResult = TryAttachNearestFallback(holdEnd, fallbackSearchKey, holdStartMap, holdStartByLane, processedHoldStartsByEndKey, timeTolerance, attachContext);
            }

            return attachResult;
        }

        private static void AttachExplicitStartReference(
            BmsNote holdEnd,
            Dictionary<string, List<(object Note, BmsNote BmsNote)>> holdStartMap,
            Dictionary<string, object> processedHoldStartsByEndKey,
            HoldEndAttachContext attachContext)
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
                    AttachEndNote(startNoteObj, holdEnd, attachContext);
                }
                holdStartMap.Remove(mapKey);
            }

            processedHoldStartsByEndKey[mapKey] = null;
        }

        private static HoldAttachResult TryAttachExactFallback(
            BmsNote holdEnd,
            string fallbackSearchKey,
            Dictionary<string, List<(object Note, BmsNote BmsNote)>> holdStartMap,
            Dictionary<string, object> processedHoldStartsByEndKey,
            float timeTolerance,
            HoldEndAttachContext attachContext)
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

            AttachEndNote(holdStart.Note, holdEnd, attachContext);
            processedHoldStartsByEndKey[fallbackSearchKey] = holdStart.Note;
            MelonLogger.Msg($"[HoldNoteProcessor] ✓ 홀드 끝 노트 매칭 성공: Time={holdEnd.Time:F3}, StartTime={holdStart.BmsNote.Time:F3}, Duration={holdStart.BmsNote.Duration:F3}");
            return HoldAttachResult.Matched;
        }

        private static HoldAttachResult TryAttachNearestFallback(
            BmsNote holdEnd,
            string fallbackSearchKey,
            Dictionary<string, List<(object Note, BmsNote BmsNote)>> holdStartMap,
            Dictionary<(int Lane, bool IsLeft), List<(string Key, object Note, BmsNote BmsNote, int EndSample)>> holdStartByLane,
            Dictionary<string, object> processedHoldStartsByEndKey,
            float timeTolerance,
            HoldEndAttachContext attachContext)
        {
            var laneKey = (holdEnd.Lane, holdEnd.IsLeft);
            if (!holdStartByLane.TryGetValue(laneKey, out var laneNotes))
                return HoldAttachResult.NotMatched;

            string bestMatchKey = null;
            (object Note, BmsNote BmsNote)? bestMatch = null;
            float bestTimeDiff = float.MaxValue;

            foreach (var (key, noteObj, bmsNote, endSample) in laneNotes)
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

            object finalNote = pair.Note;
            if (finalNote == null)
            {
                MelonLogger.Warning($"[HoldNoteProcessor] noteList에 없는 홀드 시작(고아 로직 없음) 스킵(범위검색): Time={matchedBmsNote.Time:F3}, Lane={matchedBmsNote.Lane}");
                AddHoldStart(holdStartMap, bestMatchKey, null, matchedBmsNote);
                return HoldAttachResult.Skip;
            }

            AttachEndNote(finalNote, holdEnd, attachContext);
            processedHoldStartsByEndKey[bestMatchKey] = finalNote;
            processedHoldStartsByEndKey[fallbackSearchKey] = finalNote;
            MelonLogger.Msg($"[HoldNoteProcessor] ✓ 홀드 끝 노트 매칭 성공 (범위 검색): Time={holdEnd.Time:F3}, StartTime={matchedBmsNote.Time:F3}, Duration={matchedBmsNote.Duration:F3}, Diff={bestTimeDiff:F3}");
            return HoldAttachResult.Matched;
        }

        private static void AttachEndNote(object startNoteObj, BmsNote holdEnd, HoldEndAttachContext attachContext)
        {
            NoteProcessorHelper.AddEndNoteToConnectNodeArray(
                startNoteObj,
                holdEnd,
                attachContext.NoteCreateDataType,
                attachContext.NoteDirectionIndexEnum,
                attachContext.NoteSizeEnum,
                attachContext.CreateNoteCreateData,
                attachContext.GetEnumValue,
                attachContext.SetFieldValue,
                endDirection: "CENTER_MIDDLE",
                processorName: "HoldNoteProcessor",
                copyTurnDirection: false);
        }
    }
}
