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

        /// <summary>
        /// <see cref="AlbumManager.GetCurrentBgmFile"/>가 고른 BGM(없으면 null)과 고른 적이 있는지.
        /// 주입 루프(2초마다)·곡 종료 훅·곡 선택마다 후보를 다시 정렬하지 않게 둡니다. 다시 스캔하면 AlbumInfo가 새로 만들어져 저절로 비워집니다.
        /// </summary>
        internal string ChosenBgmFile;
        internal bool HasChosenBgm;
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

        /// <summary>
        /// 앨범 목록이 바뀔 때마다 올라가는 번호. 곡 목록 postfix는 곡 선택 씬 한 번에 4번쯤 불리는데, 그때마다 앨범 전체를
        /// 정렬하고 ID 배정기(정렬 3번)를 돌리면 곡이 수천 개일 때 씬 진입이 무거워집니다. 이 번호가 같으면 결과를 재사용합니다(P18).
        /// </summary>
        public static int AlbumsVersion { get; private set; }

        private static int _sortedAlbumsVersion = -1;
        private static List<KeyValuePair<string, AlbumInfo>> _sortedAlbums = new List<KeyValuePair<string, AlbumInfo>>();

        /// <summary>앨범을 키 이름순(서수, 대소문자 무시)으로. 목록이 바뀌지 않았으면 같은 목록을 돌려줍니다. 바꾸지 말 것.</summary>
        public static IReadOnlyList<KeyValuePair<string, AlbumInfo>> GetAlbumsSortedByKey()
        {
            if (_sortedAlbumsVersion != AlbumsVersion)
            {
                _sortedAlbums = _albums.OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase).ToList();
                _sortedAlbumsVersion = AlbumsVersion;
            }
            return _sortedAlbums;
        }
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
            new Dictionary<string, (MusicID, string)>(StringComparer.OrdinalIgnoreCase);

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
            ModLog.Verbose($"[AlbumManager] 아티스트 첫 곡 등록: {artistId} (정규화: {normalizedKey}) -> MusicID: {musicId}, 제목: '{title}'");
        }

        public static (MusicID musicId, string title)? GetArtistFirstSong(string artistId)
        {
            if (string.IsNullOrWhiteSpace(artistId))
                return null;

            if (_artistIdToFirstSong.TryGetValue(artistId, out var songInfo))
                return songInfo;

            string normalizedId = CharacterNames.Normalize(artistId);
            if (!string.IsNullOrWhiteSpace(normalizedId) &&
                _artistIdToFirstSong.TryGetValue(normalizedId, out songInfo))
            {
                return songInfo;
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
            var album = _currentAlbum;
            if (album == null || album.BgmFiles.Count == 0)
                return null;

            if (!album.HasChosenBgm)
            {
                album.ChosenBgmFile = ChooseBgmFile(album.BgmFiles);
                album.HasChosenBgm = true;
            }

            return album.ChosenBgmFile;
        }

        /// <summary>
        /// BGM 후보를 고릅니다. ogg, mp3 순으로 쓰고, wav는 파일명이 music/bgm/song으로 시작하는 것만 씁니다.
        /// 키음(Hold end.wav 등)이 BGM으로 뽑히지 않게 하려는 것입니다(C3). 맞는 파일이 없으면 null이라 원본 BGM이 나옵니다.
        /// 같은 확장자가 여럿이면 파일 이름순으로 앞선 것을 씁니다.
        /// </summary>
        internal static string ChooseBgmFile(IReadOnlyList<string> candidates)
        {
            var sorted = candidates.OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase).ToList();

            return sorted.FirstOrDefault(f => HasExtension(f, ".ogg"))
                ?? sorted.FirstOrDefault(f => HasExtension(f, ".mp3"))
                ?? sorted.FirstOrDefault(f => HasExtension(f, ".wav") && IsBgmFileName(f));
        }

        private static bool HasExtension(string path, string extension)
        {
            return path.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBgmFileName(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            return name.StartsWith("music", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("bgm", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("song", StringComparison.OrdinalIgnoreCase);
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
                    ModLog.Verbose($"[AlbumManager] MusicID로 앨범 선택: {album.AlbumName} (MusicID: {musicID})");
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
                ModLog.Verbose($"[AlbumManager] MusicID-앨범 매핑 등록: {musicID} -> {album.AlbumName}");
            }
        }

        public static bool IsCustomChartMusicID(MusicID musicID)
        {
            return _musicIdToAlbumMap.ContainsKey(musicID);
        }

        /// <summary>
        /// 커스텀 MusicID에 등록된 앨범입니다. 원본 곡 ID처럼 등록되지 않은 ID면 null입니다.
        /// 현재 앨범을 바꾸지 않으므로 커서와 무관하게 특정 곡의 정보를 읽을 때 씁니다.
        /// </summary>
        public static AlbumInfo GetAlbumByMusicID(MusicID musicID)
        {
            _musicIdToAlbumMap.TryGetValue(musicID, out AlbumInfo album);
            return album;
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

                ModLog.Verbose($"[AlbumManager] MusicID-원본 제목 매핑 등록: {musicID} -> {originalTitle}");
            }
        }

        public static string GetOriginalTitle(MusicID? musicID)
        {
            if (musicID == null) return null;
            _musicIdToOriginalTitleMap.TryGetValue(musicID.Value, out string originalTitle);
            return originalTitle;
        }

        /// <summary>
        /// 값이 어떤 원제목과 완전히 같은지 봅니다. 캐시한 집합은 밖으로 내보내지 않습니다(H9).
        /// 집합을 돌려주면 호출하는 쪽이 바꿔 버릴 수 있어서 조회만 제공합니다.
        /// </summary>
        public static bool IsOriginalTitle(string value)
        {
            return !string.IsNullOrEmpty(value) && _allOriginalTitles.Contains(value);
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
                AlbumsVersion++;
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
                    RegisterScannedAlbum(albumFolder, hwaFolderPath);
                }

                SelectDefaultAlbum();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[AlbumManager] 앨범 스캔 오류: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }

        /// <summary>
        /// hwa 폴더 구성의 지문(파일 이름·크기·수정 시각)입니다. 곡 선택 씬에 들어올 때 이 값이 바뀌었으면
        /// 다시 스캔해, 곡을 추가하거나 info.txt·BMS를 고친 뒤 게임을 재시작하지 않아도 되게 합니다(H12).
        /// DirectoryInfo 열거는 파일 정보를 목록 조회 결과에서 바로 채우므로 파일마다 따로 stat하지 않습니다.
        /// </summary>
        public static ulong ComputeFolderSignature(string hwaFolderPath)
        {
            const ulong FnvOffset = 14695981039346656037UL;
            const ulong FnvPrime = 1099511628211UL;
            ulong hash = FnvOffset;

            void Mix(string text)
            {
                foreach (char ch in text)
                {
                    hash ^= ch;
                    hash *= FnvPrime;
                }
                hash ^= '|';
                hash *= FnvPrime;
            }

            void MixInt64(long value)
            {
                ulong u = (ulong)value;
                for (int i = 0; i < 8; i++)
                {
                    hash ^= (byte)(u & 0xFF);
                    hash *= FnvPrime;
                    u >>= 8;
                }
            }

            void MixFolder(DirectoryInfo dir)
            {
                Mix(dir.Name);
                foreach (var file in dir.EnumerateFiles().OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
                {
                    Mix(file.Name);
                    MixInt64(file.Length);
                    MixInt64(file.LastWriteTimeUtc.Ticks);
                }
            }

            try
            {
                var root = new DirectoryInfo(hwaFolderPath);
                if (!root.Exists)
                    return 0;

                MixFolder(root);
                foreach (var dir in root.EnumerateDirectories()
                    .Where(d => !string.Equals(d.Name, FallbackFontCandidates.UserFontFolderName, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
                {
                    MixFolder(dir);
                }
                return hash;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[AlbumManager] hwa 폴더 지문 계산 실패: {ex.Message}");
                return 0;
            }
        }

        private static List<string> EnumerateAlbumFolders(string hwaFolderPath)
        {
            // hwa/fonts는 FontFallback이 읽는 사용자 폰트 폴더입니다. 라이선스 txt 등이 있으면 가짜 곡으로 잡히므로 뺍니다.
            var albumFolders = Directory.GetDirectories(hwaFolderPath, "*", SearchOption.TopDirectoryOnly)
                .Where(dir => !string.Equals(Path.GetFileName(dir), FallbackFontCandidates.UserFontFolderName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            albumFolders.Add(hwaFolderPath);
            MelonLogger.Msg($"[AlbumManager] {albumFolders.Count}개 앨범 폴더 발견");
            return albumFolders;
        }

        private static void RegisterScannedAlbum(string albumFolder, string hwaFolderPath)
        {
            var albumInfo = ScanAlbumFolder(albumFolder);
            if (albumInfo == null)
            {
                return;
            }

            string albumKey = GetAlbumKey(albumFolder, hwaFolderPath);
            _albums[albumKey] = albumInfo;
            AlbumsVersion++;
            ModLog.Verbose($"[AlbumManager] 앨범 등록: {albumKey} ({albumInfo.BmsFiles.Count}개 BMS, {albumInfo.ImageFiles.Count}개 이미지, {albumInfo.BgaFiles.Count}개 BGA, {albumInfo.BgmFiles.Count}개 BGM)");
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

                // 열거 순서는 보장되지 않으므로 파일 목록을 이름순으로 정렬해 "첫 번째 파일"이 항상 같게 합니다(H8).
                albumInfo.BmsFiles.Sort(StringComparer.OrdinalIgnoreCase);
                albumInfo.ImageFiles.Sort(StringComparer.OrdinalIgnoreCase);
                albumInfo.BgaFiles.Sort(StringComparer.OrdinalIgnoreCase);
                albumInfo.BgmFiles.Sort(StringComparer.OrdinalIgnoreCase);

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
            else if (ext == ".txt")
            {
                // 열거 순서에 기대지 않고 이름순으로 가장 앞선 txt를 씁니다(I4).
                if (string.IsNullOrEmpty(albumInfo.TxtFile) ||
                    string.Compare(filePath, albumInfo.TxtFile, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    albumInfo.TxtFile = filePath;
                }
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

        /// <summary>
        /// 앨범 폴더 경로에서 앨범 키를 만듭니다. hwa 루트 자신일 때만 <c>"root"</c>를 돌려주고,
        /// <c>hwa/hwa</c>처럼 이름이 <c>hwa</c>인 하위 폴더는 루트와 겹치지 않게 그대로 <c>"hwa"</c>로 둡니다(I4).
        /// </summary>
        internal static string GetAlbumKey(string albumFolderPath, string hwaRootPath = null)
        {
            if (string.IsNullOrEmpty(albumFolderPath))
                return RootAlbumKey;

            string trimmed = albumFolderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.IsNullOrEmpty(hwaRootPath))
            {
                string trimmedRoot = hwaRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(trimmed, trimmedRoot, StringComparison.OrdinalIgnoreCase))
                    return RootAlbumKey;
            }

            string albumKey = Path.GetFileName(trimmed);
            if (string.IsNullOrEmpty(albumKey))
                return RootAlbumKey;

            // hwaRootPath 없이 호출된 경우에만 예전처럼 폴더 이름 "hwa"를 루트로 봅니다.
            if (string.IsNullOrEmpty(hwaRootPath) && string.Equals(albumKey, "hwa", StringComparison.OrdinalIgnoreCase))
                return RootAlbumKey;

            return albumKey;
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
                AlbumsVersion++;
            }

            return album;
        }

        #endregion
    }
}
