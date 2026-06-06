# GUNVOLT RECORDS Cychronicle - BMS 커스텀 차트 모드

GUNVOLT RECORDS Cychronicle 게임을 위한 MelonLoader 기반 커스텀 모드입니다. BMS 파일을 파싱하여 커스텀 차트를 주입하고, BGA/BGM을 교체하는 기능을 제공합니다.

## 테스트/확인 환경

- 게임 이름: GUNVOLT RECORDS Cychronicle
- 게임 개발사: INTI CREATES
- 유니티 버전: 2021.3.31f1
- 게임 버전: 1.1.0

## 주요 기능

### 1. BMS 파일 파싱 및 커스텀 차트 주입
- `hwa` 폴더에 있는 BMS 파일(`*.bms`, `*.bme`, `*.bml`)을 자동으로 스캔
- BMS 파일을 파싱하여 게임의 `NoteCreateData` 형식으로 변환
- 게임의 `cFairyModeNotesManager.mFairyNoteCreateDataArray`에 파싱된 노트를 주입하여 커스텀 차트로 플레이 가능
- 지원하는 노트 타입:
  - **일반 노트 (01)**: Touch 노트로 변환
  - **홀드 노트 (02-19)**: Hold 노트로 변환 (02: 홀드 시작, 19: 홀드 끝)
  - **플릭 노트 (03-0A)**: Flick 노트로 변환 (8방향 플릭: 03=왼쪽, 04=왼쪽위, 05=위, 06=오른쪽위, 07=오른쪽, 08=오른쪽아래, 09=아래, 0A=왼쪽아래)
  - **페어리 노트 (11-18)**: Fairy 노트로 변환 (8방향 페어리, 1A-1B와 쌍을 이루어 페어리 노트 생성)

### 2. BGA/BGM 교체 및 동기화
- `hwa` 폴더의 비디오 파일(`*.mp4`)을 자동으로 BGA로 교체
- `hwa` 폴더의 오디오 파일(`*.mp3`, `*.wav`, `*.ogg`)을 자동으로 BGM으로 교체
- **BGA**: Unity `VideoPlayer`를 사용하여 스트리밍 방식으로 재생
- **BGM**: 게임의 `cBGMBeatManager`를 통해 `setClip` 메서드로 주입
- 플레이 씬(`FairyModeScene`, `PlayMovieScene`)에서만 주입 시도
- **BGA/BGM 동기화**: `BgaBgmSyncManager`를 통해 BGA와 BGM을 자동으로 동기화
  - BGA 재생 시작 시 BGM 시간에 맞춰 즉시 동기화
  - 0.1초마다 동기화 상태 모니터링 및 재동기화 (0.1초 이상 차이 발생 시)
  - `cBGMBeatManager.getCurrentSample()` 또는 `getAudioSorceCurrentTime()` 메서드로 BGM 시간 확인
  - 여러 VideoPlayer 지원 (모든 활성화된 VideoPlayer 동기화)

### 2-1. 프리뷰 BGM 주입 (곡 선택 화면)
- 곡 선택 화면에서 커스텀 차트 선택 시 프리뷰 BGM 자동 재생
- **구현 클래스**: `AudioClipPatch`, `CustomBgmPlayer`, `GameFlowHooks`
- **핵심 메서드**: 
  - `cMusicSelectSceneUIUpdater.noticeChangedMusic()` 후킹 (`AudioClipPatch.NoticeChangedMusicPostfix`)
  - `cMusicSelectSceneSelectingMusicUI.coOpen()` 후킹 (`GameFlowHooks.CoOpenPrefix (구현: GameFlowHooks.Navigation.cs)`)
- **동작 방식**:
  - 커스텀 차트 선택 시 (`noticeChangedMusic` 또는 `coOpen` 호출):
    - `CustomBgmPlayer.InjectCustomBgm()`로 새로운 AudioSource 생성 및 커스텀 BGM 주입
    - `PreviewAudioManager.StopPreviewAndAmbient()`로 원본 프리뷰/환경음 중지
    - 커스텀 BGM 재생 시작 (루프 재생)
    - `CustomAssetManager.LoadCustomArtwork()`로 커스텀 아트워크 로드
    - `ArtworkUpdater.UpdateArtwork()`로 아트워크 업데이트
  - 일반 곡 선택 시:
    - `CustomBgmPlayer.CleanupAndRestore()`로 커스텀 BGM 정리 및 원본 프리뷰 복원
- **CustomBgmPlayer 특징**:
  - 독립적인 GameObject와 AudioSource 생성 (`CustomPreviewBGM`)
  - `DontDestroyOnLoad`로 씬 전환 시에도 유지
  - 루프 재생 지원
  - 플레이 씬 진입 시 자동 정리
- **coOpen 후킹**:
  - `cMusicSelectSceneSelectingMusicUI.coOpen()` 메서드를 후킹하여 커스텀 차트 감지
  - `mCurrentDispData.mMusicSelectData.musicID` 및 `songTitle`을 통해 현재 선택된 곡 확인
  - **곡 제목 기반 판단 로직**:
    1. 곡 제목이 원본 제목 목록(`GetAllOriginalTitles()`)에 있으면 → 일반 곡 (커스텀 차트 아님)
    2. 곡 제목이 커스텀 차트 앨범 제목이면 → 커스텀 차트
    3. 둘 다 아니면 MusicID로 확인 (기존 로직)
  - **커스텀 차트인 경우**:
    - 자동으로 아트워크와 BGM 로드
    - 곡 이름과 MusicID 로깅
  - **일반 곡인 경우**:
    - 원본 아트워크/BGM으로 복원 (`CustomBgmPlayer.CleanupAndRestore()`)
    - 커스텀 차트 선택 상태 해제
  - `coClose()` 메서드도 후킹하여 닫기 동작 모니터링

### 3. 노트 데이터 덤프 (JSON)
- **현재 코드 기준(중요)**: 기본값은 **비활성화** (JSON 파일을 생성하지 않음)
- 파일 위치: 게임 설치 폴더의 `note` 폴더

**현재 코드 기준(중요):**
- `GRC2`의 `NoteArrayHooks`에서 `createAllNote` 시점에 덤프 호출이 존재하지만,
  기본값이 비활성화라 **파일이 생성되지 않습니다.**
- 덤프가 필요하면 `GRC2/Harmony/Hooks/NoteArray/NoteArrayHooks.Patching.cs`에서 아래 설정만 바꾸면 됩니다:
  - `NoteArrayJsonDumper.Initialize(noteFolderPath, isEnabled: false);` → `true`
- **홀드/페어리 노트 디버깅**: `connectNodeDataArray` 필드를 재귀적으로 직렬화하여 노트 연결 구조 확인 가능
- **JSON 형식**: 들여쓰기 형식으로 가독성 향상
- 디버깅 및 분석 용도

## 프로젝트 구조 및 주요 클래스 상세

### 폴더 구조 변경 (2026-05-11)
- 현재 관리 대상 소스 파일은 `bin/obj` 생성 파일을 제외하고 **130개 `.cs` 파일**입니다.
- 리팩터링 과정에서 partial 파일 수가 늘어난 대신, 한 폴더에 파일이 납작하게 몰리지 않도록 **클래스/기능 단위 하위 폴더**로 묶었습니다.
- 네임스페이스(`GRC2.Core`, `GRC2.Injectors`, `GRC2.Harmony.*` 등)는 유지되므로 런타임 동작에는 영향이 없습니다.

주요 묶음:
- `GRC2/Core/Album/AlbumManager/*`: 앨범 스캔, 선택, 현재 에셋 조회, MusicID/아티스트 매핑
- `GRC2/Core/Assets/ResultScene/*`: 결과 씬 아트워크/난이도 적용
- `GRC2/Core/Infrastructure/GameTypeInspector/*`: 게임 타입 탐색, 필터, 로깅, 리플렉션 보조
- `GRC2/Builders/NoteCreateData/*`: NoteCreateData 생성, perfectSample 보정, BMS 역검색 캐시
- `GRC2/Converters/BmsNote/*`: BMS 노트 변환, 끝 노트 처리, 배열 생성, 검증
- `GRC2/Harmony/Hooks/GameFlow/*`: 게임 플로우 훅
- `GRC2/Harmony/Hooks/MusicScrollView/*`: 곡 목록 주입 훅
- `GRC2/Harmony/Hooks/NoteArray/*`: 노트 배열 주입 훅
- `GRC2/Harmony/Handlers/AudioSource/*`: AudioSource 탐색 및 프리뷰 BGM 교체 보조
- `GRC2/Harmony/Handlers/PreviewAudio/*`: 원본 프리뷰/환경음 중지 및 복원
- `GRC2/Injectors/Bga/BgmSync/*`: BGA/BGM 동기화
- `GRC2/Injectors/Bgm/Formatting/*`: BGM 후킹 로그 포맷팅
- `GRC2/Injectors/GameEnd/Monitor/*`: 게임 종료 시간 모니터링 및 샘플 조정
- `GRC2/Helpers/MusicInjection/*`: 곡 주입 디버그 출력
- `GRC2/Helpers/NoteArrayDump/*`: 노트 배열 JSON 덤프
- `GRC2/Processors/HoldNote/*`: 홀드 노트 연결 처리
- `GRC2/Services/CustomChart/*`: 커스텀 차트 감지/적용

### Core/
#### Core/Scene/
- **SceneDetector.cs**: 모드 진입점
  - `OnInitializeMelon()`: 모드 초기화
    - `MusicInjector.Initialize()`: 곡 목록 주입 시스템 초기화 (Harmony 패치)
    - 디버그 모드 토글 없음 (항상 활성화)
    - `hwa` 폴더 생성
    - `AlbumManager.ScanAlbums(_hwaFolderPath)`: 앨범 폴더 스캔 및 파일 매핑
    - `GameTypeInspector.SearchFairyModeNotesManager()`: `cFairyModeNotesManager` 타입 탐색 (게임 타입/필드 확인)
    - `SceneDetector.ScanAndParseBmsFiles()`:
      - `hwa` 내 **모든 앨범 폴더의 모든 BMS 파일**(`*.bms/*.bme/*.bml`)을 스캔/파싱
      - 파싱 결과는 **파일 전체 경로 기준 캐시**(`ParsedBmsNotesByFile`)에 보관 (동일 파일명 충돌 방지)
      - 실제 노트 주입에 사용되는 `ParsedBmsNotes`는 **현재 선택된 앨범의 현재 BMS 파일(기본: 첫 번째)**로 설정
    - `NoteArrayHooks.Initialize(_hwaFolderPath, ParsedBmsNotes, debugModeEnabled)`: Harmony 후킹 설정 (내부에서 NoteArrayJsonDumper 초기화, JSON 덤프 기본 비활성화)
    - `BgmInjector.Initialize()`: BGM 후킹 설정
    - `BgmBgaInjector.Initialize(_hwaFolderPath)`: BGA/BGM 파일 검색 (앨범별)
    - `HarmonyHookManager.Initialize()`: BGA 종료 메서드 모니터링
