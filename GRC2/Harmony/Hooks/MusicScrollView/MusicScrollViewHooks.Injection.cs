using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GRC2.Core;
using GRC2.Helpers;

namespace GRC2.Harmony.Hooks
{
    public static partial class MusicScrollViewHooks
    {
        private const BindingFlags InstanceMemberFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const int CustomMusicIdStartValue = 54;
        private const int CustomMusicIdEndValue = 511;
        private const int CustomMusicIdOverflowBaseValue = 10000;
        private static readonly string[] DifficultyOrder = { "easy", "normal", "hard", "expert" };

        private static void InjectCustomMusicToCellList(object scrollViewInstance, IList cellList)
        {
            try
            {
                if (!TryBuildMusicScrollInjectContext(cellList, out MusicScrollInjectContext ctx))
                    return;

                int injectedCount = 0;
                int albumIndex = 0;
                int totalAlbums = ctx.AllAlbums.Count;

                foreach (var album in ctx.AllAlbums.Values)
                {
                    albumIndex++;
                    string albumTitle = album.SongInfo?.Title ?? album.AlbumName ?? "커스텀 곡";
                    LogVerbose($"[MusicScrollViewHooks]   앨범 [{albumIndex}/{totalAlbums}]: '{album.AlbumName}', 곡 제목: '{albumTitle}'");

                    try
                    {
                        ProcessSingleAlbumInject(ctx, album, albumTitle, albumIndex, totalAlbums, cellList, ref injectedCount);
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[MusicScrollViewHooks]   앨범 '{album.AlbumName}' 주입 실패: {ex.Message}");
                    }
                }

                MelonLogger.Msg($"[MusicScrollViewHooks] 커스텀 곡 주입 완료: {injectedCount}개 추가 (총 {cellList.Count}개)");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MusicScrollViewHooks]   커스텀 곡 주입 오류: {ex.Message}");
                MelonLogger.Warning($"[MusicScrollViewHooks]   스택 트레이스: {ex.StackTrace}");
            }
        }

    }
}
