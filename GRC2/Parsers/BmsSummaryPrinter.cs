using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                // 곡 제목 추출 (SongInfo에서 가져오거나 파일명에서)
                string songTitle = null;

                // label이 있으면 우선 사용 (앨범명 등 그룹 라벨)
                if (!string.IsNullOrWhiteSpace(label))
                {
                    songTitle = label;
                }
                else
                {
                    songTitle = "custom chart";
                    try
                    {
                        var songInfo = Core.SceneDetector.SongInfo;
                        if (songInfo != null && !string.IsNullOrEmpty(songInfo.Title))
                        {
                            songTitle = songInfo.Title;
                        }
                    }
                    catch
                    {
                        // SongInfo를 가져올 수 없으면 파일명 사용
                        songTitle = Path.GetFileNameWithoutExtension(filePath);
                    }
                }
                
                // 노트 타입별 개수 집계
                int totalNotes = notes.Count;
                int touchNotes = notes.Count(n => n.Type == NoteType.Touch);
                int holdNotes = notes.Count(n => n.Type == NoteType.Hold);
                int holdEndNotes = notes.Count(n => n.Type == NoteType.HoldEnd);
                int flickNotes = notes.Count(n => n.Type == NoteType.Flick);
                int fairyNotes = notes.Count(n => n.Type == NoteType.Fairy);
                int fairyEndNotes = notes.Count(n => n.Type == NoteType.FairyEnd);
                
                // 홀드 노트 검증: 모든 홀드 시작 노트에 끝 노트가 있는지 확인
                bool allHoldNotesHaveEnd = true;
                if (holdNotes > 0)
                {
                    var holdStarts = notes.Where(n => n.Type == NoteType.Hold).ToList();
                    var holdEnds = notes.Where(n => n.Type == NoteType.HoldEnd).ToList();
                    
                    foreach (var holdStart in holdStarts)
                    {
                        var hasEnd = holdEnds.Any(end => 
                            Math.Abs((end.Time - (holdStart.Time + holdStart.Duration))) < 0.01f &&
                            end.Lane == holdStart.Lane &&
                            end.IsLeft == holdStart.IsLeft);
                        
                        if (!hasEnd)
                        {
                            allHoldNotesHaveEnd = false;
                            break;
                        }
                    }
                }
                
                // 페어리 노트 검증: 모든 페어리 시작 노트에 끝 노트가 있는지 확인
                bool allFairyNotesHaveEnd = true;
                if (fairyNotes > 0)
                {
                    var fairyStarts = notes.Where(n => n.Type == NoteType.Fairy).ToList();
                    var fairyEnds = notes.Where(n => n.Type == NoteType.FairyEnd).ToList();
                    
                    foreach (var fairyStart in fairyStarts)
                    {
                        var hasEnd = fairyEnds.Any(end => 
                            Math.Abs((end.Time - (fairyStart.Time + fairyStart.Duration))) < 0.01f &&
                            end.Lane == fairyStart.Lane &&
                            end.IsLeft == fairyStart.IsLeft);
                        
                        if (!hasEnd)
                        {
                            allFairyNotesHaveEnd = false;
                            break;
                        }
                    }
                }
                
                // 요약 출력
                if (printLeadingBlankLine)
                {
                    MelonLogger.Msg("");
                }
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
                
                // 홀드/페어리 노트 검증 결과
                bool hasHoldNotes = holdNotes > 0;
                bool hasFairyNotes = fairyNotes > 0;
                bool allHoldValid = hasHoldNotes && allHoldNotesHaveEnd;
                bool allFairyValid = hasFairyNotes && allFairyNotesHaveEnd;
                
                if (hasHoldNotes || hasFairyNotes)
                {
                    // 경고 메시지 먼저 출력 (문제가 있는 경우)
                    if (hasHoldNotes && !allHoldValid)
                    {
                        MelonLogger.Msg($"  - ⚠️ 홀드 노트 중 끝노트가 없는 노트가 있습니다 ({holdNotes}개 시작, {holdEndNotes}개 끝)");
                    }
                    if (hasFairyNotes && !allFairyValid)
                    {
                        MelonLogger.Msg($"  - ⚠️ 페어리 노트 중 끝노트가 없는 노트가 있습니다 ({fairyNotes}개 시작, {fairyEndNotes}개 끝)");
                    }
                    
                    // 성공 메시지 출력 (둘 다 유효한 경우)
                    if (allHoldValid && allFairyValid)
                    {
                        MelonLogger.Msg($"  - ✓ 모든 홀드/페어리 노트에 끝노트가 있습니다");
                    }
                    else if (allHoldValid && !hasFairyNotes)
                    {
                        MelonLogger.Msg($"  - ✓ 모든 홀드 노트에 끝노트가 있습니다");
                    }
                    else if (allFairyValid && !hasHoldNotes)
                    {
                        MelonLogger.Msg($"  - ✓ 모든 페어리 노트에 끝노트가 있습니다");
                    }
                }
                
                if (printTrailingBlankLine)
                {
                    MelonLogger.Msg("");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BmsParser] 파싱 결과 요약 출력 중 오류: {ex.Message}");
            }
        }
    }
}


























