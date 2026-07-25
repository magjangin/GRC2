using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GRC2.Core;
using MelonLoader;

namespace GRC2.Harmony.Hooks
{
    /// <summary>
    /// 원본 곡 목록이 다시 만들어진 직후 커스텀 곡을 추가합니다.
    /// 필터/정렬보다 앞에서 실행되므로 커스텀 곡도 원본과 같은 목록 규칙을 따릅니다.
    /// </summary>
    public static class MusicScrollViewHooks
    {
        private const BindingFlags InstanceFlags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // MusicID 0~53은 실제 곡, 54~511은 SAVEABLE_ID_END(512) 전의 빈 영역입니다.
        private const int CustomMusicIdStart = 54;
        private const int CustomMusicIdEnd = 511;

        private static readonly string[] DifficultyOrder =
        {
            "easy", "normal", "hard", "expert"
        };

        private static readonly Dictionary<object, TemplateSong> TemplateSongs =
            new Dictionary<object, TemplateSong>();

        public static void InitializeMusicDataByDefaultPostfix(object __instance)
        {
            try
            {
                if (__instance == null || CustomAssetManager.IsSceneWhereInjectionDisallowed())
                    return;

                if (!TryGetCellList(__instance, out IList cellList) || cellList.Count == 0)
                    return;

                // 이 시점의 목록에는 원본 곡만 있으므로 정렬/필터 상태에 흔들리지 않는 매핑이 됩니다.
                RegisterArtistFirstSongs(cellList);
                TemplateSongs.Clear();
                InjectCustomMusic(cellList);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MusicScrollViewHooks] 커스텀 곡 목록 주입 오류: {ex.Message}");
            }
        }

        private static bool TryGetCellList(object instance, out IList cellList)
        {
            FieldInfo listField = instance.GetType().GetField(
                "mCellHaviableMusicDataList",
                InstanceFlags);

            cellList = listField?.GetValue(instance) as IList;
            return cellList != null;
        }

        private static void RegisterArtistFirstSongs(IList cellList)
        {
            var seenArtists = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (object item in cellList)
            {
                if (!TryGetMusicSelectData(item, out object musicData))
                    continue;

                Type dataType = musicData.GetType();
                object artistId = GetFieldValue(dataType, musicData, "artistID");
                object musicId = GetFieldValue(dataType, musicData, "musicID");
                string title = GetFieldValue(dataType, musicData, "songTitle")?.ToString();
                string artistKey = artistId?.ToString();

                if (string.IsNullOrWhiteSpace(artistKey) ||
                    musicId == null ||
                    string.IsNullOrWhiteSpace(title) ||
                    !seenArtists.Add(artistKey))
                {
                    continue;
                }

                AlbumManager.RegisterArtistFirstSong(artistKey, musicId, title);
            }
        }

        private static void InjectCustomMusic(IList cellList)
        {
            var albums = AlbumManager.GetAllAlbums();
            if (albums == null || albums.Count == 0)
                return;

            if (!TryCreateContext(cellList[0], out InjectContext context))
                return;

            int injectedCount = 0;
            foreach (AlbumInfo album in albums.Values)
            {
                int customIdValue = CustomMusicIdStart + injectedCount;
                if (customIdValue > CustomMusicIdEnd)
                {
                    MelonLogger.Warning(
                        $"[MusicScrollViewHooks] 커스텀 MusicID 공간이 부족해 나머지 곡을 건너뜁니다. " +
                        $"최대 {CustomMusicIdEnd - CustomMusicIdStart + 1}곡");
                    break;
                }

                if (TryCreateCustomItem(context, album, customIdValue, cellList.Count, out object newItem))
                {
                    cellList.Add(newItem);
                    injectedCount++;
                }
            }

            MelonLogger.Msg(
                $"[MusicScrollViewHooks] 커스텀 곡 주입 완료: {injectedCount}개 (총 {cellList.Count}개)");
        }

