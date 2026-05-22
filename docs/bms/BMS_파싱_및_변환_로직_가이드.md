# BMS 파싱 및 게임 노트 변환 로직 가이드

## 개요

이 문서는 BMS 파일을 파싱하고 게임의 노트 데이터로 변환하는 전체 프로세스를 설명합니다.

**최신 업데이트 (2025년)**:
- 홀드/페어리 노트 매칭: **상호 참조(StartNote/EndNote)** 기반 매칭, 같은 **레인(Lane) + 방향(IsLeft)** 조건
- `allBmsNotes`에서 직접 찾기로 부동소수점 오차 문제 해결, 폴백 시 **BPM 기반 동적 시간 허용 범위** (0.02~0.15초)
- 노트 주입: `NoteArrayHooks.TryInjectBmsNotes()`에서 **ShouldInjectCustomContent()**(커스텀 선택 + 주입 금지 씬 아님)로 제어

**중요(현재 코드 기준)**:
- **BMS 파싱(스캔/캐시)**은 `SceneDetector.OnInitializeMelon()`에서 `hwa` 내 **모든 앨범 폴더의 모든 BMS 파일**(`*.bms/*.bme/*.bml`)을 스캔/파싱해 **파일 전체 경로 기준 캐시**(`ParsedBmsNotesByFile`)로 보관합니다.
  - 동일 파일명(예: `hwa2.bms`)이 여러 폴더에 있어도, **전체 경로**로 구분되므로 결과가 덮어써지지 않습니다.
  - 요약 로그(`=== BMS 파일 파싱 결과 ===`)도 이 전체 파싱 결과를 기반으로 출력됩니다.
- **실제 노트 주입 대상(`ParsedBmsNotes`)**은 “현재 선택된 앨범의 현재 BMS 파일(기본: 첫 번째 파일)” 기준으로 설정됩니다. (한 앨범에 BMS가 여러 개면, 파싱은 모두 되지만 주입은 현재 선택 파일 1개만 반영)
- **노트 주입(Harmony Hook)**은 `NoteArrayHooks.TryInjectBmsNotes()`에서 **`CustomAssetManager.ShouldInjectCustomContent()`**로 제어됩니다.
  - 즉, **커스텀 차트가 선택되어 있고**, **주입 금지 씬**(SoundPlayerScene, MoviePlayer_MovieSelect)이 **아닐 때만** BMS 노트가 주입됩니다.
  - `hwa` 루트에 BMS가 있어도 일반 곡 선택 시 또는 주입 금지 씬에서는 노트가 주입되지 않습니다.

## 전체 아키텍처

```
BMS 파일 (.bms)
    ↓
[BmsParser] - BMS 파일 파싱
    ↓
BmsNote 리스트
    ↓
[BmsNoteConverter] - 게임 노트 변환
    ↓
NoteCreateData 배열 (게임 노트)
```

## 1. BMS 파일 파싱 (BmsParser)

### 1.1 주요 클래스 및 구조

#### BmsNote 클래스
```csharp
public class BmsNote
{
    public int Channel { get; set; }        // BMS 채널 번호
    public float Tick { get; set; }         // measure 단위 위치
    public float Time { get; set; }          // 초 단위 시간
    public int Lane { get; set; }           // 게임 레인 (0-2)
    public bool IsLeft { get; set; }        // 왼쪽 레인 여부
    public NoteType Type { get; set; }      // 노트 타입
    public NoteDirection? Direction { get; set; }  // 방향 (플릭/페어리)
    public float Duration { get; set; }     // 홀드/페어리 길이 (초)
}
```

#### NoteType 열거형
- `Touch`: 일반 노트 (01)
- `Hold`: 홀드 노트 시작 (02)
- `HoldEnd`: 홀드 노트 끝 (19)
- `Flick`: 플릭 노트 (03-0A)
- `Fairy`: 페어리 노트 (11-18)
- `FairyEnd`: 페어리 끝 노트 (1A-1B)

#### NoteDirection 열거형
- 8방향: Left, LeftUp, Up, RightUp, Right, RightDown, Down, LeftDown

### 1.2 채널 매핑

BMS 채널 → 게임 레인 매핑:

| BMS 채널 | 게임 레인 | 위치 |
|---------|----------|------|
| 16      | 0        | Left 1 |
| 11      | 1        | Left 2 |
| 12      | 2        | Left 3 |
| 14      | 0        | Right 1 |
| 15      | 1        | Right 2 |
| 18      | 2        | Right 3 |

