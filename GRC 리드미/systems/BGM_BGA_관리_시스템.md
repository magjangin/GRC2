# BGM/BGA 관리 시스템

이 문서는 GUNVOLT RECORDS Cychronicle 게임의 BGM/BGA 관리 시스템을 설명합니다.

## `cBGMBeatManager` 클래스

BGM 및 비트 관리를 담당하는 클래스입니다.

### 추정 클래스 구조

```csharp
namespace IntiCreates
{
    public class cBGMBeatManager : MonoBehaviour
    {
        // 오디오 소스
        private AudioSource _sorce;  // 오디오 소스 (오타로 추정, 실제로는 _source일 수도 있음)
        
        // BGM 관련 필드
        private AudioClip currentClip;  // 현재 재생 중인 BGM 클립
        private float bgmLength;        // BGM 길이 (초)
        
        // 메서드
        public void setClip(AudioClip clip, bool autoPlay)  // BGM 클립 설정
        public void setClip(string clipPath, bool autoPlay)  // BGM 클립 경로로 설정 (오버로드)
        public void requestPlayAudio()  // 오디오 재생 요청
        public void requestLoadBGM()  // BGM 로드 요청
        public void requestPause()  // 일시정지 요청
        public AudioClip getAudioClip()  // 현재 AudioClip 반환
        public int getCurrentSample()  // 현재 재생 샘플 수 반환 (48000Hz 기준)
        public bool isReadyPlay()  // 재생 준비 여부 확인
        public void setTime(float time)  // 재생 시간 설정 (시간 제한 적용됨)
        public void setBPM(float bpm)  // BPM 설정
        public void setSample(int sample)  // 샘플 수 설정
    }
}
```

### 주요 메서드 동작 추정

**`setClip(AudioClip clip, bool autoPlay)` 메서드:**
```csharp
public void setClip(AudioClip clip, bool autoPlay)
{
    // 1. 현재 클립 저장
    currentClip = clip;
    
    // 2. AudioSource에 클립 할당
    _sorce.clip = clip;
    
    // 3. BGM 길이 저장
    if (clip != null)
    {
        bgmLength = clip.length;
    }
    
    // 4. 자동 재생 옵션
    if (autoPlay)
    {
        requestPlayAudio();
    }
}
```

**`requestPlayAudio()` 메서드:**
```csharp
public void requestPlayAudio()
{
    // 1. AudioSource 재생
    if (_sorce != null && _sorce.clip != null)
    {
        _sorce.Play();
    }
    
    // 2. 비트 매칭 시작 (추정)
    StartBeatMatching();
}
```

## VideoPlayer 통합

게임은 Unity의 `VideoPlayer` 컴포넌트를 사용하여 BGA를 재생합니다.

### 추정 구조

```csharp
// 게임 내 VideoPlayer 사용 방식 (추정)
public class VideoManager : MonoBehaviour
{
    private VideoPlayer videoPlayer;  // Unity VideoPlayer 컴포넌트
    
    public void SetVideoURL(string url)
    {
        videoPlayer.url = url;
        videoPlayer.Prepare();
    }
    
    public void PlayVideo()
    {
        videoPlayer.Play();
    }
}
```

## BGA/BGM 동기화 시스템

모드는 `BgaBgmSyncManager`를 통해 BGA와 BGM을 자동으로 동기화합니다.

### 동기화 동작 원리

**1. 동기화 시작 (`BgaBgmSyncManager.StartSync()`)**
- BGA 재생 시작 시 `BgaInjector`에서 호출
- BGM 오디오 소스 찾기 (`GetCurrentAudioSource()`)
  - 우선순위: `cBGMBeatManager`에서 AudioSource 찾기
  - 실패 시: 모든 AudioSource 검색하여 최적의 소스 선택 (이름, 재생 상태, 클립 존재 여부로 점수화)
- 즉시 동기화 수행 (`SyncBgaToBgm()`)
- 지속적인 동기화 모니터링 코루틴 시작 (`SyncCoroutine()`)

**2. 즉시 동기화 (`SyncBgaToBgm()`)**
- BGM 시간 가져오기:
  - `AudioSource.time` (재생 중인 경우)
  - `cBGMBeatManager.getCurrentSample()` → 샘플을 초로 변환 (48000Hz 기준)
  - `cBGMBeatManager.getAudioSorceCurrentTime()` (대체 방법)
- 모든 VideoPlayer를 BGM 시간에 맞춰 동기화:
  - `videoPlayer.time = bgmTime % videoPlayer.length` (비디오 길이로 모듈로 연산하여 루프 처리)

