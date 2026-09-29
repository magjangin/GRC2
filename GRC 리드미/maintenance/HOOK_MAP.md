# GRC2 Hook Map

This document is the maintenance map for Harmony and MelonLoader entry points.
Every hook should have a clear owner, purpose, and removal condition.

## Entry Points

### `SceneDetector.OnInitializeMelon`

File: `GRC2/Core/SceneDetector.cs`

Main startup path. It:

- applies every `[HarmonyPatch]` in the mod assembly through `SceneDetector.InitializeHarmony()`;
- locates the game `hwa` folder;
- scans albums, song metadata, custom artwork, and BMS files;
- supplies parsed BMS data to the already-registered note-array hook;
- initializes BGM/BGA injectors.

### `SceneDetector.OnSceneWasLoaded`

File: `GRC2/Core/SceneDetector.cs` (scene-routing section)

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

### `SceneDetector.OnUpdate`

File: `GRC2/Core/SceneDetector.cs`

Runs every frame after initialization. It:

- calls `CustomKeySettings.PollFileChange()` once a second (log only, see the
  config reload section below);
- in play scenes (`BgmBgaInjector.IsPlayScene()`), handles **Space/Esc** as a pause
  toggle: if the pause menu is open and active it calls
  `cRythmGamePauseMenuHud.requestPushContinueButton()`, otherwise it forces the
  private `cRythmGameManager.setPauseButtonPusable(true)` and calls the public
  `requestPause()`. `setPauseButtonPusable` is bound once through
  `AccessTools.MethodDelegate`, which is the mod's only reflection-based method
  lookup; `mPauseMenuWork` is read through `FieldRefAccess`.

Removal risk: keyboard pause/resume in play scenes stops working.

### `SceneDetector.OnGUI`

File: `GRC2/Core/SceneDetector.cs` → `GRC2/Core/GameHud.cs`

Draws the judgment bar with IMGUI in play scenes only (see the GameHud section below).

## Required Hook Groups

### Music select list injection

Owner files:

- `GRC2/Core/SceneDetector.cs` (`InitializeHarmony`)
- `GRC2/Harmony/MusicScrollViewHooks.cs`

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

- `GRC2/Harmony/GameFlowHooks.cs`
- `GRC2/Harmony/AudioClipPatch.cs`

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

- `GRC2/Harmony/NoteArrayHooks.cs`
- `GRC2/Converters/BmsNoteConverter.cs`
- `GRC2/Builders/*`
- `GRC2/Processors/*`

Patched game targets:

- `IntiCreates.cFairyModeNotesManager.createAllNote`

Purpose:

- convert parsed BMS notes into game `NoteCreateData` objects;
- replace `mFairyNoteCreateDataArray` before the game creates notes.

`createNote` dereferences `createData.connectNodeDataArray[0]` with no null
check for both `NoteTypeId.Fairy` and `NoteTypeId.Hold`, so any such note that
reaches the array without an end note attached crashes `createAllNote`.
`BmsNoteConverter` guards this in two places, and both are needed:

- `CheckMissingEndNotes` works on the parsed BMS notes and catches charts that
  are missing 19 / 1A / 1B entries outright;
- `CheckIncompleteConnectedNotes` works on the final `NoteCreateData` list and
  catches what the first one structurally cannot see — fairy starts whose
  `Duration` stayed `0` because no partner was ever matched (the BMS-level check
  filters on `Duration > 0`), and notes that passed the BMS check but lost their
  end during `HoldNoteProcessor`/`FairyNoteProcessor` matching.

Both cancel the whole injection and return `null`; `NoteArrayHooks` then leaves
`mFairyNoteCreateDataArray` alone, so the original chart plays under the custom
BGM/BGA instead of the game crashing. Nothing is shown in-game when this
happens; the only notice is in the log ([알려진_문제.md](알려진_문제.md) H2).

`createAllNote` is **not** only a play-scene call. The Music Select scene's
option preview window (`cMusicSelectPreviewWindowManager.coUpdateNote`) calls
`loadNoteData4OptionPreview` and then `requestSetCreatableCreateData(false)`
(which calls `createAllNote`) on open and again at every loop point. The hook
only checks `ShouldInjectCustomContent()` (custom chart selected, not in
`SoundPlayerScene`/`MoviePlayer_MovieSelect`), so it also replaces the preview's
note array with the BMS chart there — and at that point
`ReloadCurrentAlbumAssets` has not run yet, so the chart is the previously
loaded album's, not the selected one. Log evidence (2026-08-03 build,
`MelonLoader/Logs/26-8-3_9-50-11.log` lines 318–341): two conversions in Music
Select of the 361-note startup chart while the selected album had 362 notes.
See [알려진_문제.md](알려진_문제.md) H1.

