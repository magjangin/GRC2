using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GRC2.Processors;
using MelonLoader;
using GRC2.Helpers;

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
        // [0-9]인 이유: .NET의 \d는 전각·아랍 숫자 같은 유니코드 숫자도 잡아 int.Parse가 던지고, 그러면 파일의 노트를
        // 전부 버립니다(2026-11-25, SourceRulesTests가 \d를 막음).
        private static readonly Regex MeasureRegex = new Regex(@"^#([0-9]{3})([0-9]{2}):", RegexOptions.Compiled);
        private static readonly Regex WavKeyRegex = new Regex(@"^#WAV([0-9A-Za-z]{2,3})(?:\s|:)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex TitleHeaderRegex = new Regex(@"^#TITLE(?::|\s)\s*(.+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ArtistHeaderRegex = new Regex(@"^#ARTIST(?::|\s)\s*(.+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// BMS 파일에서 #TITLE, #ARTIST 헤더를 추출합니다. info.txt가 없는 앨범의 폴백으로 쓰입니다(F1).
        /// </summary>
        public static (string Title, string Artist) ExtractHeaderMetadata(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return (null, null);

            string title = null;
            string artist = null;

            using (var reader = new StringReader(text))
            {
                string line;
                int linesRead = 0;
                while ((line = reader.ReadLine()) != null && linesRead++ < 200)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal)) continue;

                    if (title == null)
                    {
                        var m = TitleHeaderRegex.Match(trimmed);
                        if (m.Success)
                        {
                            title = m.Groups[1].Value.Trim().Normalize(System.Text.NormalizationForm.FormC);
                            if (artist != null) break;
                            continue;
                        }
                    }

                    if (artist == null)
                    {
                        var m = ArtistHeaderRegex.Match(trimmed);
                        if (m.Success)
                        {
                            artist = m.Groups[1].Value.Trim().Normalize(System.Text.NormalizationForm.FormC);
                            if (title != null) break;
                            continue;
                        }
                    }

                    // 마디 데이터(#xxx01: 등)가 나오기 시작하면 헤더 영역이 끝난 것이므로 중단
                    if (trimmed.Length >= 6 && trimmed[0] == '#' &&
                        trimmed[1] >= '0' && trimmed[1] <= '9' &&
                        trimmed[2] >= '0' && trimmed[2] <= '9' &&
                        trimmed[3] >= '0' && trimmed[3] <= '9')
                    {
                        break;
                    }
                }
            }

            return (title, artist);
        }

        /// <summary>
        /// BMS 파일 파싱 메인 메서드
        /// </summary>
        public static List<BmsNote> ParseBmsFile(string filePath, bool printSummary = true)
        {
            if (!File.Exists(filePath))
            {
                MelonLogger.Error($"[BmsParser] 파일을 찾을 수 없습니다: {filePath}");
                return new List<BmsNote>();
            }

            try
            {
                ModLog.Verbose($"[BmsParser] BMS 파일 파싱 시작: {filePath}");

                byte[] bytes = File.ReadAllBytes(filePath);
                string text = TextFileDecoder.Decode(bytes, out bool isValidUtf8);
                if (!isValidUtf8)
                {
                    MelonLogger.Warning($"[BmsParser] {Path.GetFileName(filePath)}이(가) UTF-8이 아닙니다. 특수문자나 주석이 손상될 수 있습니다.");
                }

                return ParseBmsText(text, filePath, printSummary);
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, "[BmsParser]", "파싱 오류 (이 파일의 노트를 모두 버립니다)");
                return new List<BmsNote>();
            }
        }

        /// <summary>
        /// 텍스트 내용에서 BMS 노트를 파싱합니다.
        /// </summary>
        public static List<BmsNote> ParseBmsText(string text, string sourceName = "text", bool printSummary = false)
        {
            var notes = new List<BmsNote>();
            if (string.IsNullOrWhiteSpace(text))
                return notes;

            try
            {
                var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                var bpmChanges = new List<BpmChange>();
                var bpmIndexTable = new Dictionary<int, float>(); // #BPM01: 140 등 인덱스→BPM 값
                float baseBpm = 120f;  // 기본 BPM
                float baseFreq = 60f / baseBpm;
                int noteValueWidth = DetectNoteDataValueWidth(lines);

                // 1단계: BPM 정보 수집 (기본 BPM + #BPMXX: value 인덱스 테이블)
                CollectBpmInfo(lines, ref baseBpm, ref baseFreq, bpmIndexTable);

                // 1-1단계: 마디 길이 배율 채널 02 (#xxx02:factor) 수집 (E1 완전 수정)
                var measureLengths = CollectMeasureLengths(lines);
                var measureStartTicks = measureLengths.Count > 0 ? BuildMeasureStartTicks(measureLengths, 1000) : null;

                // 2단계: 노트 데이터 파싱
                ParseNotes(lines, notes, baseBpm, baseFreq, bpmIndexTable, bpmChanges, noteValueWidth, measureLengths, measureStartTicks);

                // 노트 순서 무결성 보장: 마디 정의가 뒤섞인 파일이라도 Tick 순으로 정렬 (P30)
                notes.Sort((a, b) => a.Tick.CompareTo(b.Tick));

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
                    BmsSummaryPrinter.PrintParseSummary(notes, sourceName);
                }

                try
                {
                    MelonLogger.Msg($"[BmsParser] 파싱 완료: {notes.Count}개 노트");
                }
                catch { }
                return notes;
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, "[BmsParser]", "텍스트 파싱 오류");
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
                if (trimmed.StartsWith("//", StringComparison.Ordinal))
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
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//", StringComparison.Ordinal))
                    continue;

                // 기본 BPM 설정 (#BPM) 및 인덱스 테이블 (#BPMXX: value)
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
        }

        /// <summary>
        /// 채널 02(#xxx02:factor)로 지정된 마디 길이 배율을 수집합니다(E1).
        /// </summary>
        public static Dictionary<int, float> CollectMeasureLengths(string[] lines)
        {
            var lengths = new Dictionary<int, float>();
            if (lines == null) return lengths;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//", StringComparison.Ordinal))
                    continue;

                var measureMatch = MeasureRegex.Match(line);
                if (measureMatch.Success)
                {
                    int measure = int.Parse(measureMatch.Groups[1].Value);
                    int channel = int.Parse(measureMatch.Groups[2].Value);
                    if (channel == 0x02)
                    {
                        var data = line.Substring(measureMatch.Length).Trim();
                        if (float.TryParse(data, NumberStyles.Float, CultureInfo.InvariantCulture, out float length) && length > 0f)
                        {
                            lengths[measure] = length;
                        }
                    }
                }
            }
            return lengths;
        }

        /// <summary>
        /// 마디별 시작 Tick 누적 배열을 계산합니다.
        /// </summary>
        public static float[] BuildMeasureStartTicks(Dictionary<int, float> measureLengths, int maxMeasure = 1000)
        {
            var startTicks = new float[maxMeasure + 1];
            float current = 0f;
            for (int m = 0; m <= maxMeasure; m++)
            {
                startTicks[m] = current;
                float len = (measureLengths != null && measureLengths.TryGetValue(m, out float l)) ? l : 1.0f;
                current += len;
            }
            return startTicks;
        }

        /// <summary>
        /// 노트 데이터 파싱
        /// </summary>
        private static void ParseNotes(
            string[] lines,
            List<BmsNote> notes,
            float baseBpm,
            float baseFreq,
            Dictionary<int, float> bpmIndexTable,
            List<BpmChange> bpmChanges,
            int noteValueWidth,
            Dictionary<int, float> measureLengths = null,
            float[] measureStartTicks = null)
        {
            int currentMeasure = 0;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("//", StringComparison.Ordinal))
                    continue;

                // Measure 정의 (#XXXYY: ...)
                var measureMatch = MeasureRegex.Match(line);
                if (measureMatch.Success)
                {
                    currentMeasure = int.Parse(measureMatch.Groups[1].Value);
                    var channel = int.Parse(measureMatch.Groups[2].Value);
                    var data = line.Substring(measureMatch.Length).Trim();

                    float startTick = (measureStartTicks != null && currentMeasure >= 0 && currentMeasure < measureStartTicks.Length)
                        ? measureStartTicks[currentMeasure]
                        : currentMeasure;
                    float lengthMultiplier = (measureLengths != null && measureLengths.TryGetValue(currentMeasure, out float len))
                        ? len
                        : 1.0f;

                    // BPM 변화 처리 (채널 03은 직접 BPM 값, 채널 08은 #BPMxx 인덱스. 04~07 BGA 채널은 건너뜁니다)
                    if (channel == 0x03 || channel == 0x08)
                    {
                        ProcessBpmChange(channel, data, currentMeasure, baseBpm, bpmIndexTable, bpmChanges, startTick, lengthMultiplier);
                    }

                    // 노트 채널 처리 (11-12, 14-16, 18)
                    if (BmsNoteDataParser.ChannelToLaneMap.ContainsKey(channel))
                    {
                        var parsedNotes = BmsNoteDataParser.ParseNoteData(currentMeasure, channel, data, noteValueWidth, startTick, lengthMultiplier);
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
        /// BPM 변화 처리. 채널 03은 값(16진수)이 BPM 자체이고, 채널 08은 값이 #BPMxx 표의 인덱스입니다(E1).
        /// 04·05·06·07은 BGA 채널이라 BPM 변화가 아닙니다. 예전에는 이 채널들도 BPM 인덱스로 읽었습니다.
        /// </summary>
        private static void ProcessBpmChange(
            int channel,
            string data,
            int currentMeasure,
            float baseBpm,
            Dictionary<int, float> bpmIndexTable,
            List<BpmChange> bpmChanges,
            float measureStartTick = -1f,
            float measureLengthMultiplier = 1.0f)
        {
            if (channel != 0x03 && channel != 0x08) return;

            var values = BmsNoteDataParser.ParseHexData(data);
            var measureLength = values.Count;
            if (measureLength == 0) return;

            float startTick = measureStartTick >= 0f ? measureStartTick : currentMeasure;

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

                // measure 내 위치 반영 (0.0~1.0) 및 마디 길이 배율 적용
                var positionInMeasure = (float)i / measureLength;
                var tick = startTick + positionInMeasure * measureLengthMultiplier;

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
            // BPM 변화 리스트를 한 번 정렬(OrderBy는 안정 정렬이라 같은 Tick의 순서가 유지됨)하고,
            // 변화마다의 누적 시간을 미리 계산해 노트마다 이분 탐색합니다(P8: 예전에는 노트마다 목록을 처음부터 훑음).
            var sortedBpmChanges = bpmChanges.OrderBy(b => b.Tick).ToList();
            var timeline = new BpmTimeline(baseFreq, sortedBpmChanges);

            foreach (var note in notes)
            {
                note.Time = timeline.ToSeconds(note.Tick);

                // Duration이 Tick 단위로 저장되어 있다면 Time 단위로 변환
                if (note.Duration > 0 && (note.Type == NoteType.Hold || note.Type == NoteType.Fairy))
                {
                    // Duration을 Time 단위로 변환
                    var startTime = note.Time;
                    var endTick = note.Tick + note.Duration;
                    var endTime = timeline.ToSeconds(endTick);
                    note.Duration = endTime - startTime;
                }
            }
        }

        /// <summary>
        /// Tick을 시간(초)으로 변환합니다. 노트마다 변화 목록을 처음부터 훑는 기준 구현입니다.
        /// 파서는 같은 결과를 내는 <see cref="BpmTimeline"/>을 쓰고, 이 메서드는 그 기준값(테스트)으로 남겨 둡니다.
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
