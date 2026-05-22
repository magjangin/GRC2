using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GRC2.Harmony.Hooks;
using GRC2.Helpers;
using GRC2.Parsers;
using MelonLoader;

namespace GRC2.Core
{
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

        private void LoadCustomAssets()
        {
            try
            {
                MelonLogger.Msg("[SceneDetector] 커스텀 아트워크 및 프리뷰 BGM 파일 스캔 시작");

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

                var currentBgmFile = AlbumManager.GetCurrentBgmFile();
                if (!string.IsNullOrEmpty(currentBgmFile) && File.Exists(currentBgmFile))
                {
                    MelonLogger.Msg($"[SceneDetector] 프리뷰 BGM 파일 발견: {Path.GetFileName(currentBgmFile)}");
                    CustomAssetManager.LoadCustomPreviewBGM(currentBgmFile);
                }
                else
                {
                    MelonLogger.Msg("[SceneDetector] 프리뷰 BGM 파일(music.ogg)을 찾을 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(ex, "[SceneDetector]", "커스텀 아트워크 및 프리뷰 BGM 로드 오류");
            }
        }
    }
}
