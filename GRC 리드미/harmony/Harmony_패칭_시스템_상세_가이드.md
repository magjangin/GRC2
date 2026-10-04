# Harmony 패칭 시스템 상세 가이드

## 개요

GRC2는 게임의 `Assembly-CSharp.dll`을 컴파일 타임에 직접 참조하고,
Harmony 특성으로 패치 대상을 선언합니다. 모드 시작 시
`SceneDetector.InitializeHarmony()`가 `PatchAll()`을 한 번 호출하며, 이후 별도
Patcher 초기화나 지연 등록 코루틴은 사용하지 않습니다.

## 프로젝트 참조

`GRC2/GRC2.csproj`의 `GameManaged` 경로를 기준으로 다음 어셈블리를
참조합니다.

- `Assembly-CSharp.dll`: `IntiCreates.*` 게임 타입
- `com.rlabrecque.steamworks.net.dll`: Steamworks 패치 타입
- `UnityEngine.*.dll`, `Unity.*.dll`: Unity와 TextMeshPro 타입
- `0Harmony.dll`: Harmony 특성과 런타임 패처

게임 DLL 두 개에는 `<Private>false</Private>`를 지정합니다. 이 DLL들은
게임 설치본이 소유하므로 모드 배포물에 복사하는 대상이 아닙니다.

## 초기화 흐름

```text
SceneDetector.OnInitializeMelon()
  -> SceneDetector.InitializeHarmony()
     -> new Harmony("GRC2.MusicInjector")
     -> PatchAll(typeof(SceneDetector).Assembly)
  -> hwa 폴더 준비, CustomKeySettings.Initialize (savecustomkey/config.txt)
  -> 앨범과 BMS 데이터 로드
  -> NoteArrayHooks.UpdateBmsNotes(...)
```

`Assembly-CSharp.dll`은 MelonLoader가 모드를 초기화하기 전에 게임
프로세스에 로드되어 있습니다. 따라서 이전의 1초 대기 코루틴과
`Assembly.LoadFrom()` 경로 탐색은 필요하지 않습니다.

## 패치 선언 방식

대상이 하나인 클래스는 클래스에 직접 특성을 붙입니다.

```csharp
[HarmonyPatch(typeof(cMusicSelectArtWork), "requestSetArtworkSprite")]
public static class ArtWorkPatch
{
    [HarmonyPrefix]
    public static void RequestSetArtworkSpritePrefix(ref Sprite useSprite)
    {
        // 패치 본문
    }
}
```

한 기능 클래스가 여러 게임 메서드를 다룰 때는 대상별 중첩 패치
클래스를 두고 기존 처리 메서드로 위임합니다.

```csharp
[HarmonyPatch(typeof(cMusicSelectSceneUIUpdater), "backToPreScreen")]
private static class BackToPreScreenPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        BackToPreScreenPrefix();
    }
}
```

이 구조는 대상 하나와 Prefix/Postfix 하나의 관계를 명시하며,
`PatchAll()`이 자동으로 검색할 수 있습니다.

## 현재 패치 그룹

