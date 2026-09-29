# 아티스트 ID(캐릭터) 기반 템플릿 곡 선택

커스텀 곡을 시작할 때 원본 게임이 요구하는 "실제 곡 데이터"를 어느 원본 곡에서 빌려 올지 정하는 방식을 설명합니다.
(2026-09-28 코드 기준)

## 왜 필요한가

곡 시작 창(`coOpenPreMusicStartWindow`)과 그 뒤의 플레이 준비는 `soRythmGameMusicDataMap`에서 `mCurentMusicId`의
곡 데이터를 찾습니다. 커스텀 MusicID(54~)에는 데이터가 없으므로, 시작 직전에 **원본 곡 하나의 ID를 빌려**(템플릿 곡) 씁니다.
원본 흐름은 이후 템플릿 곡의 캐릭터(`MusicData.charactorID`)를 쓰므로, 템플릿 곡을 커스텀 곡의 캐릭터와 맞춰야
시작 이후 캐릭터 관련 표시가 커스텀 곡과 어긋나지 않습니다.

## 게임의 캐릭터 값

`artistID`는 게임의 `PCD.MainCharactor` enum이며 값은 세 개뿐입니다.

| enum | 값 | `info.txt`에 쓸 수 있는 표기 (대소문자 무시) |
|------|----|------------------------------------------|
| `Morpho` | 0 | `Morpho`, `르호` |
| `Roro` | 1 | `Roro` |
| `Luxair` | 2 | `Luxair`, `룩시아` |

한글 표기 변환은 [CharacterNames.cs](../../GRC2/Helpers/CharacterNames.cs)의 `CharacterNames.Normalize()` 한 곳에 있고,
[AlbumManager.cs](../../GRC2/Core/AlbumManager.cs)(첫 곡 등록·조회)와
[MusicScrollViewHooks.cs](../../GRC2/Harmony/MusicScrollViewHooks.cs)(`artistID` 결정)가 이것을 씁니다.
(2026-09-29 이전에는 `AlbumManager.NormalizeArtistId()`와 `MusicScrollViewHooks.NormalizeCharacterName()` 두 곳에 같은 내용이 있었습니다.)

## 1. 캐릭터별 첫 곡 등록

곡 목록이 만들어질 때(`initializeMusicDataByDefault` postfix) 원본 곡을 목록 순서대로 보면서, 캐릭터마다 처음 나온 곡의
`(MusicID, 제목)`을 `AlbumManager.RegisterArtistFirstSong()`으로 등록합니다. 키는 enum 이름(`Morpho` 등)입니다.

```
[AlbumManager] 아티스트 첫 곡 등록: Morpho (정규화: Morpho) -> MusicID: …, 제목: '…'
```

## 2. 커스텀 곡마다 템플릿 곡 결정

같은 postfix에서 커스텀 항목을 만들 때 `RegisterTemplateSong()`이 템플릿 곡을 정합니다.

1. `info.txt`의 `캐릭터` 값 → 없으면 `아티스트` 값으로 `GetArtistFirstSong()`을 찾습니다.
2. 찾지 못하면 목록 첫 곡(항목을 복사할 때 쓴 곡)을 템플릿으로 씁니다.
3. 템플릿 곡의 제목을 `RegisterOriginalTitle(템플릿 ID, 제목)`으로 등록합니다(TextPatch가 바꿀 대상).

커스텀 항목의 `artistID`도 캐릭터 값이 위 세 가지 중 하나면 그 값으로 바꿉니다. 아니면 템플릿 값을 유지합니다.

## 3. 곡 시작 직전 ID 치환

`GameFlowHooks.CoOpenPreMusicStartWindowPrefix()` (`cMusicSelectSceneUIUpdater.coOpenPreMusicStartWindow` prefix):

1. 커스텀 곡이 선택된 상태(`ShouldInjectCustomContent()`)일 때만 동작합니다.
2. 캐릭터(없으면 아티스트)로 `GetArtistFirstSong()`을 찾고, 없으면 `MusicScrollViewHooks.TryGetTemplateSong()`으로
   2단계에서 정한 템플릿 곡을 씁니다.
3. `__instance.mCurentMusicId`를 템플릿 곡 ID로 바꾸고, 그 제목을 원본 제목으로 등록합니다.

원본은 이 값을 되돌리지 않으므로 이후 흐름은 템플릿 곡 데이터로 진행됩니다. 세이브에 템플릿 ID가 남지 않도록
`setCurrentSelectDataToGameData`와 결과 씬의 `initializePreFade`에서 커스텀 ID로 다시 고칩니다
([커스텀_곡_주입_시스템_분석.md](커스텀_곡_주입_시스템_분석.md)).

## 4. 제목 교체

템플릿 곡 제목이 화면에 나오면 [TextPatch](텍스트_패치_시스템_분석.md)가 커스텀 제목으로 바꿉니다.

## 주의사항

- 캐릭터를 쓰지 않거나 세 값 중 어느 것도 아니면, 목록 첫 곡이 템플릿이 됩니다.
- 템플릿 곡의 오프셋·기본 BPM 등은 커스텀 차트에도 적용됩니다([알려진_문제.md](../maintenance/알려진_문제.md) B2).

## 관련 문서

- [커스텀_곡_주입_시스템_분석.md](커스텀_곡_주입_시스템_분석.md)
- [텍스트_패치_시스템_분석.md](텍스트_패치_시스템_분석.md)
- [앨범_관리_시스템_분석.md](앨범_관리_시스템_분석.md)