### 1.3 파싱 프로세스

#### 단계 1: BPM 정보 수집
```csharp
// 기본 BPM (#BPM)
#BPM 120.0

// BPM 인덱스 (#BPMXX: value)
#BPM01: 140.0
```

**현재 구현 주의사항(코드 기준):**
- `#BPMXX`는 파싱되지만(로그 출력), **실제 시간 계산에 완전하게 반영되지 않을 수 있습니다.**
- BPM 변화 채널 처리도 단순화되어 있어, 복잡한 BPM 변화를 사용하는 BMS는 **타이밍 정확도가 떨어질 수 있습니다.**


#### 단계 2: 노트 데이터 파싱
```csharp
// Measure 정의 형식: #XXXYY: data
#00111: 0001000000000000  // measure 1, 채널 11, 16진수 데이터
```

**데이터 형식:**
- 2자리 16진수로 구성
- 각 값은 measure 내 위치를 나타냄
- 0이 아닌 값은 노트 존재

**노트 타입 판별:**
- `01`: Touch 노트
- `02`: Hold 시작
- `03-0A`: Flick (방향별)
- `11-18`: Fairy (방향별)
- `19`: Hold 끝
- `1A-1B`: Fairy 끝 (1A=Left 턴, 1B=Right 턴 — `BmsNoteDataParser`에서 방향 설정)

#### 단계 3: 홀드 노트 매칭
- **같은 레인(`Lane`) + 같은 방향(`IsLeft`)**에서 `02`(시작)와 `19`(끝)을 매칭
- Duration 계산 (Tick 단위), 상호 참조(`StartNote`/`EndNote`) 설정

#### 단계 4: 페어리 노트 매칭
- **같은 레인(`Lane`) + 같은 방향(`IsLeft`)** 안에서 `11-18`(시작)과 `1A-1B`(끝)을 1차 매칭
- Tick 오름차순 정렬, 같은 Tick이면 Start(Fairy) 먼저. FIFO 큐로 1:1 연결
- Duration 계산 (Tick 단위), 상호 참조(`StartNote`/`EndNote`) 설정

#### 단계 5: 시간 계산
- BPM 변화를 고려하여 Tick → Time 변환
- 1 measure = 4 beats
- BPM 변화가 있으면 구간별로 계산

#### 단계 6: 페어리 2차 보정
- 시간 계산 후 `FairyNoteProcessor.ReconcileFairyNotes()` 실행
- 1차 FIFO에서 놓친 페어리 쌍을 같은 레인 + 같은 방향 기준으로 다시 매칭
- BPM 기반 허용 오차를 적용하고, 그래도 남으면 가장 가까운 미래 끝 노트와 강제 매칭

### 1.4 시간 계산 알고리즘

```csharp
private static float CalculateTime(float tick, float baseBpm, float baseFreq, List<BpmChange> sortedBpmChanges)
{
    // BPM 변화가 없는 경우
    if (sortedBpmChanges.Count == 0)
    {
        return tick * 4f * baseFreq;  // 1 measure = 4 beats
    }
    
    // BPM 변화가 있는 경우 구간별 계산
    // 각 BPM 변화 지점까지의 시간을 누적 계산
}
```

## 2. 게임 노트 변환 (BmsNoteConverter)

### 2.1 변환 프로세스

#### 단계 1: 기본 노트 변환
```csharp
ConvertBmsNotesToNoteCreateData(List<BmsNote> bmsNotes)
```

1. BMS 노트를 시간순으로 정렬
2. 각 노트를 `NoteCreateData`로 변환
3. 홀드/페어리 끝 노트는 별도 저장

#### 단계 2: 홀드 끝 노트 처리
- 홀드 시작 노트의 `connectNodeDataArray`에 끝 노트 추가
- `HoldNoteProcessor.ProcessHoldEndNotes() (구현: HoldNoteProcessor.ConnectNodes.cs)` 호출

#### 단계 3: 페어리 끝 노트 처리
- 페어리 시작 노트의 `connectNodeDataArray`에 끝 노트 추가
- `FairyNoteProcessor.ProcessFairyEndNotes() (구현: FairyNoteProcessor.ConnectNodes.cs)` 호출

