using GRC2.Core;
using MelonLoader;

namespace GRC2.Services
{
    internal static partial class CustomChartHandler
    {
        private static bool IsCustomChart(object musicID, string songTitle)
        {
            var originalTitles = AlbumManager.GetAllOriginalTitles();
            if (originalTitles.Contains(songTitle))
            {
                MelonLogger.Msg($"[CustomChartHandler] 🔍 원본 제목 감지: '{songTitle}' → 일반 곡");
                return false;
            }

            var allAlbums = AlbumManager.GetAllAlbums();
            bool foundInCustomAlbums = false;
            if (allAlbums != null)
            {
                foreach (var album in allAlbums.Values)
                {
                    if (album.SongInfo != null && album.SongInfo.Title == songTitle)
                    {
                        foundInCustomAlbums = true;
                        break;
                    }
                }
            }

            if (foundInCustomAlbums)
            {
                MelonLogger.Msg($"[CustomChartHandler] 🔍 커스텀 차트 제목 감지: '{songTitle}' → 커스텀 차트");
                return true;
            }

            bool isCustom = AlbumManager.IsCustomChartMusicID(musicID);
            MelonLogger.Msg($"[CustomChartHandler] 🔍 MusicID로 확인: {isCustom}");
            return isCustom;
        }
    }
}
