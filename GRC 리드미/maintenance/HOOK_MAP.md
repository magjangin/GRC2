# GRC2 Hook Map

This document is the maintenance map for Harmony and MelonLoader entry points.
Every hook should have a clear owner, purpose, and removal condition.

## Entry Points

### `SceneDetector.OnInitializeMelon`

File: `GRC2/Core/Scene/SceneDetector.cs`

Main startup path. It:

- applies every `[HarmonyPatch]` in the mod assembly through `SceneDetector.InitializeHarmony()`;
- locates the game `hwa` folder;
- scans albums, song metadata, custom artwork, and BMS files;
- supplies parsed BMS data to the already-registered note-array hook;
- initializes BGM/BGA injectors.

### `SceneDetector.OnSceneWasLoaded`

File: `GRC2/Core/Scene/SceneDetector.cs` (scene-routing section)

Scene routing path. It starts or stops custom BGM/BGA/artwork injection depending on
the loaded scene.

The Music Select scene's actual Unity scene file name is `MusicSelectScene_Hasegawa`,
not `MusicSelectScene` (confirmed from `Latest.log`). The scene-name check here uses
`StartsWith("MusicSelectScene")` rather than an exact match for that reason. An exact
match previously meant this branch never ran, so returning to Music Select from the
pause menu mid-play (`cRythmGameManager.backToMusicSelectScene`, which loads
`SceneId.MusicSelect` directly and does not pass through `RythmGameResultScene`) never
called `BgmBgaInjector.ResetPlaySceneState()`. `_isPlayScene` stayed `true`, so
`TextPatch.IsPlayOrLoadingScene()` (which trusts `BgmBgaInjector.IsPlayScene()`) and
the scene-routing fallback's `PlaySceneArtworkInjector.StartArtworkInjection()` both
kept treating Music Select as a play scene and kept forcing the just-played custom
chart's title/artwork onto whatever the scene displayed, regardless of which song was
actually selected.

Removal risk: reverting to an exact `"MusicSelectScene"` match reintroduces the stuck
`_isPlayScene` state and the title/artwork bleed-through described above whenever
Music Select is reached by any path other than the result scene.

## Required Hook Groups

### Music select list injection

Owner files:

- `GRC2/Core/Scene/SceneDetector.cs` (`InitializeHarmony`)
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
- `IntiCreates.cMusicSelectSceneUIUpdater.setCurrentSelectDataToGameData`
- `IntiCreates.cMusicSelectPreMusicStartWindowManager.requestOpenWindow`
- `IntiCreates.soRythmGameMusicDataMap.getIsUsableMusicID`

Purpose:

- detect the currently selected custom chart;
- mute the `mPreviewAudioSorce` and `mAmbientAudioSorce` fields owned by the
  active `cMusicSelectSceneUIUpdater`;
- stop preview audio before gameplay;
- map a custom id to a valid original song only while the game opens its
  pre-play window;
- replace the artwork after the real pre-play window has opened;
- fix up `lastPlayedMusicID`/`isNew` after the original write, and make
  `getIsUsableMusicID` accept registered custom ids (see below).

`coOpenPreMusicStartWindow` needs a real `MusicID` to look up `MusicData`, so
its prefix temporarily points `mCurentMusicId` at the selected artist's real
first song ("template song") for the rest of the pre-play flow, and nothing
in the original game reverts that swap. This means `startRythmGame`'s and
`backToPreScreen`'s calls to `setCurrentSelectDataToGameData(...)` write
`lastPlayedMusicID` and `playerMusicData[(int)mCurentMusicId].isNew` against
the template song instead of the custom chart. A prefix/postfix attempt to
swap `mCurentMusicId` back to the real custom id only for that one call
caused the game to hang on the pre-game cut-in scene (`_isPlayScene` never
flipped to `true`, so `BgmBgaInjector` polled forever) and was reverted.

The fix instead leaves `mCurentMusicId` alone and patches around it:

- a `setCurrentSelectDataToGameData` **postfix** re-reads
  `AlbumManager.GetCurrentMusicID()` and overwrites the just-written
  `lastPlayedMusicID` (and, on `isRhythmGameStart`, `playerMusicData[id].isNew`)
  with the real custom id, so the wrong write never reaches disk;
