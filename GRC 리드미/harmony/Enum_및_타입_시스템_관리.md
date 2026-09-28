# Enum 및 타입 시스템 관리

모드가 쓰는 게임 enum과, BMS 값을 게임 enum으로 바꾸는 [`EnumValueHelper`](../../GRC2/Helpers/EnumValueHelper.cs)를 정리합니다.
게임 타입은 `Assembly-CSharp.dll`을 직접 참조하므로 `Enum.Parse`나 런타임 타입 탐색을 쓰지 않습니다.
값은 `Decompiled/`로 확인했습니다. (2026-09-28 기준)

## 게임 enum

| enum (네임스페이스) | 멤버 = 값 |
|---------------------|-----------|
| `NoteTypeId` (`IntiCreates.RythmGame.FairyMode`) | Touch=0, Flick=1, Fairy=2, Hold=3, Boost_ButForSystem=4, Hold_Middle=5, NUM=6 |
| `NoteLaneLeftRight` (`…FairyMode`) | Left=0, Right=1, Num=2 |
| `NoteSubLaneType` (`…FairyMode`) | Lane_1=0, Lane_2=1, Lane_3=2, Num=3 |
| `NoteDirectionIndex` (`IntiCreates.RythmGame`) | LEFT_BOTTOM=0, CENTER_BOTTOM=1, RIGHT_BOTTOM=2, LEFT_MIDDLE=3, CENTER_MIDDLE=4, RIGHT_MIDDLE=5, LEFT_TOP=6, CENTER_TOP=7, RIGHT_TOP=8, NUM=9 |
| `TurnDirection` (`IntiCreates.RythmGame`) | Right=0, Left=1, NUM=2 |
| `NoteSize` (`IntiCreates.RythmGame`) | Scale1=0, Scale1_5Right=1, Scale1_5Left=2, Scale2=3(=FullOfFairyLane), NUM=4 |
| `JudgeType` (`IntiCreates.RythmGame`) | PERFECT=0, GREAT=1, GOOD=2, BAD=3, MISS=4 |
| `ResultClearBadge` (`IntiCreates.RythmGame`) | NoPlayed=0, NoCleard=1, Cleard=2, FullCombo=3, AllPerfect=4 |
| `PCD.MainCharactor` (`IntiCreates`) | Morpho=0, Roro=1, Luxair=2 |

`slideEndFlickDirection` 필드는 별도 enum이 아니라 `NoteDirectionIndex`입니다(기본값 `NUM`).

### `soRythmGameMusicDataMap.MusicID`

| 범위 | 의미 |
|------|------|
| 0~21 | 기본 곡 (`FIRST_VER_DATA_TOP` ~ `FIRST_VER_DATA_END`) |
| 22~53 | DLC 곡 (`DLC1_DATA_TOP` ~ `DLC2_02_DATA_END`) |
| **54~511** | 비어 있음 → 모드의 커스텀 곡 ID |
| 512 | `SAVEABLE_ID_END` (세이브 배열 크기) |
| 8000~ | `UNIQUE_TOP`, 튜토리얼 등 |

## BMS → 게임 매핑 (`EnumValueHelper`)

모두 `switch` 문으로 되어 있고 캐시가 없습니다.

| 메서드 | 매핑 |
|--------|------|
| `GetNoteTypeId(NoteType)` | Touch→Touch, Hold/HoldEnd→Hold, Flick→Flick, Fairy/FairyEnd→Fairy |
| `GetDirectionIndex(NoteDirection)` | Left→LEFT_MIDDLE, LeftUp→LEFT_TOP, Up→CENTER_TOP, RightUp→RIGHT_TOP, Right→RIGHT_MIDDLE, RightDown→RIGHT_BOTTOM, Down→CENTER_BOTTOM, LeftDown→LEFT_BOTTOM, 그 밖→CENTER_MIDDLE |
| `GetDirectionIndexFromLane(lane, isLeft)` | 홀드 시작용. Lane 0/1/2 → *_BOTTOM/*_MIDDLE/*_TOP (좌우에 따라 LEFT_/RIGHT_) |
| `GetSubLaneType(lane)` | 0→Lane_1, 1→Lane_2, 2→Lane_3 |
| `GetLaneLeftRight(isLeft)` | true→Left, false→Right |
| `ToLaneIndex(NoteSubLaneType)` | 위의 역방향 |
| `ToBmsNoteType(NoteTypeId)` | Fairy→Fairy, Hold/Hold_Middle→Hold, Touch→Touch, Flick→Flick, 그 밖→null |
| `IsLeftDirection(NoteDirectionIndex)` | LEFT_BOTTOM/LEFT_MIDDLE/LEFT_TOP이면 true (CENTER는 오른쪽 취급) |

## 모드 쪽 enum ([BmsDataModels.cs](../../GRC2/Parsers/BmsDataModels.cs))

```csharp
public enum NoteType { Touch, Hold, HoldEnd, Flick, Fairy, FairyEnd }
public enum NoteDirection { None, Left, LeftUp, Up, RightUp, Right, RightDown, Down, LeftDown }
```

## 새 enum 값을 쓸 때

1. `Decompiled/`에서 enum 정의와 원본이 그 값을 어떻게 쓰는지 확인합니다.
2. 게임 enum 타입을 그대로 참조합니다(문자열로 찾지 않음). 게임 업데이트로 이름이 바뀌면 컴파일 오류로 드러납니다.
3. `EnumValueHelper`에 `switch` 한 줄을 추가하고, 이 문서의 표를 갱신합니다.