**3. 지속적인 동기화 모니터링 (`SyncCoroutine()`)**
- 0.1초마다 동기화 상태 확인
- BGM 재생 시작 감지 및 즉시 동기화
- BGA와 BGM 시간 차이 확인:
  - 0.1초 이상 차이 발생 시 재동기화
  - 마지막 동기화로부터 1초 이상 경과한 경우에만 재동기화 (과도한 동기화 방지)
- 여러 VideoPlayer 지원 (모든 활성화된 VideoPlayer 동기화)

**4. 동기화 중지 (`StopSync()`)**
- 코루틴 중지
- 상태 리셋 (오디오 소스, VideoPlayer 배열 초기화)

### 동기화 알고리즘

```csharp
// BGA 시간 = BGM 시간 % 비디오 길이 (루프 처리)
float syncTime = (float)(bgmTime % videoPlayer.length);
videoPlayer.time = syncTime;

// 시간 차이 확인 (0.1초 이상 차이 시 재동기화)
float expectedVideoTime = (float)(bgmTime % videoPlayer.length);
float timeDiff = Mathf.Abs((float)(videoPlayer.time - expectedVideoTime));
if (timeDiff > 0.1f && Mathf.Abs(bgmTime - lastSyncTime) > 1f)
{
    SyncBgaToBgm(); // 재동기화
}
```

### BGM 시간 가져오기 방법

1. **AudioSource.time** (우선순위 1)
   - 재생 중인 경우 직접 시간 가져오기

2. **cBGMBeatManager.getCurrentSample()** (우선순위 2)
   - 샘플 단위로 시간 반환
   - 변환: `time = sample / 48000f`

3. **cBGMBeatManager.getAudioSorceCurrentTime()** (우선순위 3)
   - 직접 초 단위로 시간 반환

### 동기화 성능 최적화

- **과도한 동기화 방지**: 마지막 동기화로부터 1초 이상 경과한 경우에만 재동기화
- **여러 VideoPlayer 지원**: 모든 활성화된 VideoPlayer를 한 번에 동기화
- **오디오 소스 캐싱**: 한 번 찾은 오디오 소스는 재사용
- **조건부 동기화**: 재생 중이고 준비된 VideoPlayer만 동기화

## `cMoviePlayerMoviePlaySceneManagerObject` 클래스

BGA 제어를 담당하는 클래스입니다. (Timeline 조정은 제거됨 - 4개 필드 조정만으로 충분)

## `cMusicSelectSceneUIUpdater` 클래스 (곡 선택 화면)

곡 선택 화면의 UI 업데이트 및 프리뷰 BGM 관리를 담당하는 클래스입니다.

### 주요 필드

- **`mCurrentUsingPreviewBGMClip`** (AudioClip): 현재 사용 중인 프리뷰 BGM 클립
- **`mPreviewAudioSorce`** (AudioSource): 프리뷰 오디오를 재생하는 AudioSource (오타: "Sorce" → "Source")
- **`mCurrentPreviewMusicID`** (MusicID): 현재 프리뷰로 재생 중인 곡의 ID
- **`mCurentMusicId`** (MusicID): 현재 선택된 곡의 ID

### 주요 메서드

1. **`noticeChangedMusic(MusicID nextMusicID)`**
   - 곡 선택 변경 알림 메서드
   - **핵심 메서드**: 실제 프리뷰 BGM이 변경되는 시점
   - 커스텀 차트 선택 시 이 메서드에서 BGM 주입 성공

2. **`coChangePreviewBGM(MusicID loadMusicID, MusicID preMusicID)`** (IEnumerator)
   - 프리뷰 BGM 변경 코루틴
   - 비동기로 프리뷰 BGM을 교체

3. **`getPreviewAudioClipAddressablePath(MusicData musicData)`** (String)
   - 선택된 곡의 프리뷰 오디오 클립의 Addressable 경로 반환
   - 일반 곡일 때 빈 경로 반환하여 프리뷰 BGM 로드 차단 가능

4. **`requestFadeCurrentPlayingPreviewMusic(Boolean isFadeIn, Single fadeTime)`**
   - 현재 재생 중인 프리뷰 BGM 페이드 인/아웃 요청

### 프리뷰 BGM 주입 메커니즘

**구현 클래스:**
- `AudioClipPatch`: `noticeChangedMusic` 후킹 담당
- `CustomBgmPlayer`: 커스텀 BGM 재생 관리
- `PreviewAudioManager`: 원본 프리뷰/환경음 제어

