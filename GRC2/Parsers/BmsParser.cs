using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GRC2.Processors;
using MelonLoader;

namespace GRC2.Parsers
{
    /// <summary>
    /// BMS 파일 파서 - 메인 파싱 로직
    /// </summary>
    public static class BmsParser
    {
        // BPM 관련 상세 로그 제어 플래그 (성능 최적화용, 기본 비활성화)
        private static readonly bool EnableBpmLogging = false;

        // 정규식 캐싱 (성능 최적화)
        private static readonly Regex BpmRegex = new Regex(@"^#BPM\s+([0-9.]+)", RegexOptions.Compiled);
        private static readonly Regex BpmIndexRegex = new Regex(@"^#BPM([0-9A-Fa-f]{2}):\s*([0-9.]+)", RegexOptions.Compiled);
        private static readonly Regex MeasureRegex = new Regex(@"^#(\d{3})(\d{2}):", RegexOptions.Compiled);

        /// <summary>
        /// BMS 파일 파싱 메인 메서드
        /// </summary>
        public static List<BmsNote> ParseBmsFile(string filePath, bool printSummary = true)
        {
            var notes = new List<BmsNote>();
            
            if (!File.Exists(filePath))
            {
                MelonLogger.Error($"[BmsParser] 파일을 찾을 수 없습니다: {filePath}");
                return notes;
            }

            try
            {
                MelonLogger.Msg($"[BmsParser] BMS 파일 파싱 시작: {filePath}");
                
                var lines = File.ReadAllLines(filePath);
                var bpmChanges = new List<BpmChange>();
                var bpmIndexTable = new Dictionary<int, float>(); // #BPM01: 140 등 인덱스→BPM 값
                float baseBpm = 120f;  // 기본 BPM
                float baseFreq = 60f / baseBpm;

                // 1단계: BPM 정보 수집 (기본 BPM + #BPMXX: value 인덱스 테이블)
                CollectBpmInfo(lines, ref baseBpm, ref baseFreq, bpmIndexTable);

                // 2단계: 노트 데이터 파싱
                ParseNotes(lines, notes, baseBpm, baseFreq, bpmIndexTable, bpmChanges);

                // 3단계: 홀드 노트 매칭 (02-19 쌍)
                HoldNoteProcessor.MatchHoldNotes(notes);

                // 4단계: 페어리 노트 매칭 (11-18과 1A-1B 쌍)
                FairyNoteProcessor.MatchFairyNotes(notes);

                // 5단계: 시간 계산
                CalculateNoteTimes(notes, baseBpm, baseFreq, bpmChanges);

                // 6단계: 페어리 노트 2차 보정 (시간 기반)
                FairyNoteProcessor.ReconcileFairyNotes(notes);

                // 파싱 결과 요약 출력 (옵션)
                if (printSummary)
                {
                    BmsSummaryPrinter.PrintParseSummary(notes, filePath);
                }
                
                MelonLogger.Msg($"[BmsParser] 파싱 완료: {notes.Count}개 노트");
                return notes;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[BmsParser] 파싱 오류: {ex.Message}\n{ex.StackTrace}");
                return notes;
            }
        }

        /// <summary>
        /// BPM 정보 수집: 기본 BPM + #BPMXX: value 인덱스 테이블 (채널 03-08 데이터에서 인덱스로 참조)
        /// </summary>
        private static void CollectBpmInfo(string[] lines, ref float baseBpm, ref float baseFreq,
            Dictionary<int, float> bpmIndexTable)
        {
            if (bpmIndexTable == null) return;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                    continue;

                // 기본 BPM 설정 (#BPM)
                if (line.StartsWith("#BPM"))
                {
                    var match = BpmRegex.Match(line);
                    if (match.Success)
                    {
                        baseBpm = float.Parse(match.Groups[1].Value);
                        baseFreq = 60f / baseBpm;
                        if (EnableBpmLogging)
                        {
                            MelonLogger.Msg($"[BmsParser] 기본 BPM: {baseBpm}");
                        }
                    }
                }

                // BPM 인덱스 테이블 (#BPMXX: value) — measure 데이터에서 hexValue로 참조됨
                var bpmMatch = BpmIndexRegex.Match(line);
                if (bpmMatch.Success)
                {
                    var bpmIndex = Convert.ToInt32(bpmMatch.Groups[1].Value, 16);
                    var bpmValue = float.Parse(bpmMatch.Groups[2].Value);
                    bpmIndexTable[bpmIndex] = bpmValue;
                    if (EnableBpmLogging)
                    {
                        MelonLogger.Msg($"[BmsParser] BPM 인덱스 {bpmIndex:X2}: {bpmValue}");
                    }
                }
            }
        }

