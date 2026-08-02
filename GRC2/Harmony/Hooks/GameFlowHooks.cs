using System;
using GRC2.Core;
using GRC2.Harmony.Handlers;
using GRC2.Injectors;
using HarmonyLib;
using IntiCreates;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace GRC2.Harmony.Hooks
{
    /// <summary>
    /// 곡 선택 화면에서 실제로 상태를 보정해야 하는 흐름만 처리합니다.
    /// </summary>
    public static class GameFlowHooks
    {
        private static readonly AccessTools.FieldRef<cMusicSelectPreMusicStartWindowManager, Image> PreMusicArtworkImageRef =
            AccessTools.FieldRefAccess<cMusicSelectPreMusicStartWindowManager, Image>("mArtworkImage");

        public static void BackToPreScreenPrefix()
        {
            try
            {
                // 곧 씬이 폐기되므로 원본 AudioSource를 전역 복원하지 않고 캐시만 비웁니다.
                CustomBgmPlayer.Cleanup();
                PreviewAudioManager.DiscardMutedSourceState();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] backToPreScreen 정리 오류: {ex.Message}");
            }
        }

        public static void StartRythmGamePrefix()
        {
            try
            {
                CustomBgmPlayer.Cleanup();
                PreviewAudioManager.DiscardMutedSourceState();

                // 직전 커스텀 곡의 종료 시간이 다음 플레이에 적용되지 않도록 초기화합니다.
                BgmFinishTimeManager.Reset();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] startRythmGame 정리 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 원본 팝업은 MusicDataMap에 존재하는 MusicID가 필요합니다.
        /// 커스텀 선택일 때만 같은 캐릭터의 실제 첫 곡 ID로 임시 치환합니다.
        /// </summary>
        public static void CoOpenPreMusicStartWindowPrefix(cMusicSelectSceneUIUpdater __instance)
        {
            try
            {
                if (__instance == null || !CustomAssetManager.ShouldInjectCustomContent())
                    return;

                AlbumInfo album = AlbumManager.GetCurrentAlbum();
                string artistId = album?.SongInfo?.Character;
                if (string.IsNullOrWhiteSpace(artistId))
                    artistId = album?.SongInfo?.Artist;
                if (album == null)
                    return;

                object previousMusicId = __instance.mCurentMusicId;
                var firstSong = string.IsNullOrWhiteSpace(artistId)
                    ? null
                    : AlbumManager.GetArtistFirstSong(artistId);
                object firstMusicId = firstSong?.musicId;
                string firstTitle = firstSong?.title;

                if (firstMusicId == null)
                {
                    MusicScrollViewHooks.TryGetTemplateSong(
                        previousMusicId,
                        out firstMusicId,
                        out firstTitle);
                }

                if (!(firstMusicId is soRythmGameMusicDataMap.MusicID resolvedMusicId))
                {
                    MelonLogger.Warning(
                        $"[GameFlowHooks] '{artistId ?? "unknown"}'에 대응하는 원본 기준 곡을 찾지 못했습니다.");
                    return;
                }

                if (!Equals(previousMusicId, firstMusicId))
                    __instance.mCurentMusicId = resolvedMusicId;

                AlbumManager.RegisterOriginalTitle(firstMusicId, firstTitle);
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(
                    $"[GameFlowHooks] coOpenPreMusicStartWindow MusicID 보정 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// setCurrentSelectDataToGameData는 위 coOpenPreMusicStartWindow가 빌려간 템플릿 곡
        /// ID(mCurentMusicId)를 그대로 lastPlayedMusicID/playerMusicData[].isNew에 저장합니다.
        /// mCurentMusicId 자체를 되돌리면 startRythmGame 이후 씬 전환 로직이 실제 MusicData를
        /// 필요로 하는 지점에서 깨져 컷인 씬이 멈추는 회귀가 났던 적이 있어(HOOK_MAP.md 참조),
        /// 여기서는 그 필드는 건드리지 않고 원본이 이미 저장을 마친 "직후"에 세이브 값만
        /// 실제 커스텀 ID로 다시 고쳐씁니다.
        /// </summary>
        public static void SetCurrentSelectDataToGameDataPostfix(bool isRhythmGameStart)
        {
            try
            {
                if (!CustomAssetManager.IsCustomChartSelected())
                    return;

                if (!(AlbumManager.GetCurrentMusicID() is soRythmGameMusicDataMap.MusicID customMusicId))
                    return;

                sSaveDataDirector saveDirector = SingletonMonoBehaviour<sSaveDataDirector>.Instance;
                sSaveDataDirector.SavableGameData gameData = saveDirector?.getCurrentActiveGameData();
                if (gameData?.playerData == null)
                    return;

                if (gameData.playerData.lastPlayedMusicID == customMusicId)
                    return;

                soRythmGameMusicDataMap.MusicID previousId = gameData.playerData.lastPlayedMusicID;
                gameData.playerData.lastPlayedMusicID = customMusicId;

                if (isRhythmGameStart)
                {
                    var musicData = gameData.playerMusicData;
                    int index = (int)customMusicId;
                    if (musicData != null && index >= 0 && index < musicData.Length && musicData[index] != null)
                    {
                        musicData[index].isNew = false;
                    }
                }

                MelonLogger.Msg(
                    $"[GameFlowHooks] setCurrentSelectDataToGameData 보정: lastPlayedMusicID {previousId} -> {customMusicId} (isRhythmGameStart={isRhythmGameStart})");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(
                    $"[GameFlowHooks] setCurrentSelectDataToGameData 보정 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 커스텀 MusicID는 원본 mMusicDataList에 실제 항목이 없어 getIsUsableMusicID가 항상
        /// false를 반환합니다. 그 결과 곡 선택 씬 재진입 시 getMusicIDUsable이 lastPlayedMusicID를
        /// 무시하고 FIRST_VER_DATA_TOP으로 되돌아가, 위에서 저장을 바로잡아도 커서가 여전히
        /// 엉뚱한 곡에 위치합니다. 등록된 커스텀 ID에 한해 "사용 가능"으로 인정해 되돌려줍니다.
        /// </summary>
        public static void GetIsUsableMusicIDPostfix(soRythmGameMusicDataMap.MusicID id, ref bool __result)
        {
            if (__result || CustomAssetManager.IsSceneWhereInjectionDisallowed())
                return;

            if (AlbumManager.IsCustomChartMusicID(id))
            {
                __result = true;
                MelonLogger.Msg($"[GameFlowHooks] getIsUsableMusicID 보정: {id} -> true (커스텀 곡)");
            }
        }

        /// <summary>
        /// 팝업의 원본 artwork 로딩이 끝난 직후 실제 Image 필드에 커스텀 이미지를 적용합니다.
        /// </summary>
        public static void PreMusicStartWindowOpenedPostfix(cMusicSelectPreMusicStartWindowManager __instance)
        {
            try
            {
                if (__instance == null || !CustomAssetManager.ShouldInjectCustomContent())
                    return;

                string imageFile = AlbumManager.GetCurrentImageFile();
                if (string.IsNullOrWhiteSpace(imageFile))
                    return;

                Image artworkImage = PreMusicArtworkImageRef(__instance);
                if (artworkImage == null)
                    return;

                if (CustomAssetManager.TryGetCustomArtwork(imageFile, out Sprite sprite))
                {
                    artworkImage.sprite = sprite;
                    return;
                }

                string requestedImage = System.IO.Path.GetFullPath(imageFile);
                CustomAssetManager.RequestCustomArtwork(
                    requestedImage,
                    loadedSprite =>
                    {
                        string currentImage = AlbumManager.GetCurrentImageFile();
                        if (artworkImage == null ||
                            !CustomAssetManager.ShouldInjectCustomContent() ||
                            string.IsNullOrWhiteSpace(currentImage) ||
                            !string.Equals(
                                System.IO.Path.GetFullPath(currentImage),
                                requestedImage,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return;
                        }

                        artworkImage.sprite = loadedSprite;
                    });
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] 시작 전 아트워크 적용 오류: {ex.Message}");
            }
        }

        [HarmonyPatch(typeof(cMusicSelectSceneUIUpdater), "backToPreScreen")]
        private static class BackToPreScreenPatch
        {
            [HarmonyPrefix]
            private static void Prefix()
            {
                BackToPreScreenPrefix();
            }
        }

        [HarmonyPatch(typeof(cMusicSelectSceneUIUpdater), "startRythmGame")]
        private static class StartRythmGamePatch
        {
            [HarmonyPrefix]
            private static void Prefix()
            {
                StartRythmGamePrefix();
            }
        }

        [HarmonyPatch(typeof(cMusicSelectSceneUIUpdater), "coOpenPreMusicStartWindow")]
        private static class OpenPreMusicStartWindowPatch
        {
            [HarmonyPrefix]
            private static void Prefix(cMusicSelectSceneUIUpdater __instance)
            {
                CoOpenPreMusicStartWindowPrefix(__instance);
            }
        }

        [HarmonyPatch(typeof(cMusicSelectPreMusicStartWindowManager), "requestOpenWindow")]
        private static class PreMusicStartWindowOpenedPatch
        {
            [HarmonyPostfix]
            private static void Postfix(cMusicSelectPreMusicStartWindowManager __instance)
            {
                PreMusicStartWindowOpenedPostfix(__instance);
            }
        }

        [HarmonyPatch(typeof(cMusicSelectSceneUIUpdater), "setCurrentSelectDataToGameData")]
        private static class SetCurrentSelectDataToGameDataPatch
        {
            [HarmonyPostfix]
            private static void Postfix(bool isRhythmGameStart)
            {
                SetCurrentSelectDataToGameDataPostfix(isRhythmGameStart);
            }
        }

        [HarmonyPatch(typeof(soRythmGameMusicDataMap), "getIsUsableMusicID")]
        private static class GetIsUsableMusicIDPatch
        {
            [HarmonyPostfix]
            private static void Postfix(soRythmGameMusicDataMap.MusicID id, ref bool __result)
            {
                GetIsUsableMusicIDPostfix(id, ref __result);
            }
        }
    }
}
