# GRC2 — 인수인계 문서 (AGENTS.md)

> 작성일 2026-11-15, 2026-11-16 갱신(업데이트 내성 추가, 3~5·7절 정리), 2026-11-18 갱신(폰트 fallback, 곡 상한·고정 MusicID, 로그 감소, hwa 재스캔). 폴더의 코드·프로젝트 파일·기존 문서와 실제 게임 설치 폴더(Steam `.acf`, `UnityPlayer.dll`, `MelonLoader.dll`)를 읽어 정리했다.
> 2026-11-17~18 변경은 곡 선택 화면까지 인게임으로 확인했다(7절에 범위 명시). **"실측"** 표시는 디스크에서 직접 읽은 값, **"미확인"**은 다음 세션에서 확인할 항목이다.

## 0. 프로젝트 개요

- **무엇인가:** GUNVOLT RECORDS Cychronicle 커스텀 차트 모드(v0.6.0, GitHub 릴리즈 v0.6.0). BMS 차트·BGM·BGA·아트워크·메타데이터를 주입하고, 스팀/DLC 체크(`SteamAPI.Init`, `SteamApps.BIsDlcInstalled`, `IntiCreates.Application.isDLCEnable`)를 우회. 테스트 프로젝트 `GRC2.Tests`, 심볼 도구 `tools/CsSymbolTool`.
- **종류:** 게임 모드
- **최근 수정일(파일 기준):** 2026-11-18
- **git 원격:** `origin`(GitHub magjangin/GRC2), `local`(`H:\local-repos\GRC2.git`) — 둘 다 main을 밀어 둔다
- **최근 커밋:** `git log --oneline -8`로 본다(여기 복사해 두면 금방 낡는다). 2026-11-18 작업은 `feat/font-fallback-and-cleanup` 브랜치를 main에 병합한 것이다.
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
- **게임 폰트에 없는 글자는 TMP 전역 fallback으로 그린다**(`Core/FontFallback.cs`, 2026-11-17). 게임은 언어별 Static TMP 아틀라스만 써서 커스텀 곡의 간체 한자 등이 □로 나왔다. 첫 씬 로드 때 `hwa/fonts/` → 윈도우 폰트(msyh·msjh·YuGothM·meiryo·malgun·seguisym) 순으로 Dynamic `TMP_FontAsset`을 만들어 `TMP_Settings.fallbackFontAssets`에 붙인다. 게임 폰트에 있는 글자는 바꾸지 않으므로 Harmony 훅이 필요 없고, 언어를 바꿔도 유지된다.
- **커스텀 MusicID는 54~7999(이름 붙은 enum 값 제외), 폴더별 고정**(`Core/CustomMusicIdRegistry.cs`, `Helpers/CustomMusicIdAllocator.cs`, 2026-11-18). 예전 상한 511(458곡)은 세이브 `playerMusicData`가 512칸이라서였다. 세이브가 JsonUtility JSON이라 배열을 늘려도 그대로 저장·로드되고 게임 루프는 512까지만 돌거나 `getIsUsableMusicID`로 걸러서, 곡 목록을 만들 때 `EnsureSaveSlots`로 늘린다. 8000 이상은 튜토리얼·미리보기(`UNIQUE_TOP`) 전용이라 쓰지 않는다. 폴더 → ID 맵은 `UserData/GRC2/musicid_map.txt`(첫 배정은 예전 이름순 54부터와 같아 기존 기록 유지).
- **로그는 `ModLog`로 나눈다**(2026-11-18). 곡·노트·매핑마다 찍히는 것은 `ModLog.Verbose`(config `VerboseLog=1`일 때만), 같은 상태를 반복해 알리는 것은 `ModLog.InfoOnChange`. 곡 상한을 올리면 곡 수에 비례하는 로그가 수천 줄이 되기 때문이다. 경고·오류는 그대로 `MelonLogger`.
- **시작할 때는 현재 곡 BMS만 파싱**하고, 곡 선택 씬 진입 때 `hwa` 지문이 바뀌었으면 다시 스캔한다(2026-11-18). 고정 MusicID가 있어서 재스캔해도 기록이 옮겨 가지 않는다.- 훅 소유 파일·제거 위험의 정본은 [HOOK_MAP.md](GRC%20리드미/maintenance/HOOK_MAP.md), 문제별 상세는 [알려진_문제.md](GRC%20리드미/maintenance/알려진_문제.md).

## 4. 핵심 훅 시그니처와 주입 파이프라인

