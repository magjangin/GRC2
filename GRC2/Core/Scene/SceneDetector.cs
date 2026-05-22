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
            
            // 스팀 업데이트 차단
            MelonLogger.Msg("[GRC2] [Main] 스팀 업데이트 차단 시작...");
            Helpers.SteamManifestLocker.LockManifest();
            MelonLogger.Msg("[GRC2] [Main] 스팀 업데이트 차단 완료");
            
            // 곡 주입 시스템 초기화 (곡목록에 커스텀 차트 추가)
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

                // BmsNoteConverter 초기화
                MelonLogger.Msg("[SceneDetector] BmsNoteConverter 초기화 시작...");
                BmsNoteConverter.Initialize();
                MelonLogger.Msg("[SceneDetector] BmsNoteConverter 초기화 완료");

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
                
                // 커스텀 아트워크 및 프리뷰 BGM 로드 (앨범별)
                MelonLogger.Msg("[SceneDetector] 커스텀 아트워크 및 프리뷰 BGM 스캔 시작...");
                LoadCustomAssets();
                
                // BMS 파일 스캔 및 파싱 (앨범별)
                MelonLogger.Msg("[SceneDetector] BMS 파일 스캔 시작...");
                ScanAndParseBmsFiles();
                MelonLogger.Msg($"[SceneDetector] BMS 파일 스캔 완료: {ParsedBmsNotesByFile.Count}개 파일, 현재 선택 파일 노트 {ParsedBmsNotes.Count}개");

                // NoteArrayHooks 초기화 (BMS 노트 주입)
                NoteArrayHooks.Initialize(ParsedBmsNotes);


                // BgmInjector 초기화 (Harmony 후킹)
                MelonLogger.Msg("[SceneDetector] BgmInjector 초기화 시작...");
                BgmInjector.Initialize();
                MelonLogger.Msg("[SceneDetector] BgmInjector 초기화 완료");
                
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

    }
}
