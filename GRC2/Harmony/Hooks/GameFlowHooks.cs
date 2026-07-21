using System.Reflection;
using GRC2.Core;
using GRC2.Parsers;
using MelonLoader;
using System;
using GRC2.Harmony.Handlers;
using GRC2.Services;
using System.Collections;
using UnityEngine;
using System.Diagnostics;

namespace GRC2.Harmony.Hooks
{
    /// <summary>
    /// 게임 흐름 관련 후킹 - 곡 선택 → 게임 시작 → 이전 화면 돌아가기
    /// cMusicSelectSceneUIUpdater의 메서드들을 후킹
    /// </summary>
    public static partial class GameFlowHooks
    {
        private const BindingFlags InstanceFieldFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly string[] StartRythmGameImportantFields =
        {
            "mCurrentDispData",
            "mMusicSelectData",
            "mSelectedMusicID",
            "mIsCurrentAutoPlay",
            "mCurrentMusicID",
            "mMusicID"
        };
    }

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

    public static partial class GameFlowHooks
    {
    

        public static void BackToPreScreenPrefix(object __instance)
        {
            try
            {
                MelonLogger.Msg("===========================================");
                MelonLogger.Msg("[GameFlowHooks] ⬅️ backToPreScreen() 호출됨");
                MelonLogger.Msg($"[GameFlowHooks]   인스턴스: {__instance?.GetType().Name ?? "null"}");
                MelonLogger.Msg($"[GameFlowHooks]   시간: {DateTime.Now:HH:mm:ss.fff}");
                MelonLogger.Msg("[GameFlowHooks]   설명: 이전 화면(곡 선택 화면 등)으로 돌아감");
                
                // 커스텀 프리뷰 BGM 즉시 중지
                CustomBgmPlayer.Cleanup();
                MelonLogger.Msg("[GameFlowHooks] ✅ 커스텀 프리뷰 BGM 중지됨");
                
                // 원래 음소거했던 프리뷰/환경음 복원 (볼륨 1.0으로 재생)
                PreviewAudioManager.RestoreMutedAudioSources();
                MelonLogger.Msg("[GameFlowHooks] ✅ 원본 오디오 소스 복원됨");
                
                MelonLogger.Msg("===========================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] backToPreScreen 오류: {ex.Message}");
            }
        }

        public static void CoOpenPrefix(object __instance)
        {
            try
            {
                // SoundPlayerScene / MoviePlayer_MovieSelect에서는 리스트 선택을 곡 선택 씬으로 착각하지 않음
                if (CustomAssetManager.IsSceneWhereInjectionDisallowed())
                    return;

                if (__instance != null)
                {
                    // 커스텀 차트 감지 및 아트워크/OGG 로드
                    CustomChartHandler.UpdateCustomChartTitle(__instance);
                    
                    // 커스텀 차트 감지 여부 확인하여 텍스트 훅 스위치 제어
                    // CustomChartHandler.UpdateCustomChartTitle에서 이미 처리했으므로 그 결과 확인
                    bool isCustomChart = CustomAssetManager.IsCustomChartSelected();
                    
                    // 텍스트 훅 스위치 제어 (커스텀 차트면 ON, 일반 곡이면 OFF)
                    GRC2.Harmony.Handlers.TextPatch.EnableTextReplacement(isCustomChart);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] coOpen 오류: {ex.Message}");
            }
        }

        public static void CoClosePrefix(object __instance)
        {
            try
            {
                MelonLogger.Msg("===========================================");
                MelonLogger.Msg("[GameFlowHooks] 🚪 coClose() 호출됨");
                MelonLogger.Msg($"[GameFlowHooks]   인스턴스: {__instance?.GetType().Name ?? "null"}");
                MelonLogger.Msg($"[GameFlowHooks]   시간: {DateTime.Now:HH:mm:ss.fff}");
                MelonLogger.Msg("[GameFlowHooks]   설명: 닫기 코루틴");
                MelonLogger.Msg("===========================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] coClose 오류: {ex.Message}");
            }
        }
}