- **Melon 특성:** `[assembly: MelonInfo(typeof(GRC2.Core.SceneDetector), "GUNVOLT RECORDS Cychronicle", "0.6.0", "화영왕")]`, `MelonGame("INTI CREATES", "GUNVOLT RECORDS Cychronicle")`
- **시작 순서(`SceneDetector.OnInitializeMelon`):** Harmony 패치 적용 → 게임 폴더 계산 → `GameCompatibility.LogStartupReport` → `CustomKeySettings.Initialize`(VerboseLog 결정) → `CustomMusicIdRegistry.Initialize` → 앨범 스캔(+hwa 지문) → 곡 정보 → 아트워크 → 현재 곡 BMS만 파싱 → `NoteArrayHooks.UpdateBmsNotes`. 끝에 단계별 ms를 한 줄로 남긴다.
- **씬 로드마다:** `FontFallback.EnsureInstalled`(첫 회에 폰트 생성). 곡 선택 씬이면 `RescanAlbumsIfChanged` → (약 200ms 뒤) 곡 목록 postfix가 ID 배정·세이브 칸 확장·주입.
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
- **`Font.CreateDynamicFontFromOSFont`로 TMP 폰트 만들기** → 쓰지 않음. TMP의 `FontEngine.LoadFontFace(Font)`는 폰트 데이터가 필요하므로 `new Font(전체경로)`(내부적으로 `Internal_CreateFontFromPath`)를 쓴다. 이 방식은 `.ttc`도 게임에서 로드되는 것을 확인했다(2026-11-17).
- **`吉`이 한글처럼 보여 폰트 매핑 오류를 의심** → 아니었다. 게임 `PCDMainfont`의 둥근 글씨체 `吉`이 작게 보인 것(곡 선택 진입 시 `[폰트]` 진단 로그로 확인). 게임 폰트가 가진 글자는 fallback으로 넘어가지 않는다.
- **커스텀 MusicID 맵 파일을 `hwa/`에 두기** → 안 됨. `hwa` 루트도 앨범("root")으로 스캔되고 txt는 곡 정보로 읽혀 가짜 곡이 생긴다. 같은 이유로 `hwa/fonts`는 앨범 스캔에서 뺀다.
- **맵 파일을 `savecustomkey/`에 두기** → 사용자가 원치 않음(2026-11-18). `savecustomkey`에는 사용자가 고치는 `config.txt`만 둔다. 모드가 만드는 데이터는 MelonLoader 관례대로 `UserData/GRC2/`에 둔다(`MelonEnvironment.UserDataDirectory`). 잠깐 쓰던 `savecustomkey/musicid_map.txt`는 시작할 때 자동으로 옮긴다.
- **커스텀 ID에 512나 8000 이상 쓰기** → 512는 `SAVEABLE_ID_END`라는 이름 붙은 값(`ToString`·`Enum.TryParse` 경로가 이름으로 바뀜), 8000~8999는 튜토리얼 판정(`id >= UNIQUE_TOP`). 배정기는 이름 붙은 enum 값을 전부 건너뛴다.- **리플렉션 기반 필드 접근 시스템** → 현재 코드와 맞지 않아 `GRC 리드미/archive/`로 옮김. 지금은 컴파일 타임 타입 참조 + `AccessTools.FieldRefAccess`.
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
- `dotnet test GRC2.Tests\GRC2.Tests.csproj`가 134개 모두 통과해야 한다(2026-11-18 기준).
- 2026-11-18 인게임 확인: 초기화 146ms 로그, 기존 5곡 ID 54~58 유지, 임시 테스트 앨범 ID 600 → `세이브 곡 칸을 512 → 601개로 늘렸습니다`·곡 선택에서 `MusicID: 600`, "주입 완료" 로그 1회. **미확인:** ID 512 이상 곡을 끝까지 플레이해 결과 화면·세이브까지 가는 것, 게임을 켠 채 곡을 추가했을 때의 재스캔(단위 테스트만). 실제 세이브 배열은 이 확인으로 601칸이 됐다(무해, 근거는 3절 MusicID 항목).- 폰트 fallback(2026-11-17)은 **곡 선택 화면에서만** 인게임 확인했다(`麦`·`盖` 정상 표시, 로그 `[폰트] fallback 폰트 5/5개 준비`). 로딩·플레이·결과 화면의 제목 표시는 같은 전역 목록을 쓰지만 아직 직접 보지 않았다. 게임 설치본이 기준 빌드와 다르면 `GameCompatibilityTests`가 먼저 실패한다.
- **인게임 검증은 아직 안 됐다.** 2026-10-04~10-08의 수정들과 2026-11-16의 호환성 점검은 빌드·단위 테스트로만 확인했다. 게임에서 곡 선택 → 플레이 → 결과 → 복귀를 돌려 보고, 로그의 `[호환성]` 줄이 "점검 통과"인지 확인한 뒤 이 문서의 "미확인"을 지운다.
- `fix/bms-parser-conversion`(BPM 채널·홀드 짝짓기·원본 로더 후처리 이식, H15)은 2026-11-16에 main에 병합했다. **인게임 확인 전**이므로 노트 타이밍·홀드 이상이 보이면 이 병합(`git log --merges`)부터 의심한다.
