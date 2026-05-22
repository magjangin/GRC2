using System;
using System.Collections.Generic;
using MelonLoader;

namespace GRC2.Core
{
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
}