#### 단계 4: 마지막 노트 설정
- `perfectSample`이 가장 큰 노트에 `isLast = true` 설정
- `connectNodeDataArray`의 끝 노트도 고려

### 2.2 NoteCreateData 생성 (NoteCreateDataBuilder)

#### 주요 필드 설정

| 필드명 | 설명 | 변환 방법 |
|--------|------|----------|
| `perfectSample` | 정확한 타이밍 (샘플) | `Time * 48000` |
| `laneLeftRightID` | 왼쪽/오른쪽 레인 | `IsLeft` → Enum |
| `subLaneID` | 서브 레인 (0-2) | `Lane` → Enum |
| `noteTypeID` | 노트 타입 | `NoteType` → Enum |
| `directionIndex` | 방향 (플릭/페어리) | `Direction` → Enum |
| `turnDireciton` | 회전 방향 (페어리) | `Direction`에서 추출 |
| `noteSize` | 노트 크기 | 기본값: Scale1 |
| `connectNodeDataArray` | 연결 노트 배열 | 홀드/페어리 끝 노트 |

#### 노트 생성자 파라미터 정보

**생성자 로깅 (현재 코드 기준):**
- 생성자 호출 성공 로그는 `NoteConstructorHelper` 내부의 `EnableConstructorLogging` 플래그로 제어되며 **기본값은 false**(비활성화)입니다.
- 게임의 `NoteCreateData` 생성자/필드 목록을 확인하려면 `NoteConstructorHelper.LogNoteCreateDataConstructorsAndFields()`를 호출하면 됩니다. 생성자 목록과 길이·방향 관련 필드 이름을 로그로 출력합니다.
- 생성자 시도 순서: 2개 파라미터 (time,lane / sample,lane) → 3개 (time,lane,type) → 4개 (time,lane,type,leftRight) → 5개 (lane,time,sample,type,leftRight) → 6개 (lane,time,sample,type,leftRight,direction).

#### 노트 타입별 특수 처리

**Touch 노트:**
- `directionIndex`: CENTER_MIDDLE (기본값)
- `connectNodeDataArray`: 빈 배열

**Hold 노트:**
- `directionIndex`: 레인에 따라 자동 설정
- `connectNodeDataArray`: 끝 노트 포함

**Flick 노트:**
- `directionIndex`: 플릭 방향 (03-0A → 8방향)
- `connectNodeDataArray`: 빈 배열

**Fairy 노트:**
- `directionIndex`: 페어리 방향 (11-18 → 8방향)
- `turnDireciton`: LEFT/RIGHT (방향에서 추출)
- `connectNodeDataArray`: 끝 노트 포함

## 3. 노트 프로세서

### 3.1 HoldNoteProcessor

#### MatchHoldNotes()
- 같은 레인에서 `02`(시작)와 `19`(끝) 매칭
- Duration 계산 (Tick 단위)
- **개선**: `ChannelToLaneMap`을 사용하여 모든 노트 채널(11, 12, 14, 15, 16, 18) 포함

#### ProcessHoldEndNotes()
- 홀드 시작 노트의 `connectNodeDataArray`에 끝 노트 추가
- 끝 노트의 `directionIndex`: CENTER_MIDDLE
- 끝 노트의 `noteSize`: Scale1
- **개선**: `allBmsNotes`에서 직접 홀드 시작 노트를 찾아서 `noteList`와 매칭
- **개선**: `perfectSample`을 직접 비교하여 정확한 매칭 및 중복 생성 방지
- **개선**: 매칭되지 않은 노트를 새로 생성할 때 `noteList`에 추가
- **개선**: 시간 허용 범위 0.05초로 확대
- **개선**: 범위 검색으로 근사치 매칭 지원
- **개선**: 끝 노트의 `laneLeftRightID`, `subLaneID`, `noteTypeID`를 시작 노트와 동일하게 설정

**매칭 알고리즘 (개선된 버전):**
```csharp
// 1. allBmsNotes에서 홀드 시작 노트 직접 찾기
var holdStartBmsNotes = allBmsNotes.Where(n => n.Type == NoteType.Hold && n.Duration > 0).ToList();

// 2. perfectSample 직접 비교하여 noteList와 매칭
var expectedPerfectSample = (int)(holdStartBms.Time * SAMPLE_RATE);
foreach (var noteObj in noteList)
{
    var perfectSample = perfectSampleField?.GetValue(noteObj);
    if (perfectSample != null && (int)perfectSample == expectedPerfectSample)
    {
        // Lane, IsLeft, noteTypeID도 확인
        holdStartMap[key] = (noteObj, holdStartBms);
    }
}

// 3. Dictionary를 사용한 O(1) 검색
var key = $"{Lane}_{IsLeft}_{EndTime:F3}";
```

