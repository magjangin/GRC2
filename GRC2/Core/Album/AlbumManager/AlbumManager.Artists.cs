using System;
using MelonLoader;

namespace GRC2.Core
{
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
}
