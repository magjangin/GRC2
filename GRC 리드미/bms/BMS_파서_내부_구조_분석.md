# BMS 파서 내부 구조 분석

BMS 파일을 `List<BmsNote>`로 만드는 [`BmsParser`](../../GRC2/Parsers/BmsParser.cs)와
[`BmsNoteDataParser`](../../GRC2/Parsers/BmsNoteDataParser.cs)를 설명합니다. (2026-09-28 코드 기준)

## 파싱 순서

```csharp
public static List<BmsNote> ParseBmsFile(string filePath, bool printSummary = true)
{
    var lines = File.ReadAllLines(filePath);                 // UTF-8
    int noteValueWidth = DetectNoteDataValueWidth(lines);    // 2 또는 3
    CollectBpmInfo(lines, ref baseBpm, ref baseFreq, bpmIndexTable);          // 1. #BPM, #BPMxx:
    ParseNotes(lines, notes, baseBpm, baseFreq, bpmIndexTable, bpmChanges, noteValueWidth); // 2. 노트 + BPM 변화
    HoldNoteProcessor.MatchHoldNotes(notes);                 // 3. 02-19 짝 (tick 기준)
    FairyNoteProcessor.MatchFairyNotes(notes);               // 4. 11~18 - 1A/1B 1차 짝 (tick 기준)
    CalculateNoteTimes(notes, baseBpm, baseFreq, bpmChanges); // 5. tick → 초, Duration도 초로
    FairyNoteProcessor.ReconcileFairyNotes(notes);           // 6. 페어리 2차 보정 (초 기준)
    if (printSummary) BmsSummaryPrinter.PrintParseSummary(notes, filePath);
    return notes;
}
```

- 파일이 없으면 빈 리스트, 도중에 예외가 나면 그때까지 모은 노트를 반환합니다(BPM 단계에서 나면 빈 리스트).
- 3·4·6단계는 [홀드_노트_처리_가이드.md](홀드_노트_처리_가이드.md)를 봅니다.

## 데이터 모델 ([BmsDataModels.cs](../../GRC2/Parsers/BmsDataModels.cs))

```csharp
public class BmsNote
{
    public int Channel;          // BMS 채널 (10진수로 읽음: "16" → 16)
    public float Tick;           // 마디 단위 위치 (예: 5.5 = 5마디 한가운데)
    public float Time;           // 초
    public int Lane;             // 0~2 (게임 Lane_1~3)
    public bool IsLeft;
    public NoteType Type;        // Touch, Hold, HoldEnd, Flick, Fairy, FairyEnd
    public NoteDirection? Direction; // 플릭/페어리 방향, 페어리 끝의 회전(Left/Right)
    public float Duration;       // 홀드/페어리 길이 (매칭 직후 tick, 5단계 이후 초)
    public BmsNote StartNote, EndNote; // 짝 참조
    public float BaseBpm;        // 파일의 #BPM (매칭 허용 오차 계산용)
}
```

## 정규식

```csharp
BpmRegex      = ^#BPM\s+([0-9.]+)                        // #BPM 124
BpmIndexRegex = ^#BPM([0-9A-Fa-f]{2}):\s*([0-9.]+)       // #BPM01: 140  (콜론 필수)
MeasureRegex  = ^#(\d{3})(\d{2}):                        // #00516:0001…
WavKeyRegex   = ^#WAV([0-9A-Za-z]{2,3})(?:\s|:)          // WAV 키 폭 감지
```

`//`로 시작하는 줄과 빈 줄은 건너뜁니다. 채널은 두 자리 **10진수**로 읽으므로 `1A` 같은 16진 채널은 인식하지 않습니다.

## 채널 → 레인

```csharp
ChannelToLaneMap = {
    { 16, (0, true)  }, { 11, (1, true)  }, { 12, (2, true)  },   // 왼쪽 Lane_1~3
    { 14, (0, false) }, { 15, (1, false) }, { 18, (2, false) },   // 오른쪽 Lane_1~3
};
```

## 노트 데이터

`ParseNoteData(measure, channel, data, valueWidth)`:

1. `data`를 `valueWidth`(2 또는 3)자씩 끊어 16진수로 읽습니다(`ParseHexData`, 읽지 못한 조각은 버림).
2. 값이 0이 아니면 `tick = measure + i / 조각 수`로 노트를 만듭니다.
3. `GetNoteType(value, out direction)`:

| 값 | Type | Direction |
|----|------|-----------|
| `01` | Touch | — |
| `02` | Hold | — |
| `19` | HoldEnd | — |
| `1A` / `1B` | FairyEnd | Left / Right (회전 방향) |
| `03`~`0A` | Flick | Left, LeftUp, Up, RightUp, Right, RightDown, Down, LeftDown |
| `11`~`18` | Fairy | 위와 같은 순서 |
| 그 밖 | Touch | — |

`DetectNoteDataValueWidth()`는 `#WAV001`처럼 3자리 WAV 키가 하나라도 있으면 3을, 아니면 2를 돌려줍니다.

## BPM

- `#BPM n`: 기본 BPM (없으면 120). `baseFreq = 60 / BPM` (1박 길이).
- `#BPMxx: n`: 인덱스 표(`bpmIndexTable[xx] = n`).
- 채널 03~08의 데이터: 2자리씩 읽어 0이 아닌 값을 표 인덱스로 보고 `BpmChange { Tick, Bpm, Freq }`를 추가합니다.
  표에 없는 인덱스면 기본 BPM을 씁니다.
- 숫자는 `float.Parse`(현재 문화권)로 읽습니다.

표준 BMS와 다른 점(채널 03 직접 값, 04/06/07 BGA 채널, 02 마디 길이, 09 STOP)은 [알려진_문제.md](../maintenance/알려진_문제.md) E1에 있습니다.

## 시간 계산

1마디 = 4박이므로 **초 = tick × 4 × (60 / BPM) = tick × 240 / BPM**입니다.

```csharp
public static float CalculateTime(float tick, float baseBpm, float baseFreq, List<BpmChange> sortedBpmChanges)
{
    if (sortedBpmChanges.Count == 0)
        return tick * 4f * baseFreq;

    float time = 0f, lastTick = 0f, lastFreq = baseFreq;
    foreach (var change in sortedBpmChanges)           // Tick 오름차순
    {
        if (change.Tick > tick) break;
        if (change.Tick > lastTick)
        {
            time += (change.Tick - lastTick) * 4f * lastFreq;
            lastTick = change.Tick;
        }
        lastFreq = change.Freq;
    }
    if (tick > lastTick)
        time += (tick - lastTick) * 4f * lastFreq;
    return time;
}
```

- BPM 변화 목록은 한 번만 정렬합니다.
- 홀드/페어리의 `Duration`(tick)은 `CalculateTime(시작 + Duration) - CalculateTime(시작)`으로 초로 바꿉니다.

예: 124 BPM에서 `#00516:00000100` → 2자리씩 슬롯 4개 중 3번째(i = 2)가 `01` → tick = 5 + 2/4 = 5.5 →
5.5 × 240 / 124 ≈ 10.645초.

## 요약 출력 ([BmsSummaryPrinter.cs](../../GRC2/Parsers/BmsSummaryPrinter.cs))

노트 종류별 개수와, 홀드/페어리 시작마다 `시작 시간 + Duration`에 같은 레인의 끝 노트가 있는지(0.01초 이내)를 출력합니다.

## 테스트

[GRC2.Tests](../../GRC2.Tests)의 `BmsNoteDataParserTests`, `BmsTimeCalculatorTests`가 WAV 폭 감지, 3자리 파싱, BPM 없는 시간 계산을 확인합니다.
