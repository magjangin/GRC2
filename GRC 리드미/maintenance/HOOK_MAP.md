# GRC2 Hook Map

This document is the maintenance map for Harmony and MelonLoader entry points.
Every hook should have a clear owner, purpose, and removal condition.

## Entry Points

### `SceneDetector.OnInitializeMelon`

File: `GRC2/Core/Scene/SceneDetector.cs`

Main startup path. It:

- applies every `[HarmonyPatch]` in the mod assembly through `MusicInjector.Initialize()`;
- locates the game `hwa` folder;
- scans albums, song metadata, custom artwork, and BMS files;
- supplies parsed BMS data to the already-registered note-array hook;
- initializes BGM/BGA injectors.

### `SceneDetector.OnSceneWasLoaded`

File: `GRC2/Core/Scene/SceneDetector.cs` (scene-routing section)

Scene routing path. It starts or stops custom BGM/BGA/artwork injection depending on
the loaded scene.

## Required Hook Groups

### Music select list injection

Owner files:

- `GRC2/Core/Bootstrap/MusicInjector.cs`
- `GRC2/Harmony/Hooks/MusicScrollViewHooks.cs`

Patched game targets:

- `IntiCreates.cMusicSelectScrollView.initializeMusicDataByDefault` (postfix)

Purpose:

- make custom albums appear in the music select list;
- inject custom items after the game rebuilds its default list and before its
  normal filter/sort pipeline runs;
- register original artist first-song mappings used by custom artist handling;
- provide a stable original-song template for the pre-play window.

Removal risk:

- custom songs may disappear from the music select UI.

### Selection and pre-play state tracking

Owner files:

- `GRC2/Harmony/Hooks/GameFlowHooks.cs`
- `GRC2/Harmony/Handlers/AudioClipPatch.cs`

Patched game targets:

- `IntiCreates.cMusicSelectSceneUIUpdater.noticeChangedMusic`
- `IntiCreates.cMusicSelectSceneUIUpdater.startRythmGame`
- `IntiCreates.cMusicSelectSceneUIUpdater.coOpenPreMusicStartWindow`
- `IntiCreates.cMusicSelectSceneUIUpdater.backToPreScreen`
- `IntiCreates.cMusicSelectPreMusicStartWindowManager.requestOpenWindow`

Purpose:

- detect the currently selected custom chart;
- mute the `mPreviewAudioSorce` and `mAmbientAudioSorce` fields owned by the
  active `cMusicSelectSceneUIUpdater`;
- stop preview audio before gameplay;
- map a custom id to a valid original song only while the game opens its
  pre-play window;
- replace the artwork after the real pre-play window has opened.

`PreviewAudioManager` mutes by stopping the source, zeroing its volume, and
clearing `clip` (never `.mute = true`). `sSoundManager2D` pools every
`AudioSource` across BGM, ambient, and judge SE, and reclaims a slot once its
`clip` is `null`; leaving `clip` set would strand the slot, and leaving
`.mute = true` would silently mute whatever unrelated sound the pool later
hands that slot to, since nothing in the original assembly ever clears
`.mute` back to `false`. The saved volume/clip pair is restored on
`RestoreMutedAudioSources()`.

Removal risk:

- the mod may fail to know which custom chart is selected;
- reverting the mute path to `.mute = true` reintroduces silent, unrelated
  sounds (including judge SE) whenever the pool later reuses that slot.

### Note array replacement

Owner files:

- `GRC2/Harmony/Hooks/NoteArrayHooks.cs`
- `GRC2/Converters/BmsNoteConverter.cs`
- `GRC2/Builders/*`
- `GRC2/Processors/*`

Patched game targets:

- `IntiCreates.cFairyModeNotesManager.createAllNote`

Purpose:

- convert parsed BMS notes into game `NoteCreateData` objects;
- replace `mFairyNoteCreateDataArray` before the game creates notes.

Removal risk:

- custom charts may load custom music but keep original note data.

### Cover, title, and text replacement

Owner files:

- `GRC2/Harmony/Handlers/ArtWorkPatch.cs`
- `GRC2/Harmony/Handlers/TextPatch.cs`

