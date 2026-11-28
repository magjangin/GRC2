# GRC2 — 인수인계 문서 (AGENTS.md)

> 작성일 2026-11-15, 2026-11-16 갱신(업데이트 내성 추가, 3~5·7절 정리), 2026-11-18 갱신(폰트 fallback, 곡 상한 확대, 로그 감소, hwa 재스캔, 긴 곡 제목 스크롤, 외부 MusicID 맵 파일/설정 완전 제거 및 메모리 배정, 세이브 슬롯 하이스코어·클리어 뱃지 연결, 아트워크 수정 시각 캐시 무효화, 게임 상수 계약 추가), 2026-11-19 갱신(전수 리팩토링·병목 개선: SongInfoParser 컴파일 정규식 캐싱, HoldNoteProcessor 값 튜플 키, AlbumManager 아티스트 조회 O(1)화, BmsSummaryPrinter 단일 패스 집계, BgaInjector/BgaBgmSyncManager/BmsNoteConverter/SceneDetector 장문 메서드 분할, TextMarquee 코너 버퍼 재사용, 재스캔 시 BMS 캐시 초기화, TMP_Settings fallback 필드 테스트 추가), 2026-11-20 갱신(상수 계약을 런타임 이름 조회로 바로잡고 노트 enum까지 확대, MusicID 범위 런타임화, 원본 곡 추가 경고, `tools/check_game_version.ps1`), 2026-11-23 갱신(Harmony 이중 패치 수정 `HarmonyDontPatchAll`, 게임 필드 접근 `LazyFieldRef` 격리, BGA 동기화 재탐색 간격, `CHANGELOG.md`·`tools/release.ps1`, v0.7.0), 2026-11-23 2차 갱신(게임 PC 전체 감사: 상세 로그 보간 처리기·씬 이름 캐시·폰트 진단 축소·훅 실패 점검 반영·try 없는 훅의 `IsAvailable` 확인, v0.7.0 빌드·테스트·게시), 2026-11-24 갱신(2차 감사: BPM 변속 시간 이분 탐색 `BpmTimeline`, 서수 문자열 비교와 `SourceRulesTests`, MelonLoader·Unity 버전 점검, 아트워크 이미지 미발견 로그, v0.7.1), 2026-11-25 갱신(3차 감사: BGA 동기화 `BgaSyncPolicy` main 병합, 정규식 숫자 `[0-9]` 규칙, 결과 화면 □ 진단 실행당 1회화, v0.7.2), 2026-11-25 2차 갱신(4차 감사: 게임 씬 이름 시작 점검 `GameSceneNames`, 반복 경고 `ModLog.WarningThrottled`, BMS 캐시 최근 8곡 상한 `RecentKeys`, 문서 역슬래시 손상 복구, v0.7.3), 2026-11-26 갱신(5차 감사: 오프라인 언어 이름 `koreana` 수정 `SteamLanguage`, 곡 목록 정렬·ID 배정 재사용 `AlbumsVersion`, 프리뷰 캐시 LRU, info.txt UTF-8 아님 경고 `TextFileDecoder`, v0.7.4), 2026-11-26 2차 갱신(6차 감사: 아트워크 캐시 `RecentKeys` LRU 전환 및 stale 경로 즉시 제거 `CustomAssetManager`, BGA 준비 대기 매 프레임 LINQ 할당 루프 제거 및 특수문자 캐싱 `BgaInjector`, BGM 선택 단일 패스 탐색 `ChooseBgmFile`, 텍스트·오디오 서수 비교 일관화, NoteCreateData 핵심 필드 계약 검증, v0.7.5), 2026-11-27 갱신(7차 감사: TurnDirection/NoteSize enum 계약 추가 `GameCompatibility`, SongInfoParser StringReader 스트림 순회 메모리 할당 제거, 결과 씬 updater 네이티브 수명 검사 강화, v0.7.6), 2026-11-27 2차 갱신(8차 감사: BmsNoteConverter OrderBy 제거 및 인플레이스 정렬, BmsParser 인코딩 통일 및 TextFileDecoder 적용, BmsNoteDataParser 홀수 자릿수 패딩 안전성, PlayerMusicPlayData 필드 계약 5개 추가, v0.7.7), 2026-11-28 갱신(9차 감사: BMS 채널 02 마디 길이 배율 지원으로 변박 타이밍 밀림 완전 해결, FairyNoteProcessor 단일 패스 최적화, BmsParser 노트 리스트 정렬 무결성 보장, soRythmGameMusicDataMap.MusicData 핵심 필드 계약 3개 추가, v0.7.8). 폴더의 코드·프로젝트 파일·기존 문서와 실제 게임 설치 폴더(Steam `.acf`, `UnityPlayer.dll`, `MelonLoader.dll`)를 읽어 정리했다.
> 2026-11-17~18 변경은 곡 선택 화면까지 인게임으로 확인했다(7절에 범위 명시). **"실측"** 표시는 디스크에서 직접 읽은 값, **"미확인"**은 다음 세션에서 확인할 항목이다.

## 0. 프로젝트 개요

- **무엇인가:** GUNVOLT RECORDS Cychronicle 커스텀 차트 모드(v0.7.8, 2026-11-28 GitHub 릴리즈 게시. v0.7.7·v0.7.6은 2026-11-27, v0.7.5·v0.7.4는 2026-11-26, v0.7.3·v0.7.2는 2026-11-25, v0.7.1은 2026-11-24, v0.7.0은 2026-11-23). BMS 차트·BGM·BGA·아트워크·메타데이터를 주입하고, 스팀/DLC 체크(`SteamAPI.Init`, `SteamApps.BIsDlcInstalled`, `IntiCreates.Application.isDLCEnable`)를 우회. 테스트 프로젝트 `GRC2.Tests`, 심볼 도구 `tools/CsSymbolTool`.
- **종류:** 게임 모드
- **최근 수정일(파일 기준):** 2026-11-28
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

