using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
        private string _gameFolder;
        private string _lastParsedBmsFile = null; // 마지막으로 파싱한 BMS 파일 경로
        public static List<Parsers.BmsNote> ParsedBmsNotes { get; private set; } = new List<Parsers.BmsNote>();
        // 파일 경로(전체 경로) 기준 캐시: 동일 파일명(hwa2.bms)이라도 폴더가 다르면 충돌하지 않음
        public static Dictionary<string, List<Parsers.BmsNote>> ParsedBmsNotesByFile { get; private set; }
            = new Dictionary<string, List<Parsers.BmsNote>>(StringComparer.OrdinalIgnoreCase);
        public static Parsers.SongInfo SongInfo { get; private set; } = new Parsers.SongInfo();

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("[SceneDetector] 모드 초기화 시작");
            var total = System.Diagnostics.Stopwatch.StartNew();
            var phase = System.Diagnostics.Stopwatch.StartNew();
            var timings = new List<string>();
            void Mark(string name)
            {
                timings.Add($"{name} {phase.ElapsedMilliseconds}ms");
                phase.Restart();
            }

            // 모드 어셈블리의 HarmonyPatch 특성을 한 번에 등록합니다.
            InitializeHarmony();
            Mark("Harmony");

            try
            {
                // Application.dataPath는 "게임폴더/게임명_Data"이므로 한 단계 위가 게임 설치 폴더입니다.
                var gameFolder = Path.GetDirectoryName(Application.dataPath);
                _gameFolder = gameFolder;
                _hwaFolderPath = Path.Combine(gameFolder, "hwa");
                MelonLogger.Msg($"[SceneDetector] 게임 폴더: {gameFolder}");

                // 게임 업데이트 내성: 빌드 ID, 어셈블리 MVID, 이름으로 접근하는 필드를 점검해 로그 한 곳에 모읍니다(진단 전용).
                GameCompatibility.LogStartupReport(gameFolder);
                Mark("호환성");

                if (!Directory.Exists(_hwaFolderPath))
                {
                    Directory.CreateDirectory(_hwaFolderPath);
                    MelonLogger.Msg($"[SceneDetector] hwa 폴더 생성: {_hwaFolderPath}");
                }

                // AutoPlay/판정조작/VerboseLog 등 설정(savecustomkey 폴더). 이후에는 씬이 바뀔 때마다 Reload로 다시 읽습니다.
                // 상세 로그 여부가 여기서 정해지므로 앨범 스캔보다 먼저 읽습니다.
                CustomKeySettings.Initialize(gameFolder);
                CustomMusicIdRegistry.Initialize(gameFolder);

                // 하위 단계 하나가 예외를 내도 _isInitialized가 켜지도록 감쌉니다(H12).
                // Harmony 패치는 이미 적용된 상태라, 여기서 _isInitialized가 false로 남으면 패치만 돌고 씬 라우팅이 멈춥니다.
                try
                {
                    AlbumManager.ScanAlbums(_hwaFolderPath);
                    _albumSignature = AlbumManager.ComputeFolderSignature(_hwaFolderPath);
                    Mark($"앨범 {AlbumManager.GetAllAlbums().Count}개");

                    var currentSongInfo = AlbumManager.GetCurrentSongInfo();
                    if (currentSongInfo != null)
                        SongInfo = currentSongInfo;

                    LoadCustomArtwork();
                    Mark("아트워크");

                    ScanAndParseBmsFiles();
                    Mark("BMS");

                    NoteArrayHooks.UpdateBmsNotes(ParsedBmsNotes);
                }
                catch (Exception stepEx)
                {
                    Helpers.ErrorLogger.LogException(stepEx, "[SceneDetector]", "초기 에셋 준비 오류 (모드는 계속 동작합니다)");
                }

                _isInitialized = true;
                MelonLogger.Msg($"[SceneDetector] 모드 초기화 완료 {total.ElapsedMilliseconds}ms ({string.Join(", ", timings)})");
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

            ModLog.Verbose("[SceneDetector] Harmony 초기화 중...");

            try
            {
                _harmonyInstance = new HarmonyLib.Harmony("GRC2.MusicInjector");
                _harmonyInstance.PatchAll(typeof(SceneDetector).Assembly);
                ModLog.Verbose("[SceneDetector] Harmony 자동 패치 적용 완료");
                LogPatchedMethodCount();
            }
            catch (Exception ex)
            {
                // PatchAll은 첫 실패에서 멈춰 뒤의 패치 클래스가 빠집니다(H4). 적용된 것을 되돌리고 클래스마다 다시 적용합니다.
                MelonLogger.Error($"[SceneDetector] Harmony PatchAll 실패 ({ex.Message}). 패치 클래스를 하나씩 다시 적용합니다.");
                MelonLogger.Msg($"[SceneDetector] 스택 트레이스: {ex.StackTrace}");
                PatchTypesOneByOne();
            }
        }

        /// <summary>패치 클래스를 하나씩 적용합니다. 한 클래스가 실패해도 나머지는 적용되고, 실패한 클래스 이름을 남깁니다(H4).</summary>
        private static void PatchTypesOneByOne()
        {
            if (_harmonyInstance == null)
                _harmonyInstance = new HarmonyLib.Harmony("GRC2.MusicInjector");

            _harmonyInstance.UnpatchSelf();

            int applied = 0;
            int failed = 0;
            foreach (var type in typeof(SceneDetector).Assembly.GetTypes())
            {
                if (!HasPatchAttributes(type))
                    continue;

                try
                {
                    _harmonyInstance.CreateClassProcessor(type).Patch();
                    applied++;
                }
                catch (Exception ex)
                {
                    failed++;
                    MelonLogger.Error($"[SceneDetector] 패치 클래스 적용 실패: {type.FullName} - {ex.Message}");
                }
            }

            MelonLogger.Msg($"[SceneDetector] 패치 클래스 적용 {applied}개, 실패 {failed}개");
            LogPatchedMethodCount();
        }

        /// <summary>실제로 패치된 게임 메서드 수를 남깁니다. 업데이트 뒤 이 수가 줄었다면 대상이 사라진 훅이 있다는 뜻입니다.</summary>
        private static void LogPatchedMethodCount()
        {
            try
            {
                MelonLogger.Msg($"[SceneDetector] 패치된 게임 메서드 {_harmonyInstance.GetPatchedMethods().Count()}개");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SceneDetector] 패치된 메서드 수 확인 실패: {ex.Message}");
            }
        }

        private static bool HasPatchAttributes(Type type)
        {
            if (type.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length > 0)
                return true;

            const BindingFlags flags = BindingFlags.Static | BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var method in type.GetMethods(flags))
            {
                if (method.GetCustomAttributes(inherit: false).Any(IsHarmonyAnnotation))
                    return true;
            }

            return false;
        }

        private static bool IsHarmonyAnnotation(object attribute)
        {
            return attribute is HarmonyPrefix || attribute is HarmonyPostfix ||
                attribute is HarmonyFinalizer || attribute is HarmonyTranspiler;
        }

        #endregion

        #region 플레이 씬에서 Space/ESC 키 입력 시 일시정지 메뉴를 여닫는 처리

        // 진입점 클래스의 정적 초기화에서 필드 이름 조회가 던지면(게임 업데이트로 필드가 바뀐 경우) 모드 전체가 로드되지 않습니다.
        // 그래서 처음 쓸 때 한 번만 조회하고, 실패하면 일시정지 단축키만 꺼집니다.
        private static AccessTools.FieldRef<IntiCreates.cRythmGameManager, IntiCreates.cRythmGamePauseMenuHud> _pauseMenuWorkRef;
        private static bool _pauseMenuWorkRefFailed;

        private static IntiCreates.cRythmGamePauseMenuHud GetPauseMenuWork(IntiCreates.cRythmGameManager manager)
        {
            if (_pauseMenuWorkRef == null && !_pauseMenuWorkRefFailed)
            {
                try
                {
                    _pauseMenuWorkRef = AccessTools.FieldRefAccess<IntiCreates.cRythmGameManager, IntiCreates.cRythmGamePauseMenuHud>("mPauseMenuWork");
                }
                catch (Exception ex)
                {
                    _pauseMenuWorkRefFailed = true;
                    MelonLogger.Error($"[SceneDetector] mPauseMenuWork 필드를 찾지 못해 일시정지 단축키 일부가 꺼집니다: {ex.Message}");
                }
            }

            return _pauseMenuWorkRef == null ? null : _pauseMenuWorkRef(manager);
        }


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
                var pauseMenuWork = GetPauseMenuWork(manager);

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
                    // 원본은 로딩·시작·클리어 연출 중에 일시정지 버튼을 잠급니다. 예전에는 여기서 잠금을 강제로 풀었기 때문에
                    // 그 구간에서도 Space/Esc로 메뉴가 열렸습니다(H13). 이제 잠금은 그대로 두고, 원본 requestPause가 스스로 판단합니다.
                    manager.requestPause();
                    MelonLogger.Msg("[SceneDetector] ⏸️ 키 입력 (Space/ESC) -> 일시정지 요청 (원본이 잠금 상태면 무시됨)");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(ex, "[SceneDetector]", "Pause 키 처리 오류");
            }
        }

        #endregion

        #region 곡 정보 / 아트워크 / BMS 스캔

        /// <summary>
        /// 시작할 때는 현재 선택 앨범의 BMS만 파싱합니다. 다른 곡은 플레이 씬에 들어갈 때
        /// <see cref="ReloadCurrentAlbumAssets"/>가 처음 한 번 파싱해 캐시합니다.
        /// 예전에는 모든 앨범의 BMS를 시작할 때 파싱하고 곡마다 요약을 찍어, 곡 수에 비례해 시작이 느려지고
        /// 쓰지 않는 노트까지 메모리에 들고 있었습니다(곡 상한을 늘리면서 병목이 됨).
        /// </summary>
        private void ScanAndParseBmsFiles()
        {
            try
            {
                var albums = AlbumManager.GetAllAlbums();
                if (albums == null || albums.Count == 0)
                {
                    MelonLogger.Msg("[SceneDetector] 앨범 정보가 없습니다. (BMS 스캔 스킵)");
                    return;
                }

                int albumsWithBms = albums.Values.Count(a => a?.BmsFiles != null && a.BmsFiles.Count > 0);
                int totalBmsFiles = albums.Values.Sum(a => a?.BmsFiles?.Count ?? 0);
                MelonLogger.Msg($"[SceneDetector] BMS 파일 {totalBmsFiles}개(앨범 {albumsWithBms}개) 발견 - 현재 곡만 지금 파싱하고 나머지는 플레이할 때 파싱합니다.");

                ParsedBmsNotesByFile.Clear();

                var currentBmsFile = AlbumManager.GetCurrentBmsFile();
                if (string.IsNullOrEmpty(currentBmsFile))
                {
                    ParsedBmsNotes = new List<BmsNote>();
                    _lastParsedBmsFile = null;
                    MelonLogger.Msg("[SceneDetector] 현재 선택된 앨범에 BMS 파일이 없습니다.");
                    return;
                }

                var currentNotes = BmsParser.ParseBmsFile(currentBmsFile, printSummary: false) ?? new List<BmsNote>();
                ParsedBmsNotesByFile[currentBmsFile] = currentNotes;
                _bmsParsedWriteTimeUtc[currentBmsFile] = GetWriteTimeUtcOrMin(currentBmsFile);
                ParsedBmsNotes = currentNotes;
                _lastParsedBmsFile = currentBmsFile;
                ModLog.Verbose($"[SceneDetector] 현재 선택 BMS 노트 로드 완료: {Path.GetFileName(currentBmsFile)} / {ParsedBmsNotes.Count}개");
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
                if (string.IsNullOrEmpty(currentBmsFile))
                {
                    // 현재 앨범에 BMS가 없으면 이전 앨범의 차트가 남지 않도록 빈 목록을 넘깁니다(C2).
                    // 빈 목록이면 주입이 건너뛰어져 원본 차트가 나옵니다.
                    ParsedBmsNotes = new List<BmsNote>();
                    _lastParsedBmsFile = null;
                    NoteArrayHooks.UpdateBmsNotes(ParsedBmsNotes);
                    MelonLogger.Msg("[SceneDetector] 현재 앨범에 BMS가 없어 노트 주입 목록을 비웠습니다.");
                }
                else if (_lastParsedBmsFile != currentBmsFile || ParsedBmsNotes == null || ParsedBmsNotes.Count == 0 ||
                         IsBmsCacheStale(currentBmsFile))
                {
                    ModLog.Verbose($"[SceneDetector] BMS 노트 준비: {Path.GetFileName(currentBmsFile)}");
                    var notes = GetOrParseBms(currentBmsFile);

                    ParsedBmsNotes = notes;
                    _lastParsedBmsFile = currentBmsFile;

                    // 노트가 0개여도 넘깁니다. 예전에는 0개면 넘기지 않아 이전 앨범의 차트가 남았습니다(C2).
                    NoteArrayHooks.UpdateBmsNotes(ParsedBmsNotes);
                    ModLog.Verbose($"[SceneDetector] BMS 노트 업데이트 완료: {ParsedBmsNotes.Count}개 노트");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(ex, "[SceneDetector]", "앨범 에셋 다시 로드 오류");
            }
        }

        /// <summary>BMS 파일 → 파싱했을 때의 수정 시각. 차트를 고친 뒤 재시작 없이 다음 플레이에 반영하려고 둡니다(H12).</summary>
        private static readonly Dictionary<string, DateTime> _bmsParsedWriteTimeUtc =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        private static DateTime GetWriteTimeUtcOrMin(string path)
        {
            try { return File.GetLastWriteTimeUtc(path); }
            catch { return DateTime.MinValue; }
        }

        private static bool IsBmsCacheStale(string file)
        {
            return !_bmsParsedWriteTimeUtc.TryGetValue(file, out var parsedAt) || parsedAt != GetWriteTimeUtcOrMin(file);
        }

        /// <summary>캐시가 있고 파일이 그대로면 캐시를, 아니면 새로 파싱합니다.</summary>
        private static List<BmsNote> GetOrParseBms(string file)
        {
            if (ParsedBmsNotesByFile.TryGetValue(file, out var cached) && !IsBmsCacheStale(file))
                return cached;

            var writeTime = GetWriteTimeUtcOrMin(file);
            var notes = BmsParser.ParseBmsFile(file) ?? new List<BmsNote>();
            ParsedBmsNotesByFile[file] = notes;
            _bmsParsedWriteTimeUtc[file] = writeTime;
            return notes;
        }

        /// <summary>마지막 앨범 스캔 때의 hwa 지문. 곡 선택 씬에 들어올 때 비교합니다.</summary>
        private ulong _albumSignature;

        /// <summary>
        /// hwa 폴더가 바뀌었으면 앨범을 다시 스캔합니다. 곡 목록 주입(initializeMusicDataByDefault)은 씬 로드 뒤에
        /// 돌므로 여기서 다시 읽으면 바로 그 목록에 반영됩니다. MusicID는 폴더별로 고정돼 있어 기존 곡의 기록이 옮겨 가지 않습니다(C4).
        /// </summary>
        private void RescanAlbumsIfChanged()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            ulong signature = AlbumManager.ComputeFolderSignature(_hwaFolderPath);
            if (signature == 0 || signature == _albumSignature)
            {
                ModLog.Verbose($"[SceneDetector] hwa 변경 없음 (확인 {sw.ElapsedMilliseconds}ms)");
                return;
            }

            AlbumManager.ScanAlbums(_hwaFolderPath);
            _albumSignature = signature;
            ParsedBmsNotesByFile.Clear();
            _bmsParsedWriteTimeUtc.Clear();
            ParsedBmsNotes = new List<BmsNote>();
            _lastParsedBmsFile = null;
            SongInfo = AlbumManager.GetCurrentSongInfo() ?? new SongInfo();
            MelonLogger.Msg($"[SceneDetector] hwa 폴더 변경 감지 - 앨범 다시 스캔: {AlbumManager.GetAllAlbums().Count}개 ({sw.ElapsedMilliseconds}ms)");
        }
        private void LoadCustomArtwork()
        {
            try
            {
                ModLog.Verbose("[SceneDetector] 커스텀 아트워크 파일 스캔 시작");

                var imageFile = AlbumManager.GetCurrentImageFile();
                if (!string.IsNullOrEmpty(imageFile))
                {
                    ModLog.Verbose($"[SceneDetector] 커스텀 아트워크 파일 발견: {Path.GetFileName(imageFile)}");
                    CustomAssetManager.LoadCustomArtwork(imageFile);
                }
                else
                {
                    ModLog.Verbose("[SceneDetector] 현재 선택된 앨범에 커스텀 아트워크 이미지 파일이 없습니다.");
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
            TextPatch.OnSceneLoaded(sceneName);

            // 게임 폰트에 없는 글자(간체 한자 등)를 시스템 폰트로 그리게 TMP 전역 fallback을 붙입니다.
            // 처음 한 번만 폰트를 만들고, 이후에는 목록에 붙어 있는지만 확인합니다.
            FontFallback.EnsureInstalled(_gameFolder);

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
                RouteSceneLoaded(sceneName);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(ex, "[SceneDetector]", "씬 처리 중 오류");
            }
        }

        private void RouteSceneLoaded(string sceneName)
        {
            if (sceneName == "FairyModeScene" || sceneName == "PlayMovieScene")
            {
                HandlePlayScene(sceneName);
                BgmBgaInjector.StartInjection(isPlayScene: true);
                BgmGameEndMonitor.AdjustMusicDataOnSceneLoad();
                return;
            }

            if (sceneName == "RenderCutinScene")
            {
                ModLog.Verbose("[SceneDetector] RenderCutinScene 로드 처리 시작 (플레이 씬 로딩)");
                CustomBgmPlayer.Cleanup();
                ModLog.Verbose("[SceneDetector] ✅ 로딩 씬 진입 - 커스텀 프리뷰 BGM 중지");
                BgmBgaInjector.StartInjection(isPlayScene: true);
                InjectCurrentAlbumIfCustomSelected();
                return;
            }

            if (sceneName == ResultSceneName)
            {
                MelonLogger.Msg($"[SceneDetector] 결과 씬 감지: {sceneName} - BGM 주입 중지");
                BgmBgaInjector.StopInjection();
                BgmBgaInjector.ResetPlaySceneState();
                return;
            }

            if (sceneName.StartsWith("MusicSelectScene", StringComparison.Ordinal))
            {
                // 실제 씬 파일명은 "MusicSelectScene_Hasegawa"처럼 접미사가 붙어있어
                // 정확히 일치하는 이름("MusicSelectScene")만 확인하면 이 분기가 절대
                // 실행되지 않습니다. 그 결과 플레이 씬에서 일시정지 메뉴로 곡 선택
                // 화면으로 돌아와도 _isPlayScene이 꺼지지 않아, 방금 플레이하던 곡의
                // 제목/아트워크 강제 치환이 곡 선택 화면에서도 계속 적용됩니다.
                MelonLogger.Msg($"[SceneDetector] 곡 선택 씬 감지: {sceneName} - 플레이 씬 상태 해제");
                BgmBgaInjector.StopInjection();
                BgmBgaInjector.ResetPlaySceneState();
                AudioClipPatch.ResetHandledSelection();
                RescanAlbumsIfChanged();
                FontFallback.LogCoverageDiagnostics();
                return;
            }

            if (sceneName == "SoundPlayerScene" || sceneName == "MoviePlayer_MovieSelect")
            {
                MelonLogger.Msg($"[SceneDetector] 씬 감지: {sceneName} - 커스텀 주입 비활성화 (플레이 씬 아님)");
                BgmBgaInjector.StopInjection();
                BgmBgaInjector.ResetPlaySceneState();
                CustomAssetManager.SetCustomChartSelected(false);
                return;
            }

            MelonLogger.Msg($"[SceneDetector] 알 수 없는 씬: {sceneName} - 게임 플레이 씬일 수 있습니다");
            if (BgmBgaInjector.IsPlayScene())
            {
                ModLog.Verbose($"[SceneDetector] 플레이 씬 상태 감지: {sceneName}");
                if (CustomAssetManager.ShouldInjectCustomContent())
                    InjectCurrentAlbumIfCustomSelected();
            }
            else
            {
                BgmBgaInjector.StartInjection(isPlayScene: false);
            }
        }

        private void InjectCurrentAlbumIfCustomSelected()
        {
            if (CustomAssetManager.IsCustomChartSelected())
            {
                ReloadCurrentAlbumAssets();
                PlaySceneArtworkInjector.StartArtworkInjection();
            }
        }

        private void HandlePlayScene(string sceneName)
        {
            ModLog.Verbose($"[SceneDetector] {sceneName} 로드 처리 시작");

            try
            {
                CustomBgmPlayer.Cleanup();
                ModLog.Verbose("[SceneDetector] ✅ 플레이 씬 진입 - 커스텀 프리뷰 BGM 중지");

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
