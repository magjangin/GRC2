using System;
using System.Collections;
using System.IO;
using MelonLoader;
using UnityEngine;
using GRC2.Core;
using GRC2.Helpers;
using HarmonyLib;
using IntiCreates;
using TMPro;

namespace GRC2.Harmony.Handlers
{
    /// <summary>
    /// cRythmGameResultSceneUpdater.initializePreFade를 직접 후킹해서 아트워크/곡 제목을 주입합니다.
    /// 씬 로드 이벤트 이후 GameObject 경로를 탐색하며 재시도 폴링하던 기존 ResultSceneInjector 방식과 달리,
    /// 게임이 실제로 필드를 세팅하는 시점(__instance)에 직접 접근하므로 타이밍 경쟁이 없습니다.
    /// </summary>
    [HarmonyPatch(typeof(cRythmGameResultSceneUpdater), "initializePreFade")]
    public static class ResultSceneUpdaterPatch
    {
        private static readonly AccessTools.FieldRef<cRythmGameResultSceneUpdater, cRythmGameResultSceneManageObject.InitializeParam> SceneInitParamRef =
            AccessTools.FieldRefAccess<cRythmGameResultSceneUpdater, cRythmGameResultSceneManageObject.InitializeParam>("mSceneInitializeParam");

        private static readonly AccessTools.FieldRef<cRythmGameResultSceneUpdater, cUIMusicLVLoopImage> MusicLvUiRef =
            AccessTools.FieldRefAccess<cRythmGameResultSceneUpdater, cUIMusicLVLoopImage>("mMusicLVUI");

        private static readonly AccessTools.FieldRef<cRythmGameResultSceneUpdater, TextMeshProUGUI> MusicNameTextRef =
            AccessTools.FieldRefAccess<cRythmGameResultSceneUpdater, TextMeshProUGUI>("mMusicNameText");

        private static readonly AccessTools.FieldRef<cRythmGameResultSceneUpdater, UnityEngine.UI.Image> ArtworkImageRef =
            AccessTools.FieldRefAccess<cRythmGameResultSceneUpdater, UnityEngine.UI.Image>("mArtWorkImage");

        private static readonly string[] DifficultyOrder = { "easy", "normal", "hard", "expert" };

        [HarmonyPostfix]
        public static void InitializePreFadePostfix(cRythmGameResultSceneUpdater __instance)
        {
            try
            {
                if (__instance == null) return;
                if (!CustomAssetManager.IsCustomChartSelected()) return;

                MelonLogger.Msg("[ResultSceneUpdaterPatch] initializePreFade 후킹 진입");

                ApplyMusicNameText(__instance);
                ApplyDifficultyLevel(__instance);
                QueueArtwork(__instance);
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(ex, "[ResultSceneUpdaterPatch]", "InitializePreFadePostfix 오류");
            }
        }

        /// <summary>
        /// 실제로 플레이한 난이도 "하나"의 레벨 숫자만 mMusicLVUI.setLV(int)로 덮어씁니다.
        /// (기존 ResultSceneInjector 폴백은 4개 난이도를 모두 이어붙여 mDifficultyText에 덮어쓰는 버그가 있었음)
        /// </summary>
        private static void ApplyDifficultyLevel(cRythmGameResultSceneUpdater updater)
        {
            try
            {
                var songInfo = AlbumManager.GetCurrentSongInfo();
                if (songInfo?.DifficultyNumbers == null || songInfo.DifficultyNumbers.Count == 0) return;

                var sceneInitParam = SceneInitParamRef(updater);
                if (sceneInitParam == null) return;

                int difficultyIndex = (int)sceneInitParam.difficulty;
                if (difficultyIndex < 0 || difficultyIndex >= DifficultyOrder.Length) return;

                string key = DifficultyOrder[difficultyIndex];
                if (!songInfo.DifficultyNumbers.TryGetValue(key, out int level)) return;

                var musicLvUi = MusicLvUiRef(updater);
                if (musicLvUi == null) return;

                musicLvUi.setLV(level);
                MelonLogger.Msg($"[ResultSceneUpdaterPatch] ✅ 난이도 레벨 직접 적용: {key} = {level}");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[ResultSceneUpdaterPatch]", "ApplyDifficultyLevel 오류");
            }
        }

        private static void ApplyMusicNameText(cRythmGameResultSceneUpdater updater)
        {
            try
            {
                var songInfo = AlbumManager.GetCurrentSongInfo();
                if (songInfo == null || string.IsNullOrEmpty(songInfo.Title)) return;

                var textComponent = MusicNameTextRef(updater);
                if (textComponent == null)
                {
                    MelonLogger.Warning("[ResultSceneUpdaterPatch] mMusicNameText가 아직 설정되지 않았습니다.");
                    return;
                }

                textComponent.text = songInfo.Title;
                MelonLogger.Msg($"[ResultSceneUpdaterPatch] ✅ 곡 제목 직접 적용: {songInfo.Title}");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[ResultSceneUpdaterPatch]", "ApplyMusicNameText 오류");
            }
        }

        private static void QueueArtwork(cRythmGameResultSceneUpdater updater)
        {
            string imageFile = AlbumManager.GetCurrentImageFile();
            if (string.IsNullOrEmpty(imageFile) || !File.Exists(imageFile))
                return;

            string requestedPath = Path.GetFullPath(imageFile);
            if (CustomAssetManager.TryGetCustomArtwork(
                requestedPath,
                out Sprite cachedSprite))
            {
                MelonCoroutines.Start(
                    WaitAndApplyArtwork(updater, cachedSprite));
                return;
            }

            CustomAssetManager.RequestCustomArtwork(
                requestedPath,
                sprite =>
                {
                    string currentPath = AlbumManager.GetCurrentImageFile();
                    if (sprite == null ||
                        !CustomAssetManager.IsCustomChartSelected() ||
                        string.IsNullOrEmpty(currentPath) ||
                        !string.Equals(
                            Path.GetFullPath(currentPath),
                            requestedPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    MelonCoroutines.Start(
                        WaitAndApplyArtwork(updater, sprite));
                });
        }

        private static IEnumerator WaitAndApplyArtwork(
            cRythmGameResultSceneUpdater updater,
            Sprite customSprite)
        {
            if (updater == null || customSprite == null)
                yield break;

            float timeout = 10f;
            while (timeout > 0f)
            {
                // 씬이 폐기되면 updater도 파괴됩니다.
                if (updater == null)
                    yield break;

                if (updater.isAbleFadeOpenOnSceneStart())
                    break;

                timeout -= Time.deltaTime;
                yield return null;
            }

            if (updater == null)
                yield break;

            var image = ArtworkImageRef(updater);
            if (image != null)
            {
                image.sprite = customSprite;
                MelonLogger.Msg("[ResultSceneUpdaterPatch] ✅ 아트워크 직접 적용 (initializePreFade 후킹 경로)");
            }
            else
            {
                MelonLogger.Warning("[ResultSceneUpdaterPatch] mArtWorkImage가 설정되어 있지 않습니다.");
            }
        }
    }
}