        /// <summary>
        /// 노트 데이터 파싱
        /// </summary>
        private static void ParseNotes(string[] lines, List<BmsNote> notes, float baseBpm, float baseFreq,
            Dictionary<int, float> bpmIndexTable, List<BpmChange> bpmChanges)
        {
            int currentMeasure = 0;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//"))
                    continue;

                // Measure 정의 (#XXXYY: ...)
                var measureMatch = MeasureRegex.Match(line);
                if (measureMatch.Success)
                {
                    currentMeasure = int.Parse(measureMatch.Groups[1].Value);
                    var channel = int.Parse(measureMatch.Groups[2].Value);
                    var data = line.Substring(measureMatch.Length).Trim();

                    // BPM 변화 처리 (채널 03-08: 데이터는 BPM 인덱스 목록, bpmIndexTable로 실제 값 조회)
                    if (channel >= 0x03 && channel <= 0x08)
                    {
                        ProcessBpmChange(channel, data, currentMeasure, baseBpm, bpmIndexTable, bpmChanges);
                    }

                    // 노트 채널 처리 (11-12, 14-16, 18)
                    if (BmsNoteDataParser.ChannelToLaneMap.ContainsKey(channel))
                    {
                        var parsedNotes = BmsNoteDataParser.ParseNoteData(currentMeasure, channel, data);
                        // ⚡ 파싱된 노트에 BaseBpm 설정 (매칭 시간 오차 범위 계산용)
                        foreach (var note in parsedNotes)
                        {
                            note.BaseBpm = baseBpm;
                        }
                        notes.AddRange(parsedNotes);
                    }
                }
            }
        }

        /// <summary>
        /// BPM 변화 처리: 채널 03-08 데이터는 measure 내 슬롯별 BPM 인덱스(hex). 인덱스→실제 BPM은 bpmIndexTable 사용.
        /// </summary>
        private static void ProcessBpmChange(int channel, string data, int currentMeasure, float baseBpm,
            Dictionary<int, float> bpmIndexTable, List<BpmChange> bpmChanges)
        {
            if (channel < 0x03 || channel > 0x08) return;

            var hexValues = BmsNoteDataParser.ParseHexData(data);
            var measureLength = hexValues.Count;
            if (measureLength == 0) return;

            for (int i = 0; i < hexValues.Count; i++)
            {
                var bpmIndexRef = hexValues[i];
                if (bpmIndexRef <= 0) continue;

                // BPM 인덱스 테이블에서 실제 BPM 조회 (없으면 기본 BPM)
                var bpmValue = (bpmIndexTable != null && bpmIndexTable.TryGetValue(bpmIndexRef, out var v)) ? v : baseBpm;

                // measure 내 위치 반영 (0.0~1.0)
                var positionInMeasure = (float)i / measureLength;
                var tick = currentMeasure + positionInMeasure;

                bpmChanges.Add(new BpmChange
                {
                    Tick = tick,
                    Bpm = bpmValue,
                    Freq = 60f / bpmValue
                });
            }
        }

        /// <summary>
        /// 노트 시간 계산
        /// </summary>
        private static void CalculateNoteTimes(List<BmsNote> notes, float baseBpm, float baseFreq, List<BpmChange> bpmChanges)
        {
            // BPM 변화 리스트를 한 번 정렬 (성능 최적화)
            var sortedBpmChanges = bpmChanges.OrderBy(b => b.Tick).ToList();
            
            foreach (var note in notes)
            {
                note.Time = BmsTimeCalculator.CalculateTime(note.Tick, baseBpm, baseFreq, sortedBpmChanges);
                
                // Duration이 Tick 단위로 저장되어 있다면 Time 단위로 변환
                if (note.Duration > 0 && (note.Type == NoteType.Hold || note.Type == NoteType.Fairy))
                {
                    // Duration을 Time 단위로 변환
                    var startTime = note.Time;
                    var endTick = note.Tick + note.Duration;
                    var endTime = BmsTimeCalculator.CalculateTime(endTick, baseBpm, baseFreq, sortedBpmChanges);
                    note.Duration = endTime - startTime;
                }
            }
        }
    }
}
