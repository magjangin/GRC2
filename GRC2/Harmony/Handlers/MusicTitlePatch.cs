using MelonLoader;
using System;
using GRC2.Core;

namespace GRC2.Harmony.Handlers
{
    /// <summary>
    /// cMusicSelectScrollViewDataGetter.getMusicTitle 메서드 후킹 - 커스텀 차트 제목 반환
    /// </summary>
    public static class MusicTitlePatch
    {
        public static void GetMusicTitlePostfix(object __instance, object key, ref string __result)
        {
            try
            {
                if (AlbumManager.IsCustomChartMusicID(key))
                {
                    AlbumManager.SelectAlbumByMusicID(key);

                    var currentSongInfo = AlbumManager.GetCurrentSongInfo();
                    string songTitle = currentSongInfo?.Title;

                    if (!string.IsNullOrWhiteSpace(songTitle))
                    {
                        __result = songTitle;
                        MelonLogger.Msg($"[MusicTitlePatch] 🎵 곡 제목 반환: '{songTitle}'");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MusicTitlePatch] getMusicTitle 후킹 중 오류: {ex.Message}");
            }
        }
    }
}
