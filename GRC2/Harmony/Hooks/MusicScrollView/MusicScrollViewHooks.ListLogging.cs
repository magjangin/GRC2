using GRC2.Core;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;


namespace GRC2.Harmony.Hooks
{
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
}
