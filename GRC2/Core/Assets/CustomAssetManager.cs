using MelonLoader;
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Reflection;
using GRC2.Helpers;

namespace GRC2.Core
{
    /// <summary>
    /// 커스텀 아트워크와 BGM 관리
    /// </summary>
    public static class CustomAssetManager
    {
        private static Sprite customArtworkSprite = null;
        private static AudioClip customPreviewBGM = null;
        private static bool isCustomChartSelected = false;
        private static object customChartMusicID = null; // 커스텀 차트의 MusicID 저장
        private static string lastLoadedImagePath = null; // 마지막으로 로드한 이미지 경로 캐싱 (성능 최적화)
        

        /// <summary>
        /// 커스텀 아트워크 로드 (512x512 이미지는 50% 축소)
        /// </summary>
        public static void LoadCustomArtwork(string imagePath)
        {
            try
            {
                if (System.IO.File.Exists(imagePath))
                {
                    // 성능 최적화: 같은 파일이면 재로드하지 않음
                    if (lastLoadedImagePath != null && 
                        lastLoadedImagePath.Equals(imagePath, StringComparison.OrdinalIgnoreCase) &&
                        customArtworkSprite != null)
                    {
                        // 이미 로드된 스프라이트가 있고 경로가 같으면 재로드 생략
                        return;
                    }

                    // 기존 스프라이트 해제
                    if (customArtworkSprite != null)
                    {
                        if (customArtworkSprite.texture != null)
                        {
                            UnityEngine.Object.Destroy(customArtworkSprite.texture);
                        }
                        UnityEngine.Object.Destroy(customArtworkSprite);
                        customArtworkSprite = null;
                    }
                    
                    byte[] imageData = System.IO.File.ReadAllBytes(imagePath);
                    Texture2D originalTexture = new Texture2D(2, 2);
                    // LoadImage는 Texture2D의 인스턴스 메서드
                    if (originalTexture.LoadImage(imageData))
                    {
                        int originalWidth = originalTexture.width;
                        int originalHeight = originalTexture.height;
                        
                        // 리사이즈/리샘플은 하지 않고 원본 해상도 그대로 사용
                        Texture2D finalTexture = originalTexture;
                        MelonLogger.Msg($"[CustomAssetManager] ✅ 커스텀 아트워크 로드 완료: {System.IO.Path.GetFileName(imagePath)} ({originalWidth}x{originalHeight})");
                        
                        // 스프라이트 생성
                        customArtworkSprite = Sprite.Create(finalTexture, new Rect(0, 0, finalTexture.width, finalTexture.height), 
                            new Vector2(0.5f, 0.5f), 51.2f); // 픽셀당 단위 51.2
                        
                        // 스프라이트 이름 설정 (중요: Unity에서 이름이 비어있으면 표시 문제 발생 가능)
                        string spriteName = System.IO.Path.GetFileNameWithoutExtension(imagePath);
                        customArtworkSprite.name = spriteName;
                        if (finalTexture != null)
                        {
                            finalTexture.name = spriteName;
                        }
                        
                        // 로드한 경로 캐싱
                        lastLoadedImagePath = imagePath;
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[CustomAssetManager] ❌ 커스텀 아트워크 로드 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 커스텀 프리뷰 BGM 로드
        /// </summary>
        public static void LoadCustomPreviewBGM(string audioPath)
        {
            try
            {
                if (System.IO.File.Exists(audioPath))
                {
                    // UnityWebRequestMultimedia.GetAudioClip 사용
                    MelonLoader.MelonCoroutines.Start(LoadAudioClipCoroutine(audioPath));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[CustomAssetManager] 커스텀 프리뷰 BGM 로드 실패: {ex.Message}");
            }
        }

        private static System.Collections.IEnumerator LoadAudioClipCoroutine(string audioPath)
        {
            using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(
                "file://" + audioPath, AudioType.UNKNOWN))
            {
                yield return www.SendWebRequest();
                if (www.result == UnityWebRequest.Result.Success)
                {
                    customPreviewBGM = DownloadHandlerAudioClip.GetContent(www);
                    MelonLogger.Msg($"[CustomAssetManager] ✅ 커스텀 프리뷰 BGM 로드 완료: {audioPath} (길이: {customPreviewBGM.length:F2}초)");
                }
                else
                {
                    MelonLogger.Msg($"[CustomAssetManager] 커스텀 프리뷰 BGM 로드 실패: {www.error}");
                }
            }
        }

        /// <summary>
        /// MusicID나 MusicData로 커스텀 차트인지 확인
        /// </summary>
        public static bool IsCustomChart(object musicID, object musicData)
        {
            try
            {
                // 1순위: MusicID로 앨범 매핑 확인 (가장 확실한 방법 - 모든 앨범 지원)
                if (musicID != null)
                {
                    if (AlbumManager.IsCustomChartMusicID(musicID))
                    {
                        return true;
                    }
                }
                
                // 2순위: 기존 방식 (하위 호환성)
                if (musicID != null && customChartMusicID != null)
                {
                    if (musicID.Equals(customChartMusicID))
                    {
                        return true;
                    }
                }
                
                // 3순위: MusicData에서 songTitle 확인 (대체 방법)
                if (musicData != null)
                {
                    Type musicDataType = musicData.GetType();
                    FieldInfo songTitleField = musicDataType.GetField("songTitle", 
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    if (songTitleField != null)
                    {
                        object songTitle = songTitleField.GetValue(musicData);
                        string songTitleStr = songTitle?.ToString() ?? "";
                        if (songTitleStr == "custom chart")
                        {
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[CustomAssetManager] IsCustomChartSelectedByTitle", "리플렉션 실패");
            }
            return false;
        }

        public static Sprite GetCustomArtwork() => customArtworkSprite;
        public static AudioClip GetCustomPreviewBGM() => customPreviewBGM;
        public static void SetCustomChartSelected(bool selected) => isCustomChartSelected = selected;
        public static bool IsCustomChartSelected() => isCustomChartSelected;

        /// <summary>
        /// 이 씬에서는 커스텀 BGM/BGA/노트 주입을 절대 하지 않음 (실행 순서와 무관하게 주입 방지)
        /// </summary>
        public static bool IsSceneWhereInjectionDisallowed()
        {
            try
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (!scene.IsValid()) return false;
                var name = scene.name ?? "";
                return name == "SoundPlayerScene" || name == "MoviePlayer_MovieSelect";
            }
            catch { return false; }
        }

        /// <summary>
        /// 커스텀 주입을 수행해도 되는지 (선택 플래그 + 주입 금지 씬 아님). 주입하는 모든 경로에서 사용.
        /// </summary>
        public static bool ShouldInjectCustomContent()
        {
            if (IsSceneWhereInjectionDisallowed()) return false;
            return IsCustomChartSelected();
        }
        public static void SetCustomChartMusicID(object musicID) => customChartMusicID = musicID;
        public static object GetCustomChartMusicID() => customChartMusicID;
        
        
        /// <summary>
        /// 현재 로드된 이미지 경로 가져오기 (성능 최적화용)
        /// </summary>
        public static string GetLoadedImagePath() => lastLoadedImagePath;
        
        /// <summary>
        /// 특정 경로의 이미지가 이미 로드되어 있는지 확인
        /// </summary>
        public static bool IsImageLoaded(string imagePath)
        {
            return !string.IsNullOrEmpty(imagePath) &&
                   lastLoadedImagePath != null &&
                   lastLoadedImagePath.Equals(imagePath, StringComparison.OrdinalIgnoreCase) &&
                   customArtworkSprite != null;
        }
    }
}