- **고정 여부:** 스팀 설치본은 고정 안 됨 — `AutoUpdateBehavior`가 `0`(항상 자동 업데이트)이고 `.acf`가 읽기 전용이 아니면 스팀이 게임을 업데이트할 수 있다. 업데이트되면 위 Build ID와 달라지므로 먼저 Build ID를 비교할 것.
- **독립 사본(2026-11-24):** `H:\GUNVOLT RECORDS Cychronicle hwa`에 스팀 설치본 전체를 robocopy로 복사했다(파일 1,608개·4,815,027,179바이트 일치, exe·`UnityPlayer.dll`·`Assembly-CSharp.dll`·`Mods\GRC2.dll`(v0.7.1)·`MelonLoader.dll` 해시 일치). 스팀이 모르는 폴더라 **Build 13530823으로 고정**된다. 바탕화면 `C:\Users\화평\Desktop\게임들\GUNVOLT_RECORDS_Cychronicle.exe - 바로 가기.lnk`는 이 사본을 실행한다(원래 대상은 스팀 설치본의 같은 exe). 주의:
  - **세이브는 공유된다.** 게임은 `Application.persistentDataPath`(`%USERPROFILE%\AppData\LocalLow\INTI CREATES\…`)에 저장하므로 사본과 스팀 설치본이 같은 세이브를 쓴다. `hwa`·`savecustomkey`·`Mods`·`UserData`는 폴더별로 따로다.
  - 사본 위에는 `.acf`가 없어 시작 점검이 `Steam Build ID를 읽지 못했습니다 … MVID로만 점검`을 찍는다(정상). 버전 비교는 MVID·MelonLoader·Unity로 한다.
  - **빌드·테스트·배포 기본 경로는 이 사본이다**(2026-11-24 변경). `GRC2.csproj`·`GRC2.Tests.csproj`의 `GamePath`, `GameCompatibilityTests`, `build_*.bat`의 `GAME_PATH`, `tools\release.ps1`·`check_game_version.ps1`의 `-GamePath` 기본값이 모두 `H:\GUNVOLT RECORDS Cychronicle hwa`다. 스팀 설치본에 빌드·배포하려면 `-p:GamePath=…`, `$env:GRC2_GAME_PATH`, `-GamePath`로 덮어쓴다. `check_game_version.ps1`은 사본에 `.acf`가 없어 Build ID를 건너뛰고, 대신 `-SteamPath`(기본 스팀 설치본)의 Build ID를 `[참고]`로 보여 준다(종료 코드에는 안 넣음) — 스팀판이 업데이트되면 여기서 안다.
- **DepotDownloader 다운그레이드:** 이 폴더에서 다운그레이드 기록은 찾지 못했다(**미확인**). 버전을 고정하려면 DepotDownloader로 위 Build ID의 매니페스트를 받아 두고, `.acf`를 읽기 전용으로 바꿔 업데이트를 막는다.

## 2. 런타임 백엔드 및 디컴파일 경로

