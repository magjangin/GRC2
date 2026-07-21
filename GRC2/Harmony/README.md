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
| **Registration** | `GRC2.Harmony.Registration` | `Harmony.Patch(...)`로 타입·메서드를 **등록**하는 클래스. 여섯 개의 `*Patcher` 클래스가 `Registration/Patchers.cs` 한 파일에 모여 있습니다. |

`Core/Bootstrap/MusicInjector.cs` 등에서 위 타입들을 묶어 초기화합니다.

## 파일 구성

2026-07-21 정리로 partial 분할을 모두 병합해 **클래스당 파일 하나**를 유지합니다.
`Hooks/`에는 `GameFlowHooks.cs`, `MusicScrollViewHooks.cs`, `NoteArrayHooks.cs`가,
`Handlers/`에는 패치 본문 클래스들(`AudioClipPatch`, `TextPatch`, `PreviewAudioManager` 등)이 파일 단위로 있습니다.
네임스페이스와 public hook 메서드 이름은 분할 시절과 동일합니다.

| `GRC2/` 하위 폴더 | 한 줄 |
|----------------|--------|
| **`Core/Bootstrap`** | 모드 기동 시 Harmony·주입을 **연결·초기화** (`MusicInjector`). 실제 Prefix/Postfix 구현은 아래 **`Harmony/`** 에 있습니다. |
| **`Harmony/`** | 게임 메서드 후킹: Hooks → Handlers → Registration. |
| **`Injectors/`** | BGM/BGA 등 **런타임 리소스 주입** (Harmony 훅 본문과는 다른 축). |
| **`Loaders/`** | 게임 타입 로딩 등 진입 보조. |

현재 훅 목록과 정리 이력은 [`docs/maintenance/HOOK_MAP.md`](../../docs/maintenance/HOOK_MAP.md)를 기준으로 봅니다.
