# 아티스트 ID 기반 MusicID 및 텍스트 교체 시스템 분석

## 개요

이 문서는 아티스트 ID를 기반으로 MusicID를 변경하고 텍스트를 교체하는 시스템의 상세한 동작 원리를 설명합니다.

## 시스템 목적

커스텀 차트를 선택했을 때, 해당 차트의 아티스트 ID를 확인하여 해당 아티스트의 첫 곡의 MusicID로 변경하고, 원본 제목을 커스텀 제목으로 교체하는 시스템입니다.

## 전체 흐름

```
1. 곡 목록 초기화 (initializeAllItemByCrrentMusicData)
   ↓
2. 아티스트 ID별 첫 곡 정보 수집 및 등록
   ├─ 각 곡의 아티스트 ID 확인 (MusicSelectData.artistID)
   ├─ 아티스트 ID 정규화 (NormalizeArtistId)
   └─ 첫 곡의 MusicID와 제목을 AlbumManager에 등록
   ↓
3. 커스텀 차트 선택 (coOpen)
   ↓
4. 곡 시작 전 윈도우 열기 (coOpenPreMusicStartWindow)
   ├─ 현재 앨범의 캐릭터(아티스트 ID) 확인
   ├─ 해당 아티스트의 첫 곡 MusicID 조회
   ├─ MusicID 변경 (mCurentMusicId, mCurrentMusicID)
   ├─ 원본 제목 등록
   └─ 아티스트 ID 저장
   ↓
5. 플레이 씬 진입
   ↓
6. 텍스트 교체 (TextPatch.SetTextPrefix)
   ├─ 원본 제목 목록 확인
   ├─ 현재 앨범의 커스텀 제목으로 교체
   └─ 플레이 씬/결과 씬에서만 작동
```

## 1. 아티스트 ID 수집 단계

### 1.1 곡 목록 초기화 시점

**위치**: `MusicScrollViewHooks.InitializeAllItemByCrrentMusicDataPrefix (구현: MusicScrollViewHooks.ListLogging.cs)`

**동작**:
1. 게임의 원본 곡 목록을 순회
2. 각 곡의 `MusicSelectData`에서 `artistID` 필드 추출
3. 아티스트 ID별로 첫 곡 정보 수집

### 1.2 아티스트 ID 정규화

**위치**: `AlbumManager.NormalizeArtistId (구현: AlbumManager.Mappings.cs)`

**목적**: 다양한 형식의 아티스트 ID를 통일된 형식으로 변환

**변환 규칙**:
```csharp
// 대소문자 무시
"Morpho" == "morpho" == "MORPHO"

// 한글/영문 변환
"르호" → "Morpho"
"Roro" → "Roro" (대소문자 무시)
"Luxair" → "Luxair" (대소문자 무시)
```

**구현 로직**:
```csharp
private static string NormalizeArtistId(string artistId)
{
    if (string.IsNullOrWhiteSpace(artistId))
        return null;
    
    // 대소문자 무시
    string normalized = artistId.Trim();
    
    // 한글/영문 매핑
    if (normalized.Equals("르호", StringComparison.OrdinalIgnoreCase) ||
        normalized.Equals("Morpho", StringComparison.OrdinalIgnoreCase))
        return "Morpho";
    
    if (normalized.Equals("Roro", StringComparison.OrdinalIgnoreCase))
        return "Roro";
    
    if (normalized.Equals("Luxair", StringComparison.OrdinalIgnoreCase) ||
        normalized.Equals("룩시아", StringComparison.OrdinalIgnoreCase))
        return "Luxair";
    
    return normalized;
}
```

### 1.3 첫 곡 정보 등록

**위치**: `MusicScrollViewHooks.InitializeAllItemByCrrentMusicDataPrefix (구현: MusicScrollViewHooks.ListLogging.cs)`

**데이터 구조**:
```csharp
// AlbumManager 내부
private static Dictionary<string, (object musicId, string title)> _artistIdToFirstSong;

// 등록 메서드
public static void RegisterArtistFirstSong(string artistId, object musicId, string title)
{
    string normalized = NormalizeArtistId(artistId);
    if (normalized != null && !_artistIdToFirstSong.ContainsKey(normalized))
    {
        _artistIdToFirstSong[normalized] = (musicId, title);
    }
}
```