**커스텀 차트 선택 시:**
1. `noticeChangedMusic` 호출 → `AudioClipPatch.NoticeChangedMusicPostfix()` 실행
2. `AlbumManager.SelectAlbumByMusicID() (구현: AlbumManager.Mappings.cs)`로 앨범 선택
3. `CustomBgmPlayer.InjectCustomBgm()` 호출:
   - 새로운 GameObject 생성 (`CustomPreviewBGM`)
   - AudioSource 컴포넌트 추가 및 설정 (루프 재생, 최우선 재생)
   - `UnityWebRequestMultimedia.GetAudioClip()`으로 커스텀 BGM 로드
   - AudioSource에 클립 설정 및 재생 시작
4. `PreviewAudioManager.StopPreviewAndAmbient()`로 원본 프리뷰/환경음 중지
5. `CustomAssetManager.SetCustomChartSelected(true)`로 상태 설정

**일반 곡 선택 시:**
1. `noticeChangedMusic` 호출 → `AudioClipPatch.NoticeChangedMusicPostfix()` 실행
   - 현재는 호출 감지만 수행 (로깅만)
2. `coOpen` 호출 → `GameFlowHooks.CoOpenPrefix() (구현: GameFlowHooks.Navigation.cs)` 실행
3. `CustomBgmPlayer.CleanupAndRestore()` 호출:
   - 커스텀 BGM AudioSource 정리 및 GameObject 제거
   - `PreviewAudioManager.RestoreMutedAudioSources()`로 원본 프리뷰 복원
     - **개선**: `cSoundManager`에서 직접 `mPreviewAudioSorce`와 `mAmbientAudioSorce`를 찾아서 복원
     - **개선**: 중복 복원 방지 (딕셔너리와 cSoundManager의 AudioSource 중복 처리)
4. `CustomAssetManager.SetCustomChartSelected(false)`로 상태 설정
   - **중요**: BMS/BGA/BGM 노트 주입은 `CustomAssetManager.ShouldInjectCustomContent()`로 제어 (커스텀 선택 + 주입 금지 씬(SoundPlayerScene, MoviePlayer_MovieSelect) 아님)

### CustomBgmPlayer 특징

- **독립적인 AudioSource**: 게임의 원본 프리뷰 시스템과 분리
- **DontDestroyOnLoad**: 씬 전환 시에도 유지 (플레이 씬 진입 시 수동 정리)
- **루프 재생**: 무한 반복 재생
- **자동 정리**: 플레이 씬 진입 시 `SceneDetector`에서 자동으로 정리

## `cMusicSelectSceneSelectingMusicUI` 클래스 (곡 선택 UI)

곡 선택 UI를 관리하는 클래스입니다. 곡 선택 창을 열고 닫는 기능을 담당합니다.

### 주요 필드

- **`mCurrentDispData`**: 현재 표시 중인 데이터
  - **`mMusicSelectData`**: 곡 선택 데이터
    - **`musicID`** (MusicID): 현재 선택된 곡의 MusicID
    - 기타 곡 정보 필드들

### 주요 메서드

1. **`coOpen()`** (IEnumerator)
   - 곡 선택 창 열기 코루틴
   - **핵심 메서드**: 커스텀 차트 감지 및 아트워크/BGM 로드에 사용
   - `mCurrentDispData.mMusicSelectData.musicID`를 통해 현재 선택된 곡의 MusicID 확인
   - 커스텀 차트인 경우 자동으로 아트워크와 BGM 로드
   - **후킹**: `GameFlowHooks.CoOpenPrefix() (구현: GameFlowHooks.Navigation.cs)`에서 후킹하여 커스텀 차트 감지 및 에셋 로드

2. **`coClose()`** (IEnumerator)
   - 곡 선택 창 닫기 코루틴
   - **후킹**: `GameFlowHooks.CoClosePrefix() (구현: GameFlowHooks.Navigation.cs)`에서 후킹하여 닫기 동작 모니터링

### coOpen 후킹 메커니즘

**구현 클래스:**
- `GameFlowHooks`: `coOpen` 후킹 담당
- `PatchApplier`: `cMusicSelectSceneSelectingMusicUI` 타입 패치 적용

**커스텀 차트 선택 시 (coOpen 후킹):**
1. `coOpen` 호출 → `GameFlowHooks.CoOpenPrefix() (구현: GameFlowHooks.Navigation.cs)` 실행
2. `mCurrentDispData.mMusicSelectData.musicID` 및 `songTitle`을 통해 현재 선택된 곡 확인
3. **곡 제목 기반 판단 로직**:
   - 곡 제목이 원본 제목 목록(`GetAllOriginalTitles()`)에 있으면 → 일반 곡 (커스텀 차트 아님)
   - 곡 제목이 커스텀 차트 앨범 제목이면 → 커스텀 차트
   - 둘 다 아니면 MusicID로 확인 (`AlbumManager.IsCustomChartMusicID() (구현: AlbumManager.Mappings.cs)`)
