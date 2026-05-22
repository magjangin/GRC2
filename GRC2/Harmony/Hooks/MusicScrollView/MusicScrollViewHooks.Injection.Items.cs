using MelonLoader;
using System;
using System.Collections;
using System.Reflection;
using GRC2.Core;

namespace GRC2.Harmony.Hooks
{
    public static partial class MusicScrollViewHooks
    {
        private static void ProcessSingleAlbumInject(
            MusicScrollInjectContext ctx,
            AlbumInfo album,
            string albumTitle,
            int albumIndex,
            int totalAlbums,
            IList cellList,
            ref int injectedCount)
        {
            object newMusicSelectData = CreateNewMusicSelectData(ctx);
            if (newMusicSelectData == null)
            {
                MelonLogger.Warning("[MusicScrollViewHooks]   MusicSelectData 생성 실패");
                return;
            }

            if (ctx.SongTitleField != null)
                ctx.SongTitleField.SetValue(newMusicSelectData, albumTitle);

            ApplyMusicLvArrayFromAlbumSongInfo(album, newMusicSelectData, ctx);

            object templateSongTitle = ctx.SongTitleField.GetValue(ctx.TemplateMusicSelectData);
            string templateSongTitleStr = templateSongTitle?.ToString() ?? "";
            ApplyCustomMusicIdAndAlbumMappings(ctx, album, albumIndex, newMusicSelectData, templateSongTitleStr);

            object newItem = CreateInjectedScrollItem(ctx, newMusicSelectData, cellList, injectedCount);
            cellList.Add(newItem);
            injectedCount++;

            LogVerbose($"[MusicScrollViewHooks]   ✅ [{albumIndex}/{totalAlbums}] '{albumTitle}' 추가 완료 (앨범: {album.AlbumName})");
        }

        private static object CreateInjectedScrollItem(
            MusicScrollInjectContext ctx,
            object newMusicSelectData,
            IList cellList,
            int injectedCount)
        {
            object templateIndex = ctx.IndexField.GetValue(ctx.TemplateItem);
            object newItem = ctx.ItemConstructor.Invoke(new object[] { templateIndex, newMusicSelectData });

            int newIndex = cellList.Count + injectedCount;
            ctx.IndexField.SetValue(newItem, newIndex);
            return newItem;
        }

        private static void ApplyMusicLvArrayFromAlbumSongInfo(
            AlbumInfo album,
            object newMusicSelectData,
            MusicScrollInjectContext ctx)
        {
            if (album.SongInfo?.DifficultyNumbers == null || album.SongInfo.DifficultyNumbers.Count == 0)
                return;

            try
            {
                FieldInfo musicLVArrayField = ctx.MusicSelectDataType.GetField("musicLVArray", InstanceMemberFlags);
                if (musicLVArrayField == null)
                    return;

                int[] newLVArray = CreateDifficultyArrayFromTemplate(ctx, musicLVArrayField);
                ApplyAlbumDifficulties(album, newLVArray);
                musicLVArrayField.SetValue(newMusicSelectData, newLVArray);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MusicScrollViewHooks]   musicLVArray 설정 실패: {ex.Message}");
            }
        }

        private static int[] CreateDifficultyArrayFromTemplate(MusicScrollInjectContext ctx, FieldInfo musicLVArrayField)
        {
            object templateLVArray = musicLVArrayField.GetValue(ctx.TemplateMusicSelectData);
            if (templateLVArray is int[] templateArray && templateArray.Length >= 4)
            {
                int[] newLVArray = new int[templateArray.Length];
                Array.Copy(templateArray, newLVArray, templateArray.Length);
                return newLVArray;
            }

            return new int[4];
        }

        private static void ApplyAlbumDifficulties(AlbumInfo album, int[] newLVArray)
        {
            for (int i = 0; i < DifficultyOrder.Length && i < newLVArray.Length; i++)
            {
                string difficultyName = DifficultyOrder[i];
                if (album.SongInfo.DifficultyNumbers.ContainsKey(difficultyName))
                    newLVArray[i] = album.SongInfo.DifficultyNumbers[difficultyName];
            }
        }

    }
}
