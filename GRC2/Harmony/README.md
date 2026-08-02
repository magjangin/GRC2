# Harmony (모드 후킹 계층)

## 테스트/확인 환경

- 게임 이름: GUNVOLT RECORDS Cychronicle
- 게임 개발사: INTI CREATES
- 유니티 버전: 2021.3.31f1
- 게임 버전: 1.1.0

| 폴더 | 네임스페이스 | 역할 |
|------|--------------|------|
| **Hooks** | `GRC2.Harmony.Hooks` | 게임 메서드에 붙는 `[HarmonyPatch]` Prefix/Postfix 진입점 |
| **Handlers** | `GRC2.Harmony.Handlers` | `[HarmonyPatch]` 패치 본문과 관련 헬퍼 |

`GRC2.csproj`가 게임의 `Assembly-CSharp.dll`을 직접 참조합니다.
`Core/Scene/SceneDetector.cs`의 `InitializeHarmony()`가 모드 초기화 때 `PatchAll()`을
한 번 호출해 모드 어셈블리에 선언된 모든 Harmony 특성을 자동 등록합니다.

## 파일 구성

`Hooks/`에는 `GameFlowHooks.cs`, `MusicScrollViewHooks.cs`,
`NoteArrayHooks.cs`가 있습니다. `Handlers/`에는 `AudioClipPatch`,
`TextPatch`, `ResultSceneUpdaterPatch` 등의 패치 본문과 보조 클래스가
있습니다.

2026-07-26부터 별도 `Registration/` 계층, 지연 등록 코루틴, 수동
`Harmony.Patch(...)` 호출은 사용하지 않습니다. BGM 종료와 Steam/DLC
패치는 각 기능 폴더의 `[HarmonyPatch]` 클래스가 같은 `PatchAll()` 호출로
등록합니다.

| `GRC2/` 하위 폴더 | 한 줄 |
|-------------------|-------|
| **`Core/Bootstrap`** | 모드 기동 시 `PatchAll()`로 Harmony 특성을 자동 등록 |
| **`Harmony/`** | 게임 메서드 후킹 대상과 Prefix/Postfix 구현 |
| **`Injectors/`** | BGM/BGA 등 런타임 리소스 주입 |
| **`Loaders/`** | 직접 참조한 게임 노트 타입 매핑 |

현재 훅 목록과 정리 이력은
[`GRC 리드미/maintenance/HOOK_MAP.md`](../../GRC%20리드미/maintenance/HOOK_MAP.md)를
기준으로 봅니다.
