using System;
using System.Collections.Generic;
using System.Linq;
using GRC2.Parsers;
using MelonLoader;

namespace GRC2.Processors
{
    public static partial class HoldNoteProcessor
    {
        private static void LogHoldEndMatchFailure(BmsNote holdEnd, List<BmsNote> allBmsNotes, Dictionary<string, List<(object Note, BmsNote BmsNote)>> holdStartMap)
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
                MelonLogger.Warning($"[HoldNoteProcessor]   (해당 레인의 홀드 시작 노트가 holdStartMap에 없습니다)");
        }

        private static void LogRemainingHoldStarts(Dictionary<string, List<(object Note, BmsNote BmsNote)>> holdStartMap)
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
    }
}
