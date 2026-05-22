# Harmony (모드 후킹 계층)

## 테스트/확인 환경

- 게임 이름: GUNVOLT RECORDS Cychronicle
- 게임 개발사: INTI CREATES
- 유니티 버전: 2021.3.31f1
- 게임 버전: 1.1.0

| 폴더 | 네임스페이스 | 역할 |
|------|----------------|------|
| **Hooks** | `GRC2.Harmony.Hooks` | 게임 메서드에 붙는 Prefix/Postfix **진입점** (예: `GameFlowHooks`, `MusicScrollViewHooks`, `NoteArrayHooks`). |
| **Handlers** | `GRC2.Harmony.Handlers` | Harmony가 호출하는 **패치 본문** 및 그에 딸린 헬퍼 (예: `TextPatch`, `AudioClipPatch`, `PreviewAudioManager`). |
| **Registration** | `GRC2.Harmony.Registration` | `Harmony.Patch(...)`로 타입·메서드를 **등록**하는 클래스 (예: `*Patcher`). |

`Core/Bootstrap/MusicInjector.cs` 등에서 위 타입들을 묶어 초기화합니다.

## 하위 폴더 구조

partial 파일이 늘어난 클래스는 같은 네임스페이스를 유지한 채 클래스/기능 단위 하위 폴더로 묶습니다.

| 폴더 | 내용 |
|------|------|
| `Hooks/GameFlow/` | `GameFlowHooks` partial 묶음: 시작, pre-start window, navigation, UI window, artist 처리. |
| `Hooks/MusicScrollView/` | `MusicScrollViewHooks` partial 묶음: 목록 주입, MusicSelectData 생성, custom MusicID, 아티스트 첫 곡 매핑. |
| `Hooks/NoteArray/` | `NoteArrayHooks` partial 묶음: 노트 배열 주입 및 패칭 엔트리포인트. |
| `Handlers/AudioSource/` | `AudioSourceFinder` partial 묶음: 싱글턴/프리뷰 AudioSource 탐색, 오디오 교체. |
| `Handlers/PreviewAudio/` | `PreviewAudioManager` partial 묶음: 원본 프리뷰/환경음 mute, 복원, 모니터링. |

파일의 물리 위치만 바뀌었고, 네임스페이스와 public hook 메서드 이름은 유지됩니다.

| `GRC2/` 하위 폴더 | 한 줄 |
|----------------|--------|
| **`Core/Bootstrap`** | 모드 기동 시 Harmony·주입을 **연결·초기화** (`MusicInjector`). 실제 Prefix/Postfix 구현은 아래 **`Harmony/`** 에 있습니다. |
| **`Harmony/`** | 게임 메서드 후킹: Hooks → Handlers → Registration. |
| **`Injectors/`** | BGM/BGA 등 **런타임 리소스 주입** (Harmony 훅 본문과는 다른 축). |
| **`Loaders/`** | 게임 타입 로딩 등 진입 보조. |

현재 훅 목록과 정리 이력은 [`docs/maintenance/HOOK_MAP.md`](../../docs/maintenance/HOOK_MAP.md)를 기준으로 봅니다.
