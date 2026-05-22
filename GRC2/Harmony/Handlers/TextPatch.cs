using MelonLoader;
using System;
using System.Reflection;
using UnityEngine;
using GRC2.Core;
using GRC2.Helpers;
using GRC2.Injectors;

namespace GRC2.Harmony.Handlers
{
    /// <summary>
    /// Text와 TextMeshPro의 text 속성 setter 후킹 - 커스텀 차트의 원본 제목을 파싱된 곡 제목으로 교체
    /// 플레이 씬, 로딩 씬, 결과 씬에서 작동
    /// </summary>
    public static class TextPatch
    {
        /// <summary>
        /// 텍스트 교체 활성화 여부 (coOpen에서 커스텀 차트 감지 시 활성화)
        /// </summary>
        private static bool _isTextReplacementEnabled = false;

        /// <summary>
        /// 텍스트 교체 활성화/비활성화
        /// </summary>
        public static void EnableTextReplacement(bool enable)
        {
            _isTextReplacementEnabled = enable;
            MelonLogger.Msg($"[TextPatch] 텍스트 교체 스위치: {(enable ? "ON" : "OFF")}");
        }

        /// <summary>
        /// 현재 씬이 플레이 씬 또는 로딩 씬인지 확인
        /// </summary>
        private static bool IsPlayOrLoadingScene()
        {
            try
            {
                // BgmBgaInjector의 플레이 씬 상태 확인
                if (BgmBgaInjector.IsPlayScene())
                {
                    return true;
                }

                // 현재 씬 이름 확인
                var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (currentScene != null)
                {
                    string sceneName = currentScene.name;
                    // 플레이 씬: FairyModeScene, PlayMovieScene
                    // 로딩 씬: RenderCutinScene
                    // 결과 씬: RythmGameResultScene (결과 화면에서도 커스텀 차트 제목 표시 필요)
                    return sceneName == "FairyModeScene" || 
                           sceneName == "PlayMovieScene" || 
                           sceneName == "RenderCutinScene" ||
                           sceneName == "RythmGameResultScene";
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[TextPatch] IsPlayOrLoadingScene", "씬 이름 확인 실패");
            }
            
            return false;
        }

        public static void SetTextPrefix(object __instance, ref string value)
        {
            try
            {
                if (value == null) return;
                if (string.IsNullOrWhiteSpace(value)) return;

                if (!_isTextReplacementEnabled || !IsPlayOrLoadingScene()) return;

                string currentOriginalTitle = AlbumManager.GetOriginalTitle(AlbumManager.GetCurrentMusicID());
                var allOriginalTitles = AlbumManager.GetAllOriginalTitles();

                bool shouldReplace = (!string.IsNullOrEmpty(currentOriginalTitle) && value.Contains(currentOriginalTitle)) ||
                                     allOriginalTitles.Contains(value);

                if (shouldReplace)
                {
                    var currentSongInfo = AlbumManager.GetCurrentSongInfo();
                    if (currentSongInfo == null) return;

                    string oldValue = value;
                    string replaced = currentSongInfo.Title;
                    value = replaced;

                    MelonLogger.Msg($"[TextPatch] ✅ 텍스트 교체: '{oldValue}' -> '{replaced}'");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[TextPatch] 치명적 오류: {ex.Message}");
            }
        }
    }
}





