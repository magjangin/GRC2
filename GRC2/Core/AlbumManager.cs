using System;
using System.Collections.Generic;
using GRC2.Helpers;
using GRC2.Parsers;
using IntiCreates;
using MelonLoader;
using System.Linq;
using System.IO;

namespace GRC2.Core
{
    using MusicID = soRythmGameMusicDataMap.MusicID;

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
    public static class AlbumManager
    {
        private static readonly HashSet<string> BmsExtensions = new HashSet<string> { ".bms", ".bme", ".bml" };
        private static readonly HashSet<string> ImageExtensions = new HashSet<string> { ".jpg", ".png", ".jpeg" };
        private static readonly HashSet<string> AudioExtensions = new HashSet<string> { ".mp3", ".wav", ".ogg" };

        /// <summary>hwa 루트 앨범의 키입니다. AlbumName은 폴더 이름("hwa")이므로 이름이 아니라 이 키로 찾습니다.</summary>
        private const string RootAlbumKey = "root";

        private static Dictionary<string, AlbumInfo> _albums = new Dictionary<string, AlbumInfo>();
        private static AlbumInfo _currentAlbum = null;
        private static Dictionary<MusicID, AlbumInfo> _musicIdToAlbumMap = new Dictionary<MusicID, AlbumInfo>();
        private static Dictionary<MusicID, string> _musicIdToOriginalTitleMap = new Dictionary<MusicID, string>();

        /// <summary>_musicIdToOriginalTitleMap의 값만 모은 집합입니다. 제목이 바뀔 때만 다시 만듭니다(H9: 텍스트 설정마다 새로 만들지 않게).</summary>
        private static readonly HashSet<string> _allOriginalTitles = new HashSet<string>();

        /// <summary>앨범 → 커스텀 MusicID의 역방향 사전입니다. 텍스트 설정마다 선형 탐색하지 않게 둡니다(H9).</summary>
        private static readonly Dictionary<AlbumInfo, MusicID> _albumToMusicId = new Dictionary<AlbumInfo, MusicID>();

        /// <summary>
        /// 아티스트 ID별 첫 곡 정보 저장 (아티스트ID -> (MusicID, 제목))
        /// </summary>
        private static Dictionary<string, (MusicID musicId, string title)> _artistIdToFirstSong =
            new Dictionary<string, (MusicID, string)>();

        #region 아티스트 첫 곡 매핑

        public static void RegisterArtistFirstSong(string artistId, MusicID musicId, string title)
        {
            if (string.IsNullOrWhiteSpace(artistId) || string.IsNullOrWhiteSpace(title))
            {
                return;
            }

            string normalizedKey = CharacterNames.Normalize(artistId);
            _artistIdToFirstSong[normalizedKey] = (musicId, title);
            _artistIdToFirstSong[artistId] = (musicId, title);
            MelonLogger.Msg($"[AlbumManager] 아티스트 첫 곡 등록: {artistId} (정규화: {normalizedKey}) -> MusicID: {musicId}, 제목: '{title}'");
        }

