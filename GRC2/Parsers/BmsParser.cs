using System;
using System.Collections.Generic;
using System.Globalization;
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
        // 정규식 캐싱 (성능 최적화)
        // 헤더 명령(#BPM, #WAV)은 대소문자를 구분하지 않습니다(#wav001도 같은 키로 봅니다).
        private static readonly Regex BpmRegex = new Regex(@"^#BPM\s+([0-9.]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        // #BPMxx 뒤의 콜론은 있어도 되고 없어도 됩니다(표준은 공백 구분, E1).
        private static readonly Regex BpmIndexRegex = new Regex(@"^#BPM([0-9A-Fa-f]{2})(?::|\s)\s*([0-9.]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex MeasureRegex = new Regex(@"^#(\d{3})(\d{2}):", RegexOptions.Compiled);
        private static readonly Regex WavKeyRegex = new Regex(@"^#WAV([0-9A-Za-z]{2,3})(?:\s|:)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
                int noteValueWidth = DetectNoteDataValueWidth(lines);

                // 1단계: BPM 정보 수집 (기본 BPM + #BPMXX: value 인덱스 테이블)
                CollectBpmInfo(lines, ref baseBpm, ref baseFreq, bpmIndexTable);

                // 2단계: 노트 데이터 파싱
                ParseNotes(lines, notes, baseBpm, baseFreq, bpmIndexTable, bpmChanges, noteValueWidth);

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
                // 노트 수집 이후 단계가 실패하면 Time이 계산되지 않은 노트가 남습니다. 주입되지 않도록 이 파일의 노트를 모두 버립니다(H5).
                MelonLogger.Error($"[BmsParser] 파싱 오류 (이 파일의 노트를 모두 버립니다): {ex.Message}\n{ex.StackTrace}");
                return new List<BmsNote>();
            }
        }

        /// <summary>
        /// #WAV001, #WAV00A, #WAV010 같은 3자리 WAV 키가 있으면 노트 데이터도 3자리 단위로 읽는다.
        /// </summary>
        public static int DetectNoteDataValueWidth(IEnumerable<string> lines)
        {
            if (lines == null) return 2;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//"))
                    continue;

                var wavMatch = WavKeyRegex.Match(trimmed);
                if (wavMatch.Success && wavMatch.Groups[1].Value.Length == 3)
                    return 3;
            }

            return 2;
        }

        /// <summary>
        /// BPM 숫자를 읽습니다. 소수점은 시스템 문화권과 무관하게 항상 '.'로 읽고,
        /// 숫자가 아니거나(예: "1.2.3") 0 이하·무한대·NaN이면 false입니다.
        /// BPM이 0이면 박 길이(60 / BPM)가 무한대가 되어 모든 노트 시간이 망가지므로 값으로 받지 않습니다.
        /// </summary>
        public static bool TryParseBpm(string text, out float bpm)
        {
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out bpm)
                && bpm > 0f
                && !float.IsInfinity(bpm)
                && !float.IsNaN(bpm);
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
                if (line.StartsWith("#BPM", StringComparison.OrdinalIgnoreCase))
                {
                    var match = BpmRegex.Match(line);
                    if (match.Success)
                    {
                        if (TryParseBpm(match.Groups[1].Value, out var parsedBaseBpm))
                        {
                            baseBpm = parsedBaseBpm;
                            baseFreq = 60f / baseBpm;
                        }
                        else
                        {
                            MelonLogger.Warning($"[BmsParser] BPM 값을 읽지 못해 이 줄을 무시합니다: {line.Trim()}");
                        }
                    }
                }

                // BPM 인덱스 테이블 (#BPMXX: value) — measure 데이터에서 hexValue로 참조됨
                var bpmMatch = BpmIndexRegex.Match(line);
                if (bpmMatch.Success)
                {
                    var bpmIndex = Convert.ToInt32(bpmMatch.Groups[1].Value, 16);
                    if (TryParseBpm(bpmMatch.Groups[2].Value, out var bpmValue))
                    {
                        bpmIndexTable[bpmIndex] = bpmValue;
                    }
                    else
                    {
                        MelonLogger.Warning($"[BmsParser] BPM 값을 읽지 못해 이 줄을 무시합니다: {line.Trim()}");
                    }
                }
            }
        }

        /// <summary>
        /// 노트 데이터 파싱
        /// </summary>
        private static void ParseNotes(string[] lines, List<BmsNote> notes, float baseBpm, float baseFreq,
            Dictionary<int, float> bpmIndexTable, List<BpmChange> bpmChanges, int noteValueWidth)
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
                        var parsedNotes = BmsNoteDataParser.ParseNoteData(currentMeasure, channel, data, noteValueWidth);
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
        /// <summary>
        /// BPM 변화 처리. 채널 03은 값(16진수)이 BPM 자체이고, 채널 08은 값이 #BPMxx 표의 인덱스입니다(E1).
        /// 04·05·06·07은 BGA 채널이라 BPM 변화가 아닙니다. 예전에는 이 채널들도 BPM 인덱스로 읽었습니다.
        /// </summary>
        private static void ProcessBpmChange(int channel, string data, int currentMeasure, float baseBpm,
            Dictionary<int, float> bpmIndexTable, List<BpmChange> bpmChanges)
        {
            if (channel != 0x03 && channel != 0x08) return;

            var values = BmsNoteDataParser.ParseHexData(data);
            var measureLength = values.Count;
            if (measureLength == 0) return;

            for (int i = 0; i < values.Count; i++)
            {
                var value = values[i];
                if (value <= 0) continue;

                float bpmValue;
                if (channel == 0x03)
                {
                    bpmValue = value;
                }
                else
                {
                    // BPM 인덱스 테이블에서 실제 BPM 조회 (없으면 기본 BPM)
                    bpmValue = (bpmIndexTable != null && bpmIndexTable.TryGetValue(value, out var v)) ? v : baseBpm;
                }

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
                note.Time = CalculateTime(note.Tick, baseBpm, baseFreq, sortedBpmChanges);

                // Duration이 Tick 단위로 저장되어 있다면 Time 단위로 변환
                if (note.Duration > 0 && (note.Type == NoteType.Hold || note.Type == NoteType.Fairy))
                {
                    // Duration을 Time 단위로 변환
                    var startTime = note.Time;
                    var endTick = note.Tick + note.Duration;
                    var endTime = CalculateTime(endTick, baseBpm, baseFreq, sortedBpmChanges);
                    note.Duration = endTime - startTime;
                }
            }
        }

        /// <summary>
        /// Tick을 시간(초)으로 변환
        /// </summary>
        public static float CalculateTime(float tick, float baseBpm, float baseFreq, List<BpmChange> sortedBpmChanges)
        {
            // BPM 변화가 없는 경우
            if (sortedBpmChanges.Count == 0)
            {
                // tick은 measure 단위이므로 1 measure = 4 beats를 곱해야 함
                return tick * 4f * baseFreq;
            }

            // BPM 변화가 있는 경우
            float time = 0f;
            float lastTick = 0f;
            float lastFreq = baseFreq;

            // 정렬된 리스트에서 현재 tick보다 작거나 같은 BPM 변화만 찾기 (이미 정렬되어 있음)
            foreach (var change in sortedBpmChanges)
            {
                if (change.Tick > tick)
                    break; // 정렬되어 있으므로 더 이상 찾을 필요 없음

                // 이전 구간 계산
                if (change.Tick > lastTick)
                {
                    var offset = change.Tick - lastTick;
                    time += offset * 4f * lastFreq;  // 1 measure = 4 beats
                    lastTick = change.Tick;
                }
                lastFreq = change.Freq;
            }

            // 마지막 구간 (현재 tick까지)
            if (tick > lastTick)
            {
                var offset = tick - lastTick;
                time += offset * 4f * lastFreq;
            }

            return time;
        }
    }
}