#### Core/Album/
- **AlbumManager/**: 앨범별 파일 관리 (partial 묶음)
  - `ScanAlbums(string hwaFolderPath)`: `hwa` 폴더 내 모든 하위 폴더를 앨범으로 스캔
  - `SelectAlbum()`: 앨범 선택 (폴더 경로 또는 곡 정보로)
  - `SelectAlbumByMusicID()`: MusicID로 앨범 선택
  - `SelectAlbumBySongInfo()`: SongInfo로 앨범 선택
  - `GetAllAlbums()`: 전체 앨범 딕셔너리 반환
  - `GetCurrentBmsFile()`: 현재 선택된 앨범의 BMS 파일 반환
  - `GetCurrentImageFile()`: 현재 선택된 앨범의 이미지 파일 반환
  - `GetCurrentBgmFile()`: 현재 선택된 앨범의 BGM 파일 반환
  - `GetCurrentBgaFile()`: 현재 선택된 앨범의 BGA 파일 반환
  - `GetCurrentSongInfo()`: 현재 선택된 앨범의 곡 정보 반환
  - `IsCustomChartMusicID()`: MusicID가 커스텀 차트인지 확인
  - `RegisterArtistFirstSong()`: 아티스트 ID별 첫 곡 정보 등록 (MusicID, 제목)
  - `GetArtistFirstSong()`: 아티스트 ID로 첫 곡 정보 가져오기 (대소문자 무시, 한글/영문 변환 지원)
  - `SetCurrentArtistId()` / `GetCurrentArtistId()`: 현재 곡의 아티스트 ID 저장/조회
  - `NormalizeArtistId()`: 아티스트 ID 정규화 (대소문자 무시, 한글/영문 변환)
  - 앨범별로 BMS, 이미지, BGA, BGM 파일을 관리하여 여러 커스텀 차트 지원
#### Core/Bootstrap/
- **MusicInjector.cs**: Harmony 패치 일괄 초기화·등록 (게임 어셈블리 준비 후 지연 적용)
- **HarmonyHookManager.cs**: BGA 종료·정지 관련 메서드 탐색 후 선택적 후킹
  - `SearchBgaEndMethods()`: BGA 종료 관련 메서드 광범위 검색 및 후킹
  - `SearchPauseStopMethods()`: 정지/일시정지 버튼 관련 메서드 검색 및 후킹
#### Core/Infrastructure/
- **GameTypeInspector/**: 게임 타입 탐색 및 필드 확인 (탐색 흐름, 필터, 로깅, 리플렉션 보조로 분리)
- **AssemblySearcher.cs**: 어셈블리에서 노트 관련 데이터 탐색
- **ReflectionHelper.cs**: 리플렉션 헬퍼 메서드
  - `FindMethod()`: 타입과 메서드를 찾는 헬퍼 메서드
  - `FindType()`: 타입을 찾는 헬퍼 메서드
#### Core/Assets/
- **PlaySceneArtworkInjector.cs**: 플레이 씬에서 커스텀 아트워크 주입
  - `StartArtworkInjection()`: 아트워크 주입 코루틴 시작 (중복 방지)
  - `TryInjectArtworkImmediately()`: 아트워크 즉시 적용 시도 (성능 최적화)
  - Image 캐싱을 통한 성능 최적화
- **ResultSceneInjector.cs**: 결과 씬(RythmGameResultScene)에서 커스텀 아트워크·난이도 표시 주입
  - `StartInjection()`: 커스텀 차트 선택 시 아트워크·난이도 주입 코루틴 시작
  - `ApplyArtworkToResultScene()` / `ApplyArtworkToArtWorkObject()`: 아트워크 적용 (GameFlowHooks 등에서 호출)
  - 난이도 텍스트/필드 적용, LV 부품 비활성화 등
- **SceneHandler.cs**: 씬별 처리 로직
  - `HandleFairyModeScene()`: FairyModeScene 처리
  - `HandlePlayMovieScene()`: PlayMovieScene 처리
  - `StopPreviewBGMOnPlayScene()`: 플레이 씬 진입 시 프리뷰 BGM 중지
- **AssetLoader.cs**: 에셋 로딩 유틸리티

### Services/
- **CustomChartHandler.cs**: 커스텀 차트 감지 및 아트워크/BGM 로드 서비스
  - `UpdateCustomChartTitle(object instance)`: coOpen 등에서 호출, 곡 선택 UI 인스턴스 기준 커스텀 차트 감지 및 에셋 로드
  - `mCurrentDispData` / `mMusicSelectData` 필드 캐싱으로 리플렉션 반복 호출 최소화
  - 주입 금지 씬(SoundPlayerScene, MoviePlayer_MovieSelect)에서는 감지 건너뜀

### Parsers/
- **BmsParser.cs**: BMS 파일 파서
  - `ParseBmsFile()`: BMS 파일 파싱
    - BPM 정보 추출 (`#BPM`, `#BPMXX`)
    - 노트 데이터 파싱 (채널 11-16, 18)
    - `HoldNoteProcessor.MatchHoldNotes()`: 홀드 노트 매칭
    - `FairyNoteProcessor.MatchFairyNotes()`: 페어리 노트 매칭
    - `CalculateTime()`: tick을 초로 변환 (BPM 변화 고려)
  - **노트 타입 판별/방향 매핑은 `BmsNoteDataParser`에서 수행** (`GetNoteType`, `GetFlickDirection`, `GetFairyDirection`)
- **SongInfoParser.cs**: 곡 정보 파일(`*.txt`) 파서
  - `ParseTxtFile()`: txt 파일에서 곡 정보 파싱
    - "곡 제목 : " 또는 "title=" 형식으로 곡 제목 파싱
    - "아티스트 : " 또는 "artist=" 형식으로 아티스트 파싱
    - "난이도 : " 또는 "difficulty=" 형식으로 난이도 파싱
    - "캐릭터 : " 또는 "character=" 형식으로 캐릭터(아티스트 ID) 파싱
    - "easy = 1" 형식으로 난이도 숫자 매핑 파싱
  - `SongInfo` 클래스: 곡 정보 저장
    - `Title`: 곡 제목
    - `Artist`: 아티스트
    - `Character`: 캐릭터 (아티스트 ID로 사용)
    - `Difficulties`: 난이도 목록
    - `DifficultyNumbers`: 난이도별 숫자 매핑

### Converters/
- **BmsNoteConverter.cs / BmsNoteConverter.Validation.cs**: BMS 노트를 게임 노트로 변환
  - `ConvertBmsNotesToNoteCreateData()`: BMS 노트를 NoteCreateData 배열로 변환
    - 캐시 초기화 (`NoteCreateDataBuilder.ClearCache()`)
    - 시간 순으로 정렬
    - 홀드/페어리 끝 노트 분리 (메인 배열에 추가하지 않음)
    - `NoteCreateDataBuilder.CreateNoteCreateData()`: NoteCreateData 생성
    - `HoldNoteProcessor.ProcessHoldEndNotes() (구현: HoldNoteProcessor.ConnectNodes.cs)`: 홀드 끝 노트 연결
    - `FairyNoteProcessor.ProcessFairyEndNotes() (구현: FairyNoteProcessor.ConnectNodes.cs)`: 페어리 끝 노트 연결
    - `SetLastNoteFlag()`: 마지막 노트 찾기 및 isLast 설정

### Builders/
- **NoteCreateDataBuilder.cs**: NoteCreateData 생성 및 필드 설정
  - `CreateNoteCreateData(BmsNote)`: BmsNote를 NoteCreateData로 변환
    - `perfectSample`: Time * 48000 (샘플 변환)
    - `NoteConstructorHelper.TryFindConstructor()`로 2~6개 파라미터 생성자 시도, 실패 시 기본 생성자
    - `SetPerfectSample`, laneLeftRightID, subLaneID, noteTypeID 설정 후 `NoteFieldInitializer` 호출
    - `directionIndex`: FairyEnd → CENTER_TOP, Direction 있음 → GetDirectionIndex, Hold → GetDirectionIndexFromLane, 그 외 CENTER_MIDDLE
    - `turnDireciton`: 페어리용 — FairyEnd는 1A/1B(Direction), Fairy 시작은 EndNote.Direction, 그 외 directionIndex 문자열에서 LEFT/RIGHT
  - `GetBmsNoteFromNoteCreateData()`: 캐시 키 `"{timeKey}_{lane}_{isLeft}_{type}"` Dictionary로 O(1) 검색, `ReferenceEquals(bmsNotes)`로 캐시 무효화
  - `ClearCache()`: 변환 시작 시 호출
- **NoteConstructorHelper.cs**: NoteCreateData 생성자 탐색 및 호출
  - `TryFindConstructor()`: 2~5개 파라미터 시그니처 순서 시도, 6개(방향 포함/기본) 시도
  - `EnableConstructorLogging`: 생성자 성공 로그 (기본 false)
  - `LogNoteCreateDataConstructorsAndFields()`: 디버그용 생성자·필드 목록 로그
- **NoteFieldInitializer.cs**: NoteCreateData 필드 초기화
  - `SetDirectionIndex()`, `SetTurnDirection()` (페어리 1A/1B·EndNote 반영), `SetNoteSize()`, `SetSlideEndFlickDirection()`, `SetDefaultBooleanFields()`

### Loaders/
- **GameTypeLoader.cs**: 게임 타입 로딩 및 초기화
  - `Initialize()`: 게임 타입 로딩 및 초기화
  - 모드에서 사용되는 타입만 로드: `NoteCreateData`, `NoteTypeId`, `NoteLaneLeftRight`, `NoteSubLaneType`, `NoteDirectionIndex`, `NoteSize`, `SlideEndFlickDirection`
  - 간단한 출력 형식: 초기화 완료 시 로드된 타입 이름과 네임스페이스만 출력
  - 로직 단순화: 불필요한 디버깅 로그 제거, 검색 로직 간소화

### Injectors/
#### Injectors/Shared/
- **BgmBgaInjector.cs**: BGA/BGM 주입 메인 클래스
  - `Initialize()`: BGA/BGM 파일 검색 (앨범별, `AlbumManager` 사용)
  - `StartInjection()`: 주입 시작 (플레이 씬 플래그 설정, 코루틴 시작)
  - `InjectBgmBgaCoroutine()`: 주기적 체크 (2초마다, BGA/BGM 주입 시도)
- **PatchApplier.cs**: Harmony 패치 적용기
  - `Initialize()`: 패치 적용기 초기화
  - `PatchCoverImageTypes()`: 커버 이미지 관련 타입 패치
  - `PatchAudioClipTypes()`: 오디오 클립 관련 타입 패치
  - `PatchTextTypes()`: 텍스트 설정 관련 타입 패치
  - `PatchSelectingMusicUITypes()`: `cMusicSelectSceneSelectingMusicUI` 타입 패치
    - `coOpen()`, `coClose()` 메서드 후킹
  - `PatchFairyModeNotesManagerTypes()`: `cFairyModeNotesManager` 타입 패치
    - `setIsAutoPlay()` 메서드 후킹
#### Injectors/Bga/
- **BgaInjector.cs**: BGA 주입 로직
  - `TryInjectBgaCoroutine()`: 모든 활성화된 VideoPlayer에 BGA 설정
    - 파일 크기 확인 및 타임아웃 동적 조정 (100MB당 1초 추가, 최대 60초)
    - VideoPlayer.url 설정, Prepare, Play
- **BgmSync/**: BGA/BGM 동기화 관리
  - `StartSync()`: BGA 재생 시작 시 BGM과 동기화 시작
  - `SyncBgaToBgm()`: BGA를 BGM 시간에 맞춰 동기화
  - `SyncCoroutine()`: 지속적인 동기화 모니터링 (0.1초마다 체크)
  - `GetCurrentAudioSource()`: BGM 오디오 소스 찾기 (cBGMBeatManager 우선)
  - `GetBgmTimeFromManager()`: cBGMBeatManager에서 BGM 시간 가져오기
#### Injectors/Bgm/
- **BgmInjector.cs**: BGM 주입 메인 클래스
  - `TryInjectBgmCoroutine()`: BGM 주입 시도 (최대 10회)
    - cBGMBeatManager 인스턴스 찾기 (캐싱)
    - `BgmLoader.LoadAndInjectAudioClip()`으로 주입
  - `SetBgmLength()`/`GetBgmLength()`: BGM 길이 저장/가져오기
- **BgmLoader.cs / BgmLoader.Runtime.cs**: BGM 파일 로딩 및 주입 로직 (partial 분리)
  - `LoadAndInjectAudioClip()`: UnityWebRequestMultimedia.GetAudioClip으로 오디오 로드
    - 파일 크기 확인 및 타임아웃 동적 조정 (10MB당 1초 추가, 최대 60초)
    - `setClip` 호출, `VerifyInjection()`으로 주입 검증, `RequestPlayAudio()`로 재생 시작
    - `BgmFinishTimeManager.SetFinishTime()`으로 게임 종료 시간 설정
  - `TryInjectViaSorceField()`/`GetAudioType()`/`VerifyInjection()`/`RequestPlayAudio()`: runtime 분리 파일에 위치
#### Injectors/GameEnd/
- **BgmFinishTimeManager.cs**: 게임 종료 시간 조정
  - `SetFinishTime()`: BGM 길이와 마지막 노트 시간 중 더 큰 값으로 설정
  - `GetTargetFinishTime()`: 설정된 목표 종료 시간 반환
- **Monitor/**: 게임 종료 모니터링 (partial 묶음)
  - `AdjustMusicDataForBgmLength()`: mRythmGameMusicData의 4개 필드 조정
    - `musicFadeOutEndSample`: BGM 길이 (샘플)
    - `musicFadeOutStartSample`: BGM 길이 - 1초 (샘플)
    - `screenFadeOutStartSample`: BGM 길이 - 1.5초 (샘플)
    - `screenFadeOutEndSample`: BGM 길이 (샘플)
  - `AdjustMusicDataOnSceneLoad()`/`MonitorGameEndPrefix/Postfix()`: monitor 묶음에 위치
  - `MonitorGameEndPrefix/Postfix()`: coMonitorGameEnd 후킹 (현재 테스트 모드로 실행 허용)
#### Injectors/Bgm/
- **BgmMonitorCoroutine.cs**: BGM 모니터링 코루틴
  - `StartBgmMonitorCoroutine()`: BGM 모니터링 코루틴 시작
  - `MonitorBgmFinishCoroutine()`: 0.1초마다 BGM 재생 시간 확인
    - BGM 길이 도달 시 `requestCommonRythmGameEnd()` 호출하여 결과 화면으로 전환
- **BgmInjectorHooks.cs**: BGM Injector Harmony 후킹 초기화
  - `Initialize()`: cBGMBeatManager, cRythmGameManager 메서드 후킹 설정
- **BgmAudioStateChecker.cs**: BGM 오디오 상태 확인 (getAudioClip, isReadyPlay, AudioSource 필드 검사, 로깅)
- **BgmMethodCallHooks.cs**: BGM 메서드 호출 후킹
  - `MethodCallPostfix/PostfixVoid()`: 메서드 호출 후킹 (중요 메서드만 로깅)
  - `HandleImportantMethodCall()`: requestPlayAudio 호출 시 주입된 BGM 감지 및 모니터링 코루틴 시작
  - `SetTimePrefix()`: setTime prefix 후킹 (시간 제한 적용)
#### Injectors/Bga/
- **BgaVideoHooks.cs**: BGA/비디오 관련 후킹
  - `VideoPlayerStopPrefix()`: VideoPlayer.Stop() 후킹 (호출 스택 출력, 120초 부근 경고)
#### Injectors/Bgm/
- **Formatting/**: BGM 후킹 로그 포맷팅 유틸리티
#### Injectors/Shared/
- **GameTypeSearcher.cs**: 게임 타입 탐색
  - `SearchGameTypes()`: 모드에서 사용되는 게임 타입만 탐색
  - 로드되는 타입: `cBGMBeatManager`, `cRythmGameResultSceneUpdater`
  - 간단한 출력 형식: 초기화 완료 시 로드된 타입 이름과 네임스페이스만 출력
  - 로직 단순화: 불필요한 디버깅 검색 메서드 제거, 코드 간소화

### Processors/
- **HoldNote/**: 홀드 노트 매칭 및 처리
  - `MatchHoldNotes()`: BMS 파싱 단계에서 홀드 시작(02)과 끝(19) 매칭, Duration 계산
    - **개선**: `ChannelToLaneMap`을 사용하여 모든 노트 채널(11, 12, 14, 15, 16, 18) 포함
    - **레인/방향 매칭**: 같은 `Lane`이면서 같은 방향(`IsLeft`)인 노트끼리만 홀드 시작/끝을 매칭
  - `ProcessHoldEndNotes()`: 홀드 끝 노트를 connectNodeDataArray에 추가
    - **개선 (NEW)**: `StartNote`와 `EndNote` 필드를 통한 **상호 참조(Reference) 기반 매칭** 구현
    - **개선**: 기존 시간/레인 기반 검색은 참조가 없는 경우의 폴백(Fallback)으로만 사용되어 **매칭 정확도 100% 보장**
    - `allBmsNotes`에서 직접 홀드 시작 노트를 찾아서 `noteList`와 매칭
    - 매칭되지 않은 노트를 새로 생성할 때 `noteList`에 추가
    - **개선 (NEW)**: BPM에 따라 자동으로 조정되는 **동적 시간 허용 범위** 사용  
      - 기준: BPM 120일 때 약 0.05초  
      - 최소 0.02초 ~ 최대 0.15초 범위 내에서 가변
    - 범위 검색으로 근사치 매칭 지원 (폴백 사용 시)
    - Dictionary 생성 (Lane + IsLeft + EndTime을 키로 사용, O(1) 검색)
    - 끝 노트의 directionIndex를 CENTER_MIDDLE로 설정
    - 끝 노트의 laneLeftRightID, subLaneID, noteTypeID를 시작 노트와 동일하게 설정
- **FairyNoteProcessor.cs / FairyNoteProcessor.ConnectNodes.cs**: 페어리 노트 매칭 및 처리
  - `MatchFairyNotes()`: BMS 파싱 단계에서 페어리 시작(11-18)과 끝(1A-1B) 매칭, Duration 계산 (Tick 단위)
    - **같은 `IsLeft`(왼쪽/오른쪽) 그룹 내에서만** 매칭 — **레인 제한 없음**(레인을 넘나드는 슬라이드 연결 지원)
    - Tick 오름차순, FIFO 큐로 1:1 연결. 상호 참조 `StartNote`/`EndNote` 설정
  - `ProcessFairyEndNotes()`: 페어리 끝 노트를 connectNodeDataArray에 추가
    - **시작 노트 찾기**: `StartNote` 참조 우선 → 없으면 폴백 (Time, Lane, IsLeft) + 허용오차 0.01초
    - 페어리 끝(1A/1B)의 `directionIndex`는 createNoteCreateData에서 설정한 값(1A=Left, 1B=Right) 유지
    - 끝 노트의 `turnDireciton`을 시작 노트와 동일하게 복사
- **NoteProcessorHelper.cs**: 노트 프로세서 공통 유틸리티
  - `AddEndNoteToConnectNodeArray()`: 끝 노트를 connectNodeDataArray에 추가
    - 끝 노트를 별도로 NoteCreateData로 변환 (메인 배열에는 추가하지 않음)
    - **개선**: 시작 노트의 laneLeftRightID, subLaneID를 끝 노트에 복사
    - **개선**: 끝 노트의 noteTypeID를 명시적으로 Hold/Fairy로 설정
    - **개선**: Boolean 필드들(isLast, isCritical 등) 설정
    - directionIndex, noteSize 설정
    - copyTurnDirection 옵션 (페어리 노트용)

### Harmony/Hooks/
- **폴더 역할**: `Harmony/Registration/*Patcher`가 `Harmony.Patch`로 메서드를 등록하고, Prefix/Postfix **본문**은 주로 `Harmony/Handlers/`, 훅 **진입점**은 이 폴더(`Harmony/Hooks/`)에 둡니다. 요약표는 `GRC2/Harmony/README.md` 참고.
- **NoteArray/**: 노트 배열 후킹 및 주입
  - `Initialize(string hwaFolderPath, List<BmsNote> bmsNotes = null, bool debugMode = false)`: Harmony 후킹 설정, note 폴더 경로 계산 후 `NoteArrayJsonDumper.Initialize(noteFolderPath, isEnabled: false)` 호출 (JSON 덤프 기본 비활성화)
  - `UpdateBmsNotes()`: 앨범 변경 시 BMS 노트 업데이트
  - `CreateAllNotePrefix()`: (선택) 원본/주입 차트 JSON 덤프, BMS 노트 주입  
    - JSON 덤프는 `NoteArrayJsonDumper` 활성화 시에만 파일이 생성됩니다.
  - `CreateAllNotePostfix()`: (현재 구현에서는 별도 동작 없음)
  - `LoadFairyNoteDatasJsonToArrayPrefix()`: BMS 노트 주입
  - `TryInjectBmsNotes()`: BMS 노트 주입 시도 (공통 로직)
    - **씬/플래그 가드**: `CustomAssetManager.ShouldInjectCustomContent()`을 통해 주입 가능 여부를 일괄 판단
      - 커스텀 차트 선택 플래그(`IsCustomChartSelected()`)가 `true`인 경우에만 주입 대상
      - `SoundPlayerScene`, `MoviePlayer_MovieSelect`와 같이 **주입 금지 씬**에서는 항상 주입 차단
    - 위 조건을 만족하지 않으면 BMS 노트를 주입하지 않고 로그만 출력
  - `InjectBmsNotes()`: BMS 노트를 NoteCreateData로 변환하여 mFairyNoteCreateDataArray에 주입
- **GameFlow/**: 게임 플로우 관련 후킹 (partial 묶음)
  - `CoOpenPrefix()`: `cMusicSelectSceneSelectingMusicUI.coOpen()` 후킹
    - 커스텀 차트 감지 및 아트워크/BGM 로드
    - `mCurrentDispData.mMusicSelectData.musicID` 및 `songTitle`을 통해 현재 선택된 곡 확인
    - **곡 제목 기반 판단**: 원본 제목 목록과 커스텀 차트 앨범 제목을 비교하여 판단
    - `UpdateCustomChartTitle()`: 커스텀 차트 감지 및 에셋 로드 로직
      - 커스텀 차트: 아트워크/BGM 주입, 텍스트 훅 스위치 ON
      - 일반 곡: 원본 아트워크/BGM 복원, 텍스트 훅 스위치 OFF
    - `TextPatch.EnableTextReplacement()`: 텍스트 교체 스위치 제어
  - `CoOpenPreMusicStartWindowPrefix()`: `cMusicSelectSceneUIUpdater.coOpenPreMusicStartWindow()` 후킹
    - 곡 시작 전 윈도우 열기 시 호출
    - `TryManipulateMusicIdByArtist()`: 아티스트 ID 기반 MusicID 변경
      - 현재 선택된 앨범의 캐릭터(아티스트 ID) 확인
      - 해당 아티스트의 첫 곡 MusicID로 변경
      - 원본 제목 등록 및 아티스트 ID 저장
    - 아티스트 ID로 MusicID를 변경한 경우 커스텀 차트 처리(FIRST로 변경) 건너뛰기
  - `CoClosePrefix()`: `cMusicSelectSceneSelectingMusicUI.coClose()` 후킹
    - 닫기 동작 모니터링
  - `SetIsAutoPlayPrefix/Postfix()`: `cFairyModeNotesManager.setIsAutoPlay()` 후킹
    - 오토 플레이 설정 모니터링 (현재는 로그만 출력)

### Harmony/Handlers/
- **AudioClipPatch.cs**: 곡 선택 화면 프리뷰 BGM 주입
  - `NoticeChangedMusicPostfix()`: `noticeChangedMusic` 후킹
    - 커스텀 차트 선택 시: `CustomBgmPlayer.InjectCustomBgm()` 호출
    - 일반 곡 선택 시: `CustomBgmPlayer.CleanupAndRestore()` 호출
- **CustomBgmPlayer.cs**: 커스텀 프리뷰 BGM 재생 관리
  - `InjectCustomBgm()`: 새로운 AudioSource 생성 및 커스텀 BGM 주입
  - `Cleanup()`: 커스텀 BGM 정리
  - `CleanupAndRestore()`: 커스텀 BGM 정리 및 원본 프리뷰 복원
- **MusicInjector.cs** (파일 위치: Core): Harmony 패치 초기화 및 곡 목록 주입
  - `Initialize()`: 곡 목록 주입 시스템 초기화
  - `PatchApplier`를 통해 여러 타입 패치 적용
  - `cMusicSelectScrollView.initializeAllItemByCrrentMusicData` 후킹으로 커스텀 곡 주입
- **MusicScrollView/**: 곡 목록 주입 후킹 (partial 묶음)
  - `InitializeAllItemByCrrentMusicDataPrefix()`: `initializeAllItemByCrrentMusicData` 후킹
    - `mCellHaviableMusicDataList`에 커스텀 곡 직접 주입
    - 템플릿 곡의 `MusicSelectScrollItemData`를 복제하여 커스텀 곡 생성
    - 각 앨범마다 고유한 커스텀 MusicID 생성 (54-511, 10000 이상)
    - 템플릿 곡의 제목을 원본 제목으로 등록 (`RegisterOriginalTitle`)
    - **아티스트 ID 통계 수집**: 각 곡의 아티스트 ID를 확인하여 통계 수집
    - **아티스트 ID별 첫 곡 정보 등록**: 각 아티스트 ID별로 첫 곡의 MusicID와 제목을 `AlbumManager.RegisterArtistFirstSong() (구현: Core/Album/AlbumManager/AlbumManager.Artists.cs)`으로 등록
  - `GenerateCustomMusicID()`: 커스텀 MusicID 생성
    - enum 타입인 경우: 54-511 범위 사용 (458개 앨범), 이후 10000 이상 사용
    - 기존 곡과 충돌하지 않는 범위 사용
- **ArtWorkPatch.cs**: 커스텀 아트워크 패치
  - 곡 선택 화면 및 플레이 씬에서 커스텀 아트워크 교체
- **ArtworkUpdater.cs**: 아트워크 업데이트 관리
- **MusicTitlePatch.cs**: 곡 제목 패치
  - `GetMusicTitlePostfix()`: `getMusicTitle` **조회 결과**(`__result`)를 커스텀 제목으로 바꿀 수 있음
  - `AlbumManager.IsCustomChartMusicID(key)`가 true일 때만 적용되는데, 게임이 넘기는 `key`가 로컬라이즈 키(`GV3_001_NAME` 등)이면 주입 MusicID와 다를 수 있음 → 상세는 [텍스트_패치_시스템_분석.md](../systems/텍스트_패치_시스템_분석.md) **5.3절**
- **TextPatch.cs**: 텍스트 패치
  - **플레이 씬/결과 씬에서 작동** (`FairyModeScene`, `PlayMovieScene`, `RenderCutinScene`, `RythmGameResultScene`)
  - `coOpen`에서 커스텀 차트 감지 시 스위치 활성화 (`EnableTextReplacement`)
  - 원본 제목을 현재 앨범의 커스텀 제목으로 교체
  - 원본 제목 목록(`GetAllOriginalTitles`)에 있는 텍스트만 교체
  - `SetTextPrefix()`: `Text` / `TMP`의 **`text` setter(쓰기)** Prefix 후킹. 곡 선택 씬에서도 setter는 호출되나, 스위치·씬 조건 때문에 **교체는 하지 않음** (의도) → [텍스트_패치_시스템_분석.md](../systems/텍스트_패치_시스템_분석.md) **5.3절**
    - 원본 제목 목록에 있는 텍스트를 현재 앨범의 커스텀 제목으로 교체
    - `AlbumManager.GetCurrentSongInfo().Title` 사용
- **PreviewAudio/**: 프리뷰 오디오 관리
  - `StopPreviewAndAmbient()`: 원본 프리뷰/환경음 중지
  - `RestoreMutedAudioSources()`: 원본 프리뷰 복원
    - **개선**: `cSoundManager`에서 직접 `mPreviewAudioSorce`와 `mAmbientAudioSorce`를 찾아서 복원
    - **개선**: 중복 복원 방지 (딕셔너리와 cSoundManager의 AudioSource 중복 처리)
- **AudioSourceFinder.cs**: 오디오 소스 찾기 유틸리티

### Helpers/
- **NoteArrayJsonDumper.cs**: 노트 배열 JSON 덤프
  - `Initialize(string noteFolderPath, bool isEnabled)`: note 폴더 경로 설정, 덤프 활성화 여부 설정 (기본값 비활성화, NoteArrayHooks 내부에서 false로 호출)
  - `DumpNoteArray()`: 노트 배열을 JSON으로 덤프 (재귀적 직렬화, connectNodeDataArray 포함)
  - `SerializeNoteCreateData()`: NoteCreateData 재귀적 직렬화
  - `FormatJson()`: 들여쓰기 형식 JSON 생성
- **EnumValueHelper.cs**: Enum 값 가져오기 및 변환
  - `GetEnumValue()`: Enum 값 가져오기 (캐싱 지원)
  - `GetNoteTypeId()`: NoteType을 NoteTypeId Enum으로 변환
  - `GetDirectionIndex()`: NoteDirection을 NoteDirectionIndex Enum으로 변환
  - `GetDirectionIndexFromLane()`: 레인에 따라 directionIndex 자동 설정 (홀드 노트용)
  - `GetSubLaneType()`: Lane을 NoteSubLaneType Enum으로 변환
- **FieldAccessHelper.cs**: 필드 접근 및 캐싱
  - `GetCachedField()`: 필드 정보 캐싱 (반복적인 Reflection 호출 최소화)
  - `SetFieldValue()`: 필드 값 설정 (캐시된 필드 정보 사용)
  - `GetFieldValue()`: 필드 값 가져오기 (캐시된 필드 정보 사용)
- **ErrorLogger.cs**: 일관된 에러 로깅 헬퍼
  - `LogException()`: 예외를 표준 형식으로 로깅 (Error 레벨)
    - 컨텍스트 정보, 추가 메시지, InnerException 자동 처리
    - 스택 트레이스 자동 포함
  - `LogWarning()`: 치명적이지 않은 오류를 경고 레벨로 로깅
    - 예외 정보를 경고로 로깅 (치명적이지 않은 오류용)
- **SteamManifestLocker.cs**: Steam 게임 업데이트 차단
  - `LockManifest()`: Steam 매니페스트 파일을 읽기 전용으로 설정하여 게임 업데이트 차단
    - `SceneDetector.OnInitializeMelon()`에서 자동 호출
    - Steam 매니페스트 파일 경로: `H:\steam\steamapps\appmanifest_2585040.acf` (게임별로 다를 수 있음)
    - 파일 속성을 `ReadOnly`로 설정하여 Steam이 업데이트를 시도하지 못하도록 차단
    - 이미 읽기 전용인 경우 중복 설정 방지
    - 로그 태그: `[GRC2] [SteamManifestLock]`, `[GRC2] [Main]`
    - 파일이 없는 경우 경고 메시지 출력

### 기타 클래스/
- **CustomAssetManager.cs**: 커스텀 아트워크와 BGM 관리
  - `LoadCustomArtwork()`: 커스텀 아트워크 로드 (**리사이즈/리샘플 없음**, 원본 해상도 그대로 로드)
    - UI 크기/해상도 스케일링은 게임의 `CanvasScaler(ScaleWithScreenSize)` 설정에 의해 처리됩니다.
  - `LoadCustomPreviewBGM()`: 커스텀 프리뷰 BGM 로드
  - `IsCustomChart()`: MusicID나 MusicData로 커스텀 차트인지 확인
  - `GetCustomArtwork()`: 로드된 커스텀 아트워크 스프라이트 반환
  - `IsImageLoaded()`: 특정 경로의 이미지가 이미 로드되어 있는지 확인 (성능 최적화)
- **MusicInjector.cs**: Harmony 패치 초기화 및 관리
  - `Initialize()`: 곡 목록 주입 시스템 초기화
  - `DelayedPatch()`: 게임 어셈블리 로드 대기 후 패치 적용

## 작동 원리

### 전체 흐름도
```
1. 게임 시작
   ↓
2. 모드 초기화 (SceneDetector.OnInitializeMelon)
   ├─ SteamManifestLocker.LockManifest() - 게임 업데이트 차단 (자동 호출)
   ├─ MusicInjector.Initialize() - 곡 목록 주입 시스템 초기화
   │  ├─ Harmony 인스턴스 생성
   │  ├─ PatchApplier.Initialize() - 패치 적용기 초기화
   │  ├─ DelayedPatch() 코루틴 시작 (게임 어셈블리 로드 대기)
   │  └─ Harmony 패치 적용:
   │     ├─ (제거됨) initalizeDatas (곡 목록 초기화) 패치
   │     ├─ noticeChangedMusic (곡 선택 변경) - AudioClipPatch
   │     ├─ getMusicTitle (곡 제목 가져오기) - MusicTitlePatch
   │     ├─ 커버 이미지 관련 타입 패치 - PatchApplier.PatchCoverImageTypes()
   │     ├─ 오디오 클립 관련 타입 패치 - PatchApplier.PatchAudioClipTypes()
   │     └─ 텍스트 설정 관련 타입 패치 - PatchApplier.PatchTextTypes()
   ├─ 디버그 모드 토글 없음 (항상 활성화)
   ├─ hwa 폴더 생성
   ├─ AlbumManager.ScanAlbums(_hwaFolderPath) - 앨범 폴더 스캔
   │  └─ hwa 폴더 내 모든 하위 폴더를 앨범으로 인식
   │  └─ 각 앨범별로 BMS, 이미지, BGA, BGM 파일 매핑
   ├─ GameTypeInspector.SearchFairyModeNotesManager() - 노트 매니저 타입 탐색/필드 확인
   ├─ BMS 파일 스캔 및 파싱 (전체 앨범/전체 파일)
   │  ├─ BPM 정보 추출 (#BPM, #BPMXX)
   │  ├─ 노트 데이터 파싱 (채널 11-16, 18)
   │  ├─ 홀드 노트 매칭 (HoldNoteProcessor.MatchHoldNotes)
   │  ├─ 페어리 노트 매칭 (FairyNoteProcessor.MatchFairyNotes)
   │  └─ 시간 계산 (tick → 초 변환, BPM 변화 고려)
   ├─ BmsNoteConverter.Initialize() - 게임 타입 초기화
   ├─ NoteArrayHooks.Initialize(_hwaFolderPath, ParsedBmsNotes, debugModeEnabled) - Harmony 후킹 설정
   │  └─ createAllNote, createNote, loadFairyNoteDatasJsonToArray 후킹
   ├─ BgmInjector.Initialize() - Harmony 후킹 설정
   │  └─ BgmInjectorHooks.Initialize() - cBGMBeatManager 메서드 후킹
   ├─ BgmBgaInjector.Initialize(_hwaFolderPath) - BGA/BGM 파일 검색 (앨범별)
   │  ├─ GameTypeSearcher.SearchGameTypes() - 모드에 필요한 게임 타입 탐색 (cBGMBeatManager, cRythmGameResultSceneUpdater)
   │  ├─ AlbumManager.GetCurrentBgaFile() - 현재 앨범의 BGA 파일
   │  └─ AlbumManager.GetCurrentBgmFile() - 현재 앨범의 BGM 파일
   └─ HarmonyHookManager.Initialize() - BGA 종료 및 일시정지 메서드 모니터링
   ↓
3. 곡 선택 화면 (MusicSelectScene)
   ├─ initializeAllItemByCrrentMusicData 후킹 (MusicScrollViewHooks.InitializeAllItemByCrrentMusicDataPrefix (구현: MusicScrollViewHooks.ListLogging.cs))
   │  ├─ mCellHaviableMusicDataList에 커스텀 곡 직접 주입
   │  ├─ 템플릿 곡 복제하여 커스텀 곡 생성
   │  ├─ 커스텀 MusicID 생성 (54-511, 10000 이상)
   │  └─ 원본 제목 등록
   │  └─ PrintMusicList() - 곡 목록 출력 및 주입 확인
   │     ├─ 전체 곡 목록 출력 (원본/주입 구분)
   │     ├─ 각 곡의 MusicID, 제목, 앨범 정보 출력
   │     └─ 주입 상태 확인 및 요약 정보 출력
   ├─ noticeChangedMusic 후킹 (AudioClipPatch.NoticeChangedMusicPostfix)
   │  ├─ 커스텀 차트 선택 시:
   │  │  ├─ AlbumManager.SelectAlbumByMusicID() (구현: Core/Album/AlbumManager/AlbumManager.Mappings.cs) - 앨범 선택
   │  │  ├─ CustomBgmPlayer.InjectCustomBgm() - 커스텀 프리뷰 BGM 주입
   │  │  ├─ PreviewAudioManager.StopPreviewAndAmbient() - 원본 프리뷰 중지
   │  │  └─ ArtworkUpdater.UpdateArtwork() - 커스텀 아트워크 업데이트
   │  └─ 일반 곡 선택 시:
   │     ├─ CustomBgmPlayer.CleanupAndRestore() - 커스텀 BGM 정리 및 원본 복원
   │     └─ CustomAssetManager.SetCustomChartSelected(false)
   ├─ coOpen 후킹 (GameFlowHooks.CoOpenPrefix (구현: Harmony/Hooks/GameFlow/GameFlowHooks.Navigation.cs))
   │  ├─ 커스텀 차트 감지 (mCurrentDispData.mMusicSelectData.musicID 및 songTitle 확인)
   │  ├─ 곡 제목 기반 판단:
   │  │  ├─ 곡 제목이 원본 제목 목록에 있으면 → 일반 곡
   │  │  ├─ 곡 제목이 커스텀 차트 앨범 제목이면 → 커스텀 차트
   │  │  └─ 둘 다 아니면 MusicID로 확인
   │  ├─ 커스텀 차트 선택 시:
   │  │  ├─ AlbumManager.SelectAlbumByMusicID() (구현: Core/Album/AlbumManager/AlbumManager.Mappings.cs) - 앨범 선택
   │  │  ├─ CustomBgmPlayer.InjectCustomBgm() - 커스텀 프리뷰 BGM 주입
   │  │  ├─ PreviewAudioManager.StopPreviewAndAmbient() - 원본 프리뷰 중지
   │     │  ├─ CustomAssetManager.LoadCustomArtwork() - 커스텀 아트워크 로드
   │  │  └─ ArtworkUpdater.UpdateArtwork() - 커스텀 아트워크 업데이트
   │  │  └─ TextPatch.EnableTextReplacement(true) - 텍스트 훅 스위치 ON
   │  └─ 일반 곡 선택 시:
   │     ├─ CustomAssetManager.SetCustomChartSelected(false) - 커스텀 차트 선택 해제
   │     ├─ CustomBgmPlayer.CleanupAndRestore() - 원본 아트워크/BGM 복원
   │     └─ TextPatch.EnableTextReplacement(false) - 텍스트 훅 스위치 OFF
   ├─ coOpenPreMusicStartWindow 후킹 (GameFlowHooks.CoOpenPreMusicStartWindowPrefix (구현: Harmony/Hooks/GameFlow/GameFlowHooks.PreStartWindow.cs))
   │  ├─ 현재 선택된 앨범의 캐릭터(아티스트 ID) 확인
   │  ├─ 해당 아티스트의 첫 곡 MusicID로 변경
   │  ├─ 원본 제목 등록 및 아티스트 ID 저장
   │  └─ 아티스트 ID로 변경한 경우 커스텀 차트 처리 건너뛰기
   └─ coClose 후킹 (GameFlowHooks.CoClosePrefix (구현: Harmony/Hooks/GameFlow/GameFlowHooks.Navigation.cs))
      └─ 닫기 동작 모니터링 (로그만 출력)
   ↓
4. 플레이 씬 로드 (FairyModeScene, PlayMovieScene)
   ├─ CustomBgmPlayer.Cleanup() - 커스텀 프리뷰 BGM 중지 (중복 재생 방지)
   ├─ ReloadCurrentAlbumAssets() - 현재 앨범 상태 갱신 (주로 BMS 노트 캐시 갱신 / **아트워크 파일 재로드·리사이즈 없음**)
   ├─ BgmBgaInjector.StartInjection(isPlayScene: true) 호출
   └─ BgmGameEndMonitor.AdjustMusicDataOnSceneLoad() (구현: BgmGameEndMonitor.Adjustments.cs) - 4개 필드 조정
   ↓
5. createAllNote 후킹 (NoteArrayHooks.CreateAllNotePrefix)
   ├─ TryInjectBmsNotes() 호출
   │  ├─ 씬/플래그 가드: CustomAssetManager.ShouldInjectCustomContent() 확인
   │  │  ├─ 주입 금지 씬(SoundPlayerScene, MoviePlayer_MovieSelect)이면 주입 안 함
   │  │  └─ 커스텀 차트 미선택이면 주입 안 함
   │  ├─ 조건 불만족 시 주입 건너뛰고 로그 출력
   │  └─ 조건 만족 시 주입 진행
   ├─ (선택) 원본 노트 배열 백업 (JSON 덤프가 활성화된 경우에만)
   ├─ BMS 노트를 NoteCreateData로 변환 (BmsNoteConverter.ConvertBmsNotesToNoteCreateData)
   │  ├─ 홀드/페어리 끝 노트 분리
   │  ├─ 메인 노트 배열에 시작 노트만 추가
   │  ├─ HoldNoteProcessor.ProcessHoldEndNotes() (구현: HoldNoteProcessor.ConnectNodes.cs) - 홀드 끝 노트 연결
   │  └─ FairyNoteProcessor.ProcessFairyEndNotes() (구현: FairyNoteProcessor.ConnectNodes.cs) - 페어리 끝 노트 연결
   └─ mFairyNoteCreateDataArray 교체
   ↓
6. createNote 호출 (게임 내부)
   └─ 변환된 노트가 게임에 생성됨
   ↓
7. 텍스트 교체 (플레이 씬/결과 씬에서, TextPatch.SetTextPrefix)
   ├─ 스위치가 ON인 경우에만 작동
   ├─ 원본 제목 목록에 있는 텍스트를 현재 앨범의 커스텀 제목으로 교체
   └─ 플레이 씬/결과 씬에서만 작동 (FairyModeScene, PlayMovieScene, RenderCutinScene, RythmGameResultScene)
   ↓
8. BGA/BGM 주입 (주기적 체크, 2초마다, BgmBgaInjector.InjectBgmBgaCoroutine)
   ├─ BGA 주입 (BgaInjector.TryInjectBgaCoroutine)
   │  ├─ VideoPlayer 찾기 (모든 활성화된 VideoPlayer 지원)
   │  ├─ 파일 크기 확인 및 타임아웃 조정 (100MB당 1초 추가, 최대 60초)
   │  ├─ VideoPlayer.url 설정 및 Prepare
   │  ├─ VideoPlayer.Play()로 재생 시작
   │  └─ BgaBgmSyncManager.StartSync()로 BGA/BGM 동기화 시작
   │     ├─ BGM 오디오 소스 찾기 (cBGMBeatManager 우선)
   │     ├─ 즉시 동기화 (SyncBgaToBgm)
   │     └─ 지속적인 동기화 모니터링 시작 (0.1초마다 체크)
   └─ BGM 주입 (BgmInjector.TryInjectBgmCoroutine, 플레이 씬에서만)
      ├─ cBGMBeatManager 인스턴스 찾기 (캐싱)
      ├─ 파일 크기 확인 및 타임아웃 조정 (10MB당 1초 추가, 최대 60초)
      ├─ UnityWebRequestMultimedia.GetAudioClip()으로 로드
      ├─ BgmLoader.LoadAndInjectAudioClip()으로 주입
      │  ├─ UnityWebRequestMultimedia.GetAudioClip()으로 오디오 파일 로드
      │  ├─ cBGMBeatManager.setClip()으로 주입
      │  ├─ 주입 검증 (getAudioClip, AudioSource 확인)
      │  ├─ _sorce 필드 직접 접근 시도 (대체 방법)
      │  └─ BgmFinishTimeManager.SetFinishTime()으로 게임 종료 시간 설정
      ├─ cBGMBeatManager.requestPlayAudio()로 재생 시작
      ├─ BgmMethodCallHooks.HandleImportantMethodCall()에서 requestPlayAudio 후킹
      │  ├─ 주입된 BGM 감지 (클립 이름 확인)
      │  ├─ BgmFinishTimeManager.SetFinishTime() 재호출 (BGM 길이 재확인)
      │  └─ BgmMonitorCoroutine.StartBgmMonitorCoroutine()로 BGM 모니터링 시작
      └─ BgmGameEndMonitor.AdjustMusicDataForBgmLength() (구현: BgmGameEndMonitor.Adjustments.cs)로 4개 필드 조정 (플레이 씬 로드 시 또는 coMonitorGameEnd 후킹 시)
         ├─ musicFadeOutEndSample: BGM 길이 (샘플)
         ├─ musicFadeOutStartSample: BGM 길이 - 1초 (샘플)
         ├─ screenFadeOutStartSample: BGM 길이 - 1.5초 (샘플)
         └─ screenFadeOutEndSample: BGM 길이 (샘플)
```

## 사용 방법

### 1. 빌드
```bash
# Windows
build_release.bat  # Release 빌드
build_debug.bat  # Debug 빌드
```

빌드된 DLL 파일은 `GUNVOLT RECORDS Cychronicle/bin/Debug/` 또는 `bin/Release/`에 생성됩니다.

### 2. 설치
1. MelonLoader가 설치된 GUNVOLT RECORDS Cychronicle 게임 폴더로 이동
2. 빌드된 DLL을 `Mods` 폴더에 복사
3. 게임 실행

### 3. BMS 파일 배치

#### 단일 앨범 (hwa 폴더 직접 사용)
1. 게임 설치 폴더에 `hwa` 폴더 생성
2. BMS 파일(`*.bms`, `*.bme`, `*.bml`)을 `hwa` 폴더에 배치
3. (선택) BGA 비디오 파일(`*.mp4`)을 `hwa` 폴더에 배치
4. (선택) BGM 오디오 파일(`*.mp3`, `*.wav`, `*.ogg`)을 `hwa` 폴더에 배치
5. (선택) 곡 정보 파일(`*.txt`)을 `hwa` 폴더에 배치

#### 다중 앨범 (하위 폴더 사용)
1. 게임 설치 폴더에 `hwa` 폴더 생성
2. 각 커스텀 차트마다 `hwa` 폴더 내에 별도 폴더 생성 (예: `hwa/Album1/`, `hwa/Album2/`)
3. 각 앨범 폴더에 해당 차트의 파일들을 배치:
   - BMS 파일(`*.bms`, `*.bme`, `*.bml`)
   - (선택) BGA 비디오 파일(`*.mp4`)
   - (선택) BGM 오디오 파일(`*.mp3`, `*.wav`, `*.ogg`)
   - (선택) 커버 이미지(`*.jpg`, `*.png`, `*.jpeg`)
   - (선택) 곡 정보 파일(`*.txt`)
4. `AlbumManager`가 자동으로 모든 앨범 폴더를 스캔하여 관리
5. 곡 선택 시 `MusicID`에 따라 자동으로 해당 앨범이 선택됨

## BMS 파일 형식

### 지원하는 채널
- **채널 11-16**: 일반 레인 (게임 레인으로 매핑)
- **채널 18**: 특수 레인

### 노트 타입
- **01**: 일반 노트 (Tap)
- **02**: 홀드 시작 (Hold) - 19와 쌍을 이루어 홀드 노트 생성
- **19**: 홀드 끝 (HoldEnd) - 02와 쌍을 이루어 홀드 노트 길이 계산
- **03-0A**: 플릭 노트 (Flick) - 방향에 따라 8방향 플릭 (03: 왼쪽, 04: 왼쪽위, 05: 위, 06: 오른쪽위, 07: 오른쪽, 08: 오른쪽아래, 09: 아래, 0A: 왼쪽아래)
- **11-18**: 페어리 노트 (Fairy) - 방향에 따라 8방향 페어리 (11: 왼쪽, 12: 왼쪽위, 13: 위, 14: 오른쪽위, 15: 오른쪽, 16: 오른쪽아래, 17: 아래, 18: 왼쪽아래)
- **1A-1B**: 페어리 끝 노트 (FairyEnd) - 페어리 노트의 끝 (1A=Left 턴, 1B=Right 턴)

### BPM 헤더 형식
- `#BPM`: 기본 BPM 설정
- `#BPMXX`: 특정 measure에서 BPM 변경 (XX는 16진수 BPM 인덱스)
- BPM 변화는 `#XXXYY:ZZ` 형식으로 measure와 함께 지정됨

## 기술 스택

- **.NET Framework 4.7.2**: 프로젝트 타겟 프레임워크
- **MelonLoader**: Unity 게임 모딩 프레임워크
- **Harmony**: 런타임 메서드 패칭 라이브러리
- **Unity Engine**: 게임 엔진 (참조용)

## 어셈블리 어트리뷰트

프로젝트의 진입점은 `SceneDetector` 클래스이며, 다음 어셈블리 어트리뷰트로 정의됩니다:

```csharp
[assembly: MelonInfo(typeof(GRC2.Core.SceneDetector), "GUNVOLT RECORDS Cychronicle", "1.0.0", "화영왕")]
[assembly: MelonGame("INTI CREATES", "GUNVOLT RECORDS Cychronicle")]
```

**위치**: `GRC2/Properties/AssemblyInfo.cs`

## 참조 DLL 목록

프로젝트는 다음 DLL들을 참조합니다:

### MelonLoader 프레임워크
- **MelonLoader.dll**: MelonLoader 모딩 프레임워크 (경로: `MelonLoader/net35/MelonLoader.dll`)
- **0Harmony.dll**: Harmony 라이브러리 (경로: `MelonLoader/net35/0Harmony.dll`)

### Unity 엔진 모듈
- **UnityEngine.CoreModule.dll**: Unity 핵심 모듈
- **UnityEngine.UI.dll**: Unity UI 시스템
- **UnityEngine.VideoModule.dll**: Unity 비디오 재생 모듈
- **Unity.TextMeshPro.dll**: TextMeshPro 텍스트 렌더링
- **UnityEngine.AnimationModule.dll**: Unity 애니메이션 시스템
- **UnityEngine.PhysicsModule.dll**: Unity 물리 시스템
- **UnityEngine.AudioModule.dll**: Unity 오디오 시스템
- **UnityEngine.ParticleSystemModule.dll**: Unity 파티클 시스템
- **UnityEngine.UIModule.dll**: Unity UI 모듈
- **UnityEngine.UnityWebRequestModule.dll**: Unity 웹 요청 모듈
- **UnityEngine.UnityWebRequestAudioModule.dll**: Unity 오디오 웹 요청 모듈
- **UnityEngine.ImageConversionModule.dll**: Unity 이미지 변환 모듈

**참고**: 모든 Unity DLL은 게임 설치 폴더의 `GUNVOLT_RECORDS_Cychronicle_Data/Managed/` 경로에서 참조됩니다.

## Harmony 패치 방식

이 프로젝트는 Harmony 어트리뷰트 기반 패치 대신 **동적 패치 방식**을 사용합니다:

- `HarmonyMethod`를 사용하여 런타임에 메서드를 패치
- `harmonyInstance.Patch()` 메서드를 통해 Prefix/Postfix 패치 적용
- 어트리뷰트 기반 패치(`[HarmonyPrefix]`, `[HarmonyPostfix]`)는 사용하지 않음

**주요 패치 클래스**:
- `PatchApplier`: 패치 적용기 (각 Patcher 클래스 초기화)
- `AudioClipPatcher`: 오디오 클립 관련 패치
- `CoverImagePatcher`: 커버 이미지 관련 패치
- `SelectingMusicUIPatcher`: 곡 선택 UI 관련 패치
- `TextPatcher`: 텍스트 설정 관련 패치
- `FairyModeNotesManagerPatcher`: 노트 매니저 관련 패치
- `CharactorLoadPatcher`: 캐릭터 로딩 관련 패치

## 성능 최적화

프로젝트는 대용량 BMS 파일과 많은 노트를 처리하기 위해 여러 성능 최적화 기법을 사용합니다:

1. **필드 접근 캐싱** (`FieldAccessHelper`):
   - Reflection을 통한 필드 접근을 Dictionary로 캐싱하여 반복 호출 최소화
   - `TryGetValue`를 사용한 O(1) 필드 검색
   - 필드 정보를 한 번만 조회하여 성능 향상

2. **노트 검색 최적화** (`NoteCreateDataBuilder`):
   - Time 기반 Dictionary 캐싱 (`GetBmsNoteFromNoteCreateData`에서 O(1) 검색)
   - 참조 비교(`ReferenceEquals`)를 통한 캐시 무효화 정확도 향상
   - Time을 0.001초 단위로 반올림하여 키로 사용 (부동소수점 오차 방지)
   - `ClearCache()`로 변환 시작 시 캐시 초기화

3. **홀드/페어리 노트 매칭 최적화** (`HoldNoteProcessor`, `FairyNoteProcessor`):
   - Dictionary 기반 빠른 검색 (Lane + IsLeft + EndTime을 키로 사용)
   - 선형 검색 대신 O(1) 검색으로 성능 향상
   - 캐시 초기화를 위한 사전 호출로 최적화

4. **BGA/BGM 주입 최적화** (`BgmBgaInjector`, `BgaInjector`, `BgmInjector`, `BgmLoader`):
   - 인스턴스 캐싱 (재검색 최소화)
   - 대용량 파일 지원 (동적 타임아웃 조정)
   - 기능별 클래스 분리로 코드 가독성 및 유지보수성 향상
   - Harmony 후킹 시 프레임마다 호출되는 메서드 제외하여 성능 최적화
   - BGM 로딩 및 주입 로직 분리 (`BgmLoader`)로 재사용성 향상
   - 게임 종료 시간 관리 분리 (`BgmFinishTimeManager`)로 책임 분리

5. **노트 변환 최적화** (`BmsNoteConverter`):
   - 한 번만 정렬 (시간 순으로 정렬하여 처리)
   - 홀드/페어리 끝 노트 분리 처리로 메인 배열 크기 최소화
   - 캐시 초기화 (`NoteCreateDataBuilder.ClearCache()`)로 메모리 효율성 향상

6. **Enum 값 캐싱** (`EnumValueHelper`):
   - Enum 값 파싱 결과를 Dictionary로 캐싱
   - Type.FullName + valueName을 키로 사용하여 O(1) 검색

7. **Reflection 필드 정보 캐싱** (`BgmGameEndMonitor`):
   - mRythmGameMusicData 필드 정보를 캐싱하여 반복적인 Reflection 호출 최소화
   - 마지막 조정된 값 캐싱으로 불필요한 SetValue 방지

## 코드 품질 개선

### 중복 코드 제거
1. **씬 처리 로직 통합** (`SceneDetector.cs`):
   - `HandleFairyModeScene()`과 `HandlePlayMovieScene()`의 중복 로직을 `HandlePlayScene()` 공통 메서드로 통합
   - 코드 라인 수 약 50줄 → 30줄로 감소

2. **끝 노트 체크 로직 통합** (`BmsNoteConverter.cs / BmsNoteConverter.Validation.cs`):
   - 홀드와 페어리 끝 노트 체크 로직을 `CheckMissingEndNotes()` 공통 메서드로 통합
   - 코드 라인 수 약 100줄 → 50줄로 감소

3. **Prefix 메서드 통합** (`Harmony/Hooks/NoteArray/NoteArrayHooks.Patching.cs`):
   - `CreateAllNotePrefix()`와 `LoadFairyNoteDatasJsonToArrayPrefix()`의 중복 로직을 `TryInjectBmsNotes()` 공통 메서드로 통합
   - 에러 처리 일관성 개선 (스택 트레이스 자동 포함)

4. **생성자 찾기 로직 리팩토링** (`NoteCreateDataBuilder.cs`):
   - 반복적인 생성자 시도 로직을 `TryFindConstructor()` 메서드로 통합
   - 생성자 시그니처를 배열로 정의하여 순회 방식으로 개선
   - 코드 라인 수 약 200줄 → 80줄로 감소

### 에러 처리 일관성 개선
1. **ErrorLogger 헬퍼 클래스 추가** (`Helpers/ErrorLogger.cs`):
   - 모든 catch 블록에서 일관된 에러 로깅 패턴 사용
   - `LogException()`: 예외를 표준 형식으로 로깅 (Error 레벨)
   - `LogWarning()`: 치명적이지 않은 오류를 경고 레벨로 로깅
   - InnerException 자동 처리
   - 컨텍스트 정보 및 스택 트레이스 자동 포함

2. **표준화된 에러 처리**:
   - 약 27개의 catch 블록을 `ErrorLogger` 사용으로 통일
   - 에러 메시지 형식 일관성 확보
   - 유지보수성 향상 (에러 로깅 로직이 한 곳에 집중)

## 주의사항

1. **게임 버전 호환성**: 게임 업데이트 시 일부 기능이 작동하지 않을 수 있습니다.
2. **BMS 파일 형식**: 표준 BMS 형식을 따르는 파일만 지원합니다.
3. **성능**: 대용량 BMS 파일이나 많은 노트가 있는 경우 성능 저하가 발생할 수 있습니다. (최적화 적용으로 개선됨)
4. **페어리 노트**: 페어리 노트는 홀드 노트와 동일한 방식으로 처리되며, `turnDireciton` 필드를 통해 회전 방향이 설정됩니다.
5. **대용량 파일**: BGA/BGM 파일이 매우 큰 경우(500MB 이상) 로딩 시간이 길어질 수 있습니다.
6. **메모리 사용량**: 대용량 오디오 파일(WAV, 200MB 이상)은 메모리 사용량이 높을 수 있습니다. OGG/MP3 사용을 권장합니다.

## 알려진 제한사항

- **BGM 주입**: 플레이 씬에서만 시도되며, 최대 10회까지 재시도합니다.
- **페어리 노트**: 페어리 노트는 홀드 노트와 동일한 방식으로 처리되며, `turnDireciton` 필드를 통해 회전 방향이 설정됩니다.
- **재시작 시 주입**: 게임을 재시작하면 BGM/BGA/BMS 노트가 자동으로 재주입됩니다.
- **BMS 파일 파싱 vs 주입 대상**:
  - 초기화 시점에 **모든 앨범의 모든 BMS 파일을 파싱**해 캐시에 보관합니다. (검증/요약 로그 출력 목적 포함)
  - 하지만 실제 주입에 쓰이는 노트(`ParsedBmsNotes`)는 **현재 선택된 앨범의 현재 BMS 파일(기본: 첫 번째 파일)** 기준입니다.
  - 즉, 한 앨범에 BMS가 여러 개면 “파싱”은 모두 되지만, “주입”은 현재 선택 파일 1개만 반영됩니다. (선택 로직 확장은 추후 과제)
- **앨범 관리**: `AlbumManager`가 자동으로 앨범을 스캔하고 관리하지만, 곡 선택 시 `MusicID`에 따라 자동으로 앨범이 선택되어야 합니다.
- **BPM 변화**: 복잡한 BPM 변화가 있는 경우 정확도가 떨어질 수 있습니다. 현재 구현은 `#BPMXX`를 파싱하더라도 시간 계산에 완전하게 반영되지 않을 수 있으며, 단순 처리로 인해 오차가 발생할 수 있습니다.
- **JSON 덤프**: 현재 코드 기준 기본값은 비활성화입니다.  
  - 필요 시 `GRC2/Harmony/Hooks/NoteArray/NoteArrayHooks.Patching.cs`에서 `NoteArrayJsonDumper.Initialize(..., isEnabled: true)`로 변경하면 생성됩니다.
  - 대용량 차트의 경우 파일 크기가 매우 클 수 있습니다.
- **게임 종료 시간 조정**: 주입된 BGM 길이와 마지막 노트 시간 중 더 큰 값으로 게임 종료 시간이 설정됩니다. `BgmFinishTimeManager`가 이를 관리하며, `BgmInjectorHooks.SetTimePrefix()`에서 `cBGMBeatManager.setTime()` 메서드를 prefix로 후킹하여 시간 제한을 적용합니다.
- **BMS 작성 주의사항**: 
  - **페어리 끝 노트 필수 배치**: 페어리 노트(11-18)를 사용하는 경우, 반드시 페어리 끝 노트(1A-1B)를 배치해야 합니다. 페어리 끝 노트를 배치하지 않으면 **BMS 노트 주입이 거부됩니다**. 로그에 "BMS 파일을 다시 확인해보세요!" 메시지가 출력됩니다.
  - **홀드 끝 노트 필수 배치**: 홀드 노트(02)를 사용하는 경우, 반드시 홀드 끝 노트(19)를 배치해야 합니다. 홀드 끝 노트를 배치하지 않으면 **BMS 노트 주입이 거부됩니다**. 로그에 "BMS 파일을 다시 확인해보세요!" 메시지가 출력됩니다.
- **Harmony 후킹**: 
  - `BgmInjectorHooks`는 `cBGMBeatManager`와 `cRythmGameManager`의 주요 메서드들을 후킹하여 디버깅 및 모니터링을 수행합니다. 프레임마다 호출되는 메서드는 제외되어 성능에 영향을 최소화합니다. `requestPlayAudio` 호출 시 주입된 BGM을 자동 감지하여 게임 종료 시간을 조정하고, 게임 종료/클리어 관련 메서드들을 자동으로 탐지하여 후킹합니다.
  - `BgaVideoHooks`는 `VideoPlayer`를 후킹하여 BGA 상태를 모니터링합니다.
  - `HarmonyHookManager`는 BGA 종료 관련 메서드와 정지/일시정지 버튼 관련 메서드를 광범위하게 검색하여 후킹합니다.
- **BGM 로딩 및 주입**: 
  - `BgmLoader.LoadAndInjectAudioClip()`: BGM 파일 로딩, `setClip` 호출, 주입 검증 담당
  - `BgmLoader.TryInjectViaSorceField() (구현: BgmLoader.Runtime.cs)`: `_sorce` 필드 직접 접근 등 대체 주입 방법 시도
  - `BgmLoader.VerifyInjection() (구현: BgmLoader.Runtime.cs)`: 주입 후 `getAudioClip()`, `AudioSource.clip` 확인
  - `BgmLoader.RequestPlayAudio() (구현: BgmLoader.Runtime.cs)`: `requestPlayAudio()` 메서드 호출
  - `BgmLoader.GetAudioType() (구현: BgmLoader.Runtime.cs)`: 파일 확장자에 따라 `AudioType` 반환 (MPEG, WAV, OGGVORBIS)

## 메서드-구현 파일 매핑 인덱스

- **GameFlowHooks**
  - `CoOpenPrefix()` / `CoClosePrefix()` -> `GRC2/Harmony/Hooks/GameFlow/GameFlowHooks.Navigation.cs`
  - `TryManipulateMusicIdByArtist()` -> `GRC2/Harmony/Hooks/GameFlow/GameFlowHooks.Artist.cs`
  - `SetIsAutoPlayPrefix()` / `SetIsAutoPlayPostfix()` -> `GRC2/Harmony/Hooks/GameFlow/GameFlowHooks.UiWindows.cs`
  - `StartRythmGamePrefix()` / `CoStartRythmGamePrefix()` -> `GRC2/Harmony/Hooks/GameFlow/GameFlowHooks.Start.cs`
  - `CoOpenPreMusicStartWindowPrefix()` -> `GRC2/Harmony/Hooks/GameFlow/GameFlowHooks.PreStartWindow.cs`
- **MusicScrollViewHooks**
  - `InitializeAllItemByCrrentMusicDataPrefix()` -> `GRC2/Harmony/Hooks/MusicScrollView/MusicScrollViewHooks.ListLogging.cs`
  - `InjectCustomMusicToCellList()` -> `GRC2/Harmony/Hooks/MusicScrollView/MusicScrollViewHooks.Injection.cs`
  - `GenerateCustomMusicID()` -> `GRC2/Harmony/Hooks/MusicScrollView/MusicScrollViewHooks.CustomMusicId.cs`
  - `DoSortMusicListPrefix()` / `DoFilterMusicListPrefix()` / `DoFilterMusicListPostfix()` -> `GRC2/Harmony/Hooks/MusicScrollView/MusicScrollViewHooks.FilterSort.cs`
- **BgmGameEndMonitor**
  - `AdjustMusicDataOnSceneLoad()` / `AdjustMusicDataForBgmLength()` / `MonitorGameEndPostfix()` -> `GRC2/Injectors/GameEnd/Monitor/BgmGameEndMonitor.Adjustments.cs`
  - 샘플 필드 적용 -> `GRC2/Injectors/GameEnd/Monitor/BgmGameEndMonitor.SampleAdjustments.cs`
  - 주기적 BGM 상태 로그 -> `GRC2/Injectors/GameEnd/Monitor/BgmGameEndMonitor.PeriodicLogging.cs`
- **BgmLoader**
  - `LoadAndInjectAudioClip()` -> `GRC2/Injectors/Bgm/BgmLoader.cs`
  - `TryInjectViaSorceField()` / `VerifyInjection()` / `RequestPlayAudio()` / `GetAudioType()` -> `GRC2/Injectors/Bgm/BgmLoader.Runtime.cs`
- **BgaBgmSyncManager**
  - `StartSync()` -> `GRC2/Injectors/Bga/BgmSync/BgaBgmSyncManager.cs`
  - `GetCurrentAudioSource()` -> `GRC2/Injectors/Bga/BgmSync/BgaBgmSyncManager.AudioSource.cs`
  - `SyncBgaToBgm()` -> `GRC2/Injectors/Bga/BgmSync/BgaBgmSyncManager.Sync.cs`
  - `GetBgmTimeFromManager()` / `SyncCoroutine()` / `StopSync()` / `Reset()` -> `GRC2/Injectors/Bga/BgmSync/BgaBgmSyncManager.Runtime.cs`
- **AlbumManager**
  - `ScanAlbums()` -> `GRC2/Core/Album/AlbumManager/AlbumManager.Scanning.cs`
  - `SelectAlbum()` / `SelectAlbumBySongInfo()` -> `GRC2/Core/Album/AlbumManager/AlbumManager.Selection.cs`
  - `SelectAlbumByMusicID()` / `GetAllOriginalTitles()` -> `GRC2/Core/Album/AlbumManager/AlbumManager.Mappings.cs`
  - `RegisterArtistFirstSong()` / `GetArtistFirstSong()` / `NormalizeArtistId()` / `SetCurrentArtistId()` / `GetCurrentArtistId()` -> `GRC2/Core/Album/AlbumManager/AlbumManager.Artists.cs`
- **BmsNoteConverter**
  - `ConvertBmsNotesToNoteCreateData()` -> `GRC2/Converters/BmsNote/BmsNoteConverter.cs`
  - `ProcessHoldEndNotes()` / `ProcessFairyEndNotes()` -> `GRC2/Converters/BmsNote/BmsNoteConverter.EndNotes.cs`
  - `CreateTypedNoteArray()` -> `GRC2/Converters/BmsNote/BmsNoteConverter.Array.cs`
  - `FilterZeroTimeNotes()` / `CheckMissingEndNotes()` / `SetLastNoteFlag()` -> `GRC2/Converters/BmsNote/BmsNoteConverter.Validation.cs`
- **노트 연결 프로세서**
  - `HoldNoteProcessor.ProcessHoldEndNotes()` -> `GRC2/Processors/HoldNote/HoldNoteProcessor.ConnectNodes.cs`
  - `FairyNoteProcessor.ProcessFairyEndNotes()` -> `GRC2/Processors/FairyNoteProcessor.ConnectNodes.cs`
  - `HoldNoteProcessor.MatchHoldNotes()` -> `GRC2/Processors/HoldNote/HoldNoteProcessor.cs`
  - `FairyNoteProcessor.MatchFairyNotes()` -> `GRC2/Processors/FairyNoteProcessor.cs`

## 상세 문서

### 코드 구조 및 기술 문서 (신규 추가)
- **[Harmony 패칭 시스템 상세 가이드](../harmony/Harmony_패칭_시스템_상세_가이드.md)**: Harmony 동적 패칭 메커니즘, Prefix/Postfix 패턴, 메서드 탐색 및 필터링 기법
- **[리플렉션 및 필드 접근 시스템](../harmony/리플렉션_및_필드_접근_시스템.md)**: ReflectionHelper, FieldAccessHelper 상세 분석, 캐싱 전략 및 성능 최적화
- **[노트 생성 및 변환 파이프라인](../bms/노트_생성_및_변환_파이프라인.md)**: BmsNoteConverter, NoteCreateDataBuilder, 생성자 탐색 및 필드 초기화 전체 프로세스
- **[BMS 파서 내부 구조 분석](../bms/BMS_파서_내부_구조_분석.md)**: BmsParser, BmsNoteDataParser, BmsTimeCalculator 파싱 알고리즘 및 데이터 모델
- **[에러 처리 및 디버깅 시스템](에러_처리_및_디버깅_시스템_legacy.md)**: ErrorLogger, NoteArrayJsonDumper, 디버깅 도구 및 로깅 패턴
- **[씬 관리 및 라이프사이클](../architecture/씬_관리_및_라이프사이클.md)**: SceneDetector, SceneHandler, 씬 전환 로직 및 초기화 프로세스
- **[커스텀 에셋 로딩 시스템](../systems/커스텀_에셋_로딩_시스템.md)**: CustomAssetManager, AssetLoader, 이미지/오디오 로딩 및 캐싱 메커니즘
- **[Enum 및 타입 시스템 관리](../harmony/Enum_및_타입_시스템_관리.md)**: EnumValueHelper, GameTypeLoader, 타입 캐싱 및 변환 로직
- **[코루틴 및 비동기 처리 패턴](../maintenance/코루틴_및_비동기_처리_패턴.md)**: Unity 코루틴 활용, BGA/BGM 동기화, 타임아웃 및 재시도 메커니즘
- **[성능 최적화 기법 종합 가이드](../maintenance/성능_최적화_기법_종합_가이드.md)**: Dictionary 캐싱, 리플렉션 최적화, 메모리 관리 베스트 프랙티스

### 모드 시스템 분석 문서
- **[아티스트 ID 기반 시스템 분석](../systems/아티스트_ID_기반_시스템_분석.md)**: 아티스트 ID별 첫 곡 정보 수집, MusicID 변경, 텍스트 교체 시스템 상세 분석
- **[커스텀 곡 주입 시스템 분석](../systems/커스텀_곡_주입_시스템_분석.md)**: 앨범 스캔, 템플릿 곡 복제, 커스텀 MusicID 생성, 곡 목록 주입 로직 상세 분석
- **[앨범 관리 시스템 분석](../systems/앨범_관리_시스템_분석.md)**: 앨범 폴더 스캔, MusicID-앨범 매핑, 아티스트 ID 관리, 원본 제목 관리 상세 분석
- **[텍스트 패치 시스템 분석](../systems/텍스트_패치_시스템_분석.md)**: Harmony 후킹, 텍스트 교체 로직, 씬별 동작 제어, 활성화/비활성화 스위치 상세 분석

### 가이드 문서
- **[홀드 노트 처리 가이드](../bms/홀드_노트_처리_가이드.md)**: 홀드 노트 매칭 및 처리 로직 상세 설명
- **[게임 종료 시간 조정 가이드](../maintenance/게임_종료_시간_조정_가이드.md)**: 4개 필드 조정 및 게임 종료 시간 관리 상세 설명
- **[BMS 파싱 및 변환 로직 가이드](../bms/BMS_파싱_및_변환_로직_가이드.md)**: BMS 파일 파싱 및 게임 노트 변환 로직 상세 설명

### 게임 코드 분석 문서
- **[게임 아키텍처 개요](../architecture/게임_아키텍처_개요.md)**: 게임 아키텍처 및 네임스페이스 구조
- **[게임 클래스 구조](../architecture/게임_클래스_구조.md)**: 핵심 클래스 및 데이터 구조 상세 설명
- **[게임 플로우 및 메서드](../architecture/게임_플로우_및_메서드.md)**: 게임 플로우 및 메서드 동작 상세 설명
- **[BGM/BGA 관리 시스템](../systems/BGM_BGA_관리_시스템.md)**: BGM/BGA 관리 시스템 상세 설명
- **[게임 종료 로직](../maintenance/게임_종료_로직.md)**: 게임 종료 로직 및 모드 상호작용 상세 설명

### 기타 문서
- **[코드 리뷰](코드_리뷰_및_비유.md)**: 전체 시스템 비유 및 설계 원칙 설명

## 최신 변경 사항

### Steam 게임 업데이트 차단 기능
- **SteamManifestLocker 클래스 추가**: Steam 매니페스트 파일을 읽기 전용으로 설정하여 게임 업데이트를 차단
  - `SceneDetector.OnInitializeMelon()`에서 자동 호출
  - `LockManifest()` 메서드로 Steam 매니페스트 파일(`appmanifest_2585040.acf`)을 읽기 전용으로 설정
  - 게임 업데이트로 인한 모드 호환성 문제를 방지하기 위한 기능
  - 파일이 이미 읽기 전용인 경우 중복 설정 방지
  - 로그 형식: `[GRC2] [Main] 스팀 업데이트 차단 시작...` / `[GRC2] [SteamManifestLock] 매니페스트 파일이 이미 읽기 전용입니다.` / `[GRC2] [Main] 스팀 업데이트 차단 완료`
  - 파일이 없는 경우 경고 메시지 출력

### 아티스트 ID 기반 MusicID 및 텍스트 교체 시스템
- **아티스트 ID별 첫 곡 정보 수집**: `MusicScrollViewHooks.InitializeAllItemByCrrentMusicDataPrefix (구현: Harmony/Hooks/MusicScrollView/MusicScrollViewHooks.ListLogging.cs)`에서 곡 목록 초기화 시 각 아티스트 ID별 첫 곡의 MusicID와 제목을 수집하여 `AlbumManager`에 등록
  - 아티스트 ID 통계 출력: 각 아티스트별 곡 수 및 첫 곡 정보 표시
  - 아티스트 ID 정규화: `AlbumManager.NormalizeArtistId() (구현: Core/Album/AlbumManager/AlbumManager.Artists.cs)`로 대소문자 무시 및 한글/영문 변환 지원
    - "르호" 또는 "Morpho" → "Morpho"
    - "Roro" 또는 "roro" → "Roro"
    - "Luxair" 또는 "룩시아" → "Luxair"
- **coOpenPreMusicStartWindow 후킹**: `GameFlowHooks.CoOpenPreMusicStartWindowPrefix (구현: Harmony/Hooks/GameFlow/GameFlowHooks.PreStartWindow.cs)`에서 곡 시작 전 윈도우 열기 시 아티스트 ID 기반 처리
  - 현재 선택된 앨범의 캐릭터(아티스트 ID) 확인 (`SongInfo.Character` 또는 `SongInfo.Artist`)
  - 해당 아티스트의 첫 곡 MusicID로 변경 (`mCurentMusicId`, `mCurrentMusicID` 필드 업데이트)
  - 원본 제목 등록: 첫 곡 제목을 원본 제목으로 등록하여 TextPatch에서 사용
  - 아티스트 ID 저장: `AlbumManager.SetCurrentArtistId() (구현: Core/Album/AlbumManager/AlbumManager.Artists.cs)`로 현재 아티스트 ID 저장
  - 아티스트 ID로 MusicID를 변경한 경우 커스텀 차트 처리(FIRST로 변경) 건너뛰기
- **TextPatch 개선**: 원본 제목을 현재 앨범의 커스텀 제목으로 교체
  - `SetTextPrefix()`에서 원본 제목 목록에 있는 텍스트를 현재 앨범의 커스텀 제목으로 교체
  - `AlbumManager.GetCurrentSongInfo().Title` 사용
- **txt 파일 캐릭터 필드 지원**: `SongInfoParser.ParseTxtFile()`에서 캐릭터 필드 파싱
  - "캐릭터 : 르호" 또는 "character = Morpho" 형식 지원
  - 캐릭터 필드가 아티스트 ID로 사용됨
  - 아티스트 ID가 없으면 `SongInfo.Artist` 사용

### GameTypeLoader 및 GameTypeSearcher 정리
- **GameTypeLoader 로직 단순화**: 불필요한 디버깅 로그 제거, 검색 로직 간소화
  - 모드에서 사용되는 타입만 로드: `NoteCreateData`, `NoteTypeId`, `NoteLaneLeftRight`, `NoteSubLaneType`, `NoteDirectionIndex`, `NoteSize`, `SlideEndFlickDirection`
  - 간단한 출력 형식: 초기화 완료 시 로드된 타입 이름과 네임스페이스만 출력
- **GameTypeSearcher 로직 단순화**: 불필요한 디버깅 검색 메서드 제거
  - 모드에서 사용되는 타입만 탐색: `cBGMBeatManager`, `cRythmGameResultSceneUpdater`
  - 간단한 출력 형식: 초기화 완료 시 로드된 타입 이름과 네임스페이스만 출력
  - 불필요한 검색 메서드 제거: `SearchVideoPlayableBehaviour`, `SearchVideoPlayerTypes`, `SearchSceneTransitionTypes` 등

### BMS 노트 주입 가드 추가
- **씬/플래그 기반 주입 제어**: `NoteArrayHooks.TryInjectBmsNotes()`에서 `CustomAssetManager.ShouldInjectCustomContent()` 사용
  - 주입 금지 씬(`SoundPlayerScene`, `MoviePlayer_MovieSelect`)이면 주입 안 함
  - 커스텀 차트 미선택이면 주입 안 함
  - 조건 불만족 시 로그: `[NoteArrayHooks] ⚠️ BMS 노트 주입 건너뜀 (메서드: ..., 씬 금지 또는 커스텀 미선택)`

### 곡 제목 기반 커스텀 차트 판단 시스템
- **coOpen 후킹 개선**: 곡 제목을 기반으로 원본/커스텀 차트를 구분
  - 곡 제목이 원본 제목 목록(`GetAllOriginalTitles()`)에 있으면 → 일반 곡
  - 곡 제목이 커스텀 차트 앨범 제목이면 → 커스텀 차트
  - 둘 다 아니면 MusicID로 확인 (기존 로직)
- **일반 곡 선택 시 원본 복원**: `coOpen`에서 일반 곡 감지 시 자동으로 원본 아트워크/BGM 복원
- **곡 목록 출력 기능**: `PrintMusicList()` 메서드로 전체 곡 목록 및 주입 상태 확인
- **TextPatch 개선**: 플레이 씬(`FairyModeScene`, `PlayMovieScene`, `RenderCutinScene`)에서만 작동하도록 제한하여 버그 방지

### 아티스트 ID 기반 MusicID 및 텍스트 교체 시스템
- **아티스트 ID별 첫 곡 정보 수집**: `MusicScrollViewHooks`에서 곡 목록 초기화 시 각 아티스트 ID별 첫 곡의 MusicID와 제목을 수집하여 `AlbumManager`에 등록
  - 아티스트 ID 통계 출력: 각 아티스트별 곡 수 및 첫 곡 정보 표시
  - 아티스트 ID 정규화: 대소문자 무시 및 한글/영문 변환 지원
    - "르호" 또는 "Morpho" → "Morpho"
    - "Roro" 또는 "roro" → "Roro"
    - "Luxair" 또는 "룩시아" → "Luxair"
- **coOpenPreMusicStartWindow 후킹**: 곡 시작 전 윈도우 열기 시 아티스트 ID 기반 처리
  - 현재 선택된 앨범의 캐릭터(아티스트 ID) 확인 (`SongInfo.Character` 또는 `SongInfo.Artist`)
  - 해당 아티스트의 첫 곡 MusicID로 변경 (`mCurentMusicId`, `mCurrentMusicID` 필드 업데이트)
  - 원본 제목 등록: 첫 곡 제목을 원본 제목으로 등록하여 TextPatch에서 사용
  - 아티스트 ID 저장: `AlbumManager.SetCurrentArtistId() (구현: Core/Album/AlbumManager/AlbumManager.Artists.cs)`로 현재 아티스트 ID 저장
- **TextPatch 개선**: 원본 제목을 현재 앨범의 커스텀 제목으로 교체
  - 원본 제목 목록에 있는 텍스트를 현재 앨범의 커스텀 제목으로 교체
  - 플레이 씬/로딩 씬/결과 씬에서만 작동
- **txt 파일 캐릭터 필드 지원**: 곡 정보 파일(`*.txt`)에서 캐릭터 필드 파싱
  - "캐릭터 : 르호" 또는 "character = Morpho" 형식 지원
  - 캐릭터 필드가 아티스트 ID로 사용됨
  - 아티스트 ID가 없으면 `SongInfo.Artist` 사용

## 라이선스

이 프로젝트는 개인 사용 및 학습 목적으로 제작되었습니다.
