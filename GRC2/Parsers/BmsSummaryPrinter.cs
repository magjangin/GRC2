using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader;

namespace GRC2.Parsers
{
    /// <summary>
    /// BMS 파싱 결과 요약 출력
    /// </summary>
    public static class BmsSummaryPrinter
    {
        /// <summary>
        /// 파싱 결과 요약 출력
        /// </summary>
        public static void PrintParseSummary(
            List<BmsNote> notes,
            string filePath,
            string label = null,
            bool printHeader = true,
            bool printLeadingBlankLine = true,
            bool printTrailingBlankLine = true)
        {
            try
            {
                string songTitle = ResolveSongTitle(filePath, label);

                int totalNotes = notes.Count;
                int touchNotes = 0, holdNotes = 0, holdEndNotes = 0;
                int flickNotes = 0, fairyNotes = 0, fairyEndNotes = 0;
                var holdStarts = new List<BmsNote>();
                var holdEnds = new List<BmsNote>();
                var fairyStarts = new List<BmsNote>();
                var fairyEnds = new List<BmsNote>();

                foreach (var note in notes)
                {
                    switch (note.Type)
                    {
                        case NoteType.Touch: touchNotes++; break;
                        case NoteType.Hold: holdNotes++; holdStarts.Add(note); break;
                        case NoteType.HoldEnd: holdEndNotes++; holdEnds.Add(note); break;
                        case NoteType.Flick: flickNotes++; break;
                        case NoteType.Fairy: fairyNotes++; fairyStarts.Add(note); break;
                        case NoteType.FairyEnd: fairyEndNotes++; fairyEnds.Add(note); break;
                    }
                }

                bool allHoldNotesHaveEnd = AllStartNotesHaveEnd(holdStarts, holdEnds);
                bool allFairyNotesHaveEnd = AllStartNotesHaveEnd(fairyStarts, fairyEnds);

                if (printLeadingBlankLine)
                    MelonLogger.Msg("");
                if (printHeader)
                {
                    MelonLogger.Msg($"=== BMS 파일 파싱 결과 ===");
                    MelonLogger.Msg("");
                }

                MelonLogger.Msg($"[{songTitle}] {Path.GetFileName(filePath)}:");
                MelonLogger.Msg($"  - 총 노트: {totalNotes}개");
                MelonLogger.Msg($"  - 일반 노트(01): {touchNotes}개");
                MelonLogger.Msg($"  - 홀드 노트(02): {holdNotes}개");
                MelonLogger.Msg($"  - 플릭 노트(03-0A): {flickNotes}개");
                MelonLogger.Msg($"  - 페어리 노트(11-18): {fairyNotes}개");

                LogEndNoteValidation(holdNotes, holdEndNotes, allHoldNotesHaveEnd, fairyNotes, fairyEndNotes, allFairyNotesHaveEnd);

                if (printTrailingBlankLine)
                    MelonLogger.Msg("");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BmsParser] 파싱 결과 요약 출력 중 오류: {ex.Message}");
            }
        }

        private static string ResolveSongTitle(string filePath, string label)
        {
            if (!string.IsNullOrWhiteSpace(label))
                return label;

            try
            {
                var songInfo = Core.SceneDetector.SongInfo;
                if (songInfo != null && !string.IsNullOrEmpty(songInfo.Title))
                    return songInfo.Title;
            }
            catch
            {
                return Path.GetFileNameWithoutExtension(filePath);
            }

            return "custom chart";
        }

        private static bool AllStartNotesHaveEnd(List<BmsNote> starts, List<BmsNote> ends)
        {
            foreach (var start in starts)
            {
                float expectedEnd = start.Time + start.Duration;
                bool hasEnd = false;
                foreach (var end in ends)
                {
                    if (Math.Abs(end.Time - expectedEnd) < 0.01f &&
                        end.Lane == start.Lane &&
                        end.IsLeft == start.IsLeft)
                    {
                        hasEnd = true;
                        break;
                    }
                }

                if (!hasEnd)
                    return false;
            }

            return true;
        }

        private static void LogEndNoteValidation(
            int holdNotes, int holdEndNotes, bool allHoldNotesHaveEnd,
            int fairyNotes, int fairyEndNotes, bool allFairyNotesHaveEnd)
        {
            bool hasHoldNotes = holdNotes > 0;
            bool hasFairyNotes = fairyNotes > 0;
            if (!hasHoldNotes && !hasFairyNotes)
                return;

            bool allHoldValid = hasHoldNotes && allHoldNotesHaveEnd;
            bool allFairyValid = hasFairyNotes && allFairyNotesHaveEnd;

            if (hasHoldNotes && !allHoldValid)
                MelonLogger.Msg($"  - ⚠️ 홀드 노트 중 끝노트가 없는 노트가 있습니다 ({holdNotes}개 시작, {holdEndNotes}개 끝)");
            if (hasFairyNotes && !allFairyValid)
                MelonLogger.Msg($"  - ⚠️ 페어리 노트 중 끝노트가 없는 노트가 있습니다 ({fairyNotes}개 시작, {fairyEndNotes}개 끝)");

            if (allHoldValid && allFairyValid)
                MelonLogger.Msg($"  - ✓ 모든 홀드/페어리 노트에 끝노트가 있습니다");
            else if (allHoldValid && !hasFairyNotes)
                MelonLogger.Msg($"  - ✓ 모든 홀드 노트에 끝노트가 있습니다");
            else if (allFairyValid && !hasHoldNotes)
                MelonLogger.Msg($"  - ✓ 모든 페어리 노트에 끝노트가 있습니다");
        }
    }
}