4. **커스텀 차트인 경우**:
   - `AlbumManager.SelectAlbumByMusicID() (구현: AlbumManager.Mappings.cs)`로 앨범 선택
   - `CustomBgmPlayer.InjectCustomBgm()`로 커스텀 프리뷰 BGM 주입
   - `PreviewAudioManager.StopPreviewAndAmbient()`로 원본 프리뷰/환경음 중지
   - `CustomAssetManager.LoadCustomArtwork()`로 커스텀 아트워크 로드
   - `ArtworkUpdater.UpdateArtwork()`로 아트워크 업데이트
   - 곡 이름과 MusicID 로깅
5. **일반 곡인 경우**:
   - `CustomAssetManager.SetCustomChartSelected(false)`로 커스텀 차트 선택 해제
   - `CustomBgmPlayer.CleanupAndRestore()`로 원본 아트워크/BGM 복원
   - 원본 프리뷰/환경음 복원

**noticeChangedMusic과 coOpen의 차이:**
- **`noticeChangedMusic`**: 곡 선택 변경 시 호출되는 메서드 (프리뷰 BGM 변경 시점)
- **`coOpen`**: 곡 선택 창을 열 때 호출되는 코루틴 (UI 표시 시점)
- 두 메서드 모두 커스텀 차트 감지 및 에셋 로드를 수행하므로, 어느 쪽이 먼저 호출되더라도 정상 작동

## 커스텀 아트워크 주입 시스템

모드는 곡 선택 화면과 플레이 씬에서 커스텀 아트워크를 자동으로 주입합니다.

### `CustomAssetManager` 클래스

커스텀 아트워크와 BGM을 관리하는 중앙 관리 클래스입니다.

**주요 메서드:**
- `LoadCustomArtwork(string imagePath)`: 커스텀 아트워크 로드
  - **리사이즈/리샘플 없음**: 원본 해상도 그대로 `Texture2D`/`Sprite` 생성
  - UI 스케일은 게임의 `CanvasScaler(ScaleWithScreenSize)` 설정에 의해 처리됨
  - 이미지 경로 캐싱을 통한 성능 최적화
- `LoadCustomPreviewBGM(string audioPath)`: 커스텀 프리뷰 BGM 로드
- `IsCustomChart(object musicID, object musicData)`: MusicID나 MusicData로 커스텀 차트인지 확인
- `GetCustomArtwork()`: 로드된 커스텀 아트워크 스프라이트 반환
- `IsImageLoaded(string imagePath)`: 특정 경로의 이미지가 이미 로드되어 있는지 확인

### `PlaySceneArtworkInjector` 클래스

플레이 씬에서 커스텀 아트워크를 주입하는 클래스입니다.

**주요 기능:**
- `StartArtworkInjection()`: 아트워크 주입 코루틴 시작 (중복 방지)
- `TryInjectArtworkImmediately()`: 아트워크 즉시 적용 시도 (성능 최적화)
- Image 캐싱을 통한 성능 최적화
- GameObject.Find, Transform.Find, FindObjectsOfType 순서로 Image 검색

### `SceneHandler` 클래스

씬별 처리 로직을 담당하는 클래스입니다.

**주요 기능:**
- `HandleFairyModeScene()`: FairyModeScene 처리
- `HandlePlayMovieScene()`: PlayMovieScene 처리
- `StopPreviewBGMOnPlayScene()`: 플레이 씬 진입 시 프리뷰 BGM 중지

### 아트워크 주입 흐름

1. **곡 선택 화면**: `ArtWorkPatch`를 통해 커스텀 아트워크 교체
2. **플레이 씬**: `PlaySceneArtworkInjector`를 통해 커스텀 아트워크 주입
   - 즉시 적용 시도 (성능 최적화)
   - 실패 시 코루틴으로 재시도

## 관련 문서

- [게임 아키텍처 개요](../architecture/게임_아키텍처_개요.md): 게임 아키텍처 개요
- [게임 클래스 구조](../architecture/게임_클래스_구조.md): 핵심 클래스 및 데이터 구조 상세 설명
- [게임 플로우 및 메서드](../architecture/게임_플로우_및_메서드.md): 게임 플로우 및 메서드 동작 상세 설명
- [게임 종료 로직](../maintenance/게임_종료_로직.md): 게임 종료 로직 및 모드 상호작용 상세 설명

---

**참고**: 이 문서의 내용은 추정이며, 실제 게임 코드와 다를 수 있습니다. 정확한 정보는 게임 개발사(INTI CREATES)의 공식 문서를 참조하거나, 디컴파일러를 사용하여 직접 확인해야 합니다.




