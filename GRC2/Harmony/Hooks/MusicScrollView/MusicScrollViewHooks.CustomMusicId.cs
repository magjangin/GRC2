using MelonLoader;
using System;
using System.Reflection;
using GRC2.Core;

namespace GRC2.Harmony.Hooks
{
    public static partial class MusicScrollViewHooks
    {
        private static void ApplyCustomMusicIdAndAlbumMappings(
            MusicScrollInjectContext ctx,
            AlbumInfo album,
            int albumIndex,
            object newMusicSelectData,
            string templateSongTitleStr)
        {
            FieldInfo musicIdField = ctx.MusicIdField;
            object templateMusicId = ctx.TemplateMusicId;

            object customMusicId = GenerateCustomMusicID(templateMusicId, album, albumIndex, musicIdField?.FieldType);

            if (musicIdField != null && customMusicId != null)
            {
                try
                {
                    ApplyCompatibleMusicId(musicIdField, templateMusicId, customMusicId, album, newMusicSelectData, templateSongTitleStr);
                }
                catch (Exception ex)
                {
                    MelonLogger.Warning($"[MusicScrollViewHooks]   MusicID 설정 실패: {ex.Message}, 템플릿 MusicID 사용");
                    musicIdField.SetValue(newMusicSelectData, templateMusicId);
                }
            }
            else if (musicIdField != null)
            {
                musicIdField.SetValue(newMusicSelectData, templateMusicId);
            }
        }

        private static void ApplyCompatibleMusicId(
            FieldInfo musicIdField,
            object templateMusicId,
            object customMusicId,
            AlbumInfo album,
            object newMusicSelectData,
            string templateSongTitleStr)
        {
            Type targetType = musicIdField.FieldType;
            Type customIdType = customMusicId.GetType();

            if (targetType == customIdType || targetType.IsAssignableFrom(customIdType))
            {
                musicIdField.SetValue(newMusicSelectData, customMusicId);
                AlbumManager.RegisterMusicIDToAlbum(customMusicId, album);

                if (!string.IsNullOrWhiteSpace(templateSongTitleStr))
                {
                    AlbumManager.RegisterOriginalTitle(customMusicId, templateSongTitleStr);
                    LogVerbose($"[MusicScrollViewHooks]   원본 제목 등록: {customMusicId} -> '{templateSongTitleStr}'");
                }

                LogVerbose($"[MusicScrollViewHooks]   커스텀 MusicID 설정: {customMusicId} (타입: {customIdType.Name}, 앨범: {album.AlbumName})");
                return;
            }

            MelonLogger.Warning($"[MusicScrollViewHooks]   MusicID 타입 불일치: {customIdType.Name} -> {targetType.Name}, 템플릿 MusicID 사용");
            musicIdField.SetValue(newMusicSelectData, templateMusicId);
        }

        private static object GenerateCustomMusicID(object templateMusicId, AlbumInfo album, int albumIndex, Type targetType)
        {
            try
            {
                if (templateMusicId == null)
                    return GenerateFallbackEnumId(targetType);

                Type musicIdType = templateMusicId.GetType();

                if (musicIdType.IsEnum || (targetType != null && targetType.IsEnum))
                    return GenerateCustomEnumMusicId(musicIdType, targetType, albumIndex);

                if (musicIdType == typeof(string) || (targetType != null && targetType == typeof(string)))
                    return $"CUSTOM_{templateMusicId}_{album.AlbumName}_{albumIndex}";

                if (musicIdType.IsPrimitive || musicIdType == typeof(int) || musicIdType == typeof(long))
                    return GeneratePrimitiveMusicId(templateMusicId, musicIdType, albumIndex);

                return templateMusicId;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MusicScrollViewHooks]   MusicID 생성 오류: {ex.Message}");
                return templateMusicId;
            }
        }

        private static object GenerateFallbackEnumId(Type targetType)
        {
            if (targetType == null || !targetType.IsEnum)
                return null;

            Array enumValues = Enum.GetValues(targetType);
            return enumValues.Length > 0 ? enumValues.GetValue(0) : null;
        }

        private static object GenerateCustomEnumMusicId(Type musicIdType, Type targetType, int albumIndex)
        {
            Type enumType = targetType != null && targetType.IsEnum ? targetType : musicIdType;
            Array enumValues = Enum.GetValues(enumType);
            if (enumValues.Length == 0)
                return null;

            int customIntValue = CustomMusicIdStartValue + albumIndex - 1;
            if (customIntValue <= CustomMusicIdEndValue)
            {
                LogVerbose($"[MusicScrollViewHooks]   커스텀 enum 값 생성: 숫자 {customIntValue} (범위: {CustomMusicIdStartValue}-{CustomMusicIdEndValue}, 앨범 인덱스: {albumIndex})");
            }
            else
            {
                int offset = albumIndex - (CustomMusicIdEndValue - CustomMusicIdStartValue + 1);
                customIntValue = CustomMusicIdOverflowBaseValue + offset;
                LogVerbose($"[MusicScrollViewHooks]   커스텀 enum 값 생성: 숫자 {customIntValue} (10000 이상, 앨범 인덱스: {albumIndex}, 오프셋: {offset})");
            }

            object customId = Enum.ToObject(enumType, customIntValue);
            LogVerbose($"[MusicScrollViewHooks]   최종 커스텀 MusicID: {customId} (숫자: {customIntValue})");
            return customId;
        }

        private static object GeneratePrimitiveMusicId(object templateMusicId, Type musicIdType, int albumIndex)
        {
            try
            {
                long baseValue = Convert.ToInt64(templateMusicId);
                long newValue = baseValue + albumIndex;
                return Convert.ChangeType(newValue, musicIdType);
            }
            catch (Exception)
            {
                return templateMusicId;
            }
        }
    }
}
