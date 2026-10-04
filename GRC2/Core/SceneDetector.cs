using System;
using System.Collections.Generic;
using System.IO;
using GRC2.Helpers;
using GRC2.Harmony;
using GRC2.Injectors;
using GRC2.Parsers;
using GRC2.Converters;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using System.Linq;

namespace GRC2.Core
{
    public class SceneDetector : MelonMod
    {
        /// <summary>
        /// 이 씬만 설정 다시 읽기에서 제외합니다. BlockSave는 효과가 곡이 끝난 뒤(결과 씬의 기록
        /// 원복/세이브 차단)에 나타나므로, 여기서 값이 바뀌면 방금 끝난 판에 새 값이 적용됩니다.
        /// 제외해두면 BlockSave가 플레이 진입 시점 값으로 플레이~결과~저장 한 사이클 내내 고정됩니다.
        /// </summary>
        private const string ResultSceneName = "RythmGameResultScene";

        /// <summary>설정 파일 수정 감지 폴링 주기(초). 값 반영이 아니라 로그 알림용입니다.</summary>
        private const float SettingsPollIntervalSeconds = 1f;

        private bool _isInitialized = false;
        private float _nextSettingsPollTime;
        private string _hwaFolderPath;
        private string _lastParsedBmsFile = null; // 마지막으로 파싱한 BMS 파일 경로
        public static List<Parsers.BmsNote> ParsedBmsNotes { get; private set; } = new List<Parsers.BmsNote>();
        // 파일 경로(전체 경로) 기준 캐시: 동일 파일명(hwa2.bms)이라도 폴더가 다르면 충돌하지 않음
        public static Dictionary<string, List<Parsers.BmsNote>> ParsedBmsNotesByFile { get; private set; }
            = new Dictionary<string, List<Parsers.BmsNote>>(StringComparer.OrdinalIgnoreCase);
        public static Parsers.SongInfo SongInfo { get; private set; } = new Parsers.SongInfo();

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("[SceneDetector] 모드 초기화 시작");
            
            // 모드 어셈블리의 HarmonyPatch 특성을 한 번에 등록합니다.
            InitializeHarmony();
            
