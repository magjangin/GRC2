using System;
using System.Collections.Generic;
using GRC2.Core;
using HarmonyLib;
using IntiCreates;
using IntiCreates.RythmGame;
using MelonLoader;

namespace GRC2.Harmony.Hooks
{
    using MusicSelectData = cMusicSelectScrollViewDataGetter.MusicSelectData;
    using MusicID = soRythmGameMusicDataMap.MusicID;

    /// <summary>
    /// 원본 곡 목록이 다시 만들어진 직후 커스텀 곡을 추가합니다.
    /// 필터/정렬보다 앞에서 실행되므로 커스텀 곡도 원본과 같은 목록 규칙을 따릅니다.
    /// </summary>
    [HarmonyPatch(typeof(cMusicSelectScrollView), "initializeMusicDataByDefault")]
    public static class MusicScrollViewHooks
    {
        private static readonly AccessTools.FieldRef<cMusicSelectScrollView, List<MusicSelectScrollItemData>> CellListRef =
            AccessTools.FieldRefAccess<cMusicSelectScrollView, List<MusicSelectScrollItemData>>("mCellHaviableMusicDataList");

        // MusicID 0~53은 실제 곡, 54~511은 SAVEABLE_ID_END(512) 전의 빈 영역입니다.
        private const int CustomMusicIdStart = 54;
        private const int CustomMusicIdEnd = 511;

        private static readonly string[] DifficultyOrder =
        {
            "easy", "normal", "hard", "expert"
        };

        private static readonly Dictionary<object, TemplateSong> TemplateSongs =
            new Dictionary<object, TemplateSong>();