        public static (MusicID musicId, string title)? GetArtistFirstSong(string artistId)
        {
            if (string.IsNullOrWhiteSpace(artistId))
                return null;

            if (_artistIdToFirstSong.TryGetValue(artistId, out var songInfo))
            {
                return songInfo;
            }

            var normalizedId = CharacterNames.Normalize(artistId);
            foreach (var kvp in _artistIdToFirstSong)
            {
                var normalizedKey = CharacterNames.Normalize(kvp.Key);
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

        #endregion

        #region 현재 앨범 파일 접근

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

        #endregion

        #region MusicID 매핑

        /// <summary>
        /// 현재 선택된 앨범에 배정된 커스텀 MusicID입니다. 매핑이 없으면 null입니다.
        /// </summary>
        public static MusicID? GetCurrentMusicID()
        {
            if (_currentAlbum == null) return null;
            return GetMusicIDByAlbum(_currentAlbum);
        }

        public static MusicID? GetMusicIDByAlbum(AlbumInfo album)
        {
            if (album == null) return null;

            // 역방향 사전에서 바로 찾습니다. 매핑이 없으면 null입니다.
            return _albumToMusicId.TryGetValue(album, out MusicID musicId) ? musicId : (MusicID?)null;
        }

        public static IReadOnlyDictionary<string, AlbumInfo> GetAllAlbums()
        {
            return _albums;
        }

        public static bool SelectAlbumByMusicID(MusicID musicID)
        {
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

        public static void RegisterMusicIDToAlbum(MusicID musicID, AlbumInfo album)
        {
            if (album != null)
            {
                // 앨범 하나는 ID 하나입니다. 같은 앨범이 다른 ID에 붙어 있던 옛 연결은 지웁니다.
                if (_albumToMusicId.TryGetValue(album, out MusicID previousId) &&
                    !previousId.Equals(musicID) &&
                    _musicIdToAlbumMap.TryGetValue(previousId, out AlbumInfo previousAlbum) &&
                    previousAlbum == album)
                {
                    _musicIdToAlbumMap.Remove(previousId);
                }

                _musicIdToAlbumMap[musicID] = album;
                _albumToMusicId[album] = musicID;
                MelonLogger.Msg($"[AlbumManager] MusicID-앨범 매핑 등록: {musicID} -> {album.AlbumName}");
            }
        }

        public static bool IsCustomChartMusicID(MusicID musicID)
        {
            return _musicIdToAlbumMap.ContainsKey(musicID);
        }

        public static void RegisterOriginalTitle(MusicID musicID, string originalTitle)
        {
            if (!string.IsNullOrWhiteSpace(originalTitle))
            {
                _musicIdToOriginalTitleMap.TryGetValue(musicID, out string previousTitle);
                _musicIdToOriginalTitleMap[musicID] = originalTitle;
                if (previousTitle != originalTitle)
                {
                    RebuildOriginalTitleSet();
                }

                MelonLogger.Msg($"[AlbumManager] MusicID-원본 제목 매핑 등록: {musicID} -> {originalTitle}");
            }
        }

        public static string GetOriginalTitle(MusicID? musicID)
        {
            if (musicID == null) return null;
            _musicIdToOriginalTitleMap.TryGetValue(musicID.Value, out string originalTitle);
            return originalTitle;
        }

        /// <summary>
        /// 모든 원제목 집합입니다. 호출마다 새로 만들지 않고 캐시한 집합을 돌려주므로 호출하는 쪽은 바꾸면 안 됩니다(H9).
        /// </summary>
        public static HashSet<string> GetAllOriginalTitles()
        {
            return _allOriginalTitles;
        }

        private static void RebuildOriginalTitleSet()
        {
            _allOriginalTitles.Clear();
            foreach (var title in _musicIdToOriginalTitleMap.Values)
            {
                if (!string.IsNullOrWhiteSpace(title))
                {
                    _allOriginalTitles.Add(title);
                }
            }
        }

        #endregion

        #region 앨범 폴더 스캔

        /// <summary>
        /// hwa 폴더 내의 모든 앨범 폴더를 스캔
        /// </summary>
        public static void ScanAlbums(string hwaFolderPath)
        {
            try
            {
                _albums.Clear();
                _currentAlbum = null;
                _musicIdToAlbumMap.Clear();
                _musicIdToOriginalTitleMap.Clear();
                _artistIdToFirstSong.Clear();
                _allOriginalTitles.Clear();
                _albumToMusicId.Clear();

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

        /// <summary>
        /// 루트 앨범이 있으면 그것을, 없으면 키 순서상 첫 앨범을 고릅니다. 열거 순서에 기대지 않으려고 정렬합니다.
        /// </summary>
        private static void SelectDefaultAlbum()
        {
            if (_albums.Count == 0)
            {
                return;
            }

            AlbumInfo defaultAlbum = _albums.TryGetValue(RootAlbumKey, out var rootAlbum)
                ? rootAlbum
                : _albums.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase).First().Value;
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
            return string.IsNullOrEmpty(albumKey) || albumKey == "hwa" ? RootAlbumKey : albumKey;
        }

        #endregion

        #region 앨범 선택

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

        #endregion
    }
}
