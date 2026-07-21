using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using GRC2.Core;
using GRC2.Helpers;

namespace GRC2.Harmony.Hooks
{
    /// <summary>
    /// cMusicSelectScrollView 및 cMusicSelectScrollViewItem 메서드 후킹
    /// </summary>
    public static partial class MusicScrollViewHooks
    {
        // 상세 목록/리플렉션 로그는 기본 비활성화해서 런타임 로그 노이즈를 줄입니다.
        private static bool IsVerboseLoggingEnabled => false;

        private static void LogVerbose(string message)
        {
            if (IsVerboseLoggingEnabled)
            {
                MelonLogger.Msg(message);
            }
        }
    }

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

    public static partial class MusicScrollViewHooks
    {
        /// <summary>InjectCustomMusicToCellList 한 사이클에 필요한 리플렉션/템플릿 상태.</summary>
        private sealed class MusicScrollInjectContext
        {
            public Dictionary<string, AlbumInfo> AllAlbums;
            public object TemplateItem;
            public Type ItemType;
            public FieldInfo IndexField;
            public FieldInfo MusicSelectDataField;
            public object TemplateMusicSelectData;
            public Type MusicSelectDataType;
            public FieldInfo MusicIdField;
            public FieldInfo SongTitleField;
            public object TemplateMusicId;
            public ConstructorInfo ItemConstructor;
            public ConstructorInfo MsConstructor;
        }

        private sealed class MusicSelectDataFields
        {
            public Type Type;
            public FieldInfo MusicIdField;
            public FieldInfo SongTitleField;
            public object TemplateMusicId;
        }

        private sealed class ScrollItemTemplate
        {
            public object TemplateItem;
            public Type ItemType;
            public FieldInfo IndexField;
            public FieldInfo MusicSelectDataField;
            public object TemplateMusicSelectData;
            public ConstructorInfo ItemConstructor;
        }

        private static bool TryBuildMusicScrollInjectContext(IList cellList, out MusicScrollInjectContext ctx)
        {
            ctx = null;

            if (cellList == null || cellList.Count == 0)
            {
                LogVerbose("[MusicScrollViewHooks]   곡 목록이 비어있어 주입할 수 없습니다.");
                return false;
            }

            var allAlbums = AlbumManager.GetAllAlbums();
            if (allAlbums == null || allAlbums.Count == 0)
            {
                LogVerbose("[MusicScrollViewHooks]   앨범이 없어 주입할 수 없습니다.");
                return false;
            }

            if (!TryReadScrollItemTemplate(cellList, out ScrollItemTemplate itemTemplate))
                return false;

            if (!TryReadMusicSelectDataFields(itemTemplate.TemplateMusicSelectData, out MusicSelectDataFields msFields))
            {
                MelonLogger.Warning("[MusicScrollViewHooks]   MusicSelectData의 필수 필드를 찾을 수 없습니다.");
                return false;
            }

            LogVerbose($"[MusicScrollViewHooks]   🎵 커스텀 곡 주입 시작 (템플릿 MusicID: {msFields.TemplateMusicId}, 앨범 수: {allAlbums.Count})");

            ctx = CreateInjectContext(allAlbums, itemTemplate, msFields);
            return true;
        }

        private static MusicScrollInjectContext CreateInjectContext(
            Dictionary<string, AlbumInfo> allAlbums,
            ScrollItemTemplate itemTemplate,
            MusicSelectDataFields msFields)
        {
            return new MusicScrollInjectContext
            {
                AllAlbums = allAlbums,
                TemplateItem = itemTemplate.TemplateItem,
                ItemType = itemTemplate.ItemType,
                IndexField = itemTemplate.IndexField,
                MusicSelectDataField = itemTemplate.MusicSelectDataField,
                TemplateMusicSelectData = itemTemplate.TemplateMusicSelectData,
                MusicSelectDataType = msFields.Type,
                MusicIdField = msFields.MusicIdField,
                SongTitleField = msFields.SongTitleField,
                TemplateMusicId = msFields.TemplateMusicId,
                ItemConstructor = itemTemplate.ItemConstructor,
                MsConstructor = ResolveMusicSelectDataConstructor(msFields.Type)
            };
        }

