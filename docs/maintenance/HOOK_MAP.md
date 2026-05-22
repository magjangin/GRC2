# GRC2 Hook Map

This document is the maintenance map for Harmony and MelonLoader entry points.
Every hook should have a clear owner, purpose, and removal condition.

## Entry Points

### `SceneDetector.OnInitializeMelon`

File: `GRC2/Core/Scene/SceneDetector.cs`

Main startup path. It:

- locks the Steam manifest;
- initializes music-list hooks through `MusicInjector.Initialize()`;
- locates the game `hwa` folder;
- scans albums, song metadata, custom assets, and BMS files;
- initializes BMS note conversion;
- initializes note-array replacement hooks;
- initializes BGM/BGA injectors.

### `SceneDetector.OnSceneWasLoaded`

File: `GRC2/Core/Scene/SceneDetector.SceneRouting.cs`

Scene routing path. It starts or stops custom BGM/BGA/artwork injection depending on
the loaded scene.

## Required Hook Groups

### Music select list injection

Owner files:

- `GRC2/Core/Bootstrap/MusicInjector.cs`
- `GRC2/Harmony/Hooks/MusicScrollView/*`

Patched game targets:

- `IntiCreates.cMusicSelectScrollViewDataGetter.getMusicTitle`
- `IntiCreates.cMusicSelectScrollView.initializeAllItemByCrrentMusicData`

Purpose:

- make custom albums appear in the music select list;
- inject custom items when the music select scroll view initializes its item list;
- register original artist first-song mappings used by custom artist handling;
- replace displayed titles for custom music ids.

Removal risk:

- custom songs may disappear from the music select UI.

### Selection and pre-play state tracking

Owner files:

- `GRC2/Harmony/Registration/AudioClipPatcher.cs`
- `GRC2/Harmony/Registration/SelectingMusicUIPatcher.cs`
- `GRC2/Harmony/Hooks/GameFlow/*`
- `GRC2/Harmony/Handlers/AudioClipPatch.cs`

Patched game targets:

- `IntiCreates.cMusicSelectSceneUIUpdater.noticeChangedMusic`
- `IntiCreates.cMusicSelectSceneUIUpdater.changeDifficulty`
- `IntiCreates.cMusicSelectSceneUIUpdater.startRythmGame`
- `IntiCreates.cMusicSelectSceneUIUpdater.coStartRythmGame`
- `IntiCreates.cMusicSelectSceneUIUpdater.coOpenPreMusicStartWindow`
- `IntiCreates.cMusicSelectSceneUIUpdater.backToPreScreen`
- `IntiCreates.cMusicSelectSceneUIUpdater.openSortWindow`
- `IntiCreates.cMusicSelectSceneUIUpdater.openFilterWindow`
- `IntiCreates.cMusicSelectSceneSelectingMusicUI.coOpen`
- `IntiCreates.cMusicSelectSceneSelectingMusicUI.coClose`

Purpose:

- detect the currently selected custom chart;
- update preview audio and artwork state;
- stop preview audio before gameplay;
- keep custom selection state consistent when returning or opening UI windows.

Removal risk:

- the mod may fail to know which custom chart is selected.

### Note array replacement

Owner files:

- `GRC2/Harmony/Hooks/NoteArray/NoteArrayHooks.cs`
- `GRC2/Harmony/Hooks/NoteArray/NoteArrayHooks.Patching.cs`
- `GRC2/Converters/BmsNote/*`
- `GRC2/Builders/*`
- `GRC2/Processors/*`

Patched game targets:

- `IntiCreates.cFairyModeNotesManager.createAllNote`
- `IntiCreates.cFairyModeNotesManager.loadFairyNoteDatasJsonToArray`

Purpose:

- convert parsed BMS notes into game `NoteCreateData` objects;
- replace `mFairyNoteCreateDataArray` before the game creates notes.

Removal risk:

- custom charts may load custom music but keep original note data.

### Cover, title, and text replacement

Owner files:

- `GRC2/Harmony/Registration/CoverImagePatcher.cs`
- `GRC2/Harmony/Registration/TextPatcher.cs`
- `GRC2/Harmony/Handlers/ArtWorkPatch.cs`
- `GRC2/Harmony/Handlers/MusicTitlePatch.cs`
- `GRC2/Harmony/Handlers/TextPatch.cs`

Patched game targets:

- `IntiCreates.cMusicSelectArtWork.requestSetArtworkSprite`
- UI text setters discovered by `TextPatcher`
- `IntiCreates.cMusicSelectScrollViewDataGetter.getMusicTitle`

Purpose:

- display custom title and artwork instead of the original song assets.

Removal risk:

- gameplay may still function, but UI may show original song data.

### BGM and game-end timing

Owner files:

- `GRC2/Injectors/Bgm/BgmInjector.cs`
- `GRC2/Injectors/Bgm/BgmInjectorHooks.cs`
- `GRC2/Injectors/Bgm/BgmMethodCallHooks.cs`
- `GRC2/Injectors/GameEnd/*`

Patched game targets:

- `IntiCreates.cBGMBeatManager.setClip`
- `IntiCreates.cBGMBeatManager.requestLoadBGM`
- `IntiCreates.cBGMBeatManager.requestPlayAudio`
- `IntiCreates.cBGMBeatManager.requestPause`
- `IntiCreates.cBGMBeatManager.setBPM`
- `IntiCreates.cBGMBeatManager.setSample`
- `IntiCreates.cBGMBeatManager.setTime`
- `IntiCreates.cBGMBeatManager.getAudioClip`
- `IntiCreates.cBGMBeatManager.isReadyPlay`
- `IntiCreates.cRythmGameManager.requestCommonRythmGameEnd`
- `IntiCreates.cRythmGameManager.coMonitorGameEnd`
- `IntiCreates.cRythmGameManager.coClearRythmGameEnd`

Purpose:

- replace gameplay BGM;
- keep custom track timing from ending too early;
- adjust result/game-end behavior for custom audio length.

Removal risk:

- custom audio may not play correctly or the song may end at the original timing.

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

### 2026-05-15

- Moved project documentation into `docs/` by topic.
- Added root `README.md` and `docs/README.md` as the current documentation entry points.
- Updated this hook map to reflect the post-cleanup music-scroll hook surface.
