using GRC2.Core;
using GRC2.Parsers;
using MelonLoader;
using System;
using System.Reflection;

namespace GRC2.Harmony.Hooks
{
    public static partial class GameFlowHooks
    {
        private static bool TryManipulateMusicIdByArtist(object instance, Type instanceType, object currentMusicID)
        {
            try
            {
                if (!TryGetArtistIdFromCurrentAlbum(out string artistIdFromAlbum))
                    return false;

                MelonLogger.Msg($"[GameFlowHooks]   🎨 앨범에서 아티스트 ID 확인: {artistIdFromAlbum}");
                AlbumManager.SetCurrentArtistId(artistIdFromAlbum);

                if (!TryGetFirstSongForArtist(artistIdFromAlbum, out object firstMusicId, out string firstTitle))
                    return false;

                MelonLogger.Msg($"[GameFlowHooks]   📌 첫 곡 정보: MusicID={firstMusicId}, 제목='{firstTitle}'");
                AlbumManager.RegisterOriginalTitle(firstMusicId, firstTitle);
                MelonLogger.Msg($"[GameFlowHooks]   ✅ 원본 제목 등록: {firstMusicId} -> '{firstTitle}'");

                ApplyFirstSongMusicIdToUpdaterInstance(instance, instanceType, firstMusicId, currentMusicID);
                return true;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] MusicID 조작 오류: {ex.Message}");
                MelonLogger.Warning($"[GameFlowHooks] 스택 트레이스: {ex.StackTrace}");
                return false;
            }
        }

        private static bool TryGetArtistIdFromCurrentAlbum(out string artistId)
        {
            artistId = null;
            var currentAlbum = AlbumManager.GetCurrentAlbum();
            if (currentAlbum?.SongInfo == null)
            {
                MelonLogger.Msg("[GameFlowHooks]   ⚠️ 현재 앨범 정보를 찾을 수 없습니다");
                return false;
            }

            artistId = currentAlbum.SongInfo.Character ?? "";
            if (string.IsNullOrWhiteSpace(artistId))
                artistId = currentAlbum.SongInfo.Artist ?? "";

            if (string.IsNullOrWhiteSpace(artistId))
            {
                MelonLogger.Msg("[GameFlowHooks]   ⚠️ 앨범에서 아티스트 ID를 찾을 수 없습니다");
                return false;
            }
            return true;
        }

        private static bool TryGetFirstSongForArtist(string artistId, out object firstMusicId, out string firstTitle)
        {
            firstMusicId = null;
            firstTitle = null;
            var firstSongInfo = AlbumManager.GetArtistFirstSong(artistId);
            if (firstSongInfo == null)
            {
                MelonLogger.Msg($"[GameFlowHooks]   ⚠️ 아티스트 '{artistId}'의 첫 곡 정보를 찾을 수 없습니다");
                return false;
            }
            (firstMusicId, firstTitle) = firstSongInfo.Value;
            return true;
        }

        private static void ApplyFirstSongMusicIdToUpdaterInstance(
            object instance,
            Type instanceType,
            object firstMusicId,
            object previousMusicId)
        {
            FieldInfo currentMusicIdField = instanceType.GetField("mCurentMusicId",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (currentMusicIdField != null)
            {
                currentMusicIdField.SetValue(instance, firstMusicId);
                MelonLogger.Msg($"[GameFlowHooks]   ✅ mCurentMusicId 필드 업데이트 [게임 필드명]: {previousMusicId} -> {firstMusicId}");
            }

            FieldInfo currentMusicIDField = instanceType.GetField("mCurrentMusicID",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (currentMusicIDField != null)
            {
                currentMusicIDField.SetValue(instance, firstMusicId);
                MelonLogger.Msg($"[GameFlowHooks]   ✅ mCurrentMusicID 필드 업데이트 [게임 필드명]: {previousMusicId} -> {firstMusicId}");
            }
        }

        private static void UpdateMusicSelectDataFields(object musicSelectData, Type musicSelectDataType, AlbumInfo album)
        {
            try
            {
                if (album?.SongInfo == null)
                    return;

                var songInfo = album.SongInfo;
                MelonLogger.Msg("[GameFlowHooks]   🔧 커스텀 차트 정보로 필드 업데이트 시작:");
                UpdateSongTitleFieldIfPresent(musicSelectData, musicSelectDataType, songInfo);
                TryUpdateArtistFieldFromSongInfo(musicSelectData, musicSelectDataType, songInfo);
                LogOptionalMusicSelectDebugFields(musicSelectData, musicSelectDataType);
                MelonLogger.Msg("[GameFlowHooks]   ✅ 필드 업데이트 완료");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] UpdateMusicSelectDataFields 오류: {ex.Message}");
            }
        }

        private static void UpdateSongTitleFieldIfPresent(object musicSelectData, Type musicSelectDataType, SongInfo songInfo)
        {
            FieldInfo songTitleField = musicSelectDataType.GetField("songTitle",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (songTitleField != null && !string.IsNullOrEmpty(songInfo.Title))
            {
                songTitleField.SetValue(musicSelectData, songInfo.Title);
                MelonLogger.Msg($"[GameFlowHooks]     ✅ songTitle: {songInfo.Title}");
            }
        }

        private static void TryUpdateArtistFieldFromSongInfo(object musicSelectData, Type musicSelectDataType, SongInfo songInfo)
        {
            if (string.IsNullOrEmpty(songInfo.Artist))
                return;
            FieldInfo artistField = musicSelectDataType.GetField("artist",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? musicSelectDataType.GetField("artistName",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?? musicSelectDataType.GetField("mArtist",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (artistField == null)
                return;
            try
            {
                artistField.SetValue(musicSelectData, songInfo.Artist);
                MelonLogger.Msg($"[GameFlowHooks]     ✅ artist: {songInfo.Artist}");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[GameFlowHooks]     ⚠️ artist 필드 설정 실패: {ex.Message}");
            }
        }

        private static void LogOptionalMusicSelectDebugFields(object musicSelectData, Type musicSelectDataType)
        {
            string[] possibleFieldNames = {
                "difficulty", "level", "genre", "bpm", "length",
                "mDifficulty", "mLevel", "mGenre", "mBpm", "mLength",
                "songArtist", "composer", "mComposer"
            };
            foreach (var fieldName in possibleFieldNames)
            {
                FieldInfo field = musicSelectDataType.GetField(fieldName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (field == null) continue;
                try
                {
                    object currentValue = field.GetValue(musicSelectData);
                    MelonLogger.Msg($"[GameFlowHooks]     📌 {fieldName}: {currentValue} (타입: {field.FieldType.Name})");
                }
                catch (Exception)
                {
                    // 디버그 읽기 실패 시 무시
                }
            }
        }
    }
}