        private static bool TryReadScrollItemTemplate(IList cellList, out ScrollItemTemplate itemTemplate)
        {
            itemTemplate = null;
            object templateItem = cellList[0];
            if (templateItem == null)
            {
                MelonLogger.Warning("[MusicScrollViewHooks]   템플릿 항목이 null입니다.");
                return false;
            }

            Type itemType = templateItem.GetType();
            FieldInfo indexField = itemType.GetField("mIndex", InstanceMemberFlags);
            FieldInfo musicSelectDataField = itemType.GetField("mMusicSelectData", InstanceMemberFlags);

            if (indexField == null || musicSelectDataField == null)
            {
                MelonLogger.Warning("[MusicScrollViewHooks]   필수 필드를 찾을 수 없습니다.");
                return false;
            }

            return TryCreateScrollItemTemplate(templateItem, itemType, indexField, musicSelectDataField, out itemTemplate);
        }

        private static bool TryCreateScrollItemTemplate(
            object templateItem,
            Type itemType,
            FieldInfo indexField,
            FieldInfo musicSelectDataField,
            out ScrollItemTemplate itemTemplate)
        {
            itemTemplate = null;
            object templateMusicSelectData = musicSelectDataField.GetValue(templateItem);
            if (templateMusicSelectData == null)
            {
                MelonLogger.Warning("[MusicScrollViewHooks]   템플릿 MusicSelectData가 null입니다.");
                return false;
            }

            ConstructorInfo itemConstructor = ResolveScrollItemConstructor(itemType);
            if (itemConstructor == null)
            {
                MelonLogger.Warning("[MusicScrollViewHooks]   적절한 생성자를 찾을 수 없습니다.");
                return false;
            }

            itemTemplate = new ScrollItemTemplate
            {
                TemplateItem = templateItem,
                ItemType = itemType,
                IndexField = indexField,
                MusicSelectDataField = musicSelectDataField,
                TemplateMusicSelectData = templateMusicSelectData,
                ItemConstructor = itemConstructor
            };
            return true;
        }

        private static bool TryReadMusicSelectDataFields(object templateMusicSelectData, out MusicSelectDataFields fields)
        {
            Type musicSelectDataType = templateMusicSelectData.GetType();
            FieldInfo musicIdField = musicSelectDataType.GetField("musicID", InstanceMemberFlags);
            FieldInfo songTitleField = musicSelectDataType.GetField("songTitle", InstanceMemberFlags);

            if (musicIdField == null || songTitleField == null)
            {
                fields = null;
                return false;
            }

            fields = new MusicSelectDataFields
            {
                Type = musicSelectDataType,
                MusicIdField = musicIdField,
                SongTitleField = songTitleField,
                TemplateMusicId = musicIdField.GetValue(templateMusicSelectData)
            };
            return true;
        }

        private static ConstructorInfo ResolveScrollItemConstructor(Type itemType)
        {
            ConstructorInfo[] constructors = itemType.GetConstructors(InstanceMemberFlags);
            foreach (var constructor in constructors)
            {
                ParameterInfo[] parameters = constructor.GetParameters();
                if (parameters.Length == 2)
                    return constructor;
            }
            return null;
        }

        private static ConstructorInfo ResolveMusicSelectDataConstructor(Type musicSelectDataType)
        {
            ConstructorInfo[] msConstructors = musicSelectDataType.GetConstructors(InstanceMemberFlags);
            LogMusicSelectDataConstructors(msConstructors);

            return FindDefaultMusicSelectDataConstructor(msConstructors) ??
                FindCopyMusicSelectDataConstructor(msConstructors, musicSelectDataType);
        }

        private static void LogMusicSelectDataConstructors(ConstructorInfo[] constructors)
        {
            LogVerbose($"[MusicScrollViewHooks]   MusicSelectData 생성자 개수: {constructors.Length}");

            foreach (var constructor in constructors)
            {
                ParameterInfo[] parameters = constructor.GetParameters();
                string paramInfo = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
                LogVerbose($"[MusicScrollViewHooks]   생성자: 파라미터 수={parameters.Length}, ({paramInfo})");
            }
        }

