using GRC2.Core;
using HarmonyLib;
using IntiCreates;
using MelonLoader;
using System;
using System.IO;

namespace GRC2.Harmony.Handlers
{
    /// <summary>
    /// 원본 noticeChangedMusic 완료 후 커스텀 선택 상태와 프리뷰를 동기화합니다.
    /// </summary>
    [HarmonyPatch(typeof(cMusicSelectSceneUIUpdater), "noticeChangedMusic")]
    public static class AudioClipPatch
    {
        private static object _lastHandledMusicId;

        [HarmonyPostfix]
        public static void NoticeChangedMusicPostfix(
            cMusicSelectSceneUIUpdater __instance,
            object nextMusicID)
        {
            try
            {
                if (nextMusicID == null)
                {
                    return;
                }

                if (!AlbumManager.IsCustomChartMusicID(nextMusicID))
                {
                    HandleNormalSongSelection();
                    return;
                }

                // 원본 UI가 같은 MusicID를 여러 번 알리는 경우 대용량 에셋 요청을
                // 반복해서 시작하지 않습니다.
                if (CustomAssetManager.IsCustomChartSelected() &&
                    Equals(_lastHandledMusicId, nextMusicID))
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