**등록 시점**:
- 곡 목록 초기화 시 각 아티스트의 첫 곡만 등록
- 이미 등록된 아티스트 ID는 건너뜀 (첫 곡만 유지)

**통계 출력 예시**:
```
[MusicScrollViewHooks] Morpho: 30개 곡, 첫 곡 '푸른 경계' (MusicID: GV3_001)
[MusicScrollViewHooks] Roro: 16개 곡, 첫 곡 '백은의 약속' (MusicID: GV3_015)
[MusicScrollViewHooks] Luxair: 8개 곡, 첫 곡 '고귀한 효광' (MusicID: GV3_008)
```

## 2. 커스텀 차트 선택 단계

**구현 위치**: `GameFlowHooks.CoOpenPrefix (구현: GameFlowHooks.Navigation.cs)`에서 `GRC2.Services.CustomChartHandler.UpdateCustomChartTitle(instance)`를 호출하여 커스텀 차트 감지 및 아트워크/BGM 로드를 수행한다. CustomChartHandler는 필드 캐싱(mCurrentDispData, mMusicSelectData 등)으로 리플렉션 호출을 최소화한다.

### 2.1 txt 파일에서 아티스트 ID 파싱

**위치**: `SongInfoParser.ParseTxtFile`

**파싱 형식**:
```
캐릭터 : 르호
character = Morpho
```

**우선순위**:
1. `SongInfo.Character` (캐릭터 필드)
2. `SongInfo.Artist` (아티스트 필드, 대체)

**저장 위치**: `SongInfo.Character`

## 3. 곡 시작 전 윈도우 열기 단계

### 3.1 후킹 지점

**위치**: `GameFlowHooks.CoOpenPreMusicStartWindowPrefix (구현: GameFlowHooks.cs)`

**후킹 메서드**: `cMusicSelectSceneUIUpdater.coOpenPreMusicStartWindow()`

**호출 시점**: 곡 선택 후 게임 시작 전 윈도우가 열릴 때

### 3.2 MusicID 변경 로직

**메서드**: `TryManipulateMusicIdByArtist`

**단계별 동작**:

#### 1단계: 현재 앨범 정보 확인
```csharp
var currentSongInfo = AlbumManager.GetCurrentSongInfo();
if (currentSongInfo == null) return false;

// 아티스트 ID 확인 (캐릭터 우선, 없으면 아티스트)
string artistId = currentSongInfo.Character ?? currentSongInfo.Artist;
if (string.IsNullOrWhiteSpace(artistId)) return false;
```

#### 2단계: 첫 곡 정보 조회
```csharp
var firstSong = AlbumManager.GetArtistFirstSong(artistId) (구현: AlbumManager.Mappings.cs);
if (firstSong.musicId == null) return false;
```

#### 3단계: MusicID 변경
```csharp
// MusicSelectData의 musicID 변경
musicIdField.SetValue(musicSelectData, firstSong.musicId);

// 인스턴스의 mCurentMusicId, mCurrentMusicID 변경
mCurentMusicIdField?.SetValue(__instance, firstSong.musicId);
mCurrentMusicIDField?.SetValue(__instance, firstSong.musicId);
```

#### 4단계: 원본 제목 등록
```csharp
// 첫 곡의 제목을 원본 제목으로 등록 (TextPatch에서 사용)
AlbumManager.RegisterOriginalTitle(firstSong.musicId, firstSong.title) (구현: AlbumManager.Mappings.cs);
```

#### 5단계: 아티스트 ID 저장
```csharp
// 현재 아티스트 ID 저장 (TextPatch에서 사용)
AlbumManager.SetCurrentArtistId(artistId) (구현: AlbumManager.Mappings.cs);
```

### 3.3 커스텀 차트 처리 건너뛰기

**목적**: 아티스트 ID로 MusicID를 변경한 경우, 기본 커스텀 차트 처리(FIRST로 변경)를 건너뜀

**구현**:
```csharp
bool musicIdChanged = TryManipulateMusicIdByArtist(__instance);
if (musicIdChanged)
{
    // 아티스트 ID로 변경했으므로 기본 커스텀 차트 처리 건너뛰기
    return;
}
```