    public static partial class GameFlowHooks
    {
        /// <summary>
        /// coOpenPreMusicStartWindow prefix - 곡 시작 전 윈도우 열기
        /// </summary>
        public static void CoOpenPreMusicStartWindowPrefix(object __instance)
        {
            try
            {
                if (CustomAssetManager.IsSceneWhereInjectionDisallowed())
                    return;

                LogPreStartWindowOpen(__instance);
                TryManipulatePreStartWindowMusicId(__instance);
                MelonLogger.Msg("===========================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] coOpenPreMusicStartWindow 오류: {ex.Message}");
                MelonLogger.Warning($"[GameFlowHooks] 스택 트레이스: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// coOpenPreMusicStartWindow postfix - 곡 시작 전 윈도우 열린 뒤 아트워크 적용
        /// (MusicID→FIRST·앨범 선택은 CustomChartHandler.coOpen에서 수행, 윈도우 아트워크만 여기서)
        /// </summary>
        public static void CoOpenPreMusicStartWindowPostfix(object __instance)
        {
            try
            {
                if (CustomAssetManager.IsSceneWhereInjectionDisallowed())
                    return;

                MelonLogger.Msg("[GameFlowHooks] 🪟 coOpenPreMusicStartWindow() 완료");
                TrySchedulePreStartWindowCustomArtwork();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] coOpenPreMusicStartWindow Postfix 오류: {ex.Message}");
            }
        }

        private static void LogPreStartWindowOpen(object instance)
        {
            MelonLogger.Msg("===========================================");
            MelonLogger.Msg("[GameFlowHooks] 🪟 coOpenPreMusicStartWindow() 호출됨");
            MelonLogger.Msg($"[GameFlowHooks]   인스턴스: {instance?.GetType().Name ?? "null"}");
            MelonLogger.Msg($"[GameFlowHooks]   시간: {DateTime.Now:HH:mm:ss.fff}");
            MelonLogger.Msg("[GameFlowHooks]   설명: 곡 시작 전 윈도우 열기");
        }

        private static void TryManipulatePreStartWindowMusicId(object instance)
        {
            if (instance == null)
                return;

            Type instanceType = instance.GetType();
            object currentMusicID = AudioClipPatch.GetCurrentMusicIDFromInstance(instance);
            MelonLogger.Msg($"[GameFlowHooks]   📌 현재 MusicID: {currentMusicID ?? "null"} (타입: {currentMusicID?.GetType().Name ?? "null"})");

            bool musicIdChanged = TryManipulateMusicIdByArtist(instance, instanceType, currentMusicID);
            if (musicIdChanged)
            {
                MelonLogger.Msg("[GameFlowHooks]   ✅ 아티스트 ID 기반 MusicID 변경 완료");
            }
        }

        private static void TrySchedulePreStartWindowCustomArtwork()
        {
            if (!CustomAssetManager.IsCustomChartSelected())
                return;

            var imageFile = AlbumManager.GetCurrentImageFile();
            if (string.IsNullOrEmpty(imageFile) || !System.IO.File.Exists(imageFile))
                return;

            CustomAssetManager.LoadCustomArtwork(imageFile);
            var sprite = CustomAssetManager.GetCustomArtwork();
            if (sprite != null)
                MelonCoroutines.Start(ApplyPreStartWindowArtworkDelayed(sprite));
        }

        private static IEnumerator ApplyPreStartWindowArtworkDelayed(Sprite sprite)
        {
            yield return new WaitForSeconds(0.15f);
            try
            {
                if (sprite != null && ResultSceneInjector.ApplyArtworkToArtWorkObject(sprite))
                {
                    MelonLogger.Msg("[GameFlowHooks] ✅ 곡 시작 전 윈도우 アート워크 적용");
                }
                else
                {
                    PlaySceneArtworkInjector.StartArtworkInjection();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] 곡 시작 전 윈도우 아트워크 적용 오류: {ex.Message}");
            }
        }
    }

    public static partial class GameFlowHooks
    {
        /// <summary>
        /// startRythmGame prefix - 리듬 게임 시작 요청
        /// </summary>
        public static void StartRythmGamePrefix(object __instance)
        {
            Stopwatch totalSw = Stopwatch.StartNew();
            Stopwatch stepSw = new Stopwatch();
            try
            {
                LogStartRythmGameInvocationHeader(__instance);
                LogStartRythmGameCallStackSample();
                LogStartRythmGameActiveSceneName();
                LogStartRythmGameImportantInstanceFields(__instance);
                RunStartRythmGameAudioCleanupAndRestore(ref stepSw);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] startRythmGame 오류: {ex.Message}");
                MelonLogger.Warning($"[GameFlowHooks] 스택 트레이스: {ex.StackTrace}");
            }
            finally
            {
                totalSw.Stop();
                MelonLogger.Msg($"[GameFlowHooks] ⏱️ 전체 처리 시간: {totalSw.ElapsedMilliseconds}ms");
                MelonLogger.Msg("===========================================");
            }
        }

        /// <summary>
        /// coStartRythmGame prefix - 리듬 게임 시작 코루틴
        /// </summary>
        public static void CoStartRythmGamePrefix(object __instance)
        {
            try
            {
                MelonLogger.Msg("===========================================");
                MelonLogger.Msg("[GameFlowHooks] 🎮 coStartRythmGame() 호출됨");
                MelonLogger.Msg($"[GameFlowHooks]   인스턴스: {__instance?.GetType().Name ?? "null"}");
                MelonLogger.Msg("===========================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] coStartRythmGame 오류: {ex.Message}");
            }
        }

        private static void LogStartRythmGameInvocationHeader(object __instance)
        {
            MelonLogger.Msg("===========================================");
            MelonLogger.Msg("[GameFlowHooks] 🎮 startRythmGame() 호출됨");
            MelonLogger.Msg($"[GameFlowHooks]   인스턴스: {__instance?.GetType().Name ?? "null"}");
            MelonLogger.Msg($"[GameFlowHooks]   시간: {DateTime.Now:HH:mm:ss.fff}");
            MelonLogger.Msg("[GameFlowHooks]   설명: 곡 선택 화면에서 게임 시작 버튼 클릭");
        }

        private static void LogStartRythmGameCallStackSample()
        {
            try
            {
                StackTrace stackTrace = new StackTrace(true);
                MelonLogger.Msg("[GameFlowHooks] 📚 호출 스택:");
                for (int i = 1; i < Math.Min(4, stackTrace.FrameCount); i++)
                {
                    StackFrame frame = stackTrace.GetFrame(i);
                    if (frame == null) continue;
                    string methodName = frame.GetMethod()?.Name ?? "unknown";
                    string className = frame.GetMethod()?.DeclaringType?.Name ?? "unknown";
                    MelonLogger.Msg($"[GameFlowHooks]   [{i}] {className}.{methodName}()");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[GameFlowHooks]   호출 스택 읽기 실패: {ex.Message}");
            }
        }

        private static void LogStartRythmGameActiveSceneName()
        {
            try
            {
                string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                MelonLogger.Msg($"[GameFlowHooks] 🎬 현재 씬: {sceneName}");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[GameFlowHooks]   씬 정보 읽기 실패: {ex.Message}");
            }
        }

        private static void LogStartRythmGameImportantInstanceFields(object __instance)
        {
            if (__instance == null) return;
            try
            {
                Type instanceType = __instance.GetType();
                MelonLogger.Msg($"[GameFlowHooks] 🔍 인스턴스 필드 정보:");
                foreach (string fieldName in StartRythmGameImportantFields)
                    LogOneStartRythmImportantField(instanceType, __instance, fieldName);
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[GameFlowHooks]   필드 정보 읽기 실패: {ex.Message}");
            }
        }

        private static void LogOneStartRythmImportantField(Type instanceType, object instance, string fieldName)
        {
            try
            {
                FieldInfo field = instanceType.GetField(fieldName, InstanceFieldFlags);
                if (field == null) return;
                object value = field.GetValue(instance);
                string valueStr = value?.ToString() ?? "null";
                if (valueStr.Length > 100) valueStr = valueStr.Substring(0, 100) + "...";
                MelonLogger.Msg($"[GameFlowHooks]   ⭐ {fieldName}: {valueStr}");
                if (fieldName == "mCurrentDispData" && value != null)
                    LogMusicIdUnderCurrentDispData(value);
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[GameFlowHooks]   ⚠️ {fieldName} 읽기 실패: {ex.Message}");
            }
        }

        private static void LogMusicIdUnderCurrentDispData(object dispData)
        {
            try
            {
                Type dispDataType = dispData.GetType();
                FieldInfo musicSelectDataField = dispDataType.GetField("mMusicSelectData", InstanceFieldFlags);
                if (musicSelectDataField == null) return;
                object musicSelectData = musicSelectDataField.GetValue(dispData);
                if (musicSelectData == null) return;
                Type musicSelectDataType = musicSelectData.GetType();
                FieldInfo musicIDField = musicSelectDataType.GetField("musicID", InstanceFieldFlags);
                if (musicIDField == null) return;
                object musicID = musicIDField.GetValue(musicSelectData);
                MelonLogger.Msg($"[GameFlowHooks]     🎵 musicID: {musicID}");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[GameFlowHooks]     mCurrentDispData 내부 musicID 읽기 실패: {ex.Message}");
            }
        }

        private static void RunStartRythmGameAudioCleanupAndRestore(ref Stopwatch stepSw)
        {
            stepSw.Restart();
            CustomBgmPlayer.Cleanup();
            stepSw.Stop();
            MelonLogger.Msg($"[GameFlowHooks] ✅ 커스텀 프리뷰 BGM 중지됨 ({stepSw.ElapsedMilliseconds}ms)");
            stepSw.Restart();
            PreviewAudioManager.RestoreMutedAudioSources();
            stepSw.Stop();
            MelonLogger.Msg($"[GameFlowHooks] ✅ 원본 오디오 소스 복원됨 ({stepSw.ElapsedMilliseconds}ms)");
        }
    }

    public static partial class GameFlowHooks
    {
    

        public static void SetIsAutoPlayPrefix(object __instance, bool isAuto)
        {
            try
            {
                MelonLogger.Msg("===========================================");
                MelonLogger.Msg($"[GameFlowHooks] 🎮 setIsAutoPlay() 호출됨");
                MelonLogger.Msg($"[GameFlowHooks]   인스턴스: {__instance?.GetType().Name ?? "null"}");
                MelonLogger.Msg($"[GameFlowHooks]   isAuto: {isAuto}");
                
                if (__instance != null)
                {
                    Type instanceType = __instance.GetType();
                    
                    // mIsCurrentAutoPlay 필드 확인
                    FieldInfo autoPlayField = instanceType.GetField("mIsCurrentAutoPlay", 
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    if (autoPlayField != null)
                    {
                        object currentValue = autoPlayField.GetValue(__instance);
                        MelonLogger.Msg($"[GameFlowHooks]   현재 mIsCurrentAutoPlay 값: {currentValue}");
                    }
                }
                
                MelonLogger.Msg("===========================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] setIsAutoPlay 오류: {ex.Message}");
            }
        }

        public static void SetIsAutoPlayPostfix(object __instance, bool isAuto)
        {
            try
            {
                if (__instance != null)
                {
                    Type instanceType = __instance.GetType();
                    
                    // mIsCurrentAutoPlay 필드 확인
                    FieldInfo autoPlayField = instanceType.GetField("mIsCurrentAutoPlay", 
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    
                    if (autoPlayField != null)
                    {
                        object newValue = autoPlayField.GetValue(__instance);
                        MelonLogger.Msg($"[GameFlowHooks]   설정 후 mIsCurrentAutoPlay 값: {newValue}");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] setIsAutoPlay Postfix 오류: {ex.Message}");
            }
        }

        public static void PushButtonPrefix(object __instance, object __0)
        {
            try
            {
                MelonLogger.Msg("===========================================");
                MelonLogger.Msg("[GameFlowHooks] 🔘 pushButton() 호출됨");
                MelonLogger.Msg($"[GameFlowHooks]   인스턴스: {__instance?.GetType().Name ?? "null"}");
                MelonLogger.Msg($"[GameFlowHooks]   시간: {DateTime.Now:HH:mm:ss.fff}");
                MelonLogger.Msg("[GameFlowHooks]   설명: 메인 메뉴 버튼 클릭");
                MelonLogger.Msg($"[GameFlowHooks]   매개변수: id = {__0} (타입: {__0?.GetType().Name ?? "null"})");
                MelonLogger.Msg("===========================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] pushButton 오류: {ex.Message}");
            }
        }

        
        public static void PushButtonPostfix(object __instance)
        {
            try
            {
                MelonLogger.Msg("[GameFlowHooks] 🔘 pushButton() 완료");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] pushButton Postfix 오류: {ex.Message}");
            }
        }

        public static void SetSceneStatePrefix(object __instance, object __0)
        {
            try
            {
                MelonLogger.Msg("===========================================");
                MelonLogger.Msg("[GameFlowHooks] 🔄 setSceneState() 호출됨");
                MelonLogger.Msg($"[GameFlowHooks]   인스턴스: {__instance?.GetType().Name ?? "null"}");
                MelonLogger.Msg($"[GameFlowHooks]   시간: {DateTime.Now:HH:mm:ss.fff}");
                MelonLogger.Msg("[GameFlowHooks]   설명: 씬 상태 설정");
                MelonLogger.Msg($"[GameFlowHooks]   매개변수: state = {__0} (타입: {__0?.GetType().Name ?? "null"})");
                MelonLogger.Msg("===========================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] setSceneState 오류: {ex.Message}");
            }
        }

        
        public static void SetSceneStatePostfix(object __instance)
        {
            try
            {
                MelonLogger.Msg("[GameFlowHooks] 🔄 setSceneState() 완료");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] setSceneState Postfix 오류: {ex.Message}");
            }
        }

        public static void OpenSortWindowPrefix(object __instance)
        {
            try
            {
                MelonLogger.Msg("===========================================");
                MelonLogger.Msg("[GameFlowHooks] 🔀 openSortWindow() 호출됨");
                MelonLogger.Msg($"[GameFlowHooks]   인스턴스: {__instance?.GetType().Name ?? "null"}");
                MelonLogger.Msg($"[GameFlowHooks]   시간: {DateTime.Now:HH:mm:ss.fff}");
                MelonLogger.Msg("[GameFlowHooks]   설명: 정렬 창 열기");
                MelonLogger.Msg("===========================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] openSortWindow 오류: {ex.Message}");
            }
        }

        public static void OpenFilterWindowPrefix(object __instance)
        {
            try
            {
                MelonLogger.Msg("===========================================");
                MelonLogger.Msg("[GameFlowHooks] 🔍 openFilterWindow() 호출됨");
                MelonLogger.Msg($"[GameFlowHooks]   인스턴스: {__instance?.GetType().Name ?? "null"}");
                MelonLogger.Msg($"[GameFlowHooks]   시간: {DateTime.Now:HH:mm:ss.fff}");
                MelonLogger.Msg("[GameFlowHooks]   설명: 필터 창 열기");
                MelonLogger.Msg("===========================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] openFilterWindow 오류: {ex.Message}");
            }
        }
}
}