- **GUNVOLT RECORDS Cychronicle:** Unity / **Mono** / Unity `2021.3.31f1` / x64 (실측)
  - 설치된 MelonLoader: `0.7.3.0 (net6)` (실측)
  - 참조 어셈블리: `H:\GUNVOLT RECORDS Cychronicle hwa\GUNVOLT_RECORDS_Cychronicle_Data\Managed\`(독립 사본, 1절. 스팀 설치본과 같은 빌드)
- 모드/앱 프로젝트 `GRC2\GRC2.csproj` 타깃 프레임워크: `net472` (SDK 스타일), `LangVersion` 10.0(보간 문자열 처리기용, 3절)
- 모드/앱 프로젝트 `GRC2.Tests\GRC2.Tests.csproj` 타깃 프레임워크: `net472` (SDK 스타일)
- **이 폴더 안의 디컴파일/덤프 자료:** `Decompiled/`, `Decompiled-outline/`
- 보조 프로젝트(디컴파일본·덤퍼): `tools\CsSymbolTool\CsSymbolTool.csproj`

## 3. 설계 의도와 우회 사유 (Rationale)

- **스팀/DLC 우회는 오프라인 보존 목적이다.** `SteamAPI.Init`, `SteamApps.BIsDlcInstalled`, `cDlcDirector.IsNotYetPurchased`, `IntiCreates.Application.isDLCEnable`을 건드린다(`GRC2/Harmony/SteamApiHijacker.cs`). 다만 **`Init` 결과를 true로 강제하지 않는다**: 강제하면 `cDlcDirector.Initialize()`가 `SteamApps.GetDLCCount()`에서 예외를 던져 `sAddressableDirector`가 영원히 대기한다(알려진_문제 A3). Init 결과를 기록해 두고 성공했을 때만 `RunCallbacks`/`Shutdown`을 원본으로 보낸다.
- **세이브 차단(`BlockSave`)은 결과 씬으로만 좁혀져 있다.** 부팅 중 저장까지 막으면 세이브 없는 부팅이 무한 반복된다(A1). 결과 씬에서는 곡 기록 4개 필드를 스냅샷으로 되돌려 AutoPlay 기록이 세이브에 남지 않게 한다(`RecordBlockPatch`).
- **노트 변환은 원본 로더의 후처리를 전부 재현하지 않는다**(H15, 의도적으로 미수정). 홀드/페어리 노트는 끝 노트 짝이 없으면 주입하지 않는다 — 게임의 `cFairyModeNotesManager.createNote`가 null 검사 없이 접근해 `createAllNote`가 NRE로 곡 로딩을 통째로 깨기 때문이다(`BmsNoteConverter.cs` 주석).
- **BGM은 48kHz만 가정한다.** 노트 시계를 BGM 샘플레이트에 맞추지 않고, 아니면 오류 로그만 남긴다(B1).
- **패치는 `PatchAll` 한 번 + 실패 시 클래스별 재시도**(`SceneDetector.InitializeHarmony`). Harmony는 첫 실패에서 멈춰 뒤 클래스를 빠뜨리기 때문이다(H4). **MelonLoader 자동 패치는 `[assembly: MelonLoader.HarmonyDontPatchAll]`로 끈다** — 빠져 있던 2026-11-23까지는 모든 훅이 두 번 돌았다(알려진_문제 P4).
- **필드 이름 접근과 상수 전제는 `GameCompatibility`에 모은다.** 게임 업데이트 때 무엇이 깨졌는지 시작 로그(`[호환성]`)와 `GameCompatibilityTests`로 바로 알기 위해서다(2026-11-16, 2026-11-18 상수 계약 추가). 게임 필드는 `new LazyFieldRef<T, F>("이름")`(`Helpers/LazyFieldRef.cs`, 처음 쓸 때 조회)로 접근하고 `FieldContracts` 표에도 한 줄 넣어야 테스트가 통과한다. `static readonly AccessTools.FieldRef<…> = FieldRefAccess(…)`는 필드 하나가 사라지면 그 클래스 전체를 멈추게 해 금지(`LazyFieldRefTests`가 막음). 노트·커서마다 불리는 경로는 `IsAvailable`을 먼저 본다.
- **게임 enum·const 값은 반드시 이름으로 런타임에 읽어 비교한다**(2026-11-20). C#은 `NoteTypeId.Hold`, `MusicID.UNIQUE_TOP` 같은 값을 모드 DLL에 숫자로 박아 넣으므로, `(int)MusicID.UNIQUE_TOP`으로 점검하면 게임이 바뀌어도 배포된 DLL의 점검은 영원히 통과한다(실제로 2026-11-18~19 코드가 그랬다). `GameCompatibility.RuntimeEnumValue`/`RuntimeConstValue`를 쓴다. 노트 변환·판정이 쓰는 게임 enum 멤버(`NoteTypeId`·`NoteDirectionIndex`·`NoteSubLaneType`·`NoteLaneLeftRight`·`JudgeType`·`MusicID`)는 모두 `ConstantContracts`에 있어야 하고, 소스에 새 멤버를 쓰면 `ConstantContracts_CoverEveryGameEnumMemberInSource`가 실패한다. 값이 바뀌면 재빌드해야 노트 변환이 고쳐진다.
- **게임 폰트에 없는 글자는 TMP 전역 fallback으로 그린다**(`Core/FontFallback.cs`, 2026-11-17). 게임은 언어별 Static TMP 아틀라스만 써서 커스텀 곡의 간체 한자 등이 □로 나왔다. 첫 씬 로드 때 `hwa/fonts/` → 윈도우 폰트(msyh·msjh·YuGothM·meiryo·malgun·seguisym) 순으로 Dynamic `TMP_FontAsset`을 만들어 `TMP_Settings.fallbackFontAssets`에 붙인다. 게임 폰트에 있는 글자는 바꾸지 않으므로 Harmony 훅이 필요 없고, 언어를 바꿔도 유지된다.
- **커스텀 MusicID는 54~7999(이름 붙은 enum 값 제외), 외부 맵 파일 없이 메모리에서만 배정**(`Core/CustomMusicIdRegistry.cs`, `Helpers/CustomMusicIdAllocator.cs`, 2026-11-18). 예전 상한 511(458곡)은 세이브 `playerMusicData`가 512칸이라서였다. 세이브가 JsonUtility JSON이라 배열을 늘려도 그대로 저장·로드되고 게임 루프는 512까지만 돌거나 `getIsUsableMusicID`로 걸러서, 곡 목록을 만들 때 `EnsureSaveSlots`로 늘린다. 8000 이상은 튜토리얼·미리보기(`UNIQUE_TOP`) 전용이라 쓰지 않는다. 처음에는 폴더 → ID를 외부 파일(`UserData/GRC2/musicid_map.txt`, `savecustomkey/musicid_map.txt`)이나 `MelonPreferences.cfg`의 `[GRC2_MusicID]`에 두었으나, 사용자가 외부 맵 데이터 자체를 원치 않아(2026-11-18) 모두 제거하고 메모리에서만 이름순으로 배정한다. 한 실행 안에서는 이미 배정된 ID를 기억해 게임을 켠 채 곡을 추가해도 방금 선택·플레이하던 곡의 ID가 흔들리지 않는다. 대신 실행 사이에는 ID를 기억하지 않으므로, 앞 이름의 폴더를 추가·삭제하거나 게임에 원본 곡이 추가되면 ID가 밀려 `BlockSave=0` 기록이 다른 곡에 붙어 보일 수 있다(알려진_문제 C4 남은 한계, 원본 곡 추가는 `[호환성]` 경고). 범위 끝 `UNIQUE_TOP`과 경계 `SAVEABLE_ID_END`는 런타임에 이름으로 읽는다(2026-11-20). 시작할 때 남아 있는 레거시 맵 파일과 `MelonPreferences.cfg`의 `[GRC2_MusicID]` 섹션은 자동으로 지운다.
- **로그는 `ModLog`로 나눈다**(2026-11-18). 곡·노트·매핑마다 찍히는 것은 `ModLog.Verbose`(config `VerboseLog=1`일 때만), 같은 상태를 반복해 알리는 것은 `ModLog.InfoOnChange`. 곡 상한을 올리면 곡 수에 비례하는 로그가 수천 줄이 되기 때문이다. 경고·오류는 그대로 `MelonLogger`.
- **곡 선택 화면에서 긴 제목은 옆으로 흘린다**(`Core/TextMarquee.cs`, `Harmony/SongTitleMarqueePatch.cs`, 2026-11-18). 선택한 곡 막대의 제목 `SongTitle`은 부모 마스크 `曲名`이 잘라 긴 제목이 끊겼다. 넘칠 때만 1.5초 대기 → 초당 80 이동 → 끝에서 1.5초 대기 → 처음으로를 반복한다. RectTransform이 아니라 TMP 왼쪽 `margin`을 음수로 바꿔 옮기므로 막대 열기·닫기 애니메이션과 부딪히지 않는다. 원본 곡에도 같이 적용된다.
- **시작할 때는 현재 곡 BMS만 파싱**하고, 곡 선택 씬 진입 때 `hwa` 지문이 바뀌었으면 다시 스캔한다(2026-11-18). 한 실행 안에서는 세션 맵(`_sessionMap`)이 이미 배정된 ID를 유지하므로 게임을 켠 채 곡을 추가해도 방금 고른 곡의 ID가 바뀌지 않는다. 아트워크 캐시도 파일 수정 시각을 함께 비교해 같은 이름으로 덮어쓴 이미지를 다시 읽는다.
- **상시 도는 코루틴은 `WaitForSeconds`를 한 번만 만들어 재사용한다**(2026-11-23 감사). `BgmBgaInjector`(2초)·`BgaBgmSyncManager`(0.1초) 루프가 매 반복 새로 할당하던 것을 바꿨다. 같은 감사에서 `OnUpdate`(설정 파일 폴링은 간격 제한)·`OnGUI`(Repaint만 그림)·`FindObjectOfType` 호출처(키 입력/곡 시작 시 1회뿐)를 점검했고 추가 병목은 없었다. 새 상시 루프를 만들 때도 같은 규칙을 지킨다.
- **try 없는 훅·매 프레임 경로에서는 `LazyFieldRef.IsAvailable`을 먼저 본다**(2026-11-23, 알려진_문제 R2). 인덱서는 필드가 없으면 `MissingFieldException`을 던지는데, try 없는 Harmony 패치 메서드(예: `AutoPlayPatch`의 `createAllNote` Prefix)에서 던지면 **원본 메서드까지 깨진다**(곡 로딩 실패). 대체 경로가 뒤에 있는 곳(`ArtworkUpdater`의 `FindObjectOfType`)도 예외면 거기까지 못 간다.
- **훅 적용 실패는 `GameCompatibility.RecordHookFailure`로 모은다**(2026-11-23, R1). 클래스별 재적용(`PatchTypesOneByOne`)과 `TargetMethods`에서 못 찾은 대상을 여기 넣어야 시작 점검이 문제로 센다. 예전에는 훅 대상이 사라져도 아래 요약이 `점검 통과`였다. 새 `TargetMethods`를 만들면 못 찾은 대상도 여기에 기록할 것.
- **상세 로그는 `ModLog.Verbose($"...")`로 쓰면 꺼져 있을 때 비용이 0이다**(2026-11-23, P10). C# 10 보간 문자열 처리기(`Helpers/VerboseLogHandler.cs`)로 묶여 `VerboseLog=0`이면 구멍 계산과 문자열 생성을 건너뛴다. 그래서 두 프로젝트 `LangVersion`이 10.0이다(net472 기본 7.3). **구멍 안에 부작용 있는 식을 쓰지 말 것**(꺼져 있으면 실행되지 않음). 런타임 기능이 필요한 문법(기본 인터페이스 메서드 등)은 Unity Mono에서 안 되므로 쓰지 않는다.
- **활성 씬 이름은 `Helpers/ActiveSceneName`으로 읽는다**(2026-11-23, P11). `Scene.name`은 호출마다 새 문자열이라 매 프레임 경로에서 쓰지 않는다. 씬 핸들이 바뀔 때만 다시 읽는다. 씬 로드 이벤트로 이름을 저장하는 방식은 추가 로드 씬 때문에 판정이 달라질 수 있어 쓰지 않았다.
- **문자열 비교는 서수(`StringComparison.Ordinal[IgnoreCase]`)로 한다**(2026-11-24, 알려진_문제 P14). `StartsWith("…")`처럼 문자열 하나만 받는 오버로드는 Mono에서 현재 문화권 비교라 느리고(파일의 줄마다 불림) 결과가 문화권에 따라 달라질 수 있다. `SourceRulesTests`가 `GRC2/` 소스를 검사해 막는다(보간 로그 구멍의 부작용 금지도 같은 파일).
- **BMS 변속 시간은 `Parsers/BpmTimeline`으로 계산한다**(2026-11-24, P8). `BmsParser.CalculateTime`은 노트마다 변화 목록을 처음부터 훑는 기준 구현으로 남겨 두고, `BpmTimelineTests`가 두 결과가 비트 단위로 같은지 확인한다. 시간 계산 식을 바꾸면 둘 다 바꿔야 한다.
- **MelonLoader·Unity 버전도 검증 기준이다**(2026-11-24, R4). `GameCompatibility.TestedMelonLoaderVersion`/`TestedUnityVersion`을 시작 점검·`check_game_version.ps1`·`GameCompatibilityTests`가 함께 본다. MelonLoader를 올리면 이 값과 1절 표를 같이 고친다. 버전은 const `BuildInfo.Version`이 아니라 어셈블리 버전을 런타임에 읽는다.
- **폰트 글자 진단(`FontFallback.LogCoverageDiagnostics`)의 글자별 목록은 `VerboseLog=1` 전용**(2026-11-23, P12). 글자별로 fallback에 `tryAddCharacter: true`로 물으면 모든 곡 글자가 Dynamic 아틀라스에 구워져 곡 선택 첫 진입이 느려지고 아틀라스가 늘어난다. 기본은 개수 한 줄만 남긴다.
- **BGA는 BGM에 "탐색"이 아니라 "멈춤·배속"으로 맞춘다**(2026-11-24, 알려진_문제 L1). mp4 탐색은 키프레임부터 다시 디코딩해 0.1~0.2초 밀리므로, 작은 차이마다 탐색하면 탐색이 탐색을 부른다. 판단은 Unity 없이 테스트되는 `Helpers/BgaSyncPolicy.Decide`(BGM이 흐르기 전 `Hold`, 0.04~0.5초는 `AdjustSpeed` 0.9~1.1배, 0.5초 이상만 `Seek` 후 1초 대기)에 두고 `BgaBgmSyncManager`는 적용만 한다. 게임은 BGM을 `isPlaying`인 채 약 2초 늦게 흘리므로 `isPlaying`이 아니라 **BGM 시각이 실제로 느는지**로 판단한다.
- **정규식 숫자는 `[0-9]`로 쓴다**(2026-11-25, R5). .NET `\d`는 전각·아랍 숫자도 잡아 `int.Parse`가 던지고, BMS 파서는 예외 시 그 파일의 노트를 전부 버린다. `SourceRulesTests.ModSource_RegexDigitsAreAsciiOnly`가 막는다.
- **임시 진단 코드는 릴리즈에 넣을 수 있게 "실행당 1회·고정 파일"로 만든다**(2026-11-25). 결과 화면 □(L2) 진단 `Core/ResultScreenDiagnostics`가 그렇다(첫 커스텀 곡 결과 화면에서 한 번, `UserData/GRC2/result_screen_last.png` 덮어쓰기). 원인을 고치면 지운다.
- **게임 씬 이름은 `Core/GameSceneNames`에만 둔다**(2026-11-25, 알려진_문제 R6). 씬 이름은 BGM/BGA 주입·기록 차단·제목 치환의 분기 조건인데 필드·상수·훅과 달리 점검에 없어, 이름이 바뀌면 "점검 통과"인 채 기능만 꺼졌다. 시작 점검이 빌드 설정 씬 목록(`SceneUtility.GetScenePathByBuildIndex`)과 대조하고, `SourceRulesTests`가 다른 파일의 씬 이름 문자열을 막는다. 새 씬 분기를 만들면 상수와 `ExactNames`에 넣는다.
- **매 프레임·매 판정·매 `set_text` 경로의 catch는 `ModLog.WarningThrottled(key, …)`로 남긴다**(2026-11-25, P16). 같은 예외가 반복되면 `MelonLogger.Warning`이 초당 수백 줄이 되어 로그 쓰기 자체가 렉이 된다. 1·10·100…번째만 남는다.
- **실행 내내 커지는 캐시는 상한을 둔다**(2026-11-25, P17). BMS 파싱 캐시는 `Helpers/RecentKeys`(LRU)로 최근 8곡(`SceneDetector.MaxCachedBmsFiles`)만 남긴다. 곡 상한이 7900여 곡이라 플레이한 곡을 전부 들고 있으면 메모리가 계속 는다.
- **오프라인 대체값은 "게임이 아는 값"인지까지 확인한다**(2026-11-26, 알려진_문제 A4b). A4 수정은 예외만 막고 `"korean"`을 돌려줘 게임이 영어를 골랐다(Steam 이름은 `"koreana"`). 게임 분기 문자열은 소스에 상수로 있지 않아 컴파일로 못 잡으므로, `GameCompatibilityTests.SteamLanguageNames_ExistInGameLocaleSwitch`처럼 설치된 게임 메서드 IL의 `ldstr`을 읽어 대조한다. 다른 우회도 결과값을 게임이 문자열로 분기하면 같은 방식으로 테스트한다.
- **곡 목록 postfix의 무거운 계산은 `AlbumManager.AlbumsVersion`으로 캐시한다**(2026-11-26, P18). `initializeMusicDataByDefault` postfix는 곡 선택 씬 한 번에 4번쯤 불린다. `_albums`를 바꾸는 코드를 새로 만들면 반드시 `AlbumsVersion++`을 같이 넣는다(안 넣으면 새 앨범이 목록·ID에 반영되지 않는다).
- **텍스트 파일은 UTF-8만 읽을 수 있다**(2026-11-26, G2). 게임 `Managed`에 `I18N.CJK.dll`이 없어 `Encoding.GetEncoding(949/932)`이 런타임에서 실패한다. 디코더를 이식하는 대신 `TextFileDecoder`로 UTF-8 아님을 경고한다.
- **아트워크 캐시 순서도 `Helpers/RecentKeys`로 통일하고 stale 경로를 즉시 제거한다**(2026-11-26, P20). `CustomAssetManager`의 큐 방식은 다시 고른 곡의 LRU 갱신이 안 되고 수정 시각 무효화(`EvictCachedArtwork`) 때 큐에 찌꺼기가 남았다. 4차·5차 감사의 BMS·프리뷰 캐시와 같은 `RecentKeys`로 일관화해 캐시 상한(12장) 초과 시 안전하게 텍스처를 파괴한다.
- **BGA 준비 대기 루프에서 매 프레임 LINQ 할당을 없앤다**(2026-11-26, P21). `BgaInjector.WaitForPlayersPreparedCoroutine`은 60fps로 매 프레임 `All(vp => vp.isPrepared)` 람다를 호출해 코루틴 도중 가비지가 생겼다. 인덱서 루프로 바꾸고 특수문자 `#%?` 검사용 `char[]`도 static readonly로 캐시한다.
- **BGM 선택은 단일 패스로 처리한다**(2026-11-26, P22). `AlbumManager.ChooseBgmFile`이 `candidates.OrderBy(...).ToList()` 후 람다를 3번 순회하던 것을 한 번의 루프로 ogg, mp3, wav 우선순위 및 파일명 순서를 판별해 정렬 리스트 힙 할당을 없앴다.
- **문자열 서수 비교를 일관화한다**(2026-11-26, P23). .NET 4.7.2 `string.Contains`는 문화권에 영향받을 수 있으므로 `TextPatch`와 `BgaBgmSyncManager`에서 `IndexOf(..., StringComparison.Ordinal[IgnoreCase]) >= 0`으로 통일한다.
- **`FairyNoteEditorLoader.NoteCreateData` 핵심 필드도 계약으로 보호한다**(2026-11-26, R7). `perfectSample`, `directionIndex`, 게임 오타인 `turnDireciton` 등 9개 필드의 존재 여부를 `GameCompatibilityTests`가 리플렉션으로 검증한다.
- **`TurnDirection` 및 `NoteSize` enum도 계약으로 보호한다**(2026-11-27, R8). `NoteFieldInitializer`와 `NoteCreateDataBuilder`에서 쓰는 두 enum 멤버를 `GameCompatibility.ConstantContracts`와 소스 검증에 추가했다.
- **`SongInfoParser`는 스트림 기반으로 줄 단위 파싱한다**(2026-11-27, P24). `text.Split`으로 인한 거대 문자열 배열 힙 할당을 `StringReader` 스트림 순회로 바꿔 O(1) 수준의 최소 메모리로 동작한다.
- **결과 씬 아트워크 코루틴은 Unity Object 파괴 여부를 즉시 검사한다**(2026-11-27, P25). `updater == null || !updater` 검사를 코루틴 루프에 추가해 씬 전환 시 네이티브 객체가 파괴되면 즉시 탈출한다.
- **`BmsNoteConverter`는 인플레이스로 노트를 정렬한다**(2026-11-27, P26). `ClassifyAndConvertNotes`에서 `bmsNotes.OrderBy(...)`로 매 변환마다 발생하던 람다·정렬 버퍼·열거자 생성을 `List.Sort`와 인덱서 루프로 대체해 GC 가비지를 제거했다.
- **`BmsParser`도 `TextFileDecoder`로 인코딩을 보호한다**(2026-11-27, P27). `File.ReadAllLines` 대신 바이트를 읽어 안전하게 디코딩하고 메모리 기반 파싱 메서드 `ParseBmsText`를 제공한다.
- **`BmsNoteDataParser`는 홀수 자릿수 토큰에 0을 패딩한다**(2026-11-27, P28). 마디 데이터 길이가 자릿수의 배수가 아닐 때 자투리 토큰을 버리지 않고 상위 비트 0 패딩을 수행해 슬롯 정렬을 유지한다.
- **`PlayerMusicPlayData` 세이브 필드도 계약으로 보호한다**(2026-11-27, R9). 세이브 슬롯 확장 시 직렬화되는 `highScoreArray`, `maxComboArray`, `playCountArray`, `playFlagArray`, `isNew` 5개 필드를 `FieldContracts`에 등록했다.
- **BMS 채널 02 마디 길이 배율을 완벽히 지원한다**(2026-11-28, E1). `#xxx02:factor`로 지정된 마디 길이 배율을 누적 시작 Tick 테이블로 계산해 변박 마디(3/4박자 등)가 있는 차트도 이후 노트와 BPM 타이밍이 밀리지 않는다.
- **`FairyNoteProcessor` 시작 노트 탐색을 단일 패스로 처리한다**(2026-11-28, P29). `FindStartNoteObject`에서 Where/OrderBy/ThenBy LINQ 체인을 제거하고 단일 for 루프로 최적의 시작 노트를 찾아 GC 가비지를 없앴다.
- **`BmsParser`는 파싱 직후 노트 리스트의 정렬을 보장한다**(2026-11-28, P30). 마디 정의가 뒤섞인 파일이라도 `ParseNotes` 완료 후 `notes.Sort`로 Tick 순서 오름차순 정렬을 보장한다.
- **`soRythmGameMusicDataMap.MusicData` 핵심 필드도 계약으로 보호한다**(2026-11-28, R10). 원본 곡 데이터 구조인 `MusicData`의 `id`, `charactorID`, `movieClipFileName` 3개 필드를 `FieldContracts`에 등록했다.
- 훅 소유 파일·제거 위험의 정본은 [HOOK_MAP.md](GRC%20리드미/maintenance/HOOK_MAP.md), 문제별 상세는 [알려진_문제.md](GRC%20리드미/maintenance/알려진_문제.md).

