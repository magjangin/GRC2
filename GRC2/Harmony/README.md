# Harmony (모드 후킹 계층)

## 테스트/확인 환경

- 게임 이름: GUNVOLT RECORDS Cychronicle
- 게임 개발사: INTI CREATES
- 유니티 버전: 2021.3.31f1
- 게임 버전: 1.1.0

이 폴더의 모든 파일은 네임스페이스 `GRC2.Harmony` 하나를 씁니다.
`GRC2.csproj`가 게임의 `Assembly-CSharp.dll`을 직접 참조합니다.
`Core/SceneDetector.cs`의 `InitializeHarmony()`가 모드 초기화 때 `PatchAll()`을
한 번 호출해 모드 어셈블리에 선언된 모든 Harmony 특성을 자동 등록합니다.

## 파일 구성

`Harmony/` 아래에는 게임 메서드에 붙는 `[HarmonyPatch]` 클래스와, 그 패치가
쓰는 보조 클래스(`CustomBgmPlayer`, `PreviewAudioManager`)가 함께 있습니다.

2026-08-09 이전에는 이 폴더가 `Hooks/`와 `Handlers/` 둘로 나뉘어 있었지만
두 폴더를 가르는 규칙이 코드상 존재하지 않았고(양쪽 모두 `[HarmonyPatch]`
클래스를 담고 있었음), 네임스페이스도 한 단계 더 깊어지기만 해서 하나로
합쳤습니다. 같은 정리에서 Steam/DLC 패치 11개를 담은
`SteamApiHijacker.cs`도 `Helpers/`에서 이곳으로 옮겼습니다.

2026-07-26부터 별도 `Registration/` 계층, 지연 등록 코루틴, 수동
`Harmony.Patch(...)` 호출은 사용하지 않습니다.

## 이 폴더 밖에 있는 패치

`Injectors/BgmGameEndMonitor.cs`의 `BgmGameEndMonitor`만 예외로
`cRythmGameManager.coMonitorGameEnd` 패치를 직접 들고 있습니다. 이 패치는
BGM 주입이 계산한 종료 시각(`BgmFinishTimeManager`)과 같은 파일에서 상태를
주고받기 때문에 떼어내면 오히려 한 기능이 두 파일로 쪼개집니다.

| `GRC2/` 하위 폴더 | 한 줄 |
|-------------------|-------|
| **`Core/`** | 모드 기동(`SceneDetector`), 앨범/에셋/설정/HUD 상태 |
| **`Harmony/`** | 게임 메서드 후킹 대상과 Prefix/Postfix 구현 |
| **`Injectors/`** | BGM/BGA 등 런타임 리소스 주입 |
| **`Parsers/` `Builders/` `Converters/` `Processors/`** | BMS 파싱 → 게임 `NoteCreateData` 변환 파이프라인 |
| **`Helpers/`** | enum 매핑, 예외 로깅, 샘플/초 변환 |

현재 훅 목록과 정리 이력은
[`GRC 리드미/maintenance/HOOK_MAP.md`](../../GRC%20리드미/maintenance/HOOK_MAP.md)를
기준으로 봅니다.