- but custom ids have no entry in `mMusicDataList`, so
  `getIsUsableMusicID` always returns `false` for them, and
  `initializePreDataLoad`'s `getMusicIDUsable(lastPlayedMusicID)` call falls
  back to `MusicID.FIRST_VER_DATA_TOP` on the next Music Select entry even
  after the save fix above — a `getIsUsableMusicID` **postfix** returns `true`
  for ids `AlbumManager.IsCustomChartMusicID` recognizes (outside scenes where
  injection is disallowed), so the scene's own
  `getNeedsScrollCountUntilID`-based selection logic finds and scrolls to the
  actual injected cell unmodified.

Removal risk of the two new postfixes: cursor/highscore attribution reverts
to landing on the borrowed template song instead of the custom chart when
returning to Music Select, and result-scene high scores/clear badges may
again be written against the template song's save slot.

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
  initialization point;
- **prefix**: `initializePreFade` reads `sceneInitParam.musicData.id` (still the
  "template song" id borrowed by `coOpenPreMusicStartWindow`, see above) to
  index `playerMusicData[]`, compare/update `highScoreArray`/`playFlagArray`,
  and call `mSaveDirector.setDirty()` — all before the postfix below ever
  runs. The prefix rewrites `sceneInitParam.musicData.id` to the real custom
  id so the score/clear-badge read-compare-write targets the custom chart's
  own save slot instead of overwriting an unrelated real song's saved score.

Removal risk:

- the result screen may show the original song metadata and artwork;
- removing the prefix makes custom chart results silently overwrite the
  high score/clear badge of whichever real song was borrowed as the
  template.

### AutoPlay / judge manipulation / record blocking

Owner files:

- `GRC2/Core/CustomKey/CustomKeySettings.cs`
- `GRC2/Harmony/Handlers/AutoPlayPatch.cs`
- `GRC2/Harmony/Handlers/JudgePerfectPatch.cs`
- `GRC2/Harmony/Handlers/RecordBlockPatch.cs`

Patched game targets:

- `IntiCreates.cFairyModeNotesManager.createAllNote` (also owned by note-array
  replacement above; forces `mIsCurrentAutoPlay` when AutoPlay is enabled,
  since that field is set directly from `InitializeParam.isAutoPlay` in
  `coInitialize` and never goes through `setIsAutoPlay`)
- `IntiCreates.cFairyModeNotesManager.setIsAutoPlay`
- `IntiCreates.cNotecWorkBase.onJudgeMent` (every note-work subclass override
  calls `base.onJudgeMent(judgeParam)` first, so patching the base method
  alone covers score/combo reporting, sound, and visual effects for all note
  types)
- `IntiCreates.cRythmGameResultSceneUpdater.initializePreFade`
- `IntiCreates.cRythmGameResultSceneUpdater.coUpdateResultAnim`
- `IntiCreates.sSaveDataDirector.requestGameDataSaveToFile`

Purpose:

- ported from the standalone `GRC auto` and `GRC judge` MelonLoader mods,
  reimplemented against compile-time decompiled types (the originals used
  runtime reflection/string-based member lookup because they predated the
  direct `Assembly-CSharp.dll` reference);
- `AutoPlayPatch`/`JudgePerfectPatch` are off by default; `autoplay_enabled`
  and `judge_perfect_enabled` in `savecustomkey/config.txt` (created next to
  the `hwa` folder on first launch) are read once in `OnInitializeMelon` and
  are the only way to turn them on — there is no in-game toggle key, so a
  config edit needs a game restart to take effect;
- `RecordBlockPatch` snapshots `playerMusicData[id].{highScoreArray,
  maxComboArray, playCountArray, playFlagArray}` for the played difficulty
  before `initializePreFade` runs and restores them afterward, zeroes the
  displayed old high score, and makes `requestGameDataSaveToFile` a no-op —
  all only while `AutoPlayPatch.IsEnabled || JudgePerfectPatch.IsEnabled` is
  true, so cheated play sessions never reach the save file.
- `RecordBlockPatch` resolves the played `MusicID` itself
  (`CustomAssetManager.IsCustomChartSelected()` +
  `AlbumManager.GetCurrentMusicID()`) instead of reading
  `sceneInitParam.musicData.id` directly, so its snapshot/restore does not
  depend on Harmony prefix ordering relative to
  `ResultSceneUpdaterPatch.InitializePreFadePrefix` (which rewrites that same
  field for the borrowed-template-song fix documented above).

Removal risk:

- removing `AutoPlayPatch`/`JudgePerfectPatch` only removes the cheat
  features; removing `RecordBlockPatch` while keeping the other two would let
  AutoPlay/judge-forced results write real best scores/clear badges to the
  save file.

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