Removal risk:

- custom charts may load custom music but keep original note data;
- removing either check lets a `Fairy`/`Hold` note with an empty
  `connectNodeDataArray` reach `createAllNote`, which is an immediate
  `NullReferenceException` during song load.

### Cover, title, and text replacement

Owner files:

- `GRC2/Harmony/ArtWorkPatch.cs`
- `GRC2/Harmony/TextPatch.cs`
- `GRC2/Harmony/AudioClipPatch.cs` (`ArtworkUpdater`, not a patch: applies an
  asynchronously loaded sprite through `mArtWorkAndMusicDetail.mArtWork`)
- `GRC2/Core/PlaySceneArtworkInjector.cs` (not a patch: name-based `ArtWork`
  image lookup in play scenes, started from `SceneDetector`)

Patched game targets:

- `IntiCreates.cMusicSelectArtWork.requestSetArtworkSprite` (prefix)
- `UnityEngine.UI.Text.set_text` (prefix)
- `TMPro.TMP_Text.set_text` (prefix)

Purpose:

- display custom title and artwork instead of the original song assets.
  `TextPatch` only rewrites text in play/loading/result scenes while its switch
  is on, and replaces the *whole* string when it contains the borrowed template
  title.

Removal risk:

- gameplay may still function, but UI may show original song data.

### BGM and game-end timing

Owner files:

- `GRC2/Injectors/BgmInjector.cs`
- `GRC2/Injectors/BgmGameEndMonitor.cs`

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

- `GRC2/Harmony/ResultSceneUpdaterPatch.cs`

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

- `GRC2/Core/CustomKeySettings.cs`
- `GRC2/Harmony/AutoPlayPatch.cs`
- `GRC2/Harmony/JudgePerfectPatch.cs`
- `GRC2/Harmony/RecordBlockPatch.cs`

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
- `AutoPlayPatch`/`JudgePerfectPatch` are off when the `AutoPlay`/`AllPerfect`
  keys are missing (code fallback is `false`), but the config file generated on
  first launch (`CustomKeySettings.DefaultLines`) writes `AutoPlay=0` and
  **`AllPerfect=1`** — so a fresh install starts with the all-perfect judge
  override **on** (saves stay protected by the default `BlockSave=1`). The keys
  in `savecustomkey/config.txt` (created next to the `hwa` folder on first
  launch) are the only way to change them — there is no in-game toggle key.
  See [알려진_문제.md](알려진_문제.md) H3. Both patches read `CustomKeySettings` on every call
  instead of caching a copy, and `CustomKeySettings.Reload()` re-reads the file
  on every scene load, so a config edit takes effect from the next play
  (retry included) without a game restart — see the section below;
- `RecordBlockPatch` snapshots `playerMusicData[id].{highScoreArray,
  maxComboArray, playCountArray, playFlagArray}` for the played difficulty
  before `initializePreFade` runs and restores them afterward, zeroes the
  displayed old high score, and makes `requestGameDataSaveToFile` a no-op.
  It is gated on its own `BlockSave` config key (`CustomKeySettings.BlockSave`,
  **default `true`**), *not* on `AutoPlayPatch`/`JudgePerfectPatch` — so record
  blocking stays on even with both cheats off, and turning the cheats on
  without also setting `BlockSave=0` never reaches the save file.
- `RecordBlockPatch` resolves the played `MusicID` itself
  (`CustomAssetManager.IsCustomChartSelected()` +
  `AlbumManager.GetCurrentMusicID()`) instead of reading
  `sceneInitParam.musicData.id` directly, so its snapshot/restore does not
  depend on Harmony prefix ordering relative to
  `ResultSceneUpdaterPatch.InitializePreFadePrefix` (which rewrites that same
  field for the borrowed-template-song fix documented above).