### 3.2 FairyNoteProcessor

#### MatchFairyNotes()
- **같은 레인(`Lane`) + 같은 방향(`IsLeft`)** 안에서 `11-18`(시작)과 `1A-1B`(끝)을 1차 매칭
- Tick 오름차순 정렬, 같은 Tick이면 Fairy(시작)를 먼저 처리. **FIFO 큐**로 시작/끝 1:1 연결
- Duration 계산 (Tick 단위). 이후 `CalculateNoteTimes`에서 Tick → 초 변환
- 상호 참조 `StartNote`/`EndNote` 설정

#### ReconcileFairyNotes()
- 시간 계산이 끝난 뒤 실행되는 2차 보정 단계
- 시간 역전, 레인/방향 불일치 같은 잘못된 링크를 제거
- 남은 시작/끝 노트를 시간 기준으로 다시 매칭하고, 허용 범위를 벗어나면 가장 가까운 후보와 강제 매칭

#### ProcessFairyEndNotes()
- 페어리 시작 노트의 `connectNodeDataArray`에 끝 노트 추가
- **시작 노트 찾기**: `EndNote.StartNote`(BmsNote 참조) 우선 사용 → 없으면 같은 레인/방향 기준 샘플·시간 오차 기반 폴백 사용
- `NoteProcessorHelper.AddEndNoteToConnectNodeArray` 호출 (`endDirection`: "CENTER_TOP", `copyTurnDirection`: true)
- 페어리 끝 노트(1A/1B)의 `directionIndex`는 **createNoteCreateData에서 설정한 값 유지**(1A=Left, 1B=Right); Helper에서 덮어쓰지 않음
- 끝 노트의 `turnDireciton`: 시작 노트와 동일 복사

**1A/1B 방향 (`BmsNoteDataParser`):**
- `1A` → `NoteType.FairyEnd`, `NoteDirection.Left` (Left 턴)
- `1B` → `NoteType.FairyEnd`, `NoteDirection.Right` (Right 턴)

### 3.3 NoteProcessorHelper

#### AddEndNoteToConnectNodeArray()
- 시작 노트의 `connectNodeDataArray`에 끝 노트 추가
- 배열 크기 동적 증가
- 끝 노트 필드 설정:
  - `directionIndex`: 지정된 값 (CENTER_MIDDLE/CENTER_TOP)
  - `noteSize`: Scale1
  - `turnDireciton`: 페어리인 경우 시작 노트와 동일

## 4. 성능 최적화

### 4.1 캐싱 전략

**BmsNote 검색 캐싱:**
```csharp
// Time 기반 Dictionary 캐시
private static Dictionary<float, BmsNote> _timeToBmsNoteCache;
```

**Reflection 필드 캐싱:**
- `FieldAccessHelper`에서 필드 정보 캐싱
- `EnumValueHelper`에서 Enum 값 캐싱

### 4.2 정렬 최적화

- BMS 노트는 한 번만 정렬 (시간순)
- BPM 변화 리스트는 한 번만 정렬

### 4.3 Dictionary 기반 검색

- 홀드/페어리 끝 노트 매칭 시 Dictionary 사용
- O(1) 검색 시간

## 5. 데이터 흐름도

```
BMS 파일
    ↓
[ParseBmsFile]
    ↓
BmsNote 리스트 (Tick 단위)
    ↓
[MatchHoldNotes] [MatchFairyNotes]
    ↓
BmsNote 리스트 (Duration 포함)
    ↓
[CalculateTime] (각 노트)
    ↓
BmsNote 리스트 (Time 단위)
    ↓
[ConvertBmsNotesToNoteCreateData]
    ↓
[CreateNoteCreateData] (각 노트)
    ↓
NoteCreateData 리스트
    ↓
[ProcessHoldEndNotes] [ProcessFairyEndNotes]
    ↓
NoteCreateData 배열 (connectNodeDataArray 포함)
    ↓
[SetLastNoteFlag]
    ↓
최종 NoteCreateData 배열
```

