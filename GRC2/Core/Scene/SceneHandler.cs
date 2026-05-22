using System;
using System.Reflection;
using GRC2.Harmony.Handlers;
using MelonLoader;
using UnityEngine;

namespace GRC2.Core
{
    /// <summary>
    /// 씬별 처리 로직
    /// </summary>
    internal static class SceneHandler
    {
        /// <summary>
        /// FairyModeScene 처리
        /// </summary>
        public static void HandleFairyModeScene()
        {
            MelonLogger.Msg("[SceneHandler] FairyModeScene 로드 처리 시작");

            try
            {
                // 플레이 씬 진입 시 프리뷰 BGM 중지 (중복 재생 방지)
                StopPreviewBGMOnPlayScene();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SceneHandler] FairyModeScene 처리 실패: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }

        /// <summary>
        /// PlayMovieScene 처리
        /// </summary>
        public static void HandlePlayMovieScene()
        {
            MelonLogger.Msg("[SceneHandler] PlayMovieScene 로드 처리 시작");

            try
            {
                // 플레이 씬 진입 시 프리뷰 BGM 중지 (중복 재생 방지)
                StopPreviewBGMOnPlayScene();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SceneHandler] PlayMovieScene 처리 실패: {ex.Message}");
                MelonLogger.Error(ex.StackTrace);
            }
        }
        
        /// <summary>
        /// 플레이 씬 진입 시 프리뷰 BGM을 중지합니다 (중복 재생 방지)
        /// </summary>
        private static void StopPreviewBGMOnPlayScene()
        {
            try
            {
                // cSoundManager 타입 찾기
                Type soundManagerType = ReflectionHelper.FindType("IntiCreates.cSoundManager");
                if (soundManagerType == null)
                {
                    return;
                }
                
                // 싱글톤 인스턴스 찾기
                var soundManagers = UnityEngine.Object.FindObjectsOfType(soundManagerType);
                if (soundManagers == null || soundManagers.Length == 0)
                {
                    return;
                }
                
                object soundManagerInstance = soundManagers[0];
                
                // mPreviewAudioSorce 필드 찾기 및 중지
                FieldInfo previewSourceField = soundManagerType.GetField("mPreviewAudioSorce", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (previewSourceField != null)
                {
                    object sourceValue = previewSourceField.GetValue(soundManagerInstance);
                    if (sourceValue is AudioSource audioSource && audioSource != null)
                    {
                        if (audioSource.isPlaying)
                        {
                            audioSource.Stop();
                            MelonLogger.Msg("[SceneHandler] ✅ 플레이 씬 진입 - 프리뷰 BGM 중지 완료");
                        }
                    }
                }
                
                // mCurrentUsingPreviewBGMClip 필드도 null로 설정
                FieldInfo clipField = soundManagerType.GetField("mCurrentUsingPreviewBGMClip", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (clipField != null)
                {
                    clipField.SetValue(soundManagerInstance, null);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[SceneHandler] 프리뷰 BGM 중지 중 오류: {ex.Message}");
            }
        }
    }
}