Scope of the save block (checked against `Decompiled/`, 2026-09-28):
`requestGameDataSaveToFile` is the game's only save entry point, and
`SavableGameData` also holds `optionData`. With `BlockSave=1` the options menu,
main menu, gallery, sound player, and tutorial-flag saves are dropped too, and
the boot scene's first-save loop (`cBootSceneManageObject.coSaveInitializeGameData`,
run when the save file is missing or broken) waits for `SaveDataSaveState.Saved`
forever. See [알려진_문제.md](알려진_문제.md) A1/A2.

Removal risk:

- removing `AutoPlayPatch`/`JudgePerfectPatch` only removes the cheat
  features; removing `RecordBlockPatch` (or setting `BlockSave=0`) while
  keeping the other two would let AutoPlay/judge-forced results write real
  best scores/clear badges to the save file.

### config.txt reload (no game restart)

Owner files:

- `GRC2/Core/CustomKeySettings.cs`
- `GRC2/Core/SceneDetector.cs`

No game targets are patched — this is mod-side plumbing only.

Purpose:

- `savecustomkey/config.txt` is read in `OnInitializeMelon` and then re-read by
  `CustomKeySettings.Reload()` at the top of `SceneDetector.OnSceneWasLoaded`,
  so editing the file takes effect **from the next play** with no game restart;
