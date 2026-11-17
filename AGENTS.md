# GRC2 — 인수인계 문서 (AGENTS.md)

> 작성일 2026-11-15, 2026-11-16 갱신(업데이트 내성 추가, 3~5·7절 정리). 폴더의 코드·프로젝트 파일·기존 문서와 실제 게임 설치 폴더(Steam `.acf`, `UnityPlayer.dll`, `MelonLoader.dll`)를 읽어 정리했다.
> 이 문서 작성 시점에 인게임 재검증은 하지 않았다. **"실측"** 표시는 디스크에서 직접 읽은 값, **"미확인"**은 다음 세션에서 확인할 항목이다.

## 0. 프로젝트 개요

- **무엇인가:** GUNVOLT RECORDS Cychronicle 커스텀 차트 모드(v0.5.1). BMS 차트·BGM·BGA·아트워크·메타데이터를 주입하고, 스팀/DLC 체크(`SteamAPI.Init`, `SteamApps.BIsDlcInstalled`, `IntiCreates.Application.isDLCEnable`)를 우회. 테스트 프로젝트 `GRC2.Tests`, 심볼 도구 `tools/CsSymbolTool`.
- **종류:** 게임 모드
- **최근 수정일(파일 기준):** 2026-11-05
- **git 원격:** `origin`(GitHub magjangin/GRC2), `local`(`H:\local-repos\GRC2.git`) — 둘 다 main을 밀어 둔다
- **최근 커밋:**
  - `95799e3 Add GitHub Sponsors funding configuration`
  - `755ed42 Merge branch 'docs/refresh-2026-10-09'`
  - `d8b3e02 docs: 2026-10-09 기준으로 오래된 문서 주장 정정`
  - `05963ea feat: 커스텀 곡 크레딧(작사·작곡·편곡·채보·CD·원작) 표시 지원 및 info.txt 파서 확장`
  - `29fe985 build: 테스트 프로젝트를 솔루션에 추가, 빌드 스크립트 경로·GamePath 정리, Harmony 패치 실패 경로 개선`
- **먼저 읽을 기존 문서:**
  - [GRC 리드미\maintenance\업데이트_대응_가이드.md](GRC%20리드미/maintenance/업데이트_대응_가이드.md)
  - [GRC 리드미\README.md](GRC%20리드미/README.md)
  - [GRC 리드미\architecture\게임_아키텍처_개요.md](GRC%20리드미/architecture/게임_아키텍처_개요.md)
  - [GRC 리드미\architecture\게임_코드_분석.md](GRC%20리드미/architecture/게임_코드_분석.md)
  - [GRC 리드미\architecture\게임_클래스_구조.md](GRC%20리드미/architecture/게임_클래스_구조.md)
  - [GRC 리드미\architecture\게임_플로우_및_메서드.md](GRC%20리드미/architecture/게임_플로우_및_메서드.md)
  - [GRC 리드미\architecture\씬_관리_및_라이프사이클.md](GRC%20리드미/architecture/씬_관리_및_라이프사이클.md)
  - [GRC 리드미\archive\README_legacy.md](GRC%20리드미/archive/README_legacy.md)
  - [GRC 리드미\archive\리플렉션_및_필드_접근_시스템.md](GRC%20리드미/archive/리플렉션_및_필드_접근_시스템.md)
  - [GRC 리드미\archive\비유와_수학공식_가이드.md](GRC%20리드미/archive/비유와_수학공식_가이드.md)
  - [GRC 리드미\archive\성능_분석_및_최적화_권장사항.md](GRC%20리드미/archive/성능_분석_및_최적화_권장사항.md)

## 1. 게임 버전 및 고정 여부

| 게임 | 설치 경로 | AppID | Steam Build ID (실측) | AutoUpdateBehavior | .acf 읽기 전용 |
| --- | --- | --- | --- | --- | --- |
| GUNVOLT RECORDS Cychronicle | `H:\steam\steamapps\common\GUNVOLT RECORDS Cychronicle` | 2585040 | **13530823** | 0 | 아니오 |

- **고정 여부:** 고정 안 됨 — `AutoUpdateBehavior`가 `0`(항상 자동 업데이트)이고 `.acf`가 읽기 전용이 아니면 스팀이 게임을 업데이트할 수 있다. 업데이트되면 위 Build ID와 달라지므로 먼저 Build ID를 비교할 것.
- **DepotDownloader 다운그레이드:** 이 폴더에서 다운그레이드 기록은 찾지 못했다(**미확인**). 버전을 고정하려면 DepotDownloader로 위 Build ID의 매니페스트를 받아 두고, `.acf`를 읽기 전용으로 바꿔 업데이트를 막는다.

## 2. 런타임 백엔드 및 디컴파일 경로