        private static ConstructorInfo FindDefaultMusicSelectDataConstructor(ConstructorInfo[] constructors)
        {
            foreach (var constructor in constructors)
            {
                if (constructor.GetParameters().Length == 0)
                {
                    LogVerbose("[MusicScrollViewHooks]   ✅ 기본 생성자 발견 (파라미터 0개)");
                    return constructor;
                }
            }

            return null;
        }

        private static ConstructorInfo FindCopyMusicSelectDataConstructor(ConstructorInfo[] constructors, Type musicSelectDataType)
        {
            foreach (var constructor in constructors)
            {
                ParameterInfo[] parameters = constructor.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == musicSelectDataType)
                {
                    LogVerbose("[MusicScrollViewHooks]   ✅ 복사 생성자 발견 (파라미터 1개)");
                    return constructor;
                }
            }

            return null;
        }
    }

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

    public static partial class MusicScrollViewHooks
    {
        private const BindingFlags InstanceFieldFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private sealed class MusicListItemSnapshot
        {
            public object Index;
            public object MusicId;
            public string MusicIdText = "null";
            public string SongTitle = "null";
            public string ArtistId = "null";
        }

        public static void InitializeAllItemByCrrentMusicDataPrefix(object __instance, bool isSceneFirst)
        {
            try
            {
                // SoundPlayerScene / MoviePlayer_MovieSelect는 곡 리스트가 있지만 곡 선택 씬이 아님 → 커스텀 트랙 주입 금지
                if (CustomAssetManager.IsSceneWhereInjectionDisallowed())
                {
                    LogVerbose("[MusicScrollViewHooks] 곡 리스트 씬이지만 곡 선택 씬이 아님 - 커스텀 트랙 주입 건너뜀");
                    return;
                }

                LogVerbose("===========================================");
                LogVerbose("[MusicScrollViewHooks] 🔄 initializeAllItemByCrrentMusicData() 호출됨");
                LogVerbose($"[MusicScrollViewHooks]   인스턴스: {__instance?.GetType().Name ?? "null"}");
                LogVerbose($"[MusicScrollViewHooks]   isSceneFirst: {isSceneFirst}");
                
                if (TryGetCellList(__instance, out IList list))
                {
                    RegisterArtistFirstSongs(list);
                    InjectCustomMusicToCellList(__instance, list);
                }
                
                LogVerbose("===========================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MusicScrollViewHooks] initializeAllItemByCrrentMusicData 오류: {ex.Message}");
            }
        }

        private static bool TryGetCellList(object instance, out IList list)
        {
            list = null;
            if (instance == null)
                return false;

            FieldInfo cellListField = instance.GetType().GetField("mCellHaviableMusicDataList", InstanceFieldFlags);
            object cellList = cellListField?.GetValue(instance);
            list = cellList as IList;
            return list != null;
        }

        private static void RegisterArtistFirstSongs(IList list)
        {
            HashSet<string> seenArtistIds = new HashSet<string>();

            for (int i = 0; i < list.Count; i++)
            {
                object item = list[i];
                if (!TryReadMusicListItemSnapshot(item, out MusicListItemSnapshot snapshot))
                    continue;

                if (snapshot.ArtistId == "null" || !seenArtistIds.Add(snapshot.ArtistId))
                    continue;

                AlbumManager.RegisterArtistFirstSong(snapshot.ArtistId, snapshot.MusicId, snapshot.SongTitle);
            }
        }

        private static bool TryReadMusicListItemSnapshot(object item, out MusicListItemSnapshot snapshot)
        {
            snapshot = new MusicListItemSnapshot();
            if (item == null)
                return false;

            Type itemType = item.GetType();
            FieldInfo indexField = itemType.GetField("mIndex", InstanceFieldFlags);
            FieldInfo musicSelectDataField = itemType.GetField("mMusicSelectData", InstanceFieldFlags);

            snapshot.Index = indexField?.GetValue(item);
            object musicSelectData = musicSelectDataField?.GetValue(item);
            if (musicSelectData == null)
                return false;

            Type musicSelectDataType = musicSelectData.GetType();
            FieldInfo musicIdField = musicSelectDataType.GetField("musicID", InstanceFieldFlags);
            FieldInfo songTitleField = musicSelectDataType.GetField("songTitle", InstanceFieldFlags);
            FieldInfo artistIdField = FindArtistIdField(musicSelectDataType);

            snapshot.MusicId = musicIdField?.GetValue(musicSelectData);
            object songTitle = songTitleField?.GetValue(musicSelectData);
            object artistId = artistIdField?.GetValue(musicSelectData);

            snapshot.MusicIdText = snapshot.MusicId?.ToString() ?? "null";
            snapshot.SongTitle = songTitle?.ToString() ?? "null";
            snapshot.ArtistId = artistId?.ToString() ?? "null";
            return true;
        }

        private static FieldInfo FindArtistIdField(Type musicSelectDataType)
        {
            return musicSelectDataType.GetField("artistID", InstanceFieldFlags) ??
                musicSelectDataType.GetField("mArtistID", InstanceFieldFlags) ??
                musicSelectDataType.GetField("artistId", InstanceFieldFlags) ??
                musicSelectDataType.GetField("mArtistId", InstanceFieldFlags);
        }

    }

    public static partial class MusicScrollViewHooks
    {
        private static object CreateNewMusicSelectData(MusicScrollInjectContext ctx)
        {
            ConstructorInfo msConstructor = ctx.MsConstructor;

            if (msConstructor != null)
                return CreateMusicSelectDataFromConstructor(ctx, msConstructor);

            return CloneTemplateMusicSelectData(ctx.TemplateMusicSelectData);
        }

        private static object CreateMusicSelectDataFromConstructor(MusicScrollInjectContext ctx, ConstructorInfo msConstructor)
        {
            ParameterInfo[] constructorParams = msConstructor.GetParameters();

            if (constructorParams.Length == 0)
            {
                object newMusicSelectData = msConstructor.Invoke(null);
                CopyMusicSelectDataFieldsFromTemplate(ctx.MusicSelectDataType, ctx.TemplateMusicSelectData, newMusicSelectData);
                return newMusicSelectData;
            }

            if (constructorParams.Length == 1 && constructorParams[0].ParameterType == ctx.MusicSelectDataType)
                return msConstructor.Invoke(new object[] { ctx.TemplateMusicSelectData });

            MelonLogger.Warning($"[MusicScrollViewHooks]   지원하지 않는 생성자 파라미터: {constructorParams.Length}개");
            return null;
        }

        private static object CloneTemplateMusicSelectData(object templateMusicSelectData)
        {
            try
            {
                MethodInfo memberwiseClone = typeof(object).GetMethod("MemberwiseClone",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (memberwiseClone != null)
                {
                    object cloned = memberwiseClone.Invoke(templateMusicSelectData, null);
                    LogVerbose("[MusicScrollViewHooks]   ✅ MemberwiseClone으로 MusicSelectData 복사 성공");
                    return cloned;
                }

                MelonLogger.Warning("[MusicScrollViewHooks]   MemberwiseClone을 찾을 수 없습니다.");
                return null;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MusicScrollViewHooks]   MemberwiseClone 실패: {ex.Message}");
                return null;
            }
        }

        private static void CopyMusicSelectDataFieldsFromTemplate(
            Type musicSelectDataType,
            object templateMusicSelectData,
            object newMusicSelectData)
        {
            FieldInfo[] msFields = musicSelectDataType.GetFields(InstanceMemberFlags);
            foreach (var field in msFields)
            {
                try
                {
                    object value = field.GetValue(templateMusicSelectData);
                    field.SetValue(newMusicSelectData, value);
                }
                catch (Exception)
                {
                    // 필드 단위 복사 불가(읽기 전용 등) 시 스킵
                }
            }
        }
    }
}
