using System;
using System.Collections.Generic;
using GRC2.Parsers;
using MelonLoader;
using System.Linq;
using System.IO;

namespace GRC2.Core
{
    /// <summary>
    /// 앨범별 파일 관리 클래스
    /// </summary>
    public class AlbumInfo
    {
        public string AlbumFolderPath { get; set; }
        public string AlbumName { get; set; }
        public List<string> BmsFiles { get; set; } = new List<string>();
        public List<string> ImageFiles { get; set; } = new List<string>();
        public List<string> BgaFiles { get; set; } = new List<string>();
        public List<string> BgmFiles { get; set; } = new List<string>();
        public string TxtFile { get; set; }
        public SongInfo SongInfo { get; set; }
    }

    /// <summary>
    /// 앨범 폴더 스캔 및 파일 매핑 관리
    /// </summary>
    public static partial class AlbumManager
    {
        private static Dictionary<string, AlbumInfo> _albums = new Dictionary<string, AlbumInfo>();
        private static AlbumInfo _currentAlbum = null;
        private static Dictionary<object, AlbumInfo> _musicIdToAlbumMap = new Dictionary<object, AlbumInfo>();
        private static Dictionary<object, string> _musicIdToOriginalTitleMap = new Dictionary<object, string>();
        
        /// <summary>
        /// 아티스트 ID별 첫 곡 정보 저장 (아티스트ID -> (MusicID, 제목))
        /// </summary>
        private static Dictionary<string, (object musicId, string title)> _artistIdToFirstSong = new Dictionary<string, (object, string)>();
        private static string _currentArtistId = null;

    }