        [HarmonyPostfix]
        public static void InitializeMusicDataByDefaultPostfix(cMusicSelectScrollView __instance)
        {
            try
            {
                if (__instance == null || CustomAssetManager.IsSceneWhereInjectionDisallowed())
                    return;

                var cellList = CellListRef(__instance);
                if (cellList == null || cellList.Count == 0)
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

        private static void RegisterArtistFirstSongs(List<MusicSelectScrollItemData> cellList)
        {
            var seenArtists = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (MusicSelectScrollItemData item in cellList)
            {
                if (item == null)
                    continue;

                MusicSelectData musicData = item.mMusicSelectData;
                string artistKey = musicData.artistID.ToString();
                string title = musicData.songTitle;

                if (string.IsNullOrWhiteSpace(artistKey) ||
                    string.IsNullOrWhiteSpace(title) ||
                    !seenArtists.Add(artistKey))
                {
                    continue;
                }

                AlbumManager.RegisterArtistFirstSong(artistKey, musicData.musicID, title);
            }
        }

        private static void InjectCustomMusic(List<MusicSelectScrollItemData> cellList)
        {
            var albums = AlbumManager.GetAllAlbums();
            if (albums == null || albums.Count == 0)
                return;

            if (cellList[0] == null)
                return;

            // initializeMusicDataByDefault의 postfix인 이 메서드는 씬 진입 한 번에 여러 번
            // 호출될 수 있습니다(직접 initialize()에서 한 번, requestSortAndFilter ->
            // doFilterMusicList에서 또 한 번 등). 원본이 매번 mCellHaviableMusicDataList를
            // 새로 Clear()하지 않는 경우에도 중복 주입되지 않도록, 이전에 주입된 커스텀
            // 항목이 남아있다면 먼저 제거하고 다시 주입합니다. 중복 항목이 하나라도 있으면
            // 뒤쪽 곡의 스크롤 위치 계산(getNeedsScrollCountUntilID)이 어긋나 엉뚱한 곡이
            // 선택된 채로 화면에 표시됩니다.
            cellList.RemoveAll(item =>
            {
                if (item?.mMusicSelectData.musicID == null)
                    return false;
                int idValue = (int)item.mMusicSelectData.musicID;
                return idValue >= CustomMusicIdStart && idValue <= CustomMusicIdEnd;
            });

            MusicSelectData templateData = cellList[0].mMusicSelectData;

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

                if (TryCreateCustomItem(templateData, album, customIdValue, cellList.Count, out var newItem))
                {
                    cellList.Add(newItem);
                    injectedCount++;
                }
            }

            MelonLogger.Msg(
                $"[MusicScrollViewHooks] 커스텀 곡 주입 완료: {injectedCount}개 (총 {cellList.Count}개)");
        }

        private static bool TryCreateCustomItem(
            MusicSelectData templateData,
            AlbumInfo album,
            int customIdValue,
            int newIndex,
            out MusicSelectScrollItemData newItem)
        {
            newItem = null;
            if (album == null)
                return false;

            // MusicSelectData는 struct이므로 대입만으로 템플릿 복사가 됩니다.
            MusicSelectData musicData = templateData;

            string title = album.SongInfo?.Title ?? album.AlbumName ?? "커스텀 곡";
            var customMusicId = (MusicID)customIdValue;

            musicData.musicID = customMusicId;
            ApplyTitleFields(ref musicData, title);
            ApplyDifficultyLevels(ref musicData, album);
            ApplyAlbumMetadata(ref musicData, album, customIdValue);
            ResetPerSongProgress(ref musicData);

            newItem = new MusicSelectScrollItemData(newIndex, musicData);

            AlbumManager.RegisterMusicIDToAlbum(customMusicId, album);
            RegisterTemplateSong(templateData, album, customMusicId);
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
            MusicSelectData templateData,
            AlbumInfo album,
            MusicID customMusicId)
        {
            object templateMusicId = templateData.musicID;
            string templateTitle = templateData.songTitle;

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

        private static void ApplyTitleFields(ref MusicSelectData data, string title)
        {
            data.songTitle = title;
            data.songTitleForSort = title;
            // 루비는 일본어 제목의 읽기 표기용이며 목록에서 제목 위에 작게 노출됩니다.
            // 커스텀 곡에는 별도 읽기 표기를 사용하지 않으므로 비워 둡니다.
            data.songTitleRuby = string.Empty;
            data.songTitleRubyDirect = string.Empty;
        }

        private static void ApplyDifficultyLevels(ref MusicSelectData data, AlbumInfo album)
        {
            int length = Math.Max(data.musicLVArray?.Length ?? DifficultyOrder.Length, DifficultyOrder.Length);
            var levels = new int[length];

            for (int i = 0; i < DifficultyOrder.Length; i++)
            {
                if (album.SongInfo?.DifficultyNumbers != null &&
                    album.SongInfo.DifficultyNumbers.TryGetValue(DifficultyOrder[i], out int level))
                {
                    levels[i] = level;
                }
            }

            data.musicLVArray = levels;
        }

        private static void ApplyAlbumMetadata(ref MusicSelectData data, AlbumInfo album, int customIdValue)
        {
            data.isFull = false;
            data.isCurrentLock = false;
            data.isNeedDispNew = false;
            data.haveMV = album.BgaFiles != null && album.BgaFiles.Count > 0;
            data.defaultSortPrio = 10000f + customIdValue;
            data.sorceTitle = soRythmGameMusicDataMap.SorceTitleID.OTHER;

            string character = album.SongInfo?.Character;
            if (string.IsNullOrWhiteSpace(character))
                return;

            // 알려지지 않은 캐릭터는 안정적인 템플릿 값을 유지합니다.
            if (Enum.TryParse(NormalizeCharacterName(character), ignoreCase: true, result: out PCD.MainCharactor artist))
                data.artistID = artist;
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

        private static void ResetPerSongProgress(ref MusicSelectData data)
        {
            data.highScoreArray = new int[data.highScoreArray?.Length ?? DifficultyOrder.Length];
            data.clearBadgeArray = new ResultClearBadge[data.clearBadgeArray?.Length ?? DifficultyOrder.Length];
        }

        private sealed class TemplateSong
        {
            public object MusicId;
            public string Title;
        }
    }
}