            try
            {
                // hwa 폴더 경로 설정 (게임 설치 폴더)
                // Application.dataPath는 보통 "게임폴더/게임명_Data"를 가리키므로
                // 한 단계 위로 올라가면 게임 설치 폴더가 됩니다
                var gameFolder = Path.GetDirectoryName(Application.dataPath);
                _hwaFolderPath = Path.Combine(gameFolder, "hwa");
                MelonLogger.Msg($"[SceneDetector] 게임 폴더: {gameFolder}");
                MelonLogger.Msg($"[SceneDetector] hwa 폴더 경로: {_hwaFolderPath}");

                // hwa 폴더 생성 (없으면)
                if (!Directory.Exists(_hwaFolderPath))
                {
                    Directory.CreateDirectory(_hwaFolderPath);
                    MelonLogger.Msg("[SceneDetector] hwa 폴더 생성 완료");
                }

                // AutoPlay/판정조작 on-off 설정 (savecustomkey 폴더) 로드.
                // 이후에는 씬이 바뀔 때마다 CustomKeySettings.Reload()로 다시 읽습니다.
                CustomKeySettings.Initialize(gameFolder);

                // 앨범 폴더 스캔 (먼저 앨범들을 스캔)
                MelonLogger.Msg("[SceneDetector] 앨범 폴더 스캔 시작...");
                AlbumManager.ScanAlbums(_hwaFolderPath);
                MelonLogger.Msg("[SceneDetector] 앨범 폴더 스캔 완료");

                // 곡 정보 파일 파싱 (앨범별로 이미 파싱됨, 현재 앨범의 곡 정보 사용)
                MelonLogger.Msg("[SceneDetector] 곡 정보 확인 시작...");
                var currentSongInfo = AlbumManager.GetCurrentSongInfo();
                if (currentSongInfo != null)
                {
                    SongInfo = currentSongInfo;
                    MelonLogger.Msg($"[SceneDetector] 곡 정보 확인 완료 - 제목: {SongInfo.Title}, 아티스트: {SongInfo.Artist}");
                }
                else
                {
                    // 앨범별 곡 정보가 없으면 기존 방식으로 파싱
                    ParseSongInfo();
                    // 파싱된 곡 정보로 앨범 선택 시도
                    if (SongInfo != null)
                    {
                        AlbumManager.SelectAlbumBySongInfo(SongInfo);
                    }
                }
                
                // 커스텀 아트워크 로드 (앨범별)
                MelonLogger.Msg("[SceneDetector] 커스텀 아트워크 스캔 시작...");
                LoadCustomArtwork();
                
                // BMS 파일 스캔 및 파싱 (앨범별)
                MelonLogger.Msg("[SceneDetector] BMS 파일 스캔 시작...");
                ScanAndParseBmsFiles();
                MelonLogger.Msg($"[SceneDetector] BMS 파일 스캔 완료: {ParsedBmsNotesByFile.Count}개 파일, 현재 선택 파일 노트 {ParsedBmsNotes.Count}개");

                // 자동 패치는 이미 적용되어 있으므로 주입할 BMS 데이터만 갱신합니다.
                NoteArrayHooks.UpdateBmsNotes(ParsedBmsNotes);

                // BGA/BGM 주입 경로는 주입 루프가 매번 현재 앨범에서 읽으므로 따로 초기화할 것이 없습니다.

                _isInitialized = true;
                MelonLogger.Msg("[SceneDetector] 모드 초기화 완료");
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, "[SceneDetector]", "초기화 실패");
            }
        }

        public override void OnUpdate()
        {
            if (!_isInitialized) return;

            PollCustomKeySettingsFile();

            if (BgmBgaInjector.IsPlayScene())
            {
                HandlePauseKeyInput();
            }
        }

        /// <summary>
        /// 곡 중에 설정 파일을 고쳐도 바로 알 수 있도록 수정 여부만 주기적으로 확인해 로그를 남깁니다.
        /// 값 반영은 하지 않습니다(다음 플레이 씬 진입 시 Reload에서 처리). 게임 시간이 멈추는
        /// 일시정지 중에도 확인되도록 unscaledTime을 씁니다.
        /// </summary>
        private void PollCustomKeySettingsFile()
        {
            if (Time.unscaledTime < _nextSettingsPollTime)
                return;

            _nextSettingsPollTime = Time.unscaledTime + SettingsPollIntervalSeconds;
            CustomKeySettings.PollFileChange();
        }

        public override void OnGUI()
        {
            if (!_isInitialized) return;

            GameHud.Draw(BgmBgaInjector.IsPlayScene());
        }

        #region Harmony 패치 초기화

        private static HarmonyLib.Harmony _harmonyInstance = null;

        private static void InitializeHarmony()
        {
            if (_harmonyInstance != null) return;

            MelonLogger.Msg("[SceneDetector] Harmony 초기화 중...");

            try
            {
                _harmonyInstance = new HarmonyLib.Harmony("GRC2.MusicInjector");
                _harmonyInstance.PatchAll(typeof(SceneDetector).Assembly);
                MelonLogger.Msg("[SceneDetector] Harmony 자동 패치 적용 완료");
            }
            catch (Exception ex)
            {
                _harmonyInstance = null;
                MelonLogger.Msg($"[SceneDetector] Harmony 패치 적용 실패: {ex.Message}");
                MelonLogger.Msg($"[SceneDetector] 스택 트레이스: {ex.StackTrace}");
            }
        }

        #endregion

        #region 플레이 씬에서 Space/ESC 키 입력 시 일시정지 메뉴를 여닫는 처리

        private static readonly AccessTools.FieldRef<IntiCreates.cRythmGameManager, IntiCreates.cRythmGamePauseMenuHud> PauseMenuWorkRef =
            AccessTools.FieldRefAccess<IntiCreates.cRythmGameManager, IntiCreates.cRythmGamePauseMenuHud>("mPauseMenuWork");

        // setPauseButtonPusable은 private이므로 열린 인스턴스 델리게이트로 한 번만 바인딩합니다.
        private static readonly Action<IntiCreates.cRythmGameManager, bool> SetPauseButtonPusable =
            AccessTools.MethodDelegate<Action<IntiCreates.cRythmGameManager, bool>>(
                AccessTools.Method(typeof(IntiCreates.cRythmGameManager), "setPauseButtonPusable"));

        private static void HandlePauseKeyInput()
        {
            if (!Input.GetKeyDown(KeyCode.Space) && !Input.GetKeyDown(KeyCode.Escape))
            {
                return;
            }

            try
            {
                var manager = UnityEngine.Object.FindObjectOfType<IntiCreates.cRythmGameManager>();
                if (manager == null)
                    return;

                bool isPausing = manager.mIsPausing;
                var pauseMenuWork = PauseMenuWorkRef(manager);

                if (isPausing && pauseMenuWork != null)
                {
                    // 이미 일시정지 메뉴가 열린 상태라면 계속하기(Unpause) 시도
                    if (pauseMenuWork.getState() == IntiCreates.cRythmGamePauseMenuHud.State.Active)
                    {
                        pauseMenuWork.requestPushContinueButton();
                        MelonLogger.Msg("[SceneDetector] ⏯️ 키 입력 (Space/ESC) -> 일시정지 해제 (Continue)");
                        return;
                    }
                }

                if (!isPausing)
                {
                    // 일시정지 버튼 활성화 상태 강제 후 메뉴 열기
                    SetPauseButtonPusable?.Invoke(manager, true);
                    manager.requestPause();
                    MelonLogger.Msg("[SceneDetector] ⏸️ 키 입력 (Space/ESC) -> 일시정지 메뉴 오픈 (requestPause)");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(ex, "[SceneDetector]", "Pause 키 처리 오류");
            }
        }

        #endregion

        #region 곡 정보 / 아트워크 / BMS 스캔

        private void ParseSongInfo()
        {
            try
            {
                MelonLogger.Msg("[SceneDetector] 곡 정보 파일 파싱 시작");

                if (!Directory.Exists(_hwaFolderPath))
                {
                    MelonLogger.Warning($"[SceneDetector] hwa 폴더가 없습니다: {_hwaFolderPath}");
                    return;
                }

                var txtFiles = Directory.GetFiles(_hwaFolderPath, "*.txt", SearchOption.TopDirectoryOnly).ToList();
                if (txtFiles.Count == 0)
                {
                    MelonLogger.Msg("[SceneDetector] 곡 정보 txt 파일을 찾을 수 없습니다. 기본값 사용.");
                    return;
                }

                var firstTxtFile = txtFiles[0];
                MelonLogger.Msg($"[SceneDetector] 곡 정보 파일 파싱: {Path.GetFileName(firstTxtFile)}");

                SongInfo = SongInfoParser.ParseTxtFile(firstTxtFile);
                MelonLogger.Msg($"[SceneDetector] 곡 정보 파싱 완료 - 제목: {SongInfo.Title}, 아티스트: {SongInfo.Artist}");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(ex, "[SceneDetector]", "곡 정보 파싱 오류");
            }
        }

        private void ScanAndParseBmsFiles()
        {
            try
            {
                MelonLogger.Msg("[SceneDetector] BMS 파일 스캔 및 파싱 시작...");

                var albums = AlbumManager.GetAllAlbums();
                if (albums == null || albums.Count == 0)
                {
                    MelonLogger.Msg("[SceneDetector] 앨범 정보가 없습니다. (BMS 스캔 스킵)");
                    return;
                }

                int totalBmsFiles = 0;
                foreach (var kvp in albums)
                {
                    var albumKey = kvp.Key;
                    var album = kvp.Value;
                    var files = album?.BmsFiles ?? new List<string>();
                    if (files.Count == 0)
                    {
                        continue;
                    }

                    MelonLogger.Msg($"[SceneDetector] 앨범 폴더 '{albumKey}'에서 {files.Count}개의 BMS 파일 발견");
                    foreach (var file in files)
                    {
                        MelonLogger.Msg($"[SceneDetector]   - {Path.GetFileName(file)}");
                    }

                    totalBmsFiles += files.Count;
                }

                MelonLogger.Msg($"[SceneDetector] 총 {totalBmsFiles}개의 BMS 파일 발견, 파싱 시작...");

                ParsedBmsNotesByFile.Clear();
                var albumOrder = albums.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();

                foreach (var albumKey in albumOrder)
                {
                    var album = albums[albumKey];
                    var files = (album?.BmsFiles ?? new List<string>())
                        .Where(f => !string.IsNullOrWhiteSpace(f))
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    foreach (var file in files)
                    {
                        if (ParsedBmsNotesByFile.ContainsKey(file))
                        {
                            continue;
                        }

                        var notes = BmsParser.ParseBmsFile(file, printSummary: false);
                        ParsedBmsNotesByFile[file] = notes ?? new List<BmsNote>();
                    }
                }

                MelonLogger.Msg("");
                MelonLogger.Msg("=== BMS 파일 파싱 결과 ===");

                foreach (var albumKey in albumOrder)
                {
                    var album = albums[albumKey];
                    var files = (album?.BmsFiles ?? new List<string>())
                        .Where(f => !string.IsNullOrWhiteSpace(f))
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    foreach (var file in files)
                    {
                        if (!ParsedBmsNotesByFile.TryGetValue(file, out var notes))
                        {
                            continue;
                        }

                        BmsSummaryPrinter.PrintParseSummary(
                            notes,
                            file,
                            label: albumKey,
                            printHeader: false,
                            printLeadingBlankLine: false,
                            printTrailingBlankLine: false);
                    }
                }

                MelonLogger.Msg("");

                var currentBmsFile = AlbumManager.GetCurrentBmsFile();
                if (string.IsNullOrEmpty(currentBmsFile))
                {
                    ParsedBmsNotes = new List<BmsNote>();
                    _lastParsedBmsFile = null;
                    MelonLogger.Msg("[SceneDetector] 현재 선택된 앨범에 BMS 파일이 없습니다.");
                    return;
                }

                if (!ParsedBmsNotesByFile.TryGetValue(currentBmsFile, out var currentNotes))
                {
                    currentNotes = BmsParser.ParseBmsFile(currentBmsFile);
                    ParsedBmsNotesByFile[currentBmsFile] = currentNotes ?? new List<BmsNote>();
                }

                ParsedBmsNotes = currentNotes ?? new List<BmsNote>();
                _lastParsedBmsFile = currentBmsFile;
                MelonLogger.Msg($"[SceneDetector] 현재 선택 BMS 노트 로드 완료: {Path.GetFileName(currentBmsFile)} / {ParsedBmsNotes.Count}개");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(ex, "[SceneDetector]", "BMS 파일 스캔/파싱 오류");
            }
        }

        private void ReloadCurrentAlbumAssets()
        {
            try
            {
                var currentSongInfo = AlbumManager.GetCurrentSongInfo();
                if (currentSongInfo != null)
                {
                    SongInfo = currentSongInfo;
                }

                var currentBmsFile = AlbumManager.GetCurrentBmsFile();
                if (!string.IsNullOrEmpty(currentBmsFile))
                {
                    if (_lastParsedBmsFile != currentBmsFile || ParsedBmsNotes == null || ParsedBmsNotes.Count == 0)
                    {
                        MelonLogger.Msg($"[SceneDetector] 앨범 변경 감지 - BMS 파일 다시 파싱: {Path.GetFileName(currentBmsFile)}");
                        if (!ParsedBmsNotesByFile.TryGetValue(currentBmsFile, out var notes))
                        {
                            notes = BmsParser.ParseBmsFile(currentBmsFile);
                            ParsedBmsNotesByFile[currentBmsFile] = notes ?? new List<BmsNote>();
                        }

                        ParsedBmsNotes = notes ?? new List<BmsNote>();
                        _lastParsedBmsFile = currentBmsFile;

                        if (ParsedBmsNotes != null && ParsedBmsNotes.Count > 0)
                        {
                            NoteArrayHooks.UpdateBmsNotes(ParsedBmsNotes);
                            MelonLogger.Msg($"[SceneDetector] BMS 노트 업데이트 완료: {ParsedBmsNotes.Count}개 노트");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(ex, "[SceneDetector]", "앨범 에셋 다시 로드 오류");
            }
        }

        private void LoadCustomArtwork()
        {
            try
            {
                MelonLogger.Msg("[SceneDetector] 커스텀 아트워크 파일 스캔 시작");

                var imageFile = AlbumManager.GetCurrentImageFile();
                if (!string.IsNullOrEmpty(imageFile))
                {
                    MelonLogger.Msg($"[SceneDetector] 커스텀 아트워크 파일 발견: {Path.GetFileName(imageFile)}");
                    CustomAssetManager.LoadCustomArtwork(imageFile);
                }
                else
                {
                    MelonLogger.Msg("[SceneDetector] 현재 선택된 앨범에 커스텀 아트워크 이미지 파일이 없습니다.");
                }

            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(ex, "[SceneDetector]", "커스텀 아트워크 로드 오류");
            }
        }

        #endregion

        #region 씬 라우팅

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            MelonLogger.Msg($"[SceneDetector] 씬 로드: {sceneName} (BuildIndex: {buildIndex})");

            if (!_isInitialized)
            {
                MelonLogger.Warning("[SceneDetector] 아직 초기화되지 않았습니다.");
                return;
            }

            // 설정 파일을 여기서 다시 읽어 이번 플레이부터 반영합니다. 씬 로드는 곡이 진행 중이
            // 아닌 게 확실한 경계이고 createAllNote보다 확실히 먼저이므로, AutoPlay/판정조작처럼
            // 곡 시작 시점에 확정되는 값도 한 판 늦지 않고 제때 적용됩니다. 리트라이도 실제
            // 씬 전환(gui_requestRetry -> changeNextScene)이라 여기로 들어옵니다.
            if (sceneName != ResultSceneName)
            {
                CustomKeySettings.Reload();
            }

            try
            {
                if (sceneName == "FairyModeScene")
                {
                    HandleFairyModeScene();
                    BgmBgaInjector.StartInjection(isPlayScene: true);
                    BgmGameEndMonitor.AdjustMusicDataOnSceneLoad();
                }
                else if (sceneName == "PlayMovieScene")
                {
                    HandlePlayMovieScene();
                    BgmBgaInjector.StartInjection(isPlayScene: true);
                    BgmGameEndMonitor.AdjustMusicDataOnSceneLoad();
                }
                else if (sceneName == "RenderCutinScene")
                {
                    MelonLogger.Msg("[SceneDetector] RenderCutinScene 로드 처리 시작 (플레이 씬 로딩)");
                    CustomBgmPlayer.Cleanup();
                    MelonLogger.Msg("[SceneDetector] ✅ 로딩 씬 진입 - 커스텀 프리뷰 BGM 중지");

                    BgmBgaInjector.StartInjection(isPlayScene: true);

                    if (CustomAssetManager.IsCustomChartSelected())
                    {
                        ReloadCurrentAlbumAssets();
                        PlaySceneArtworkInjector.StartArtworkInjection();
                    }
                }
                else if (sceneName == ResultSceneName)
                {
                    MelonLogger.Msg($"[SceneDetector] 결과 씬 감지: {sceneName} - BGM 주입 중지");
                    BgmBgaInjector.StopInjection();
                    BgmBgaInjector.ResetPlaySceneState();
                }
                else if (sceneName.StartsWith("MusicSelectScene", StringComparison.Ordinal))
                {
                    // 실제 씬 파일명은 "MusicSelectScene_Hasegawa"처럼 접미사가 붙어있어
                    // 정확히 일치하는 이름("MusicSelectScene")만 확인하면 이 분기가 절대
                    // 실행되지 않습니다. 그 결과 플레이 씬에서 일시정지 메뉴로 곡 선택
                    // 화면으로 돌아와도 _isPlayScene이 꺼지지 않아, 방금 플레이하던 곡의
                    // 제목/아트워크 강제 치환이 곡 선택 화면에서도 계속 적용됩니다.
                    MelonLogger.Msg($"[SceneDetector] 곡 선택 씬 감지: {sceneName} - 플레이 씬 상태 해제");
                    BgmBgaInjector.StopInjection();
                    BgmBgaInjector.ResetPlaySceneState();
                }
                else if (sceneName == "SoundPlayerScene" || sceneName == "MoviePlayer_MovieSelect")
                {
                    MelonLogger.Msg($"[SceneDetector] 씬 감지: {sceneName} - 커스텀 주입 비활성화 (플레이 씬 아님)");
                    BgmBgaInjector.StopInjection();
                    BgmBgaInjector.ResetPlaySceneState();
                    CustomAssetManager.SetCustomChartSelected(false);
                }
                else
                {
                    MelonLogger.Msg($"[SceneDetector] 알 수 없는 씬: {sceneName} - 게임 플레이 씬일 수 있습니다");

                    if (BgmBgaInjector.IsPlayScene())
                    {
                        MelonLogger.Msg($"[SceneDetector] 플레이 씬 상태 감지: {sceneName}");
                        if (CustomAssetManager.ShouldInjectCustomContent())
                        {
                            ReloadCurrentAlbumAssets();
                            PlaySceneArtworkInjector.StartArtworkInjection();
                        }
                    }
                    else
                    {
                        BgmBgaInjector.StartInjection(isPlayScene: false);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(ex, "[SceneDetector]", "씬 처리 중 오류");
            }
        }

        private void HandleFairyModeScene()
        {
            HandlePlayScene("FairyModeScene");
        }

        private void HandlePlayMovieScene()
        {
            HandlePlayScene("PlayMovieScene");
        }

        private void HandlePlayScene(string sceneName)
        {
            MelonLogger.Msg($"[SceneDetector] {sceneName} 로드 처리 시작");

            try
            {
                CustomBgmPlayer.Cleanup();
                MelonLogger.Msg("[SceneDetector] ✅ 플레이 씬 진입 - 커스텀 프리뷰 BGM 중지");

                if (CustomAssetManager.IsCustomChartSelected())
                {
                    ReloadCurrentAlbumAssets();
                    PlaySceneArtworkInjector.StartArtworkInjection();
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(ex, "[SceneDetector]", $"{sceneName} 처리 실패");
            }
        }

        #endregion
    }
}