    public static partial class AlbumManager
    {
        public static void RegisterArtistFirstSong(string artistId, object musicId, string title)
        {
            if (string.IsNullOrWhiteSpace(artistId) || musicId == null || string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            string normalizedKey = NormalizeArtistId(artistId);
            _artistIdToFirstSong[normalizedKey] = (musicId, title);
            _artistIdToFirstSong[artistId] = (musicId, title);
            MelonLogger.Msg($"[AlbumManager] 아티스트 첫 곡 등록: {artistId} (정규화: {normalizedKey}) -> MusicID: {musicId}, 제목: '{title}'");
        }

        private static string NormalizeArtistId(string artistId)
        {
            if (string.IsNullOrWhiteSpace(artistId))
                return artistId;

            var normalized = artistId.Trim();
            if (string.Equals(normalized, "르호", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "Morpho", StringComparison.OrdinalIgnoreCase))
            {
                return "Morpho";
            }

            if (string.Equals(normalized, "Roro", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "roro", StringComparison.OrdinalIgnoreCase))
            {
                return "Roro";
            }

            if (string.Equals(normalized, "Luxair", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "룩시아", StringComparison.OrdinalIgnoreCase))
            {
                return "Luxair";
            }

            return normalized;
        }

        public static (object musicId, string title)? GetArtistFirstSong(string artistId)
        {
            if (string.IsNullOrWhiteSpace(artistId))
                return null;

            if (_artistIdToFirstSong.TryGetValue(artistId, out var songInfo))
            {
                return songInfo;
            }

            var normalizedId = NormalizeArtistId(artistId);
            foreach (var kvp in _artistIdToFirstSong)
            {
                var normalizedKey = NormalizeArtistId(kvp.Key);
                if (string.Equals(normalizedKey, normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            foreach (var kvp in _artistIdToFirstSong)
            {
                if (string.Equals(kvp.Key, artistId, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            return null;
        }

        public static void SetCurrentArtistId(string artistId)
        {
            _currentArtistId = artistId;
        }

        public static string GetCurrentArtistId()
        {
            return _currentArtistId;
        }
    }

    public static partial class AlbumManager
    {
        /// <summary>
        /// 현재 선택된 앨범의 BMS 파일 가져오기
        /// </summary>
        public static string GetCurrentBmsFile()
        {
            if (_currentAlbum == null || _currentAlbum.BmsFiles.Count == 0)
                return null;
            return _currentAlbum.BmsFiles[0];
        }

        /// <summary>
        /// 현재 선택된 앨범의 이미지 파일 가져오기
        /// </summary>
        public static string GetCurrentImageFile()
        {
            if (_currentAlbum == null || _currentAlbum.ImageFiles.Count == 0)
                return null;
            return _currentAlbum.ImageFiles[0];
        }

        /// <summary>
        /// 현재 선택된 앨범의 BGA 파일 가져오기
        /// </summary>
        public static string GetCurrentBgaFile()
        {
            if (_currentAlbum == null || _currentAlbum.BgaFiles.Count == 0)
                return null;
            return _currentAlbum.BgaFiles[0];
        }

        /// <summary>
        /// 현재 선택된 앨범의 BGM 파일 가져오기 (OGG 우선)
        /// </summary>
        public static string GetCurrentBgmFile()
        {
            if (_currentAlbum == null || _currentAlbum.BgmFiles.Count == 0)
                return null;

            var oggFile = _currentAlbum.BgmFiles.FirstOrDefault(f =>
                f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase));
            return oggFile ?? _currentAlbum.BgmFiles[0];
        }

        /// <summary>
        /// 현재 선택된 앨범의 곡 정보 가져오기
        /// </summary>
        public static SongInfo GetCurrentSongInfo()
        {
            return _currentAlbum?.SongInfo;
        }

        /// <summary>
        /// 현재 선택된 앨범 정보 가져오기
        /// </summary>
        public static AlbumInfo GetCurrentAlbum()
        {
            return _currentAlbum;
        }
    }

    public static partial class AlbumManager
    {
        public static object GetCurrentMusicID()
        {
            if (_currentAlbum == null) return null;
            return GetMusicIDByAlbum(_currentAlbum);
        }

        public static object GetMusicIDByAlbum(AlbumInfo album)
        {
            if (album == null) return null;
            
            // _musicIdToAlbumMap에서 앨범에 해당하는 MusicID 찾기
            foreach (var kvp in _musicIdToAlbumMap)
            {
                if (kvp.Value == album)
                {
                    return kvp.Key;
                }
            }
            
            // 매핑이 없으면 null 반환
            return null;
        }

        public static Dictionary<string, AlbumInfo> GetAllAlbums()
        {
            return _albums;
        }

        public static bool SelectAlbumByMusicID(object musicID)
        {
            if (musicID == null) return false;

            try
            {
                if (_musicIdToAlbumMap.TryGetValue(musicID, out AlbumInfo album))
                {
                    _currentAlbum = album;
                    MelonLogger.Msg($"[AlbumManager] MusicID로 앨범 선택: {album.AlbumName} (MusicID: {musicID})");
                    return true;
                }

                MelonLogger.Msg($"[AlbumManager] MusicID로 앨범을 찾을 수 없습니다: {musicID}");
                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[AlbumManager] MusicID로 앨범 선택 오류: {ex.Message}");
                return false;
            }
        }

        public static void RegisterMusicIDToAlbum(object musicID, AlbumInfo album)
        {
            if (musicID != null && album != null)
            {
                _musicIdToAlbumMap[musicID] = album;
                MelonLogger.Msg($"[AlbumManager] MusicID-앨범 매핑 등록: {musicID} -> {album.AlbumName}");
            }
        }

        public static bool IsCustomChartMusicID(object musicID)
        {
            if (musicID == null) return false;
            return _musicIdToAlbumMap.ContainsKey(musicID);
        }

        public static void RegisterOriginalTitle(object musicID, string originalTitle)
        {
            if (musicID != null && !string.IsNullOrWhiteSpace(originalTitle))
            {
                _musicIdToOriginalTitleMap[musicID] = originalTitle;
                MelonLogger.Msg($"[AlbumManager] MusicID-원본 제목 매핑 등록: {musicID} -> {originalTitle}");
            }
        }

        public static string GetOriginalTitle(object musicID)
        {
            if (musicID == null) return null;
            _musicIdToOriginalTitleMap.TryGetValue(musicID, out string originalTitle);
            return originalTitle;
        }

        public static System.Collections.Generic.HashSet<string> GetAllOriginalTitles()
        {
            var titles = new System.Collections.Generic.HashSet<string>();
            foreach (var title in _musicIdToOriginalTitleMap.Values)
            {
                if (!string.IsNullOrWhiteSpace(title))
                {
                    titles.Add(title);
                }
            }
            return titles;
        }
    }

    public static partial class AlbumManager
    {
        private static readonly HashSet<string> BmsExtensions = new HashSet<string> { ".bms", ".bme", ".bml" };
        private static readonly HashSet<string> ImageExtensions = new HashSet<string> { ".jpg", ".png", ".jpeg" };
        private static readonly HashSet<string> AudioExtensions = new HashSet<string> { ".mp3", ".wav", ".ogg" };

        /// <summary>
        /// hwa 폴더 내의 모든 앨범 폴더를 스캔
        /// </summary>
        public static void ScanAlbums(string hwaFolderPath)
        {
            try
            {
                _albums.Clear();
                _currentAlbum = null;

                MelonLogger.Msg("[AlbumManager] 앨범 폴더 스캔 시작");

                if (!Directory.Exists(hwaFolderPath))
                {
                    MelonLogger.Warning($"[AlbumManager] hwa 폴더가 없습니다: {hwaFolderPath}");
                    return;
                }

                foreach (var albumFolder in EnumerateAlbumFolders(hwaFolderPath))
                {
                    RegisterScannedAlbum(albumFolder);
                }

                SelectDefaultAlbum();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[AlbumManager] 앨범 스캔 오류: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }

        private static List<string> EnumerateAlbumFolders(string hwaFolderPath)
        {
            var albumFolders = Directory.GetDirectories(hwaFolderPath, "*", SearchOption.TopDirectoryOnly)
                .ToList();
            albumFolders.Add(hwaFolderPath);
            MelonLogger.Msg($"[AlbumManager] {albumFolders.Count}개 앨범 폴더 발견");
            return albumFolders;
        }

        private static void RegisterScannedAlbum(string albumFolder)
        {
            var albumInfo = ScanAlbumFolder(albumFolder);
            if (albumInfo == null)
            {
                return;
            }

            string albumKey = GetAlbumKey(albumFolder);
            _albums[albumKey] = albumInfo;
            MelonLogger.Msg($"[AlbumManager] 앨범 등록: {albumKey} ({albumInfo.BmsFiles.Count}개 BMS, {albumInfo.ImageFiles.Count}개 이미지, {albumInfo.BgaFiles.Count}개 BGA, {albumInfo.BgmFiles.Count}개 BGM)");
        }

        private static void SelectDefaultAlbum()
        {
            if (_albums.Count == 0)
            {
                return;
            }

            var defaultAlbum = _albums.Values.FirstOrDefault(a => a.AlbumName == "root")
                ?? _albums.Values.First();
            SelectAlbum(defaultAlbum.AlbumFolderPath);
        }

        /// <summary>
        /// 특정 앨범 폴더 스캔
        /// </summary>
        private static AlbumInfo ScanAlbumFolder(string albumFolderPath)
        {
            try
            {
                var albumInfo = new AlbumInfo
                {
                    AlbumFolderPath = albumFolderPath,
                    AlbumName = Path.GetFileName(albumFolderPath)
                };

                if (!TryPopulateAlbumFiles(albumInfo))
                {
                    return null;
                }

                if (!string.IsNullOrEmpty(albumInfo.TxtFile))
                {
                    albumInfo.SongInfo = SongInfoParser.ParseTxtFile(albumInfo.TxtFile);
                }

                return HasAlbumContent(albumInfo) ? albumInfo : null;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[AlbumManager] 앨범 폴더 스캔 오류 ({albumFolderPath}): {ex.Message}");
                return null;
            }
        }

        private static bool TryPopulateAlbumFiles(AlbumInfo albumInfo)
        {
            try
            {
                foreach (var filePath in Directory.EnumerateFiles(albumInfo.AlbumFolderPath, "*.*", SearchOption.TopDirectoryOnly))
                {
                    AddAlbumFile(albumInfo, filePath);
                }

                return true;
            }
            catch (DirectoryNotFoundException)
            {
                return false;
            }
        }

        private static void AddAlbumFile(AlbumInfo albumInfo, string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();

            if (BmsExtensions.Contains(ext))
            {
                albumInfo.BmsFiles.Add(filePath);
            }
            else if (ImageExtensions.Contains(ext))
            {
                albumInfo.ImageFiles.Add(filePath);
            }
            else if (ext == ".mp4")
            {
                albumInfo.BgaFiles.Add(filePath);
            }
            else if (AudioExtensions.Contains(ext))
            {
                albumInfo.BgmFiles.Add(filePath);
            }
            else if (ext == ".txt" && string.IsNullOrEmpty(albumInfo.TxtFile))
            {
                albumInfo.TxtFile = filePath;
            }
        }

        private static bool HasAlbumContent(AlbumInfo albumInfo)
        {
            return albumInfo.BmsFiles.Count > 0 ||
                albumInfo.ImageFiles.Count > 0 ||
                albumInfo.BgaFiles.Count > 0 ||
                albumInfo.BgmFiles.Count > 0 ||
                !string.IsNullOrEmpty(albumInfo.TxtFile);
        }

        private static string GetAlbumKey(string albumFolderPath)
        {
            string albumKey = Path.GetFileName(albumFolderPath);
            return string.IsNullOrEmpty(albumKey) || albumKey == "hwa" ? "root" : albumKey;
        }
    }

    public static partial class AlbumManager
    {
        /// <summary>
        /// 앨범 폴더 경로로 앨범 선택
        /// </summary>
        public static bool SelectAlbum(string albumFolderPath)
        {
            try
            {
                var album = FindOrScanAlbum(albumFolderPath);
                if (album != null)
                {
                    _currentAlbum = album;
                    MelonLogger.Msg($"[AlbumManager] 앨범 선택: {album.AlbumName} ({album.AlbumFolderPath})");
                    return true;
                }

                MelonLogger.Warning($"[AlbumManager] 앨범을 찾을 수 없습니다: {albumFolderPath}");
                return false;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[AlbumManager] 앨범 선택 오류: {ex.Message}");
                return false;
            }
        }

        private static AlbumInfo FindOrScanAlbum(string albumFolderPath)
        {
            var album = _albums.Values.FirstOrDefault(a => a.AlbumFolderPath == albumFolderPath);
            if (album != null)
            {
                return album;
            }

            album = ScanAlbumFolder(albumFolderPath);
            if (album != null)
            {
                _albums[GetAlbumKey(albumFolderPath)] = album;
            }

            return album;
        }

        /// <summary>
        /// 곡 정보(txt 파일)로 앨범 찾기 및 선택
        /// </summary>
        public static bool SelectAlbumBySongInfo(SongInfo songInfo)
        {
            if (songInfo == null) return false;

            try
            {
                var matchedAlbum = FindAlbumBySongInfo(songInfo);
                if (matchedAlbum != null)
                {
                    _currentAlbum = matchedAlbum;
                    return true;
                }

                return SelectFirstAlbumWhenAvailable();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[AlbumManager] 곡 정보로 앨범 선택 오류: {ex.Message}");
                return false;
            }
        }

        private static AlbumInfo FindAlbumBySongInfo(SongInfo songInfo)
        {
            foreach (var album in _albums.Values)
            {
                if (album.SongInfo == null)
                {
                    continue;
                }

                if (HasSameTitle(songInfo, album.SongInfo))
                {
                    MelonLogger.Msg($"[AlbumManager] 곡 제목으로 앨범 선택: {album.AlbumName} (제목: {songInfo.Title})");
                    return album;
                }

                if (HasSameArtist(songInfo, album.SongInfo))
                {
                    MelonLogger.Msg($"[AlbumManager] 아티스트로 앨범 선택: {album.AlbumName} (아티스트: {songInfo.Artist})");
                    return album;
                }
            }

            return null;
        }

        private static bool HasSameTitle(SongInfo target, SongInfo candidate)
        {
            return !string.IsNullOrEmpty(target.Title) &&
                !string.IsNullOrEmpty(candidate.Title) &&
                candidate.Title.Equals(target.Title, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasSameArtist(SongInfo target, SongInfo candidate)
        {
            return !string.IsNullOrEmpty(target.Artist) &&
                !string.IsNullOrEmpty(candidate.Artist) &&
                candidate.Artist.Equals(target.Artist, StringComparison.OrdinalIgnoreCase);
        }

        private static bool SelectFirstAlbumWhenAvailable()
        {
            if (_albums.Count == 0)
            {
                return false;
            }

            _currentAlbum = _albums.Values.First();
            MelonLogger.Msg($"[AlbumManager] 매칭되는 앨범이 없어 기본 앨범 선택: {_currentAlbum.AlbumName}");
            return true;
        }
    }
}