## 4. 핵심 훅 시그니처와 주입 파이프라인

- **Melon 특성:** `[assembly: MelonInfo(typeof(GRC2.Core.SceneDetector), "GUNVOLT RECORDS Cychronicle", "0.7.8", "화영왕")]`, `[assembly: MelonLoader.HarmonyDontPatchAll]`, `MelonGame("INTI CREATES", "GUNVOLT RECORDS Cychronicle")`
- **시작 순서(`SceneDetector.OnInitializeMelon`):** Harmony 패치 적용(실패 클래스는 `RecordHookFailure`) → 게임 폴더 계산 → `GameCompatibility.LogStartupReport`(훅 실패·빌드·MelonLoader·Unity·MVID·필드·상수) → `CustomKeySettings.Initialize`(VerboseLog 결정) → `CustomMusicIdRegistry.Initialize` → 앨범 스캔(+hwa 지문) → 곡 정보 → 아트워크 → 현재 곡 BMS만 파싱 → `NoteArrayHooks.UpdateBmsNotes`. 끝에 단계별 ms를 한 줄로 남긴다.
- **씬 로드마다:** `FontFallback.EnsureInstalled`(첫 회에 폰트 생성). 곡 선택 씬이면 `RescanAlbumsIfChanged` → (약 200ms 뒤) 곡 목록 postfix가 ID 배정·세이브 칸 확장·주입.
- **노트 주입:** `cFairyModeNotesManager.createAllNote` 훅(`NoteArrayHooks`)이 `mFairyNoteCreateDataArray`를 `BmsNoteConverter` 결과로 교체한다. 곡 선택 씬에서는 주입하지 않는다(H1).
- **BGM/BGA:** `BgmBgaInjector`가 현재 앨범 경로를 매 틱 읽어 주입한다. 커스텀 곡 종료는 `BgmGameEndMonitor`가 감시한다.
- **곡 선택/결과 화면:** `cMusicSelectScrollView.initializeMusicDataByDefault`, `cMusicSelectSceneUIUpdater.*`, `cMusicSelectSceneSelectingMusicUI.setDispMusicData`(제목 스크롤), `cRythmGameResultSceneUpdater.coUpdateResultAnim` 등을 훅한다. 전체 목록은 HOOK_MAP.md.
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
- **커스텀 MusicID 맵을 외부 파일이나 설정에 두기** → 모두 폐기(2026-11-18). `hwa/`에 두면 루트 앨범의 txt로 읽혀 가짜 곡이 생기고, `savecustomkey/`·`UserData/GRC2/musicid_map.txt`·`UserData/MelonPreferences.cfg`의 `[GRC2_MusicID]`는 사용자가 외부 맵 데이터 자체를 원치 않아 모두 없앴다. 시작할 때 남아 있는 레거시 맵 파일과 `[GRC2_MusicID]` 섹션은 자동으로 지우고, 메모리에서만 배정한다.
- **커스텀 ID에 512나 8000 이상 쓰기** → 512는 `SAVEABLE_ID_END`라는 이름 붙은 값(`ToString`·`Enum.TryParse` 경로가 이름으로 바뀜), 8000~8999는 튜토리얼 판정(`id >= UNIQUE_TOP`). 배정기는 이름 붙은 enum 값을 전부 건너뛴다.
- **게임의 `cUICommonScrollText`로 제목 스크롤** → 이 빌드에서는 `Update`가 비어 있는 미완성 컴포넌트라 쓸 수 없다. 직접 `TextMarquee`를 붙인다.
- **리플렉션 기반 필드 접근 시스템** → 현재 코드와 맞지 않아 `GRC 리드미/archive/`로 옮김. 지금은 컴파일 타임 타입 참조 + `LazyFieldRef`(내부적으로 `AccessTools.FieldRefAccess`).
- **`HarmonyDontPatchAll` 없이 직접 `PatchAll`** → MelonLoader 자동 패치와 겹쳐 모든 훅이 두 번 돌았다(노트 변환 2회, 판정바 틱 2회, 노트 속도 카오스 배율 제곱, 원제목을 포함한 커스텀 제목 이중 치환). 로그에서 "같은 메서드가 두 번 불림"처럼 보여 P4는 오래 원인 미상이었다. 2026-11-23 수정.
- **`GRC2.Tests`를 솔루션에 넣지 않기** → `test_debug.bat`이 테스트 0개로 "성공"했다(F). 지금은 솔루션에 포함.
- **게임 없는 환경에서 만든 `tools/release.ps1`을 그대로 믿기** → 2026-11-23 첫 실행에서 두 버그가 나왔다. `MelonInfo` 버전 정규식이 `typeof(...)`의 `)`에서 멈춰 항상 "버전 불일치", 릴리즈 노트를 PS 5.1 `Set-Content -Encoding utf8`로 써서 BOM 때문에 GitHub 본문 첫 `###`가 제목이 안 됨. 둘 다 고쳤다. 스크립트를 고치면 `-Publish` 없이 먼저 한 번 돌려 `dist\release-notes-*.md`를 확인한다. 2026-11-25에는 Windows PowerShell 5.1에서 `-Publish`가 `git fetch`의 stderr 진행 메시지("From …")를 `$ErrorActionPreference='Stop'` 때문에 예외로 받아 멈췄다(태그 전 단계라 부작용 없음). 게시 단계의 git·gh 호출은 `Invoke-Native`(그 구간만 Continue, 종료 코드로 판단)로 감쌌다.
- **\"로그 멈춤\"을 모드·MelonLoader 콘솔 버그로 의심**(2026-11-24) → 아니었다. 게임을 켜면 `Latest.log`가 0바이트·CPU 0으로 멈추거나, 같은 함수 안의 연속된 두 줄 사이가 4~20초씩 벌어졌다. 스팀판도 똑같이 멈췄고 `--melonloader.hideconsole`로도 느렸다. 원인은 **같은 H: 드라이브에서 다른 세션이 돌리던 `robocopy /MT:16`**(PUMP IT UP RISE 40GB 복사)로 디스크 사용률이 2000%를 넘은 것. 게임·로그가 디스크를 기다리며 멈춘 것이다. 증상이 다시 보이면 코드보다 먼저 `Get-Counter '\PhysicalDisk(*)\% Disk Time'`과 실행 중인 복사·설치 작업을 본다.
- **역슬래시가 든 문서를 셸 heredoc 속 파이썬 문자열로 고치기**(2026-11-25) → `\\`가 `\` 하나로 줄어 `\r`·`\'`가 이스케이프로 먹혔다. `CHANGELOG.md` 0.7.2의 `UserData\GRC2\result_screen_last.png`가 `GRC2esult…`로, 알려진_문제 I1의 `Replace('\\','/')`가 `Replace('','/')`로 깨진 채 커밋·게시까지 됐다. 역슬래시가 있으면 Edit 도구나 스크립트 파일(`chr(92)`)을 쓰고, 고친 뒤 `grep`으로 결과를 확인한다.
- **게임 enum·const를 코드로 읽어 상수 계약 점검하기**(`new ConstantContract(..., 512, (int)MusicID.SAVEABLE_ID_END)`) → 컴파일러가 둘 다 숫자로 박아 넣어 "기대 512, 현재 512"를 영원히 비교했다. 테스트도 다시 빌드할 때만 새 값을 보므로 배포된 DLL의 시작 점검은 업데이트를 못 잡았다(2026-11-20 수정, 이름으로 런타임 조회).