## 6. 주요 알고리즘

### 6.1 BPM 변화 처리

```csharp
// BPM 변화가 있는 경우 구간별 시간 계산
float time = 0f;
float lastTick = 0f;
float lastFreq = baseFreq;

foreach (var change in sortedBpmChanges)
{
    if (change.Tick > tick) break;
    
    // 이전 구간 계산
    if (change.Tick > lastTick)
    {
        var offset = change.Tick - lastTick;
        time += offset * 4f * lastFreq;  // 1 measure = 4 beats
        lastTick = change.Tick;
    }
    lastFreq = change.Freq;
}

// 마지막 구간
if (tick > lastTick)
{
    var offset = tick - lastTick;
    time += offset * 4f * lastFreq;
}
```

### 6.2 노트 타입 판별

```csharp
private static NoteType GetNoteType(int value, out NoteDirection? direction)
{
    if (value == 0x01) return NoteType.Touch;
    if (value == 0x02) return NoteType.Hold;
    if (value == 0x19) return NoteType.HoldEnd;
    if (value == 0x1A || value == 0x1B) return NoteType.FairyEnd;
    
    // Flick (03-0A)
    if (value >= 0x03 && value <= 0x0A)
    {
        direction = GetFlickDirection(value);
        return NoteType.Flick;
    }
    
    // Fairy (11-18)
    if (value >= 0x11 && value <= 0x18)
    {
        direction = GetFairyDirection(value);
        return NoteType.Fairy;
    }
    
    return NoteType.Touch;
}
```

### 6.3 끝 노트 매칭

```csharp
// Dictionary 기반 빠른 검색
var key = $"{Lane}_{IsLeft}_{EndTime:F3}";
if (startMap.TryGetValue(key, out var start))
{
    // 시간 차이 확인 (0.01초 이내)
    if (Math.Abs(start.BmsNote.Time + start.BmsNote.Duration - endNote.Time) < 0.01f)
    {
        // connectNodeDataArray에 추가
        AddEndNoteToConnectNodeArray(...);
    }
}
```

## 7. 에러 처리

### 7.1 파싱 에러
- 파일이 없거나 읽을 수 없는 경우: 빈 리스트 반환
- 잘못된 형식: 해당 라인 건너뛰기
- BPM 파싱 실패: 기본값 120 BPM 사용

### 7.2 변환 에러
- 노트 변환 실패: 해당 노트 건너뛰고 계속 진행
- 끝 노트 매칭 실패: 경고 로그 출력
- Reflection 실패: 기본값 사용 또는 건너뛰기

### 7.3 로깅
- 주요 단계마다 로그 출력
- 에러 발생 시 상세 스택 트레이스
- 성능 최적화를 위한 로그 제거 (디버깅용)

## 8. 확장 가능성

### 8.1 새로운 노트 타입 추가
1. `NoteType` 열거형에 추가
2. `GetNoteType()` 메서드에 판별 로직 추가
3. `CreateNoteCreateData()`에 변환 로직 추가

### 8.2 새로운 채널 지원
1. `ChannelToLaneMap`에 매핑 추가
2. 채널 번호 범위 확장

### 8.3 BPM 변화 개선
- 현재는 기본 BPM만 사용
- BPM 인덱스 테이블 지원 추가 가능

## 9. 참고 사항

### 9.1 샘플레이트
- 게임 오디오 샘플레이트: 48000 Hz
- `perfectSample = Time * 48000`

### 9.2 시간 단위
- BMS: Tick (measure 단위)
- 중간: Time (초 단위)
- 게임: Sample (샘플 단위)

### 9.3 measure와 beat
- 1 measure = 4 beats
- BPM = beats per minute
- 1 beat = 60 / BPM 초

## 10. 테스트 체크리스트

- [ ] 일반 노트 (Touch) 파싱 및 변환
- [ ] 홀드 노트 (02-19) 파싱 및 매칭
- [ ] 페어리 노트 (11-18, 1A-1B) 파싱 및 매칭
- [ ] 플릭 노트 (03-0A) 8방향 파싱
- [ ] BPM 변화 처리
- [ ] 여러 레인 동시 처리
- [ ] 마지막 노트 isLast 설정
- [ ] connectNodeDataArray 정확성
- [ ] 대용량 BMS 파일 처리
- [ ] 에러 케이스 처리