- **why scene load and not a timer or `createAllNote`**: every consumer already
  reads the `CustomKeySettings` properties live (per frame or per call), so the
  only thing that decides "when does a change land" is when the properties get
  reassigned. A scene load is by definition not mid-song, and it runs well
  before `createAllNote`, so values that are latched at song start (AutoPlay's
  `mIsCurrentAutoPlay` force, NoteSpeedChaos's multiplier cache reset) are not
  a play behind. Patching `createAllNote` instead would put the reload prefix in
  an undefined order against the `AutoPlayPatch`/`NoteSpeedChaosPatch` prefixes
  already on that method (no `HarmonyPriority`/`HarmonyBefore` between separate
  patch classes), which is exactly the "one play late" bug this avoids;
- **`RythmGameResultScene` is excluded** (`SceneDetector.ResultSceneName`).
  `BlockSave` is the one key whose effect lands *after* the song, in
  `RecordBlockPatch`'s hooks on `initializePreFade`/`requestGameDataSaveToFile`.
  Reloading on the result scene would apply a mid-song edit to the play that
  already finished; skipping it keeps `BlockSave` pinned to its play-entry value
  for the whole play → result → save cycle;
- retry is covered: `cRythmGameManager.gui_requestRetry` goes through
  `changeNextScene(SceneId.RythmGame_Fairy, …)`, a real scene change;
- `Reload()` no-ops when the file's `LastWriteTimeUtc` is unchanged, and keeps
  the previous values when `Load()` finds zero `key=value` entries — an editor
  mid-write would otherwise read as an empty file and silently reset everything
  to defaults (e.g. `AllPerfect` flipping to `false`). It is also wrapped in
  try/catch so a bad read cannot break scene routing;
- `CustomKeySettings.PollFileChange()` runs from `OnUpdate` once a second
  (`SettingsPollIntervalSeconds`, on `Time.unscaledTime` so it still ticks while
  paused). It **only logs** `설정 파일 변경 감지 - 다음 플레이부터 적용됩니다.`
  once per distinct write time; it never touches the values.

Removal risk:

- dropping the `Reload()` call returns the mod to restart-only config edits;
  dropping the `ResultSceneName` exclusion makes a `BlockSave` edit made during
  a song apply to that song's own result/save.

### Custom BGA crossfade suppression

Owner file:

- `GRC2/Harmony/BgaCrossfadePatch.cs`

Patched game target:

- `IntiCreates.cPlayMovieSceneManager.requestPlay` (postfix)

Purpose:

- `requestPlay` starts `coUpdateMovieInARow()` whenever
  `mBackGroundSceneInitParam.mvType == BackGroundMVType.GamePlay &&
  mLoadMoviePathList.Count > 1`. That coroutine swaps to the *next original*
  clip once the current one is within `mPreCrossFadeFrame` (40 frames, ~0.67s)
  of its end, and it keeps running even after `BgaInjector` has pushed a custom
  clip into the `VideoPlayer` — which made custom-BGA songs flip back to the
  original video right at the end of the song;
- the postfix stops that coroutine and clears `mUpdateSwapCoroutine`, but only
  when `BgaInjector.IsInjected` is true, so songs playing the original BGA are
  untouched. `BgaCrossfadePatch.TryStopSwapCoroutine` is also called directly by
  `BgaInjector` for the case where injection finishes *after* `requestPlay`.

Removal risk:

- custom BGA songs revert to the original clip in the last ~0.67s of playback.

### Judgment bar / note sway / note speed chaos (GameHud overlays)

Owner files:

- `GRC2/Core/GameHud.cs`
- `GRC2/Harmony/JudgmentBarPatch.cs`
- `GRC2/Harmony/NoteSwayPatch.cs`
- `GRC2/Harmony/NoteSpeedChaosPatch.cs`

Patched game targets:

- `IntiCreates.cNotecWorkBase.onJudgeMent` (judgment-bar data source, separate
  Postfix from `JudgePerfectPatch`'s Prefix on the same method)
- `IntiCreates.cNotecWorkBase.simulate` (NoteSway; only the base implementation
  — slide/fairy-cursor notes and the post-touch phase of hold notes have their
  own position code and are not covered)
- `IntiCreates.cNotecWorkBase.getNoteSpeed` (NoteSpeedChaos; non-virtual single
  definition, so one patch covers Touch/Flick/Hold/Hold_Middle/Slide/SlideGuide)
- `IntiCreates.cFairyModeNotesManager.createAllNote` (also owned by note-array
  replacement and AutoPlay above; NoteSpeedChaosPatch clears its per-note/
  per-lane multiplier cache here so each song re-randomizes)

Purpose:

- `GameHud` draws directly in `MelonMod.OnGUI()` (IMGUI) rather than building a
  `Canvas`/`Image` object tree, so there is nothing to create/destroy across
  scene loads besides a couple of cached `Texture2D`s;
- all three effects (judgment bar marker, note sway offset, note speed
  multiplier) only ever touch rendering — `mCurrentPos`/`getNoteSpeed()` output
  or an IMGUI draw call — never `perfectSample`/lane fields, so none of them
  can change judgment outcome;
- `config.txt` keys: `EnableJudgmentBar`/`JudgmentBarVertical`/
  `JudgmentBarCapsule`/`JudgmentBarLeft`, `NoteSway`/`NoteSwayAmplitude`/
  `NoteSwaySpeed`/`NoteSwayDamping`/`NoteSwayDampingTime`, `NoteSpeedChaos`/
  `NoteSpeedChaosMin`/`NoteSpeedChaosMax`/`NoteSpeedChaosPerLane`.

Removal risk:

- purely cosmetic; removing any of the three loses that visual only.

Planned (not implemented yet):

- a judgment-bar-style **live per-grade hit counter** (running PERFECT/GREAT/
  GOOD/BAD/MISS tally, updated the same way `JudgmentBarPatch` already gets
  `judgeType` from `onJudgeMent`'s Postfix);
- **per-box customization** of the 3 rectangular touch hit-zones per side
  (`cFairyJudgeCircleTouchAreas.mTouchRect`, `NoteSubLaneType.Lane_1/2/3`) —
  see [터치_판정_영역_시스템_분석.md](../systems/터치_판정_영역_시스템_분석.md).
  `cEditorVisualRect.Rect` is recomputed from its `RectTransform` on every read,
  so a future patch has to move/resize the `RectTransform`, not `mRect`.

### Steam and DLC bypass

Owner file:

- `GRC2/Harmony/SteamApiHijacker.cs`

Patched targets:

- `Steamworks.SteamAPI` initialization/lifecycle methods;
- `Steamworks.SteamApps.BIsDlcInstalled`;
- `IntiCreates.Application.isDLCEnable`;
- `IntiCreates.sAddressableDirector` DLC checks;
- `IntiCreates.cDlcDirector` purchase check and initialization.

Purpose:

- preserve the existing Steam fallback and local `DataAddon` mount behavior.
- `cDlcDirector.Initialize` postfix registers every numeric folder under
  `DataAddon/` (relative to the working directory, like the game itself) in
  `cDlcDirector.DlcList` with its full path pre-filled, so the game's
  `coMount` treats it as already mounted and `coCheckDLC` loads its
  `catalog.json`.

Current environment (2026-09-28): the game folder's `steam_api64.dll` is a
Goldberg emulator build, so `SteamAPI.Init` succeeds and the `InitPostfix`
fallback has never run (no `SteamAPI.Init returned false` in any log). If it
ever runs, forcing `Init` to `true` makes `cDlcDirector.Initialize` call
`SteamApps.GetDLCCount()` on an uninitialized API, which throws; the postfix
above is then skipped and `sAddressableDirector` waits for
`cDlcDirector.IsInitialize()` forever. The unpatched game tolerates a failed
`Init` on its own. See [알려진_문제.md](알려진_문제.md) A3–A6.

Removal risk:

- startup without Steam or local DLC asset mounting may stop working.

### BGA and BGM sync

Owner files:

- `GRC2/Injectors/BgmBgaInjector.cs`
- `GRC2/Injectors/*`

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

Known, not-yet-fixed issues found in the 2026-09-28 review (with the owning file,
evidence, and trigger conditions) are tracked in
[알려진_문제.md](알려진_문제.md). The highest-risk ones for this map are:

- `RecordBlockPatch.RequestSavePatch` blocks the boot scene's first save (A1);
- `SteamApiHijacker.InitPostfix` would hang boot if Goldberg were missing (A3);
- `BgmBgaInjector` keeps the previous album's BGA/BGM path (C1);
- `NoteArrayHooks` also fires in the Music Select option preview and injects the
  previously loaded album's chart there (H1);
- `SceneDetector.InitializeHarmony` uses one `PatchAll` call, so a single missing
  patch target after a game update can leave every later patch class unapplied (H4).

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
- `SteamApiHijacker.CoCheckDLCPostfix` and its `sAddressableDirector.coCheckDLC`
  patch (removed 2026-08-09; log-only, and fired before the coroutine body ran)
- `BgmInjectorHooks` (removed 2026-08-09; folded into `BgmGameEndMonitor`)
- `BgmLoader.TrySetClipName` (removed 2026-08-09; `AudioClip` has no `m_Name`
  field, so the reflection lookup was always null)
- `Helpers/SteamManifestLocker` (removed 2026-06-06 in `a1bff38`; it marked
  `appmanifest_2585040.acf` read-only on startup to stop Steam updates. Nothing
  replaces it, so the manifest is currently writable)

## Cleanup Log

### 2026-09-29

Documentation-only pass; no source changes.

- Full re-read of `GRC2/`, `GRC2.Tests/`, build scripts and documents. Added
  section H (new findings, with fix directions) and fix directions for the older
  items to [알려진_문제.md](알려진_문제.md).
- Corrected the AutoPlay/AllPerfect default description above (the generated
  `config.txt` has `AllPerfect=1`).
- Documented that `createAllNote` also fires in the Music Select option preview.
- Verified and left alone: patches are **not** applied twice (per-selection hook
  logs appear once); `NoteSwayPatch` does not accumulate x offsets because
  `cNotecWorkBase.simulate` reassigns `localPosition` every frame
  (`Decompiled/IntiCreates/cNotecWorkBase.cs:213`).
- Moved the change history that lived in `GRC 리드미/README.md` to
  [정리_이력.md](정리_이력.md); added missing `[보관]` banners to four archive
  documents.

### 2026-09-28

Documentation-only pass; no source changes.

- Rewrote the `systems/`, `bms/`, `architecture/`, and most `maintenance/`
  documents against the current source and `Decompiled/`; they previously
  described the removed reflection layer (`FieldAccessHelper`,
  `NoteConstructorHelper`, `GameTypeLoader`, `SceneHandler`, `CoOpenPrefix`,
  `PatchApplier`, …).
- Moved four historical documents to `archive/`: the reflection guide and the
  three performance reports.
- Added `OnUpdate`/`OnGUI` entry points, the save-block scope, the Steam/DLC
  environment note, and `SteamManifestLocker` to the removed list here.
- Added [알려진_문제.md](알려진_문제.md) with the issues found in the review.

### 2026-08-09

Audit of the whole hook surface against `Decompiled/`, plus the structural
cleanup that came out of it.

Verified correct and left alone: all 21 string-named patch targets exist with no
overload ambiguity, all 17 `AccessTools.FieldRefAccess` field names *and* types
match, and every game enum member the mod names exists. The build resolves
against the real `Assembly-CSharp.dll` with 0 warnings, so only the string-keyed
lookups needed hand-checking.

Correctness fixes:

- Added `BmsNoteConverter.CheckIncompleteConnectedNotes`, a final check on the
  array that actually goes to the game. `createNote` reads
  `connectNodeDataArray[0]` with no null check for `NoteTypeId.Fairy` and
  `NoteTypeId.Hold`, and the existing `CheckMissingEndNotes` runs on *BMS* notes
  filtered by `Duration > 0`, so a fairy start (11-18) with no 1A/1B partner
  kept `Duration == 0`, skipped the check, and reached the game with a null
  `connectNodeDataArray` — a guaranteed `NullReferenceException` inside
  `createAllNote`. Matching failures inside `HoldNoteProcessor`/
  `FairyNoteProcessor` could produce the same state. Injection is now cancelled
  (original chart plays) with a log listing the offending notes.
- Unified sample/second conversion in `Helpers/NoteSampleTime.cs`.
  `NoteCreateDataBuilder` truncated (`(int)`) while the processors rounded
  (`Math.Round(AwayFromZero)`), so the same instant could differ by one sample;
  the processors' ±2-sample search had been absorbing that.
- `BgmLoader.TrySetClipName` was a permanent no-op: `AudioClip` has no `m_Name`
  field (`UnityEngine.Object` declares only `m_CachedPtr`; `name` is an extern
  property), so the reflection lookup always returned null and the
  `nameField?.SetValue` silently did nothing. Replaced with `audioClip.name =`,
  which `CustomBgmPlayer` was already using.
- Removed the `sAddressableDirector.coCheckDLC` postfix. It only logged
  "실행 완료됨", and because the target returns `IEnumerator` the postfix fires
  when the enumerator is *created*, before the coroutine body runs — the message
  was wrong and nothing else depended on it.
- Removed a dead `if (currentScene != null)` guard in `TextPatch`
  (`SceneManager.GetActiveScene()` returns a struct) and the unused
  `object __instance` parameters in `TextPatch`/`ArtWorkPatch`. Those three were
  the exceptions to the 2026-07-26 entry's "every patch method receives
  `__instance` as its concrete game type" claim, which was not true as written.

Structural cleanup (no behavior change):

- Removed `partial` from all 7 classes that used it. None of them spanned more
  than one file — 27 class-declaration blocks collapsed into 7 (`AlbumManager`
  6→1, `SceneDetector` 5→1, `BmsNoteConverter`/`HoldNoteProcessor`/
  `BgaBgmSyncManager` 4→1, `NoteCreateDataBuilder`/`FairyNoteProcessor` 2→1),
  with `#region` markers kept where the old blocks carried section comments.
  This was leftover from the 2026-07-21 file merge, which concatenated the files
  without collapsing the class headers.
- Typed the 9 `AlbumManager` MusicID APIs as
  `soRythmGameMusicDataMap.MusicID` (nullable where "no mapping" is a real
  answer) instead of `object`. That removed the boxing plus six defensive
  `is soRythmGameMusicDataMap.MusicID x` casts at the call sites, and makes the
  backing dictionaries use the non-boxing enum comparer.
- Flattened the 8 single-file folders that carried no namespace of their own:
  `Core/{Album,Assets,CustomKey,Hud,Scene}` -> `Core/`, and
  `Injectors/{Bga,Bgm,GameEnd,Shared}` -> `Injectors/`. `Core/Hud` was also the
  only one of the five that had its own namespace (`GRC2.Core.Hud`); `GameHud`
  now sits in `GRC2.Core` with its neighbours.
- Merged `Harmony/Handlers/` and `Harmony/Hooks/` into `Harmony/`
  (namespace `GRC2.Harmony`). Nothing in the code distinguished the two — both
  held `[HarmonyPatch]` classes. Moved `SteamApiHijacker.cs` (11 patches) there
  from `Helpers/`.
- Dropped two single-target `[HarmonyPatch]` wrapper classes that only existed
  to hold one method: `BgmInjectorHooks` (now `[HarmonyPatch]` on
  `BgmGameEndMonitor` itself) and `BgaCrossfadePatch.RequestPlayPatch`. Nested
  wrappers are still used where one class patches several targets.

Documented, not changed:

- `BgmGameEndMonitor` keeps its own `coMonitorGameEnd` patch instead of moving
  to `Harmony/`; it shares state with `BgmFinishTimeManager` in the same file.
- `Builders/`, `Converters/` and `Processors/` remain three namespaces for one
  BMS→`NoteCreateData` pipeline. Their folders and namespaces at least agree,
  which was the property the flattened folders lacked.

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
  `Injectors/PatchApplier.cs`.
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
