using System;
using System.IO;
using System.Linq;
using MelonLoader;
using GRC2.Parsers;

namespace GRC2.Core
{
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
