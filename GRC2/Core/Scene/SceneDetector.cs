using System;
using System.Collections.Generic;
using System.IO;
using GRC2.Helpers;
using GRC2.Harmony.Hooks;
using GRC2.Injectors;
using GRC2.Parsers;
using GRC2.Converters;
using MelonLoader;
using UnityEngine;
using System.Linq;
using GRC2.Harmony.Handlers;

namespace GRC2.Core
{
    public partial class SceneDetector : MelonMod
    {
        private bool _isInitialized = false;
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
            MusicInjector.Initialize();
            
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
                
                // BgmBgaInjector 초기화
                MelonLogger.Msg("[SceneDetector] BgmBgaInjector 초기화 시작...");
                BgmBgaInjector.Initialize(_hwaFolderPath);
                MelonLogger.Msg("[SceneDetector] BgmBgaInjector 초기화 완료");

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

            if (BgmBgaInjector.IsPlayScene())
            {
                PauseKeyHandler.HandlePauseKeyInput();
            }
        }
    }

    public partial class SceneDetector
    {
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
    }

    public partial class SceneDetector
    {
        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            MelonLogger.Msg($"[SceneDetector] 씬 로드: {sceneName} (BuildIndex: {buildIndex})");

            if (!_isInitialized)
            {
                MelonLogger.Warning("[SceneDetector] 아직 초기화되지 않았습니다.");
                return;
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
                else if (sceneName == "RythmGameResultScene")
                {
                    MelonLogger.Msg($"[SceneDetector] 결과 씬 감지: {sceneName} - BGM 주입 중지");
                    BgmBgaInjector.StopInjection();
                    BgmBgaInjector.ResetPlaySceneState();
                }
                else if (sceneName == "MusicSelectScene")
                {
                    MelonLogger.Msg($"[SceneDetector] 곡 선택 씬 감지: {sceneName}");
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
    }
}
