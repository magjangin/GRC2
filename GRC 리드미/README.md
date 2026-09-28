# GRC2 Documentation

This folder is the current home for project documentation. Documents are grouped by maintenance use, not by original generation date.

## Start Here

- [Current Hook Map](maintenance/HOOK_MAP.md): current Harmony and MelonLoader hook ownership.
- [알려진_문제.md](maintenance/알려진_문제.md): issues found in the 2026-09-28 review that are not fixed yet
  (read section A before reinstalling the game or moving to a new PC).
- [게임_코드_분석.md](architecture/게임_코드_분석.md): topic index for every current document.
- [BMS chart rules](bms/BMS_파싱_및_변환_로직_가이드.md): what a chart, BGM, and `info.txt` must look like.
- [Harmony Layer README](../GRC2/Harmony/README.md): source-folder-level guide for `GRC2/Harmony`.
- [Legacy README](archive/README_legacy.md): older comprehensive notes kept for reference only.

`HOOK_MAP.md` is the source of truth for hook ownership. As of 2026-09-28 every
document outside `archive/` was rewritten against the current source and
`Decompiled/`. The mod uses compile-time typed access to `Assembly-CSharp`;
private fields go through `AccessTools.FieldRefAccess`, and the only
reflection-based method lookup is `cRythmGameManager.setPauseButtonPusable`
(`AccessTools.MethodDelegate` in `SceneDetector`).

## Folders

### `architecture`

Game and mod structure notes:

- `게임_아키텍처_개요.md`
- `게임_코드_분석.md`
- `게임_클래스_구조.md`
- `게임_플로우_및_메서드.md`
- `씬_관리_및_라이프사이클.md`

### `systems`

Runtime systems and user-visible mod behavior:

- `앨범_관리_시스템_분석.md`
- `커스텀_곡_주입_시스템_분석.md`
- `커스텀_에셋_로딩_시스템.md`
- `BGM_BGA_관리_시스템.md`
- `텍스트_패치_시스템_분석.md`
- `아티스트_ID_기반_시스템_분석.md`
- `터치_판정_영역_시스템_분석.md`

### `bms`

BMS parsing, note conversion, and note processing:

- `BMS_파서_내부_구조_분석.md`
- `BMS_파싱_및_변환_로직_가이드.md`
- `노트_생성_및_변환_파이프라인.md`
- `홀드_노트_처리_가이드.md`

### `harmony`

Harmony patching and game enum notes:

- `Harmony_패칭_시스템_상세_가이드.md`
- `Enum_및_타입_시스템_관리.md`

### `maintenance`

Current maintenance references, known issues, cleanup history, and timing:

- `HOOK_MAP.md`
- `알려진_문제.md`
- `게임_종료_로직.md`
- `게임_종료_시간_조정_가이드.md`
- `코루틴_및_비동기_처리_패턴.md`

### `archive`

Historical or pre-cleanup documents. These may mention removed code such as `HarmonyHookManager`, `BgaVideoHooks`, `GameTypeInspector`, `ReflectionHelper`, `FieldAccessHelper`, or `NoteArrayJsonDumper`.

Moved here on 2026-09-28 because they only describe the removed reflection layer:
`리플렉션_및_필드_접근_시스템.md`, `성능_분석_및_최적화_권장사항.md`,
`성능_최적화_기법_종합_가이드.md`, `최적화_완료_보고서.md`.

Use archive documents only when investigating old decisions.

## Current Cleanup Baseline

As of 2026-07-21:

- Deleted no-op and diagnostic hook files.
- Removed disabled note-array JSON dumping and field inspection helpers.
- Removed disabled music-scroll sort/filter/update/get-cell logging hooks.
- Removed the unreachable `CharactorLoadPatcher` and its unregistered dynamic-prefix path.
- Removed dead, never-called files: `AssetLoader.cs`, `BgmArtworkUpdater.cs`, and the `AudioSourceFinder` cluster (4 files).
- Removed orphaned XML doc comments left behind by earlier partial-class splits (`MusicScrollViewHooks.cs`, `BgmGameEndMonitor.cs`, `PreviewAudioManager.cs`, `BgmLoader.cs`, `HoldNoteProcessor.cs`).
- Current managed source count is 108 files under `GRC2/`, excluding `bin/obj` (`GRC2.Tests`: 2 files).

As of 2026-07-21 (file consolidation):

- Merged every partial-class file set into a single file per class (16 classes, 61 files -> 16 files); deleted the empty `NoteArrayHooks.MusicDataAdjust.cs`.
- Flattened folders that held only one merged class file (e.g. `Harmony/GameFlow/` -> `Harmony/GameFlowHooks.cs`).
- Merged the six `Harmony/Registration/*Patcher.cs` files into `Harmony/Registration/Patchers.cs` (class names unchanged).
- Current managed source count is 51 files under `GRC2/`, excluding `bin/obj` (`GRC2.Tests`: 2 files).

As of 2026-07-26 (v0.2.0 Refactoring & Performance Update):