- **GUNVOLT RECORDS Cychronicle:** Unity / **Mono** / Unity `2021.3.31f1` / x64 (실측)
  - 설치된 MelonLoader: `0.7.3.0 (net6)` (실측)
  - 참조 어셈블리: `H:\steam\steamapps\common\GUNVOLT RECORDS Cychronicle\GUNVOLT_RECORDS_Cychronicle_Data\Managed\`
- 모드/앱 프로젝트 `GRC2\GRC2.csproj` 타깃 프레임워크: `net472` (SDK 스타일)
- 모드/앱 프로젝트 `GRC2.Tests\GRC2.Tests.csproj` 타깃 프레임워크: `net472` (SDK 스타일)
- **이 폴더 안의 디컴파일/덤프 자료:** `Decompiled/`, `Decompiled-outline/`
- 보조 프로젝트(디컴파일본·덤퍼): `tools\CsSymbolTool\CsSymbolTool.csproj`

## 3. 설계 의도와 우회 사유 (Rationale)

- **스팀/DLC 우회는 오프라인 보존 목적이다.** `SteamAPI.Init`, `SteamApps.BIsDlcInstalled`, `cDlcDirector.IsNotYetPurchased`, `IntiCreates.Application.isDLCEnable`을 건드린다(`GRC2/Harmony/SteamApiHijacker.cs`). 다만 **`Init` 결과를 true로 강제하지 않는다**: 강제하면 `cDlcDirector.Initialize()`가 `SteamApps.GetDLCCount()`에서 예외를 던져 `sAddressableDirector`가 영원히 대기한다(알려진_문제 A3). Init 결과를 기록해 두고 성공했을 때만 `RunCallbacks`/`Shutdown`을 원본으로 보낸다.
- **세이브 차단(`BlockSave`)은 결과 씬으로만 좁혀져 있다.** 부팅 중 저장까지 막으면 세이브 없는 부팅이 무한 반복된다(A1). 결과 씬에서는 곡 기록 4개 필드를 스냅샷으로 되돌려 AutoPlay 기록이 세이브에 남지 않게 한다(`RecordBlockPatch`).
- **노트 변환은 원본 로더의 후처리를 전부 재현하지 않는다**(H15, 의도적으로 미수정). 홀드/페어리 노트는 끝 노트 짝이 없으면 주입하지 않는다 — 게임의 `cFairyModeNotesManager.createNote`가 null 검사 없이 접근해 `createAllNote`가 NRE로 곡 로딩을 통째로 깨기 때문이다(`BmsNoteConverter.cs` 주석).
- **BGM은 48kHz만 가정한다.** 노트 시계를 BGM 샘플레이트에 맞추지 않고, 아니면 오류 로그만 남긴다(B1).
- **패치는 `PatchAll` 한 번 + 실패 시 클래스별 재시도**(`SceneDetector.InitializeHarmony`). Harmony는 첫 실패에서 멈춰 뒤 클래스를 빠뜨리기 때문이다(H4).
- **필드 이름 접근은 `GameCompatibility.FieldContracts`에 모은다.** 게임 업데이트 때 무엇이 깨졌는지 시작 로그(`[호환성]`)와 `GameCompatibilityTests`로 바로 알기 위해서다(2026-11-16). 새 `FieldRefAccess<..>("이름")`를 쓰면 이 표에도 한 줄 넣어야 테스트가 통과한다.
- 훅 소유 파일·제거 위험의 정본은 [HOOK_MAP.md](GRC%20리드미/maintenance/HOOK_MAP.md), 문제별 상세는 [알려진_문제.md](GRC%20리드미/maintenance/알려진_문제.md).

## 4. 핵심 훅 시그니처와 주입 파이프라인

- **Melon 특성:** `[assembly: MelonInfo(typeof(GRC2.Core.SceneDetector), "GUNVOLT RECORDS Cychronicle", "0.5.1", "화영왕")]`, `MelonGame("INTI CREATES", "GUNVOLT RECORDS Cychronicle")`
- **시작 순서(`SceneDetector.OnInitializeMelon`):** Harmony 패치 적용 → 게임 폴더 계산 → `GameCompatibility.LogStartupReport` → `CustomKeySettings.Initialize` → 앨범 스캔 → 곡 정보 → 아트워크 → BMS 파싱 → `NoteArrayHooks.UpdateBmsNotes`.
- **노트 주입:** `cFairyModeNotesManager.createAllNote` 훅(`NoteArrayHooks`)이 `mFairyNoteCreateDataArray`를 `BmsNoteConverter` 결과로 교체한다. 곡 선택 씬에서는 주입하지 않는다(H1).
- **BGM/BGA:** `BgmBgaInjector`가 현재 앨범 경로를 매 틱 읽어 주입한다. 커스텀 곡 종료는 `BgmGameEndMonitor`가 감시한다.
- **곡 선택/결과 화면:** `cMusicSelectScrollView.initializeMusicDataByDefault`, `cMusicSelectSceneUIUpdater.*`, `cRythmGameResultSceneUpdater.coUpdateResultAnim` 등을 훅한다. 전체 목록은 HOOK_MAP.md.
- **Harmony 대상 전수:** `[HarmonyPatch]` 소스를 훑거나 `GameCompatibilityTests.EveryPatchTarget_ResolvesInReferencedGameAssembly`가 해석하는 대상을 본다(이 문서에 복사해 두면 금방 낡는다).
- **씬 이름 주의:** 곡 선택 씬의 실제 이름은 `MusicSelectScene_Hasegawa`다. 정확 일치로 비교하면 분기가 한 번도 실행되지 않는다(`StartsWith("MusicSelectScene")` 사용).

## 5. 시도했다가 실패한 것들 (Dead Ends)

- **`SteamAPI.Init` 실패를 true로 바꾸기** → 부팅 영구 대기(A3). 하지 말 것.
- **`BlockSave=1`로 모든 저장 차단** → 세이브 없는 부팅이 무한 반복되고 옵션도 저장되지 않음(A1/A2).
- **Space/Esc 일시정지에서 `setPauseButtonPusable(true)` 강제** → 로딩·클리어 연출 중에도 메뉴가 열림(H13). 지금은 원본 `requestPause()` 판단에 맡긴다.
- **결과 화면 폴백으로 난이도 4개를 이어 붙여 `mDifficultyText`에 쓰기** → 덮어쓰기 버그(`ResultSceneUpdaterPatch.cs`). 폴백을 쓰지 않는다.
- **`MusicSelectScene` 정확 일치 비교** → 실제 씬 이름이 달라 `ResetPlaySceneState()`가 호출되지 않고, 곡 선택 화면에 직전 커스텀 곡의 제목·아트워크가 남음.
- **프레임 수 기준 BGM/BGA 로딩 타임아웃** → 144Hz에서 2.4배 짧아짐. 실제 시간(초) 기준으로 바꿈(F).
- **Harmony 패치를 xUnit 테스트 호스트에서 실제로 적용하기** → Unity ECall 때문에 `SecurityException: ECall 메서드는 시스템 모듈에 패키지되어 있어야 합니다`. 대상 해석만 테스트한다.
- **`Steam 업데이트 차단`(`SteamManifestLocker`)** → 2026-06-06 커밋 `a1bff38`에서 삭제됨. 되살릴지는 사용자가 정할 일(A5).
- **리플렉션 기반 필드 접근 시스템** → 현재 코드와 맞지 않아 `GRC 리드미/archive/`로 옮김. 지금은 컴파일 타임 타입 참조 + `AccessTools.FieldRefAccess`.
- **`GRC2.Tests`를 솔루션에 넣지 않기** → `test_debug.bat`이 테스트 0개로 "성공"했다(F). 지금은 솔루션에 포함.

## 6. 빌드 명령 및 배포 경로

- 솔루션: `GRC2.sln`
- `GRC2\GRC2.csproj`:
  ```powershell
  dotnet build "GRC2\GRC2.csproj" -c Release
  ```
  - 게임 경로 속성: `H:\steam\steamapps\common\GUNVOLT RECORDS Cychronicle` (`-p:` 로 덮어쓸 수 있으면 다른 설치 경로로 빌드 가능)
- `GRC2.Tests\GRC2.Tests.csproj`:
  ```powershell
  dotnet build "GRC2.Tests\GRC2.Tests.csproj" -c Release
  ```
- 빌드/배포 스크립트: `build_debug.bat`, `build_release.bat`, `test_debug.bat`
- 배포는 스크립트가 처리한다: `build_debug.bat`, `build_release.bat` (내용에 Mods 경로 복사 포함)
- 배포 위치: `H:\steam\steamapps\common\GUNVOLT RECORDS Cychronicle\Mods\`

## 7. 다음 작업자에게

- 작업 전 1절의 Build ID와 현재 설치본 `.acf`의 `buildid`를 비교한다. 다르면 **[업데이트_대응_가이드.md](GRC%20리드미/maintenance/업데이트_대응_가이드.md)** 순서를 따른다(테스트 → 훅 시그니처 확인 → 기준 빌드 갱신).
- `dotnet test GRC2.Tests\GRC2.Tests.csproj`가 117개 모두 통과해야 한다(2026-11-16 기준). 게임 설치본이 기준 빌드와 다르면 `GameCompatibilityTests`가 먼저 실패한다.
- **인게임 검증은 아직 안 됐다.** 2026-10-04~10-08의 수정들과 2026-11-16의 호환성 점검은 빌드·단위 테스트로만 확인했다. 게임에서 곡 선택 → 플레이 → 결과 → 복귀를 돌려 보고, 로그의 `[호환성]` 줄이 "점검 통과"인지 확인한 뒤 이 문서의 "미확인"을 지운다.
- `fix/bms-parser-conversion`(BPM 채널·홀드 짝짓기·원본 로더 후처리 이식, H15)은 2026-11-16에 main에 병합했다. **인게임 확인 전**이므로 노트 타이밍·홀드 이상이 보이면 이 병합(`git log --merges`)부터 의심한다.
