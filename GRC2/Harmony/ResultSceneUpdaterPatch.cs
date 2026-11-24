using System;
using System.Collections;
using System.IO;
using MelonLoader;
using UnityEngine;
using GRC2.Core;
using GRC2.Helpers;
using GRC2.Parsers;
using HarmonyLib;
using IntiCreates;
using TMPro;

namespace GRC2.Harmony
{
    /// <summary>
    /// cRythmGameResultSceneUpdater.initializePreFade를 직접 후킹해서 아트워크/곡 제목을 주입합니다.
    /// 씬 로드 이벤트 이후 GameObject 경로를 탐색하며 재시도 폴링하던 기존 ResultSceneInjector 방식과 달리,
    /// 게임이 실제로 필드를 세팅하는 시점(__instance)에 직접 접근하므로 타이밍 경쟁이 없습니다.
    /// </summary>
    [HarmonyPatch(typeof(cRythmGameResultSceneUpdater), "initializePreFade")]
    public static class ResultSceneUpdaterPatch
    {
        private static readonly LazyFieldRef<cRythmGameResultSceneUpdater, cRythmGameResultSceneManageObject.InitializeParam> SceneInitParamRef =
            new LazyFieldRef<cRythmGameResultSceneUpdater, cRythmGameResultSceneManageObject.InitializeParam>("mSceneInitializeParam");

        private static readonly LazyFieldRef<cRythmGameResultSceneUpdater, cUIMusicLVLoopImage> MusicLvUiRef =
            new LazyFieldRef<cRythmGameResultSceneUpdater, cUIMusicLVLoopImage>("mMusicLVUI");

        private static readonly LazyFieldRef<cRythmGameResultSceneUpdater, TextMeshProUGUI> MusicNameTextRef =
            new LazyFieldRef<cRythmGameResultSceneUpdater, TextMeshProUGUI>("mMusicNameText");

        private static readonly LazyFieldRef<cRythmGameResultSceneUpdater, UnityEngine.UI.Image> ArtworkImageRef =
            new LazyFieldRef<cRythmGameResultSceneUpdater, UnityEngine.UI.Image>("mArtWorkImage");

        /// <summary>
        /// 원본 initializePreFade는 시작 직전 문지기(coOpenPreMusicStartWindow)가 원본 곡
        /// 정보 조회용으로 임시로 빌린 템플릿 곡 ID(sceneInitParam.musicData.id)를 그대로
        /// 세이브 인덱스로 써서 하이스코어/클리어뱃지를 비교·갱신하고 setDirty()까지
        /// 호출합니다. 여기서 실제 커스텀 곡 ID로 되돌리지 않으면 커스텀 차트의 플레이
        /// 결과가 엉뚱한 실제 곡의 저장 슬롯을 덮어씁니다.
        /// </summary>
        [HarmonyPrefix]
        public static void InitializePreFadePrefix(cRythmGameResultSceneManageObject.InitializeParam sceneInitParam)
        {
            try
            {
                if (sceneInitParam == null || !CustomAssetManager.IsCustomChartSelected())
                    return;

                soRythmGameMusicDataMap.MusicID? customMusicId = AlbumManager.GetCurrentMusicID();
                if (customMusicId == null)
                    return;

                sceneInitParam.musicData.id = customMusicId.Value;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[ResultSceneUpdaterPatch]", "InitializePreFadePrefix 오류");
            }
        }

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

                var sceneInitParam = SceneInitParamRef[updater];
                if (sceneInitParam == null) return;

                int difficultyIndex = (int)sceneInitParam.difficulty;
                if (difficultyIndex < 0 || difficultyIndex >= SongInfo.DifficultyKeys.Length) return;

                string key = SongInfo.DifficultyKeys[difficultyIndex];
                if (!songInfo.DifficultyNumbers.TryGetValue(key, out int level)) return;

                var musicLvUi = MusicLvUiRef[updater];
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

                var textComponent = MusicNameTextRef[updater];
                if (textComponent == null)
                {
                    MelonLogger.Warning("[ResultSceneUpdaterPatch] mMusicNameText가 아직 설정되지 않았습니다.");
                    return;
                }

                textComponent.text = songInfo.Title;
                MelonLogger.Msg($"[ResultSceneUpdaterPatch] ✅ 곡 제목 직접 적용: {songInfo.Title}");

                // 결과 화면에서만 제목 한자가 □로 나온다는 보고(2026-11-24)가 있어, 실제로 어느 폰트로 그려졌는지 남깁니다.
                // initializePreFade가 두 번 불리므로 한 번만 돌립니다.
                if (!_titleDiagnosticsQueued)
                {
                    _titleDiagnosticsQueued = true;
                    MelonCoroutines.Start(LogTitleGlyphDiagnostics(textComponent));
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[ResultSceneUpdaterPatch]", "ApplyMusicNameText 오류");
            }
        }

        private static bool _titleDiagnosticsQueued;

        /// <summary>
        /// 결과 화면 제목의 비ASCII 글자가 어느 폰트로 그려졌는지(또는 □로 바뀌었는지) 한 줄로 남깁니다.
        /// 글자를 넣은 직후에는 메시가 아직 없으므로 몇 프레임 뒤에 읽습니다.
        /// </summary>
        private static IEnumerator LogTitleGlyphDiagnostics(TextMeshProUGUI text)
        {
            // 제목이 페이드 연출 뒤에 켜질 수 있어 최대 5초까지 활성화를 기다립니다(꺼진 채로는 메시가 만들어지지 않음).
            float deadline = Time.realtimeSinceStartup + 5f;
            do
            {
                yield return null;
            }
            while (text != null && !text.isActiveAndEnabled && Time.realtimeSinceStartup < deadline);
            yield return null;
            _titleDiagnosticsQueued = false;

            try
            {
                if (text == null) yield break;

                text.ForceMeshUpdate();
                var info = text.textInfo;
                var parts = new System.Collections.Generic.List<string>();
                for (int i = 0; info != null && i < info.characterCount; i++)
                {
                    var ch = info.characterInfo[i];
                    if (ch.character < 0x80) continue;
                    string owner = ch.fontAsset != null ? ch.fontAsset.name : "없음";
                    parts.Add($"{ch.character}(U+{(int)ch.character:X4})→{owner}");
                }

                var material = text.fontSharedMaterial;
                int globalFallbacks = TMP_Settings.fallbackFontAssets?.Count ?? -1;
                int ownFallbacks = text.font?.fallbackFontAssetTable?.Count ?? -1;
                MelonLogger.Msg(
                    $"[결과 제목 진단] 표시 문자열 '{text.text}', 폰트 {text.font?.name ?? "null"}({text.font?.atlasPopulationMode}), " +
                    $"머티리얼 {material?.name ?? "null"} / 셰이더 {material?.shader?.name ?? "null"}, " +
                    $"폰트 자체 fallback {ownFallbacks}개, 전역 fallback {globalFallbacks}개, 오브젝트 {text.gameObject.name}(활성 {text.isActiveAndEnabled})");
                MelonLogger.Msg($"[결과 제목 진단] 비ASCII 글자별 폰트: {(parts.Count == 0 ? "없음" : string.Join(", ", parts))}");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[결과 제목 진단] 실패: {ex.Message}");
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
                    if (sprite == null ||
                        !CustomAssetManager.IsCustomChartSelected() ||
                        !CustomAssetManager.IsCurrentAlbumArtwork(requestedPath))
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

            if (updater == null || !ArtworkImageRef.IsAvailable)
                yield break;

            var image = ArtworkImageRef[updater];
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
