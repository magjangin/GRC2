using MelonLoader;
using UnityEngine;
using System;
using System.Reflection;
using GRC2.Core;
using GRC2.Helpers;

namespace GRC2.Harmony.Handlers
{
    /// <summary>
    /// 오디오 관련 패치 - noticeChangedMusic, changeDifficulty 후킹
    /// 실제 로직은 PreviewAudioManager, CustomBgmPlayer에 위임
    /// </summary>
    public static class AudioClipPatch
    {
        /// <summary>
        /// noticeChangedMusic 후킹 - 곡 변경 알림 시 호출
        /// 호출 감지만 수행
        /// </summary>
        public static void NoticeChangedMusicPostfix(object __instance, object nextMusicID)
        {
            try
            {
                MelonLogger.Msg("===========================================");
                MelonLogger.Msg($"[AudioClipPatch] >>> noticeChangedMusic 호출됨 <<<");
                MelonLogger.Msg($"[AudioClipPatch]   nextMusicID: {nextMusicID ?? "null"}");
                MelonLogger.Msg($"[AudioClipPatch]   nextMusicID 타입: {nextMusicID?.GetType().Name ?? "null"}");
                MelonLogger.Msg("===========================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[AudioClipPatch] ❌ noticeChangedMusic 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// changeDifficulty 후킹 - 난이도 변경 시 호출
        /// 커스텀 차트일 때 프리뷰/환경음 중지, BGM 주입 및 아트워크 업데이트
        /// </summary>
        public static void ChangeDifficultyPostfix(object __instance)
        {
            try
            {
                // 곡 선택 씬이 아닌 곳(SoundPlayerScene, MoviePlayer_MovieSelect 등)에서는 커스텀 차트 처리 안 함
                if (CustomAssetManager.IsSceneWhereInjectionDisallowed())
                    return;

                // 현재 선택된 MusicID 가져오기
                object currentMusicID = GetCurrentMusicIDFromInstance(__instance);
                
                MelonLogger.Msg($"[AudioClipPatch] changeDifficulty 호출됨 - MusicID: {currentMusicID ?? "null"}");
                
                if (currentMusicID != null && AlbumManager.IsCustomChartMusicID(currentMusicID))
                {
                    MelonLogger.Msg($"[AudioClipPatch] ✅ 난이도 변경 - 커스텀 차트 감지: {currentMusicID}");
                    
                    // 커스텀 차트이면 프리뷰/환경음 중지 및 아트워크 업데이트
                    AlbumManager.SelectAlbumByMusicID(currentMusicID);
                    CustomAssetManager.SetCustomChartSelected(true);
                    
                    // 프리뷰와 환경음 중지
                    PreviewAudioManager.StopPreviewAndAmbient();
                    
                    // 커스텀 BGM 주입
                    var bgmFile = AlbumManager.GetCurrentBgmFile();
                    if (!string.IsNullOrEmpty(bgmFile) && System.IO.File.Exists(bgmFile))
                    {
                        MelonLogger.Msg($"[AudioClipPatch] 🎵 난이도 변경 - 커스텀 프리뷰 BGM 준비: {System.IO.Path.GetFileName(bgmFile)}");
                        MelonLoader.MelonCoroutines.Start(CustomBgmPlayer.InjectCustomBgm(bgmFile));
                    }
                    
                    // 아트워크 업데이트
                    var imageFile = AlbumManager.GetCurrentImageFile();
                    if (!string.IsNullOrEmpty(imageFile) && System.IO.File.Exists(imageFile))
                    {
                        CustomAssetManager.LoadCustomArtwork(imageFile);
                        Type instanceType = __instance.GetType();
                        ArtworkUpdater.UpdateArtwork(__instance, instanceType);
                        MelonLogger.Msg("[AudioClipPatch] ✅ 난이도 변경 - 아트워크 업데이트 완료");
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[AudioClipPatch] ❌ changeDifficulty 오류: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 상태 초기화 (씬 변경 시 호출)
        /// </summary>
        public static void ResetState()
        {
            CustomBgmPlayer.ResetState();
            PreviewAudioManager.Reset();
            MelonLogger.Msg("[AudioClipPatch] 상태 초기화됨");
        }
        
        /// <summary>
        /// 인스턴스에서 현재 MusicID 가져오기
        /// </summary>
        public static object GetCurrentMusicIDFromInstance(object instance)
        {
            try
            {
                if (instance == null) return null;
                
                Type instanceType = instance.GetType();
                
                // mCurentMusicId 필드 찾기 (오타 주의: Curent)
                FieldInfo currentMusicIdField = instanceType.GetField("mCurentMusicId", 
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (currentMusicIdField != null)
                {
                    return currentMusicIdField.GetValue(instance);
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[AudioClipPatch] GetCurrentMusicIdFromInstance", "리플렉션 실패");
            }
            
            return null;
        }
    }
}