        private static bool TryCreateContext(object templateItem, out InjectContext context)
        {
            context = null;
            if (templateItem == null || !TryGetMusicSelectData(templateItem, out object templateData))
                return false;

            Type itemType = templateItem.GetType();
            Type dataType = templateData.GetType();
            FieldInfo indexField = itemType.GetField("mIndex", InstanceFlags);
            FieldInfo dataField = itemType.GetField("mMusicSelectData", InstanceFlags);
            FieldInfo musicIdField = dataType.GetField("musicID", InstanceFlags);

            if (indexField == null || dataField == null || musicIdField == null || !musicIdField.FieldType.IsEnum)
            {
                MelonLogger.Warning("[MusicScrollViewHooks] 곡 목록 필수 필드를 찾지 못했습니다.");
                return false;
            }

            ConstructorInfo itemConstructor = itemType.GetConstructor(
                InstanceFlags,
                binder: null,
                types: new[] { typeof(int), dataType },
                modifiers: null);

            if (itemConstructor == null)
            {
                MelonLogger.Warning("[MusicScrollViewHooks] MusicSelectScrollItemData 생성자를 찾지 못했습니다.");
                return false;
            }

            context = new InjectContext
            {
                TemplateData = templateData,
                DataType = dataType,
                IndexField = indexField,
                MusicIdField = musicIdField,
                ItemConstructor = itemConstructor
            };
            return true;
        }

        private static bool TryCreateCustomItem(
            InjectContext context,
            AlbumInfo album,
            int customIdValue,
            int newIndex,
            out object newItem)
        {
            newItem = null;
            if (album == null)
                return false;

            object musicData = CloneBoxedValue(context.TemplateData);
            if (musicData == null)
                return false;

            string title = album.SongInfo?.Title ?? album.AlbumName ?? "커스텀 곡";
            object customMusicId = Enum.ToObject(context.MusicIdField.FieldType, customIdValue);

            context.MusicIdField.SetValue(musicData, customMusicId);
            ApplyTitleFields(context.DataType, musicData, title);
            ApplyDifficultyLevels(context.DataType, musicData, album);
            ApplyAlbumMetadata(context.DataType, musicData, album, customIdValue);
            ResetPerSongProgress(context.DataType, musicData);

            newItem = context.ItemConstructor.Invoke(new[] { (object)newIndex, musicData });
            context.IndexField.SetValue(newItem, newIndex);

            AlbumManager.RegisterMusicIDToAlbum(customMusicId, album);
            RegisterTemplateSong(context, album, customMusicId);
            return true;
        }

        internal static bool TryGetTemplateSong(
            object customMusicId,
            out object templateMusicId,
            out string templateTitle)
        {
            templateMusicId = null;
            templateTitle = null;

            if (customMusicId == null ||
                !TemplateSongs.TryGetValue(customMusicId, out TemplateSong song))
            {
                return false;
            }

            templateMusicId = song.MusicId;
            templateTitle = song.Title;
            return templateMusicId != null;
        }

        private static void RegisterTemplateSong(
            InjectContext context,
            AlbumInfo album,
            object customMusicId)
        {
            object templateMusicId = GetFieldValue(
                context.DataType,
                context.TemplateData,
                "musicID");
            string templateTitle = GetFieldValue(
                context.DataType,
                context.TemplateData,
                "songTitle")?.ToString();

            string artistId = album.SongInfo?.Character;
            if (string.IsNullOrWhiteSpace(artistId))
                artistId = album.SongInfo?.Artist;

            var artistSong = AlbumManager.GetArtistFirstSong(artistId);
            if (artistSong != null)
            {
                templateMusicId = artistSong.Value.musicId;
                templateTitle = artistSong.Value.title;
            }

            if (templateMusicId == null)
                return;

            TemplateSongs[customMusicId] = new TemplateSong
            {
                MusicId = templateMusicId,
                Title = templateTitle
            };

            if (!string.IsNullOrWhiteSpace(templateTitle))
                AlbumManager.RegisterOriginalTitle(templateMusicId, templateTitle);
        }

        private static object CloneBoxedValue(object source)
        {
            MethodInfo cloneMethod = typeof(object).GetMethod(
                "MemberwiseClone",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return cloneMethod?.Invoke(source, null);
        }

        private static void ApplyTitleFields(Type dataType, object data, string title)
        {
            SetFieldIfPresent(dataType, data, "songTitle", title);
            SetFieldIfPresent(dataType, data, "songTitleForSort", title);
            // 루비는 일본어 제목의 읽기 표기용이며 목록에서 제목 위에 작게 노출됩니다.
            // 커스텀 곡에는 별도 읽기 표기를 사용하지 않으므로 비워 둡니다.
            SetFieldIfPresent(dataType, data, "songTitleRuby", string.Empty);
            SetFieldIfPresent(dataType, data, "songTitleRubyDirect", string.Empty);
        }