## 4. 텍스트 교체 단계

### 4.1 후킹 지점

**위치**: `TextPatch.SetTextPrefix`

**후킹 대상**: 
- `UnityEngine.UI.Text.text` setter
- `TMPro.TextMeshProUGUI.text` setter

**활성화 조건**:
- `_isTextReplacementEnabled == true` (coOpen에서 설정)
- 플레이 씬/로딩 씬/결과 씬에서만 작동

### 4.2 교체 로직

**단계별 동작**:

#### 1단계: 원본 제목 확인
```csharp
var originalTitles = AlbumManager.GetAllOriginalTitles() (구현: AlbumManager.Mappings.cs);
if (!originalTitles.Contains(value))
{
    return; // 원본 제목이 아니면 교체하지 않음
}
```

#### 2단계: 현재 앨범의 커스텀 제목 가져오기
```csharp
var currentSongInfo = AlbumManager.GetCurrentSongInfo();
if (currentSongInfo == null || string.IsNullOrWhiteSpace(currentSongInfo.Title))
{
    return;
}
```

#### 3단계: 텍스트 교체
```csharp
value = currentSongInfo.Title; // 커스텀 제목으로 교체
```

**예시**:
- 원본 제목: "푸른 경계"
- 커스텀 제목: "summertime (Arrange ver.)"
- 결과: 화면에 "summertime (Arrange ver.)" 표시

## 5. 데이터 흐름

### 5.1 아티스트 ID → 첫 곡 정보

```
아티스트 ID: "Morpho"
   ↓
AlbumManager.GetArtistFirstSong("Morpho") (구현: AlbumManager.Mappings.cs)
   ↓
반환: (musicId: GV3_001, title: "푸른 경계")
```

### 5.2 MusicID 변경 흐름

```
커스텀 차트 MusicID: 54 (커스텀)
   ↓
아티스트 ID 확인: "Morpho"
   ↓
첫 곡 MusicID 조회: GV3_001
   ↓
MusicID 변경: 54 → GV3_001
   ↓
원본 제목 등록: GV3_001 → "푸른 경계"
```

### 5.3 텍스트 교체 흐름

```
화면에 표시될 텍스트: "푸른 경계" (원본 제목)
   ↓
원본 제목 목록 확인: 포함됨 ✓
   ↓
현재 앨범의 커스텀 제목: "summertime (Arrange ver.)"
   ↓
텍스트 교체: "푸른 경계" → "summertime (Arrange ver.)"
```

## 6. 주요 클래스 및 메서드

### AlbumManager

**주요 메서드**:
- `NormalizeArtistId(string artistId)`: 아티스트 ID 정규화
- `RegisterArtistFirstSong(string artistId, object musicId, string title)`: 첫 곡 정보 등록
- `GetArtistFirstSong(string artistId)`: 첫 곡 정보 조회
- `SetCurrentArtistId(string artistId)`: 현재 아티스트 ID 저장
- `GetCurrentArtistId()`: 현재 아티스트 ID 조회

**데이터 구조**:
```csharp
// 아티스트 ID별 첫 곡 정보
private static Dictionary<string, (object musicId, string title)> _artistIdToFirstSong;

// 현재 아티스트 ID
private static string _currentArtistId;
```

### GameFlowHooks

**주요 메서드**:
- `CoOpenPreMusicStartWindowPrefix()`: 곡 시작 전 윈도우 열기 후킹
- `TryManipulateMusicIdByArtist(object instance)`: 아티스트 ID 기반 MusicID 변경

### TextPatch

**주요 메서드**:
- `SetTextPrefix(object __instance, ref string value)`: 텍스트 setter 후킹
- `EnableTextReplacement(bool enable)`: 텍스트 교체 활성화/비활성화

### MusicScrollViewHooks

**주요 로직**:
- `InitializeAllItemByCrrentMusicDataPrefix()`: 곡 목록 초기화 시 아티스트 ID 수집

## 7. 주의사항 및 제한사항

### 7.1 아티스트 ID 정규화

- 현재는 "르호", "Morpho", "Roro", "Luxair"만 지원
- 새로운 아티스트 ID 추가 시 `NormalizeArtistId` 메서드 수정 필요