- **Hook Cleanup**: Removed unregistered, empty, and logging-only hooks (Music Select, Sort, Filter, BGM state monitoring). Removed 7 unused diagnostic and helper files (`CharactorLoadPatch.cs`, `MusicTitlePatch.cs`, `CustomChartHandler.cs`, `BgmAudioStateChecker.cs`, `BgmFormattingUtils.cs`, `BgmMethodCallHooks.cs`, `BgmMonitorCoroutine.cs`). Registered only required Harmony patches. Wrapped original game-end coroutine instead of replacing it. Reduced key file line counts: `MusicScrollViewHooks.cs` (~390 lines), `GameFlowHooks.cs` (~160 lines), `Patchers.cs` (243 lines), `BgmGameEndMonitor.cs` (164 lines).
- **Custom Song Select Performance Optimization**: Removed scene-wide `AudioSource` searches, targeting preview and ambience sources directly. Replaced synchronous cover image file reading & main-thread decoding with async loading and a 12-image cache. Added 80ms image debounce and 150ms preview BGM debounce on fast scroll. Added cancellation of in-flight audio requests on song change. Applied streaming load & 3-song cache for preview BGM. Reused preview `GameObject` and `AudioSource`. Added duplicate `MusicID` check.
- **Ruby Text Fix**: Cleared `songTitleRuby` and `songTitleRubyDirect` fields to eliminate small text above custom titles while maintaining full title display and sorting functionality.

As of 2026-07-26 (Harmony automatic patch migration):

- Added direct compile-time references to `Assembly-CSharp.dll` and the
  Steamworks managed assembly without copying them to fresh build output.
- Replaced delayed reflection registration and manual `Harmony.Patch(...)`
  calls with `[HarmonyPatch]` declarations and one `PatchAll()` startup call.
- Removed `Harmony/Registration/Patchers.cs` and
  `Injectors/PatchApplier.cs`.
- Removed obsolete runtime type search and duplicate scene injection paths after
  validating their owners against `Decompiled/`: `ReflectionHelper.cs`,
  `GameTypeSearcher.cs`, `SceneHandler.cs`, and `ResultSceneInjector.cs`.
- Bound preview audio to `cMusicSelectSceneUIUpdater`, result UI to
  `initializePreFade`, BGM sync to `cBGMBeatManager`, and note types to direct
  `Assembly-CSharp` types.
- Current managed source count is 45 files under `GRC2/`, excluding `bin/obj`
  (`GRC2.Tests`: 2 files).

As of 2026-07-26 (reflection removal / lightweighting):

Every remaining string-based member lookup was replaced with compile-time typed
access, validated field by field against `Decompiled/`. The mod now contains a
single reflection call in total.

- Removed dead fallback paths that the decompiled source proved unreachable:
  `NoteConstructorHelper.cs` (`NoteCreateData` has no explicit constructor, so
  all 8 signature probes failed on every note before falling back to
  `Activator`), writes to the nonexistent `mSample`/`sample` fields, and
  `BgmLoader`'s `_sorce` fallback for a missing `setClip`.
- Note pipeline: deleted `FieldAccessHelper.cs` and `Loaders/GameTypeLoader.cs`;
  reduced `EnumValueHelper.cs` to typed mapping switches (no `Enum.Parse`, no
  value cache). `NoteCreateData` fields are now assigned directly, and the
  pipeline currency changed from `object` to `NoteCreateData`. Reverse mapping
  no longer parses `ToString()` output.
- BGM layer: `cBGMBeatManager`'s public methods (`setClip`, `getAudioClip`,
  `getAudioSorce`, `requestPlayAudio`) are called directly; two `GetFields()`
  sweeps were removed.
- UI/scene hooks: private game fields are reached through cached
  `AccessTools.FieldRefAccess` delegates (`mCellHaviableMusicDataList`,
  `mFairyNoteCreateDataArray`, `mPreviewAudioSorce`, `mArtWorkImage`, etc.).
  `MusicSelectData` is a struct, so the `MemberwiseClone` reflection became a
  plain assignment.
- The only remaining reflection at the time was `AudioClip.m_Name` in
  `BgmLoader.cs`. (Corrected 2026-08-09: that lookup was always null and was
  replaced with `audioClip.name`; see HOOK_MAP.)
- `PlaySceneArtworkInjector`'s name-based lookup is intentionally kept: no
  decompiled type owns the play-scene artwork object, and the result is cached
  per scene.
- Managed source count at that point: 42 files (~6,600 lines) under `GRC2/`,
  excluding `bin/obj` (`GRC2.Tests`: 2 files).

As of 2026-09-28 (documentation pass, no source changes):

- Rewrote every non-archive document against the current source and
  `Decompiled/`, moved four reflection-era documents to `archive/`, and added
  `maintenance/알려진_문제.md`. Details are in the HOOK_MAP cleanup log.
- Current managed source count is 43 files under `GRC2/` (including
  `Properties/AssemblyInfo.cs`), excluding `bin/obj`; `GRC2.Tests` has 3 files
  and 32 passing tests (`dotnet test GRC2.Tests\GRC2.Tests.csproj`).
