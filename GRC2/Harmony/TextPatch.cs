using MelonLoader;
using System;
using System.Collections.Generic;
using UnityEngine;
using GRC2.Core;
using GRC2.Injectors;
using HarmonyLib;

namespace GRC2.Harmony
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
        /// <summary>씬 이름으로 본 교체 대상 여부입니다. 씬이 로드될 때 SceneDetector가 한 번 정합니다.</summary>
        private static bool _isReplacementSceneByName;

        /// <summary>
        /// 씬이 로드될 때 SceneDetector가 호출합니다. 텍스트 설정마다 활성 씬 이름 문자열을 새로 만들지 않게 미리 정해 둡니다(P3).
        /// 플레이 씬: FairyModeScene, PlayMovieScene / 로딩 씬: RenderCutinScene / 결과 씬: RythmGameResultScene.
        /// </summary>
        public static void OnSceneLoaded(string sceneName)
        {
            _isReplacementSceneByName =
                sceneName == "FairyModeScene" ||
                sceneName == "PlayMovieScene" ||
                sceneName == "RenderCutinScene" ||
                sceneName == "RythmGameResultScene";
        }

        private static bool IsPlayOrLoadingScene()
        {
            // BgmBgaInjector의 플레이 씬 상태(FairyModeScene 로드 때 켜짐)와 씬 이름 판정을 함께 씁니다.
            return BgmBgaInjector.IsPlayScene() || _isReplacementSceneByName;
        }

        public static void SetTextPrefix(ref string value)
        {
            try
            {
                if (value == null) return;
                if (string.IsNullOrWhiteSpace(value)) return;

                if (!_isTextReplacementEnabled || !IsPlayOrLoadingScene()) return;

                string currentOriginalTitle = AlbumManager.GetOriginalTitle(AlbumManager.GetCurrentMusicID());
                HashSet<string> allOriginalTitles = AlbumManager.GetAllOriginalTitles();

                var currentSongInfo = AlbumManager.GetCurrentSongInfo();
                if (currentSongInfo == null) return;

                string songTitle = currentSongInfo.Title;
                string oldValue = value;

                // 원제목과 완전히 같은 문자열은 통째로 바꾸고, 원제목을 포함한 긴 문자열은 그 부분만 바꿉니다(D3).
                // 예전에는 포함만 해도 문자열 전체를 바꿔서 "원제목이 들어간 문구"가 제목만 남았습니다.
                if (allOriginalTitles.Contains(value))
                {
                    value = songTitle;
                }
                else if (!string.IsNullOrEmpty(currentOriginalTitle) && value.Contains(currentOriginalTitle))
                {
                    value = value.Replace(currentOriginalTitle, songTitle);
                }
                else
                {
                    return;
                }

                MelonLogger.Msg($"[TextPatch] ✅ 텍스트 교체: '{oldValue}' -> '{value}'");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[TextPatch] 치명적 오류: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(UnityEngine.UI.Text), "set_text")]
        private static class UnityTextSetterPatch
        {
            [HarmonyPrefix]
            private static void Prefix(ref string value)
            {
                SetTextPrefix(ref value);
            }
        }

        // TextMeshPro와 TextMeshProUGUI는 TMP_Text의 동일한 setter를 공유합니다.
        [HarmonyPatch(typeof(TMPro.TMP_Text), "set_text")]
        private static class TmpTextSetterPatch
        {
            [HarmonyPrefix]
            private static void Prefix(ref string value)
            {
                SetTextPrefix(ref value);
            }
        }
    }
}