        private static void ApplyDifficultyLevels(Type dataType, object data, AlbumInfo album)
        {
            FieldInfo levelField = dataType.GetField("musicLVArray", InstanceFlags);
            if (levelField == null)
                return;

            int length = (levelField.GetValue(data) as int[])?.Length ?? DifficultyOrder.Length;
            var levels = new int[Math.Max(length, DifficultyOrder.Length)];

            for (int i = 0; i < DifficultyOrder.Length; i++)
            {
                if (album.SongInfo?.DifficultyNumbers != null &&
                    album.SongInfo.DifficultyNumbers.TryGetValue(DifficultyOrder[i], out int level))
                {
                    levels[i] = level;
                }
            }

            levelField.SetValue(data, levels);
        }

        private static void ApplyAlbumMetadata(Type dataType, object data, AlbumInfo album, int customIdValue)
        {
            SetFieldIfPresent(dataType, data, "isFull", false);
            SetFieldIfPresent(dataType, data, "isCurrentLock", false);
            SetFieldIfPresent(dataType, data, "isNeedDispNew", false);
            SetFieldIfPresent(dataType, data, "haveMV", album.BgaFiles != null && album.BgaFiles.Count > 0);
            SetFieldIfPresent(dataType, data, "defaultSortPrio", 10000f + customIdValue);
            TrySetEnumField(dataType, data, "sorceTitle", "OTHER");

            string character = album.SongInfo?.Character;
            if (string.IsNullOrWhiteSpace(character))
                return;

            TrySetEnumField(dataType, data, "artistID", NormalizeCharacterName(character));
        }

        private static string NormalizeCharacterName(string value)
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (normalized.Equals("르호", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Morpho", StringComparison.OrdinalIgnoreCase))
                return "Morpho";
            if (normalized.Equals("Roro", StringComparison.OrdinalIgnoreCase))
                return "Roro";
            if (normalized.Equals("룩시아", StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals("Luxair", StringComparison.OrdinalIgnoreCase))
                return "Luxair";
            return normalized;
        }

        private static void ResetPerSongProgress(Type dataType, object data)
        {
            ResetArrayField(dataType, data, "highScoreArray");
            ResetArrayField(dataType, data, "clearBadgeArray");
        }

        private static void ResetArrayField(Type dataType, object data, string fieldName)
        {
            FieldInfo field = dataType.GetField(fieldName, InstanceFlags);
            if (field == null || !field.FieldType.IsArray)
                return;

            Array current = field.GetValue(data) as Array;
            int length = current?.Length ?? DifficultyOrder.Length;
            Array empty = Array.CreateInstance(field.FieldType.GetElementType(), length);
            field.SetValue(data, empty);
        }

        private static void TrySetEnumField(Type dataType, object data, string fieldName, string enumName)
        {
            FieldInfo field = dataType.GetField(fieldName, InstanceFlags);
            if (field == null || !field.FieldType.IsEnum || string.IsNullOrWhiteSpace(enumName))
                return;

            try
            {
                object enumValue = Enum.Parse(field.FieldType, enumName, ignoreCase: true);
                field.SetValue(data, enumValue);
            }
            catch
            {
                // 알려지지 않은 캐릭터/시리즈는 안정적인 템플릿 값을 유지합니다.
            }
        }

        private static bool TryGetMusicSelectData(object item, out object musicData)
        {
            musicData = null;
            if (item == null)
                return false;

            FieldInfo field = item.GetType().GetField("mMusicSelectData", InstanceFlags);
            musicData = field?.GetValue(item);
            return musicData != null;
        }

        private static object GetFieldValue(Type type, object instance, string fieldName)
        {
            return type.GetField(fieldName, InstanceFlags)?.GetValue(instance);
        }

        private static void SetFieldIfPresent(Type type, object instance, string fieldName, object value)
        {
            FieldInfo field = type.GetField(fieldName, InstanceFlags);
            if (field != null && value != null && field.FieldType.IsInstanceOfType(value))
                field.SetValue(instance, value);
        }

        private sealed class InjectContext
        {
            public object TemplateData;
            public Type DataType;
            public FieldInfo IndexField;
            public FieldInfo MusicIdField;
            public ConstructorInfo ItemConstructor;
        }

        private sealed class TemplateSong
        {
            public object MusicId;
            public string Title;
        }
    }
}