| 그룹 | 소유 파일 | 주요 대상 |
|------|-----------|-----------|
| 곡 목록 | `MusicScrollViewHooks.cs` | `cMusicSelectScrollView.initializeMusicDataByDefault` |
| 곡 선택/시작 창 | `AudioClipPatch.cs`, `GameFlowHooks.cs` | `cMusicSelectSceneUIUpdater`, `cMusicSelectPreMusicStartWindowManager`, `soRythmGameMusicDataMap.getIsUsableMusicID` |
| 노트 배열 | `NoteArrayHooks.cs` | `cFairyModeNotesManager.createAllNote` |
| 커버/텍스트 | `ArtWorkPatch.cs`, `TextPatch.cs` | `cMusicSelectArtWork`, Unity UI/TMP text setter |
| 결과 화면 | `ResultSceneUpdaterPatch.cs` | `cRythmGameResultSceneUpdater.initializePreFade` |
| 게임 종료 | `Injectors/BgmGameEndMonitor.cs` | `cRythmGameManager.coMonitorGameEnd` |
| BGA 크로스페이드 | `BgaCrossfadePatch.cs` | `cPlayMovieSceneManager.requestPlay` |
| 오토플레이/판정/기록 차단 | `AutoPlayPatch.cs`, `JudgePerfectPatch.cs`, `RecordBlockPatch.cs` | `cFairyModeNotesManager`, `cNotecWorkBase.onJudgeMent`, `cRythmGameResultSceneUpdater`, `sSaveDataDirector.requestGameDataSaveToFile` |
| 판정바/연출 | `JudgmentBarPatch.cs`, `NoteSwayPatch.cs`, `NoteSpeedChaosPatch.cs` | `cNotecWorkBase.onJudgeMent`/`simulate`/`getNoteSpeed`, `createAllNote` |
| Steam/DLC | `SteamApiHijacker.cs` | Steamworks API와 게임 DLC 검사 |

같은 메서드를 여러 클래스가 패치합니다(`createAllNote` 3곳, `onJudgeMent`·`initializePreFade` 각 2곳).
패치 클래스 사이에 `HarmonyPriority`/`HarmonyBefore`를 두지 않았으므로, 서로 실행 순서에 의존하지 않게 작성해야 합니다.

전체 대상 목록은
[`maintenance/HOOK_MAP.md`](../maintenance/HOOK_MAP.md)에서 관리합니다.

## 노트 배열 패치 주의점

노트 주입 대상은
`IntiCreates.cFairyModeNotesManager.createAllNote` 하나입니다.
`loadFairyNoteDatasJsonToArray`는 `cFairyModeNotesManager`의 메서드가 아니라
`FairyNoteEditorLoader`의 정적 메서드이므로 해당 이름을 manager 타입에서
찾아 패치하면 안 됩니다. 변환된 배열은 `createAllNote` Prefix에서
`mFairyNoteCreateDataArray`에 넣습니다.

## 새 패치 추가 절차

1. `Assembly-CSharp.dll` 또는 Steamworks DLL에서 대상 타입과 메서드
   서명을 확인합니다.
2. 대상별 `[HarmonyPatch(typeof(...), "methodName")]` 클래스를 만듭니다.
3. 패치 메서드에 `[HarmonyPrefix]` 또는 `[HarmonyPostfix]`를 붙입니다.
4. `SceneDetector.InitializeHarmony()`에 별도 등록 호출을 추가하지 않습니다.
5. `HOOK_MAP.md`에 소유 파일, 목적, 제거 위험을 기록합니다.
6. Debug 빌드와 테스트를 실행합니다.

```powershell
dotnet build GRC2.sln --no-restore --configuration Debug
dotnet test GRC2.Tests\GRC2.Tests.csproj --no-restore --configuration Debug
```

`GRC2.sln`에는 테스트 프로젝트가 없으므로 테스트는 반드시 `GRC2.Tests.csproj`를 지정해 실행합니다.
새 `.cs` 파일은 `GRC2.csproj`에 `<Compile Include>`도 추가해야 합니다(`EnableDefaultCompileItems=false`).

게임 업데이트로 타입이나 메서드가 바뀌면 컴파일 오류 또는 시작 시
`PatchAll()` 오류로 드러납니다. `SceneDetector.InitializeHarmony()`는 예외
메시지와 스택 트레이스를 MelonLoader 로그에 남깁니다.

주의: `PatchAll()`은 패치 클래스를 순서대로 처리하다 실패하면 거기서 멈추고, 이 모드는 실패해도 `Msg` 수준 로그만
남기고 초기화를 계속합니다. 게임 업데이트로 대상 하나가 사라지면 그 뒤 클래스의 패치가 빠진 채 모드가 반쯤만 동작할 수
있습니다. 게임 업데이트 뒤에는 로그에서 `Harmony 패치 적용 실패`가 있는지 가장 먼저 확인하세요
([알려진_문제.md](../maintenance/알려진_문제.md) H4).