Patched game targets:

- `IntiCreates.cMusicSelectArtWork.requestSetArtworkSprite`
- `UnityEngine.UI.Text.set_text`
- `TMPro.TMP_Text.set_text`

Purpose:

- display custom title and artwork instead of the original song assets.

Removal risk:

- gameplay may still function, but UI may show original song data.

### BGM and game-end timing

Owner files:

- `GRC2/Injectors/Bgm/BgmInjector.cs`
- `GRC2/Injectors/GameEnd/BgmGameEndMonitor.cs`

Patched game targets:

- `IntiCreates.cRythmGameManager.coMonitorGameEnd`

Purpose:

- replace gameplay BGM;
- keep custom track timing from ending too early;
- wrap the original game-end coroutine so the game's score, clear animation,
  fade, and scene transition remain intact.

Removal risk:

- custom audio may not play correctly or the song may end at the original timing.

### Result scene replacement

Owner file:

- `GRC2/Harmony/Handlers/ResultSceneUpdaterPatch.cs`

Patched game target:

- `IntiCreates.cRythmGameResultSceneUpdater.initializePreFade`

Purpose:

- replace the result title, difficulty level, and artwork at the updater's real
  initialization point.

Removal risk:

- the result screen may show the original song metadata and artwork.

### Steam and DLC bypass

Owner file:

- `GRC2/Helpers/SteamApiHijacker.cs`

Patched targets:

- `Steamworks.SteamAPI` initialization/lifecycle methods;
- `Steamworks.SteamApps.BIsDlcInstalled`;
- `IntiCreates.Application.isDLCEnable`;
- `IntiCreates.sAddressableDirector` DLC checks;
- `IntiCreates.cDlcDirector` purchase check and initialization.

Purpose:

- preserve the existing Steam fallback and local `DataAddon` mount behavior.

Removal risk:

- startup without Steam or local DLC asset mounting may stop working.

### BGA and BGM sync

Owner files:

- `GRC2/Injectors/Shared/BgmBgaInjector.cs`
- `GRC2/Injectors/Bga/*`

Runtime entry:

- `BgmBgaInjector.StartInjection()`
- `BgmBgaInjector.StopInjection()`

Purpose:

- load custom video and audio assets;
- start injection only in play scenes;
- sync BGA playback with the current BGM time.

Removal risk:

- custom video may not play, or audio/video may drift.

## Review Candidates

No active review candidates are listed here yet. Add candidates only when the
specific file, risk, and removal condition are known.

## Removed Diagnostic Code

The following names may still appear in archived documents, but they are not part
of the current source baseline:

- `HarmonyHookManager`
- `BgaVideoHooks`
- `FairyModeNotesManagerPatcher`
- `AssemblySearcher`
- `GameTypeInspector`
- `GameFlowDebugger`
- `MusicInjectionDebugger`
- `NoteArrayJsonDumper`
- `ProcessorDebugHarness`
- `CharactorLoadPatcher`
- `MusicTitlePatch`
- `CustomChartHandler`
- `BgmAudioStateChecker`
- `BgmFormattingUtils`
- `BgmMethodCallHooks`
- `BgmMonitorCoroutine`

## Cleanup Log

### 2026-05-12

- Removed the no-op `FairyModeNotesManagerPatcher` registration and source file.
- Reduced `NoteArrayHooks` to the two note-array hooks that actually inject BMS data.
- Removed no-op note-array hook registrations for `createNote`, `addFairyNoteCreateDataArray`, and `updateFromSample`.
- Removed `HarmonyHookManager`, which broadly patched BGA end and pause/stop methods with no-op prefixes.
- Removed unused diagnostic `BgaVideoHooks`.
- Removed development-only inspection/dump helpers: `AssemblySearcher`, `GameTypeInspector`, `GameFlowDebugger`, `MusicInjectionDebugger`, `NoteArrayJsonDumper`, and `ProcessorDebugHarness`.
- Simplified `NoteArrayHooks.Initialize` by removing unused `hwaFolderPath` and `debugMode` parameters.
- Removed disabled sort/filter/update/get-cell music-scroll logging hooks.
- Reduced `CharactorLoadPatcher` to the dynamic prefix factory still used by `AudioClipPatcher`.

