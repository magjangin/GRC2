# BMS 파서 내부 구조 분석

## 개요

BMS 파일 파싱 시스템의 내부 구조와 알고리즘을 상세히 분석합니다.

## 파싱 파이프라인

```
BMS 파일
    ↓
BmsParser.ParseBmsFile()
    ├─ 1. BPM 정보 수집 (CollectBpmInfo)
    ├─ 2. 노트 데이터 파싱 (ParseNotes)
    │   └─ BmsNoteDataParser.ParseNoteData()
    ├─ 3. 홀드 노트 매칭 (HoldNoteProcessor)
    ├─ 4. 페어리 노트 매칭 (FairyNoteProcessor)
    └─ 5. 시간 계산 (BmsTimeCalculator)
    ↓
List<BmsNote>
```

## 핵심 클래스

### BmsParser

**정규식 캐싱 (성능 최적화):**

```csharp
private static readonly Regex BpmRegex = new Regex(@"^#BPM\s+([0-9.]+)", RegexOptions.Compiled);
private static readonly Regex BpmIndexRegex = new Regex(@"^#BPM([0-9A-Fa-f]{2}):\s*([0-9.]+)", RegexOptions.Compiled);
private static readonly Regex MeasureRegex = new Regex(@"^#(\d{3})(\d{2}):", RegexOptions.Compiled);
```

**메인 파싱 메서드:**

```csharp
public static List<BmsNote> ParseBmsFile(string filePath, bool printSummary = true)
{
    var notes = new List<BmsNote>();
    var lines = File.ReadAllLines(filePath);
    var bpmChanges = new List<BpmChange>();
    float baseBpm = 120f;
    
    // 1. BPM 정보 수집
    CollectBpmInfo(lines, ref baseBpm, ref baseFreq, bpmChanges);
    
    // 2. 노트 파싱
    ParseNotes(lines, notes, baseBpm, baseFreq, bpmChanges);
    
    // 3. 홀드/페어리 1차 매칭 (같은 레인 + 같은 방향(IsLeft), 상호 참조 StartNote/EndNote 설정)
    HoldNoteProcessor.MatchHoldNotes(notes);
    FairyNoteProcessor.MatchFairyNotes(notes);
    
    // 4. 시간 계산
    CalculateNoteTimes(notes, baseBpm, baseFreq, bpmChanges);

    // 5. 페어리 2차 보정 (시간 기반 재매칭)
    FairyNoteProcessor.ReconcileFairyNotes(notes);
    
    return notes;
}
```

### BmsNoteDataParser

**채널-레인 매핑:**

```csharp
public static readonly Dictionary<int, int> ChannelToLaneMap = new Dictionary<int, int>
{
    { 0x11, 0 }, { 0x12, 1 }, { 0x14, 2 }, 
    { 0x15, 3 }, { 0x16, 4 }, { 0x18, 5 }
};
```

**노트 데이터 파싱:**

```csharp
public static List<BmsNote> ParseNoteData(int measure, int channel, string data)
{
    var notes = new List<BmsNote>();
    var lane = ChannelToLaneMap[channel];
    var hexValues = ParseHexData(data);
    
    for (int i = 0; i < hexValues.Count; i++)
    {
        if (hexValues[i] == 0) continue;
        
        var tick = measure + (i / (float)hexValues.Count);
        var noteType = GetNoteType(hexValues[i]);
        var direction = GetFlickDirection(hexValues[i]);
        
        notes.Add(new BmsNote
        {
            Tick = tick,
            Lane = lane,
            Type = noteType,
            Direction = direction,
            IsLeft = DetermineLeftRight(channel)
        });
    }
    
    return notes;
}
```

### BmsTimeCalculator

**시간 계산 알고리즘:**

```csharp
public static float CalculateTime(float tick, float baseBpm, float baseFreq, List<BpmChange> bpmChanges)
{
    float time = 0f;
    float currentTick = 0f;
    float currentFreq = baseFreq;
    
    // BPM 변화를 순회하며 시간 계산
    foreach (var bpmChange in bpmChanges.OrderBy(b => b.Tick))
    {
        if (bpmChange.Tick > tick)
            break;
        
        // 이전 구간의 시간 추가
        time += (bpmChange.Tick - currentTick) * currentFreq;
        currentTick = bpmChange.Tick;
        currentFreq = bpmChange.Freq;
    }
    
    // 마지막 구간의 시간 추가
    time += (tick - currentTick) * currentFreq;
    
    return time;
}
```

## BMS 파일 형식

### 헤더 정보

```bms
#BPM 120          // 기본 BPM
#BPM01: 140       // BPM 인덱스 01 = 140
#BPM02: 180       // BPM 인덱스 02 = 180
```

### 노트 데이터

```bms
#00111:01000100  // Measure 001, Channel 11 (레인 0)
#00112:00010000  // Measure 001, Channel 12 (레인 1)
```

**채널 번호:**
- 11-12, 14-16, 18: 노트 레인
- 03-08: BPM 변화

**노트 값:**
- 01: 일반 노트 (Tap)
- 02: 홀드 시작
- 19: 홀드 끝
- 03-0A: 플릭 (8방향)
- 11-18: 페어리 (8방향)
- 1A-1B: 페어리 끝

## 성능 최적화

### 1. 정규식 컴파일

```csharp
// ✅ 좋은 예: RegexOptions.Compiled
private static readonly Regex BpmRegex = new Regex(@"^#BPM\s+([0-9.]+)", RegexOptions.Compiled);

// ❌ 나쁜 예: 매번 생성
var regex = new Regex(@"^#BPM\s+([0-9.]+)");
```

### 2. BPM 변화 정렬 캐싱

```csharp
// ✅ 한 번만 정렬
var sortedBpmChanges = bpmChanges.OrderBy(b => b.Tick).ToList();

foreach (var note in notes)
{
    note.Time = CalculateTime(note.Tick, baseBpm, baseFreq, sortedBpmChanges);
}
```

### 3. 문자열 파싱 최적화

```csharp
public static List<int> ParseHexData(string data)
{
    var values = new List<int>();
    
    // 2자리씩 파싱
    for (int i = 0; i < data.Length; i += 2)
    {
        if (i + 1 < data.Length)
        {
            var hex = data.Substring(i, 2);
            values.Add(Convert.ToInt32(hex, 16));
        }
    }
    
    return values;
}
```

## 참고 자료

- `GRC2/Parsers/BmsParser.cs`
- `GRC2/Parsers/BmsNoteDataParser.cs`
- `GRC2/Parsers/BmsTimeCalculator.cs`
- `GRC2/Parsers/BmsBpmParser.cs`