## 6. 빌드 명령 및 배포 경로

- 솔루션: `GRC2.sln`
- `GRC2\GRC2.csproj`:
  ```powershell
  dotnet build "GRC2\GRC2.csproj" -c Release
  ```
  - 게임 경로 속성: `H:\GUNVOLT RECORDS Cychronicle hwa`(독립 사본, 2026-11-24부터 기본). `-p:GamePath=…`로 다른 설치 경로(예: 스팀 설치본)를 지정할 수 있다.
- `GRC2.Tests\GRC2.Tests.csproj`:
  ```powershell
  dotnet build "GRC2.Tests\GRC2.Tests.csproj" -c Release
  ```
- 빌드/배포 스크립트: `build_debug.bat`, `build_release.bat`, `test_debug.bat`(테스트 전에 `tools\check_game_version.ps1`로 설치본 버전을 먼저 비교, 다르면 경고만)
- 게임 버전 점검만: `powershell -NoProfile -ExecutionPolicy Bypass -File tools\check_game_version.ps1` (기준 값은 `GameCompatibility.cs`에서 읽음, 다르면 종료 코드 1)
- 배포는 스크립트가 처리한다: `build_debug.bat`, `build_release.bat` (내용에 Mods 경로 복사 포함)
- 배포 위치: `H:\GUNVOLT RECORDS Cychronicle hwa\Mods\`(스크립트 기본값, 바탕화면 바로가기가 실행하는 독립 사본). 스팀 설치본 `H:\steam\steamapps\common\GUNVOLT RECORDS Cychronicle\Mods\`에는 v0.7.1이 그대로 있고 이후로는 자동으로 갱신되지 않는다.

## 7. 다음 작업자에게

- 작업 전 `tools\check_game_version.ps1`로(또는 1절의 Build ID와 현재 설치본 `.acf`의 `buildid`를 직접) 비교한다. 2026-11-24 기준 Build ID·MVID·MelonLoader 0.7.3·Unity 2021.3.31f1 모두 같음. 다르면 **[업데이트_대응_가이드.md](GRC%20리드미/maintenance/업데이트_대응_가이드.md)** 순서를 따른다(테스트 → 훅 시그니처 확인 → 기준 빌드 갱신).
- `dotnet test GRC2.Tests\GRC2.Tests.csproj`가 모두 통과해야 한다(2026-11-28 9차 감사 후 게임 PC 기준 229개).
- **2026-11-28 v0.7.8(9차 감사):** BMS 채널 02 마디 길이 배율 지원으로 변박 타이밍 밀림 완전 해결(E1)·FairyNoteProcessor 단일 패스 최적화(P29)·BmsParser 노트 리스트 정렬 보장(P30)·soRythmGameMusicDataMap.MusicData 핵심 필드 계약 등록(R10). 빌드(경고 0)·테스트(229개)·게시·`hwa` 사본 `Mods\GRC2.dll` 배포까지 했고 **인게임 실행은 안 했다.**
- **2026-11-27 v0.7.7(8차 감사):** BmsNoteConverter OrderBy 제거 및 인플레이스 정렬(P26)·BmsParser 인코딩 보호 및 TextFileDecoder 적용(P27)·BmsNoteDataParser 홀수 자릿수 패딩 안전성(P28)·PlayerMusicPlayData 세이브 필드 계약 5개 등록(R9). 빌드(경고 0)·테스트(226개)·게시·`hwa` 사본 `Mods\GRC2.dll` 배포까지 했고 **인게임 실행은 안 했다.**
- **2026-11-27 v0.7.6(7차 감사):** TurnDirection/NoteSize 상수 계약 추가(R8)·SongInfoParser StringReader 할당 제거(P24)·결과 씬 updater 네이티브 수명 검사 강화(P25)·SceneDetector 플레이 씬 주입 로직 정리. 빌드(경고 0)·테스트(223개)·게시·`hwa` 사본 `Mods\GRC2.dll` 배포까지 했고 **인게임 실행은 안 했다.**
- **2026-11-26 v0.7.5(6차 감사):** 아트워크 캐시 LRU 전환(P20)·BGA 준비 대기 LINQ 할당 루프 제거(P21)·BGM 선택 단일 패스(P22)·서수 비교 일관화(P23)·NoteCreateData 필드 계약 검증(R7). 빌드(경고 0)·테스트(220개)·게시·`hwa` 사본 `Mods\GRC2.dll` 배포까지 했고 **인게임 실행은 안 했다.**
- **2026-11-26 v0.7.4(5차 감사):** 오프라인 첫 부팅 언어(A4b)·곡 목록 재사용(P18)·프리뷰 캐시 LRU(P19)·info.txt 인코딩 경고(G2). 빌드(경고 0)·테스트(214개)·게시·`hwa` 사본 `Mods\GRC2.dll` 배포까지 했고 **인게임 실행은 안 했다.** 다음에 게임을 켜면 곡 선택 진입 로그의 `커스텀 곡 주입 완료` 줄이 여전히 한 번이고 곡 수가 같은지(P18 재사용이 새 곡을 빠뜨리지 않는지), 게임을 켠 채 hwa에 곡 폴더를 추가하고 곡 선택에 다시 들어가면 그 곡이 보이는지 확인한다. A4b는 스팀을 끄고 세이브 없이 실행해야 보이므로 세이브를 백업한 뒤에만 시험한다.
- **2026-11-25 v0.7.3(4차 감사):** 씬 이름 시작 점검(R6)·반복 경고 제한(P16)·BMS 캐시 상한(P17)·문서 역슬래시 복구. 빌드(경고 0)·테스트(194개)·게시·`hwa` 사본 `Mods\GRC2.dll` 배포까지 했고 **인게임 실행은 안 했다.** 다음에 게임을 켜면 시작 로그가 `[호환성] 점검 통과 (필드 23개, 상수 26개, 씬 7개, 훅 클래스 전부 적용).`인지 본다. `게임 씬 'SoundPlayerScene'`/`'MoviePlayer_MovieSelect'` 없음 오류가 나오면 그 둘은 실측 로그에 없던 씬이므로(알려진_문제 R6 남은 한계) 같은 줄의 "현재 씬" 목록을 보고 `GameSceneNames`를 고친다.
- **사용자가 알고 있는 버그는 바로 [알려진_문제.md](GRC%20리드미/maintenance/알려진_문제.md)에 적는다.** 2026-11-24에 "결과 화면 한자 □"와 "플레이 시작 렉"이 오래전부터 있던 버그인데 어느 문서에도 없어 다음 세션이 몰랐다(L절).
- **2026-11-25 v0.7.2(3차 감사):** `fix/bga-sync-stutter`(L1 BGA 렉 수정, **인게임 확인됨**)를 main에 병합하고 브랜치를 지웠다. 같은 릴리즈에 정규식 숫자 수정(R5)·동기화 중지 로그 정리(P15)·L2 진단 1회화를 넣었다(이 셋은 빌드·테스트만). 빌드·테스트(184개)·게시·`hwa` 사본 `Mods\GRC2.dll` 배포까지 했다. 스팀 설치본 `Mods`는 v0.7.1 그대로다.
- **다음 할 일(L2, 결과 화면 □):** TMP는 `麦`·`盖`를 fallback(msyh)으로 그렸다고 답하는데 화면은 속이 빈 □. 패딩 비율 불일치는 번들 대조로 배제했다(알려진_문제 L2). v0.7.2로 커스텀 곡을 결과 화면까지 플레이하면 `[결과 화면 진단]` 줄과 `UserData/GRC2/result_screen_last.png`가 남는다 → 원인을 고치고 `ResultScreenDiagnostics`와 `ResultSceneUpdaterPatch`의 호출 한 줄을 지운다. 게임 자동 조작은 사용자가 PC를 쓰지 않을 때만(`um win drive … idle`로 확인) 한다 — 2026-11-25 세션은 사용자가 PC 사용 중이라 하지 않았다.
- **게임 번들 읽기:** 파이썬 `UnityPy`(1.25)가 설치돼 있다. `GUNVOLT_RECORDS_Cychronicle_Data/StreamingAssets/aa/StandaloneWindows64/*.bundle`(이름은 해시, `aa/catalog.json`에 에셋 경로)에서 머티리얼·TMP 폰트 값을 읽을 수 있다. 폰트·머티리얼은 `81978c783b97bfbe671ae062636ab25b.bundle`에 있었다(2026-11-25).
- **2026-11-23 v0.7.0은 게임 PC에서 빌드·테스트(158개)·게시까지 했고 `Mods\GRC2.dll`에도 배포했지만 인게임 실행은 아직 안 했다.** 다음에 게임을 켜면 ① 시작 로그 `[호환성] 점검 통과 (필드 23개, 상수 26개, 훅 클래스 전부 적용).` ② 플레이 시작 로그 `[NoteArrayHooks] BMS 노트 교체 완료`가 **한 번만**(이중 패치 수정) ③ 판정바 틱이 판정당 한 번 ④ 곡 선택 첫 진입 `[폰트] 곡 정보의 비ASCII 글자 N종: …` 한 줄(글자별 목록 없음)과 간체 한자 정상 표시 ⑤ `VerboseLog=1`로 바꾸면 상세 로그가 예전처럼 나오는지(보간 처리기)를 본다. 문제가 있으면 `v0.6.0..v0.7.0` 사이 커밋부터 의심한다.
- **2026-11-24 v0.7.1(2차 감사)도 빌드·테스트(169개)·게시·`Mods\GRC2.dll` 배포까지 했고 인게임 실행은 아직 안 했다.** 위 ①~⑤에 더해 ⑥ 시작 로그에 `[호환성] MelonLoader …`/`Unity …` 경고가 없는지 ⑦ 변속이 있는 BMS를 넣었다면 노트 타이밍이 0.7.0과 같은지(값은 테스트로 비트 동일 확인)를 본다.
- **2026-11-24 인게임 부분 확인(독립 사본, v0.7.1):** 시작 로그 `[호환성] 점검 통과 (필드 23개, 상수 26개, 훅 클래스 전부 적용).`(MelonLoader·Unity 경고 없음, `.acf` 없음 안내는 정상), `패치된 게임 메서드 42개`, `[SteamApiHijacker] cDlcDirector.Initialize Postfix` 줄이 **한 번만**(11-18 로그에서는 두 번 — 이중 패치 수정 P4 확인), 디스크 포화가 끝난 뒤(14:14) 다시 실행해 곡 선택까지 정상 진행: 초기화 207ms, 부트→로고 0.9초(11-18과 같은 속도), 곡 선택 진입 때 `[폰트] 곡 정보의 비ASCII 글자 19종: 게임 폰트 17종, 나머지 2종은 …` **한 줄만**(④ 확인, P12), `커스텀 곡 주입 완료: 5개 (총 59개)`, 제목 스크롤 진단 줄. 플레이·결과 화면(②③⑤⑥⑦)은 아직 안 봤다.
- 릴리즈: `GRC2/Properties/AssemblyInfo.cs`(AssemblyVersion·AssemblyFileVersion·MelonInfo)와 `CHANGELOG.md` 최상단 `## [버전]`을 같이 올리고 main에 머지한 뒤 `.\tools\release.ps1 -Publish`. 첨부는 `GRC2.dll` 하나.
- 2026-11-20 상수 계약 런타임화는 빌드·단위 테스트로만 확인했다. **미확인:** 인게임 시작 로그의 `[호환성] 점검 통과 (필드 23개, 상수 26개 확인)` 줄.
- 2026-11-18 제목 스크롤 인게임 확인: 곡 선택에서 `麦吉_Maggie x 盖盖Nyan - Summertime (Arrange ver.)`이 끝(`(Arrange ver.)`)까지 흘러 보임, 진단 로그 `[제목 스크롤] 대상 SongTitle: 넘침 모드 Overflow, … 가리는 부모 曲名`. **미확인:** 끝 대기 후 처음으로 돌아오는 반복과 짧은 제목 곡(변화 없어야 함)은 눈으로 보지 못했다(타이밍은 단위 테스트만).
- 2026-11-18 외부 MusicID 맵 제거: `UserData/GRC2/musicid_map.txt`, `savecustomkey/musicid_map.txt`, `MelonPreferences.cfg`의 `[GRC2_MusicID]`를 모두 쓰지 않고 시작할 때 자동으로 지운다. 배정은 메모리(`_sessionMap`)에서만 수행한다.
- 2026-11-18 인게임 확인: 초기화 146ms 로그, 기존 5곡 ID 54~58 유지, 임시 테스트 앨범 ID 600 → `세이브 곡 칸을 512 → 601개로 늘렸습니다`·곡 선택에서 `MusicID: 600`, "주입 완료" 로그 1회. **미확인:** ID 512 이상 곡을 끝까지 플레이해 결과 화면·세이브까지 가는 것, 게임을 켠 채 곡을 추가했을 때의 재스캔(단위 테스트만). 실제 세이브 배열은 이 확인으로 601칸이 됐다(무해, 근거는 3절 MusicID 항목).
- 폰트 fallback(2026-11-17)은 **곡 선택 화면에서만** 인게임 확인했다(`麦`·`盖` 정상 표시, 로그 `[폰트] fallback 폰트 5/5개 준비`). 로딩·플레이·결과 화면의 제목 표시는 같은 전역 목록을 쓰지만 아직 직접 보지 않았다. 게임 설치본이 기준 빌드와 다르면 `GameCompatibilityTests`가 먼저 실패한다.
- **인게임 검증은 아직 안 됐다.** 2026-10-04~10-08의 수정들과 2026-11-16의 호환성 점검은 빌드·단위 테스트로만 확인했다. 게임에서 곡 선택 → 플레이 → 결과 → 복귀를 돌려 보고, 로그의 `[호환성]` 줄이 "점검 통과"인지 확인한 뒤 이 문서의 "미확인"을 지운다.
- `fix/bms-parser-conversion`(BPM 채널·홀드 짝짓기·원본 로더 후처리 이식, H15)은 2026-11-16에 main에 병합했다. **인게임 확인 전**이므로 노트 타이밍·홀드 이상이 보이면 이 병합(`git log --merges`)부터 의심한다.
