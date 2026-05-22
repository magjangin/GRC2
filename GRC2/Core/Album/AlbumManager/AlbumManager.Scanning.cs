using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using GRC2.Parsers;

namespace GRC2.Core
{
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
}
