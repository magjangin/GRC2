using GRC2.Core;
using HarmonyLib;
using IntiCreates;
using MelonLoader;
using System;
using System.IO;

namespace GRC2.Harmony
{
    /// <summary>
    /// 원본 noticeChangedMusic 완료 후 커스텀 선택 상태와 프리뷰를 동기화합니다.
    /// </summary>
    [HarmonyPatch(typeof(cMusicSelectSceneUIUpdater), "noticeChangedMusic")]
    public static class AudioClipPatch
    {
        private static soRythmGameMusicDataMap.MusicID? _lastHandledMusicId;

        /// <summary>
        /// 원본 coChangePreviewBGM은 noticeChangedMusic 안에서 프리뷰 소스의 clip.name을 바로 읽습니다.
        /// 음소거하면서 clip을 비워 둔 상태면 NullReferenceException으로 원본 프리뷰가 나오지 않으므로(D1),
        /// 원본이 실행되기 전에 음소거를 풀어 clip을 돌려놓습니다. 커스텀 곡이면 postfix에서 다시 음소거합니다.
        /// </summary>
        [HarmonyPrefix]
        public static void NoticeChangedMusicPrefix()
        {
            PreviewAudioManager.RestoreMutedAudioSources();
        }

        /// <summary>
        /// 곡 선택 씬이 다시 열리면 같은 커스텀 곡을 다시 처리하도록 기억을 지웁니다.
        /// 기억이 남아 있으면 되돌아온 뒤 같은 곡을 골라도 프리뷰가 나오지 않았습니다(D2).
        /// </summary>
        public static void ResetHandledSelection()
        {
            _lastHandledMusicId = null;
        }

        [HarmonyPostfix]
        public static void NoticeChangedMusicPostfix(
            cMusicSelectSceneUIUpdater __instance,
            soRythmGameMusicDataMap.MusicID nextMusicID)
        {
            try
            {
                if (!AlbumManager.IsCustomChartMusicID(nextMusicID))
                {
                    HandleNormalSongSelection();
                    return;
                }

                // 원본 UI가 같은 MusicID를 여러 번 알리는 경우 대용량 에셋 요청을
                // 반복해서 시작하지 않습니다.
                if (CustomAssetManager.IsCustomChartSelected() &&
                    _lastHandledMusicId == nextMusicID)
                {
                    return;
                }

                if (!AlbumManager.SelectAlbumByMusicID(nextMusicID))
                {
                    return;
                }

                _lastHandledMusicId = nextMusicID;
                CustomAssetManager.SetCustomChartSelected(true);
                TextPatch.EnableTextReplacement(true);

                PreviewAudioManager.StopPreviewAndAmbient(__instance);
                ArtworkUpdater.UpdateArtwork(__instance);

                string bgmFile = AlbumManager.GetCurrentBgmFile();
                if (!string.IsNullOrEmpty(bgmFile) && File.Exists(bgmFile))
                {
                    MelonCoroutines.Start(
                        CustomBgmPlayer.InjectCustomBgm(bgmFile));
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning(
                    $"[AudioClipPatch] 곡 선택 동기화 실패: {ex.Message}");
            }
        }

        private static void HandleNormalSongSelection()
        {
            _lastHandledMusicId = null;
            TextPatch.EnableTextReplacement(false);

            if (!CustomAssetManager.IsCustomChartSelected())
            {
                return;
            }

            CustomAssetManager.SetCustomChartSelected(false);
            CustomBgmPlayer.CleanupAndRestore();
        }
    }
}
