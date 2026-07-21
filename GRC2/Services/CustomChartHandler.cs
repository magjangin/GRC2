using System;
using System.Reflection;
using GRC2.Core;
using MelonLoader;
using System.IO;
using GRC2.Harmony.Handlers;
using UnityEngine;

namespace GRC2.Services
{
    /// <summary>
    /// 커스텀 차트 처리 서비스
    /// 성능 최적화: 필드 정보 캐싱
    /// </summary>
    internal static partial class CustomChartHandler
    {
        // ⚡ 성능 최적화: 필드 정보 캐시
        private static FieldInfo _dispDataFieldCache = null;
        private static FieldInfo _musicSelectDataFieldCache = null;
        private static FieldInfo _musicIdFieldCache = null;
        private static FieldInfo _songTitleFieldCache = null;
        
        /// <summary>
        /// 커스텀 차트 감지 및 아트워크/BGM 로드
        /// </summary>
        public static void UpdateCustomChartTitle(object instance)
        {
            try
            {
                // SoundPlayerScene / MoviePlayer_MovieSelect는 곡 리스트가 있지만 곡 선택 씬이 아님 → 커스텀 차트로 착각하지 않음
                if (CustomAssetManager.IsSceneWhereInjectionDisallowed())
                {
                    MelonLogger.Msg("[CustomChartHandler] 곡 선택 씬이 아님 - 커스텀 차트 감지 건너뜀");
                    return;
                }

                MelonLogger.Msg("===========================================");
                MelonLogger.Msg("[CustomChartHandler] 🔍 coOpen() - 커스텀 차트 감지 시작");
                
                Type instanceType = instance.GetType();
                
                // ⚡ 성능 최적화: 캐시된 필드 정보 사용
                FieldInfo dispDataField = _dispDataFieldCache ?? (_dispDataFieldCache = 
                    instanceType.GetField("mCurrentDispData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
                
                if (dispDataField == null)
                {
                    MelonLogger.Msg("[CustomChartHandler] ⚠️ mCurrentDispData 필드를 찾을 수 없습니다.");
                    return;
                }
                
                object dispData = dispDataField.GetValue(instance);
                if (dispData == null)
                {
                    MelonLogger.Msg("[CustomChartHandler] ⚠️ mCurrentDispData가 null입니다.");
                    return;
                }
                
                Type dispDataType = dispData.GetType();
                MelonLogger.Msg($"[CustomChartHandler] ✅ mCurrentDispData 타입: {dispDataType.Name}");
                
                // ⚡ 성능 최적화: 캐시된 필드 정보 사용
                FieldInfo musicSelectDataField = _musicSelectDataFieldCache ?? (_musicSelectDataFieldCache = 
                    dispDataType.GetField("mMusicSelectData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
                
                if (musicSelectDataField == null)
                {
                    MelonLogger.Msg("[CustomChartHandler] ⚠️ mMusicSelectData 필드를 찾을 수 없습니다.");
                    return;
                }
                
                object musicSelectData = musicSelectDataField.GetValue(dispData);
                if (musicSelectData == null)
                {
                    MelonLogger.Msg("[CustomChartHandler] ⚠️ mMusicSelectData가 null입니다.");
                    return;
                }
                
                Type musicSelectDataType = musicSelectData.GetType();
                
                // ⚡ 성능 최적화: 캐시된 필드 정보 사용
                FieldInfo musicIdField = _musicIdFieldCache ?? (_musicIdFieldCache = 
                    musicSelectDataType.GetField("musicID", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
                
                if (musicIdField == null)
                {
                    MelonLogger.Msg("[CustomChartHandler] ⚠️ musicID 필드를 찾을 수 없습니다.");
                    return;
                }
                
                object musicID = musicIdField.GetValue(musicSelectData);
                MelonLogger.Msg($"[CustomChartHandler] 📌 현재 MusicID: {musicID}");
                
                // ⚡ 성능 최적화: 캐시된 필드 정보 사용
                FieldInfo songTitleField = _songTitleFieldCache ?? (_songTitleFieldCache = 
                    musicSelectDataType.GetField("songTitle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
                
                string songTitle = "제목 없음";
                if (songTitleField != null)
                {
                    object titleObj = songTitleField.GetValue(musicSelectData);
                    songTitle = titleObj?.ToString() ?? "제목 없음";
                }
                MelonLogger.Msg($"[CustomChartHandler] 🎵 현재 곡 이름: {songTitle}");
                
                // 곡 제목으로 커스텀 차트인지 확인
                bool isCustom = IsCustomChart(musicID, songTitle);
                
                MelonLogger.Msg($"[CustomChartHandler] 🔍 최종 커스텀 차트 여부: {isCustom}");
                
                if (!isCustom)
                {
                    HandleNonCustomChart();
                    return;
                }
                
                HandleCustomChart(instance, musicID, songTitle);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CustomChartHandler] UpdateCustomChartTitle 오류: {ex.Message}");
                MelonLogger.Warning($"[CustomChartHandler] 스택 트레이스: {ex.StackTrace}");
            }
        }

    }

    internal static partial class CustomChartHandler
    {
        private static void HandleNonCustomChart()
        {
            MelonLogger.Msg("[CustomChartHandler] 일반 곡입니다. 원본 아트워크/BGM으로 복원합니다.");
            CustomAssetManager.SetCustomChartSelected(false);
            MelonLogger.Msg("[CustomChartHandler] ✅ 커스텀 차트 선택 해제 완료");
            CustomBgmPlayer.CleanupAndRestore();
            MelonLogger.Msg("[CustomChartHandler] ✅ 원본 BGM/프리뷰 복원 완료");
            MelonLogger.Msg("===========================================");
        }

        private static void HandleCustomChart(object instance, object musicID, string songTitle)
        {
            MelonLogger.Msg($"[CustomChartHandler] 🎵 ✅ 커스텀 차트 감지됨! MusicID = {musicID}, 곡 이름 (원본 데이터) = {songTitle}");

            bool albumSelected = AlbumManager.SelectAlbumByMusicID(musicID);
            MelonLogger.Msg($"[CustomChartHandler] 📂 앨범 선택 결과: {albumSelected}");
            if (!albumSelected)
            {
                MelonLogger.Msg("[CustomChartHandler] ⚠️ 앨범 선택 실패. 종료합니다.");
                MelonLogger.Msg("===========================================");
                return;
            }

            var currentSongInfo = AlbumManager.GetCurrentSongInfo();
            string actualCustomTitle = currentSongInfo?.Title ?? "제목 없음";
            MelonLogger.Msg($"[CustomChartHandler] 🎵 실제 커스텀 차트 제목: {actualCustomTitle} (원본 곡 제목 '{songTitle}' 유지)");

            CustomAssetManager.SetCustomChartSelected(true);
            CustomAssetManager.SetCustomChartMusicID(musicID);
            MelonLogger.Msg($"[CustomChartHandler] ✅ 커스텀 차트 선택 상태 설정 완료 (MusicID 저장: {musicID})");

            PreviewAudioManager.StopPreviewAndAmbient();
            MelonLogger.Msg("[CustomChartHandler] ✅ 프리뷰/환경음 중지 완료");

            var bgmFile = AlbumManager.GetCurrentBgmFile();
            if (!string.IsNullOrEmpty(bgmFile) && File.Exists(bgmFile))
            {
                MelonLogger.Msg($"[CustomChartHandler] 🎵 커스텀 프리뷰 BGM 준비: {Path.GetFileName(bgmFile)}");
                MelonCoroutines.Start(CustomBgmPlayer.InjectCustomBgm(bgmFile));
                MelonLogger.Msg("[CustomChartHandler] ✅ 커스텀 BGM 주입 코루틴 시작");
            }
            else
            {
                MelonLogger.Msg($"[CustomChartHandler] ⚠️ BGM 파일 없음 (경로: {bgmFile ?? "null"})");
            }

            var imageFile = AlbumManager.GetCurrentImageFile();
            if (!string.IsNullOrEmpty(imageFile) && File.Exists(imageFile))
            {
                MelonLogger.Msg($"[CustomChartHandler] 🖼️ 아트워크 파일 발견: {Path.GetFileName(imageFile)}");
                CustomAssetManager.LoadCustomArtwork(imageFile);

                Type uiUpdaterType = ReflectionHelper.FindType("IntiCreates.cMusicSelectSceneUIUpdater");
                if (uiUpdaterType != null)
                {
                    UnityEngine.Object[] uiUpdaters = UnityEngine.Object.FindObjectsOfType(uiUpdaterType);
                    if (uiUpdaters != null && uiUpdaters.Length > 0)
                    {
                        ArtworkUpdater.UpdateArtwork(uiUpdaters[0], uiUpdaterType);
                        MelonLogger.Msg("[CustomChartHandler] ✅ 아트워크 업데이트 완료");
                    }
                    else
                    {
                        MelonLogger.Msg("[CustomChartHandler] ⚠️ cMusicSelectSceneUIUpdater 인스턴스를 찾을 수 없습니다.");
                    }
                }
                else
                {
                    MelonLogger.Msg("[CustomChartHandler] ⚠️ cMusicSelectSceneUIUpdater 타입을 찾을 수 없습니다.");
                }
            }
            else
            {
                MelonLogger.Msg($"[CustomChartHandler] ⚠️ 아트워크 파일 없음 (경로: {imageFile ?? "null"})");
            }

            if (instance != null)
            {
                SetMusicIdToTemplate(instance, musicID);
            }

            MelonLogger.Msg("[CustomChartHandler] ✅ 커스텀 차트 처리 완료");
            MelonLogger.Msg("===========================================");
        }

        private static void SetMusicIdToTemplate(object instance, object currentMusicID)
        {
            try
            {
                if (instance == null || currentMusicID == null) return;
                Type instanceType = instance.GetType();
                Type musicIdType = currentMusicID.GetType();
                if (!musicIdType.IsEnum) return;
                Array enumValues = Enum.GetValues(musicIdType);
                if (enumValues == null || enumValues.Length == 0) return;
                object templateMusicId = enumValues.GetValue(0);
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                var f1 = instanceType.GetField("mCurentMusicId", flags);
                if (f1 != null) f1.SetValue(instance, templateMusicId);
                var f2 = instanceType.GetField("mCurrentMusicID", flags);
                if (f2 != null) f2.SetValue(instance, templateMusicId);
                MelonLogger.Msg($"[CustomChartHandler] ✅ MusicID 필드 템플릿으로 설정: {templateMusicId}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[CustomChartHandler] SetMusicIdToTemplate 오류: {ex.Message}");
            }
        }
    }

    internal static partial class CustomChartHandler
    {
        private static bool IsCustomChart(object musicID, string songTitle)
        {
            var originalTitles = AlbumManager.GetAllOriginalTitles();
            if (originalTitles.Contains(songTitle))
            {
                MelonLogger.Msg($"[CustomChartHandler] 🔍 원본 제목 감지: '{songTitle}' → 일반 곡");
                return false;
            }

            var allAlbums = AlbumManager.GetAllAlbums();
            bool foundInCustomAlbums = false;
            if (allAlbums != null)
            {
                foreach (var album in allAlbums.Values)
                {
                    if (album.SongInfo != null && album.SongInfo.Title == songTitle)
                    {
                        foundInCustomAlbums = true;
                        break;
                    }
                }
            }

            if (foundInCustomAlbums)
            {
                MelonLogger.Msg($"[CustomChartHandler] 🔍 커스텀 차트 제목 감지: '{songTitle}' → 커스텀 차트");
                return true;
            }

            bool isCustom = AlbumManager.IsCustomChartMusicID(musicID);
            MelonLogger.Msg($"[CustomChartHandler] 🔍 MusicID로 확인: {isCustom}");
            return isCustom;
        }
    }
}