### 2026-07-21

- Merged all partial-class file sets into one file per class and removed the empty
  `NoteArrayHooks.MusicDataAdjust.cs`.
- Flattened single-class folders (`Hooks/GameFlow/`, `Hooks/MusicScrollView/`,
  `Hooks/NoteArray/`, `Handlers/PreviewAudio/`, and similar folders outside `Harmony/`).
- Merged the six `Harmony/Registration/*Patcher.cs` files into
  `Harmony/Registration/Patchers.cs`; class names and hook behavior are unchanged.

### 2026-07-26

- Reduced every hook-focused source file over 500 lines below that threshold.
- Moved music-list injection to the default-list postfix so the game's normal
  filtering and sorting still run.
- Removed unregistered, logging-only, and no-op selection/BGM hooks.
- Replaced the game-end prefix override with a postfix coroutine wrapper that
  preserves the original end-of-song flow.
- Removed the unreachable character-load/title/custom-chart helpers and the
  obsolete BGM diagnostic helper cluster.
- Added a direct non-copying `Assembly-CSharp.dll` project reference.
- Replaced delayed reflection registration and every manual `Harmony.Patch(...)`
  call with `[HarmonyPatch]` declarations and one `PatchAll()` startup call.
- Removed `Harmony/Registration/Patchers.cs` and
  `Injectors/Shared/PatchApplier.cs`.
- Removed the invalid
  `cFairyModeNotesManager.loadFairyNoteDatasJsonToArray` target; that method
  belongs to `FairyNoteEditorLoader`, while note injection is correctly owned by
  the `createAllNote` prefix.
- Removed `SceneHandler.cs`; its `cSoundManager` target does not exist in the
  current `Assembly-CSharp.dll`, and play-scene preview cleanup already belongs
  to `SceneDetector`.
- Removed the polling `ResultSceneInjector.cs`; its async artwork preparation is
  now retained inside the direct `initializePreFade` patch.
- Removed `ReflectionHelper.cs` and `GameTypeSearcher.cs`; their remaining
  targets are direct compile-time `Assembly-CSharp` references.
- Replaced dynamic note type discovery with direct mappings to
  `FairyNoteEditorLoader.NoteCreateData` and the actual game enums.
- Made `StopInjection()` reset BGM/BGA injection state so the next song can be
  injected after leaving the result scene.
- Removed every remaining string-based member lookup from the hook surface.
  Private game fields are now reached through cached
  `AccessTools.FieldRefAccess` delegates created once per field, and public
  members are called directly:
  - `cMusicSelectScrollView.mCellHaviableMusicDataList` (music list injection)
  - `cFairyModeNotesManager.mFairyNoteCreateDataArray` (note array replacement,
    also used for the last-note time)
  - `cMusicSelectSceneUIUpdater.mPreviewAudioSorce` / `mAmbientAudioSorce`
    (preview muting) and `mArtWorkAndMusicDetail` -> `mArtWork` (artwork)
  - `cRythmGameResultSceneUpdater.mSceneInitializeParam`, `mMusicLVUI`,
    `mMusicNameText`, `mArtWorkImage` (result scene)
  - `cRythmGameManager.mPauseMenuWork` and `mRythmGameMusicData`; `mIsPausing`
    and `requestPause()` are public and used directly
  - `cMusicSelectPreMusicStartWindowManager.mArtworkImage`
- Removed `NoteConstructorHelper.cs`, `Helpers/FieldAccessHelper.cs`, and
  `Loaders/GameTypeLoader.cs`; removed `BgmLoader`'s `_sorce` fallback path
  (unreachable because `cBGMBeatManager.setClip` always exists).
- Every Harmony patch method now receives `__instance` as its concrete game
  type instead of `object`.

### 2026-05-15

- Moved project documentation into `docs/` by topic.
- Added root `README.md` and `docs/README.md` as the current documentation entry points.
- Updated this hook map to reflect the post-cleanup music-scroll hook surface.
