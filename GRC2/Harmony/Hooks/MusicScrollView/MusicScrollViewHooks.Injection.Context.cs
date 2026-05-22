using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GRC2.Core;

namespace GRC2.Harmony.Hooks
{
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
}