### 7.2 첫 곡 정보 등록

- 곡 목록 초기화 시점에만 등록됨
- 게임 실행 중 곡 목록이 변경되면 재등록 필요

### 7.3 텍스트 교체

- 플레이 씬/로딩 씬/결과 씬에서만 작동
- 원본 제목 목록에 있는 텍스트만 교체
- `_isTextReplacementEnabled`가 활성화되어 있어야 함

## 8. 디버깅 팁

### 8.1 로그 확인

**아티스트 ID 수집**:
```
[MusicScrollViewHooks] Morpho: 30개 곡, 첫 곡 '푸른 경계'
```

**MusicID 변경**:
```
[GameFlowHooks] 🎵 아티스트 ID 기반 MusicID 변경: Morpho -> GV3_001
```

**텍스트 교체**:
```
[TextPatch] 📝 텍스트 교체: '푸른 경계' -> 'summertime (Arrange ver.)' (커스텀 제목)
```

### 8.2 문제 해결

**문제**: 아티스트 ID가 인식되지 않음
- **확인**: txt 파일에 "캐릭터 : " 또는 "character=" 필드가 있는지 확인
- **확인**: `NormalizeArtistId`에 해당 아티스트 ID가 등록되어 있는지 확인

**문제**: 텍스트가 교체되지 않음
- **확인**: `_isTextReplacementEnabled`가 활성화되어 있는지 확인
- **확인**: 현재 씬이 플레이 씬/로딩 씬/결과 씬인지 확인
- **확인**: 원본 제목이 `GetAllOriginalTitles()`에 등록되어 있는지 확인

## 기술 스택 및 참조

### 어셈블리 어트리뷰트

프로젝트의 진입점은 다음 어트리뷰트로 정의됩니다:

```csharp
[assembly: MelonInfo(typeof(GRC2.Core.SceneDetector), "GUNVOLT RECORDS Cychronicle", "1.0.0", "")]
[assembly: MelonGame("INTI CREATES", "GUNVOLT RECORDS Cychronicle")]
```

**위치**: `GRC2/Core/Scene/SceneDetector.cs`

### 참조 DLL

이 시스템은 다음 DLL들을 참조합니다:

- **MelonLoader.dll**: MelonLoader 모딩 프레임워크
- **0Harmony.dll**: Harmony 라이브러리 (런타임 메서드 패칭)
- **UnityEngine.CoreModule.dll**: Unity 핵심 모듈
- **UnityEngine.UI.dll**: Unity UI 시스템
- **UnityEngine.AudioModule.dll**: Unity 오디오 시스템
- **UnityEngine.VideoModule.dll**: Unity 비디오 재생 모듈
- **UnityEngine.UnityWebRequestModule.dll**: Unity 웹 요청 모듈
- **UnityEngine.UnityWebRequestAudioModule.dll**: Unity 오디오 웹 요청 모듈
- **UnityEngine.ImageConversionModule.dll**: Unity 이미지 변환 모듈
- 기타 Unity 모듈들 (AnimationModule, PhysicsModule, ParticleSystemModule, UIModule, TextMeshPro)

### Harmony 패치 방식

이 시스템은 Harmony를 통한 동적 패치 방식을 사용합니다:

- `HarmonyMethod`를 사용하여 런타임에 메서드를 패치
- `harmonyInstance.Patch()` 메서드를 통해 Prefix/Postfix 패치 적용
- 어트리뷰트 기반 패치(`[HarmonyPrefix]`, `[HarmonyPostfix]`)는 사용하지 않음

**주요 패치 클래스**:
- `GameFlowHooks`: 게임 플로우 후킹 (`coOpenPreMusicStartWindow` 등)
- `MusicScrollViewHooks`: 곡 목록 주입 후킹 (아티스트 ID 수집)
- `TextPatcher`: 텍스트 교체 후킹

## 관련 문서

- [README.md](../README.md): 전체 시스템 개요
- [게임_플로우_및_메서드.md](../architecture/게임_플로우_및_메서드.md): 게임 플로우 상세 설명
- [커스텀_곡_주입_시스템_분석.md](커스텀_곡_주입_시스템_분석.md): 커스텀 곡 주입 시스템 분석



