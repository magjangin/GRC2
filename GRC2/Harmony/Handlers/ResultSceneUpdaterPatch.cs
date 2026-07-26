using System;
using System.Collections;
using System.IO;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using GRC2.Core;
using GRC2.Helpers;
using HarmonyLib;
using IntiCreates;

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
        private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        [HarmonyPostfix]
        public static void InitializePreFadePostfix(object __instance)
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

        private static readonly string[] DifficultyOrder = { "easy", "normal", "hard", "expert" };

        /// <summary>
        /// 실제로 플레이한 난이도 "하나"의 레벨 숫자만 mMusicLVUI.setLV(int)로 덮어씁니다.
        /// (기존 ResultSceneInjector 폴백은 4개 난이도를 모두 이어붙여 mDifficultyText에 덮어쓰는 버그가 있었음)
        /// </summary>
        private static void ApplyDifficultyLevel(object updater)
        {
            try
            {
                var songInfo = AlbumManager.GetCurrentSongInfo();
                if (songInfo?.DifficultyNumbers == null || songInfo.DifficultyNumbers.Count == 0) return;

                Type type = updater.GetType();
                var sceneInitParamField = type.GetField("mSceneInitializeParam", InstanceFlags);
                object sceneInitParam = sceneInitParamField?.GetValue(updater);
                if (sceneInitParam == null) return;

                var difficultyField = sceneInitParam.GetType().GetField("difficulty", InstanceFlags);
                object difficultyValue = difficultyField?.GetValue(sceneInitParam);
                if (difficultyValue == null) return;

                int difficultyIndex = Convert.ToInt32(difficultyValue);
                if (difficultyIndex < 0 || difficultyIndex >= DifficultyOrder.Length) return;

                string key = DifficultyOrder[difficultyIndex];
                if (!songInfo.DifficultyNumbers.TryGetValue(key, out int level)) return;

                var musicLvUiField = type.GetField("mMusicLVUI", InstanceFlags);
                object musicLvUi = musicLvUiField?.GetValue(updater);
                if (musicLvUi == null) return;

                var setLvMethod = musicLvUi.GetType().GetMethod("setLV", InstanceFlags);
                if (setLvMethod == null) return;

                setLvMethod.Invoke(musicLvUi, new object[] { level });
                MelonLogger.Msg($"[ResultSceneUpdaterPatch] ✅ 난이도 레벨 직접 적용: {key} = {level}");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[ResultSceneUpdaterPatch]", "ApplyDifficultyLevel 오류");
            }
        }

        private static void ApplyMusicNameText(object updater)
        {
            try
            {
                var songInfo = AlbumManager.GetCurrentSongInfo();
                if (songInfo == null || string.IsNullOrEmpty(songInfo.Title)) return;

                var field = updater.GetType().GetField("mMusicNameText", InstanceFlags);
                var textComponent = field?.GetValue(updater);
                if (textComponent == null)
                {
                    MelonLogger.Warning("[ResultSceneUpdaterPatch] mMusicNameText 필드를 찾을 수 없습니다.");
                    return;
                }

                var textProp = textComponent.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                if (textProp != null && textProp.CanWrite)
                {
                    textProp.SetValue(textComponent, songInfo.Title);
                    MelonLogger.Msg($"[ResultSceneUpdaterPatch] ✅ 곡 제목 직접 적용: {songInfo.Title}");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[ResultSceneUpdaterPatch]", "ApplyMusicNameText 오류");
            }
        }

        private static void QueueArtwork(object updater)
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
            object updater,
            Sprite customSprite)
        {
            if (updater == null || customSprite == null)
                yield break;

            Type type = updater.GetType();
            MethodInfo isReadyMethod = type.GetMethod("isAbleFadeOpenOnSceneStart", InstanceFlags);
            FieldInfo artworkField = type.GetField("mArtWorkImage", InstanceFlags);

            float timeout = 10f;
            while (isReadyMethod != null && timeout > 0f)
            {
                if (updater is UnityEngine.Object unityObject &&
                    unityObject == null)
                {
                    yield break;
                }

                bool isReady;
                try { isReady = (bool)isReadyMethod.Invoke(updater, null); }
                catch { isReady = true; } // 리플렉션 호출 실패 시 더 기다리지 않고 바로 적용 시도

                if (isReady) break;

                timeout -= Time.deltaTime;
                yield return null;
            }

            if (updater is UnityEngine.Object finalUnityObject &&
                finalUnityObject == null)
            {
                yield break;
            }

            var image = artworkField?.GetValue(updater) as UnityEngine.UI.Image;
            if (image != null)
            {
                image.sprite = customSprite;
                MelonLogger.Msg("[ResultSceneUpdaterPatch] ✅ 아트워크 직접 적용 (initializePreFade 후킹 경로)");
            }
            else
            {
                MelonLogger.Warning("[ResultSceneUpdaterPatch] mArtWorkImage 필드를 찾을 수 없습니다.");
            }
        }
    }
}
