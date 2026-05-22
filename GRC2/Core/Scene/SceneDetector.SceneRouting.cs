using System;
using GRC2.Harmony.Handlers;
using GRC2.Helpers;
using GRC2.Injectors;
using MelonLoader;

namespace GRC2.Core
{
    public partial class SceneDetector
    {
        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            MelonLogger.Msg($"[SceneDetector] 씬 로드: {sceneName} (BuildIndex: {buildIndex})");

            if (!_isInitialized)
            {
                MelonLogger.Warning("[SceneDetector] 아직 초기화되지 않았습니다.");
                return;
            }

            try
            {
                if (sceneName == "FairyModeScene")
                {
                    HandleFairyModeScene();
                    BgmBgaInjector.StartInjection(isPlayScene: true);
                    BgmGameEndMonitor.AdjustMusicDataOnSceneLoad();
                }
                else if (sceneName == "PlayMovieScene")
                {
                    HandlePlayMovieScene();
                    BgmBgaInjector.StartInjection(isPlayScene: true);
                    BgmGameEndMonitor.AdjustMusicDataOnSceneLoad();
                }
                else if (sceneName == "RenderCutinScene")
                {
                    MelonLogger.Msg("[SceneDetector] RenderCutinScene 로드 처리 시작 (플레이 씬 로딩)");
                    CustomBgmPlayer.Cleanup();
                    MelonLogger.Msg("[SceneDetector] ✅ 로딩 씬 진입 - 커스텀 프리뷰 BGM 중지");

                    BgmBgaInjector.StartInjection(isPlayScene: true);

                    if (CustomAssetManager.IsCustomChartSelected())
                    {
                        ReloadCurrentAlbumAssets();
                        PlaySceneArtworkInjector.StartArtworkInjection();
                    }
                }
                else if (sceneName == "RythmGameResultScene")
                {
                    MelonLogger.Msg($"[SceneDetector] 결과 씬 감지: {sceneName} - BGM 주입 중지");
                    BgmBgaInjector.StopInjection();
                    BgmBgaInjector.ResetPlaySceneState();
                    ResultSceneInjector.StartInjection();
                }
                else if (sceneName == "MusicSelectScene")
                {
                    MelonLogger.Msg($"[SceneDetector] 곡 선택 씬 감지: {sceneName}");
                }
                else if (sceneName == "SoundPlayerScene" || sceneName == "MoviePlayer_MovieSelect")
                {
                    MelonLogger.Msg($"[SceneDetector] 씬 감지: {sceneName} - 커스텀 주입 비활성화 (플레이 씬 아님)");
                    BgmBgaInjector.StopInjection();
                    BgmBgaInjector.ResetPlaySceneState();
                    CustomAssetManager.SetCustomChartSelected(false);
                }
                else
                {
                    MelonLogger.Msg($"[SceneDetector] 알 수 없는 씬: {sceneName} - 게임 플레이 씬일 수 있습니다");

                    if (BgmBgaInjector.IsPlayScene())
                    {
                        MelonLogger.Msg($"[SceneDetector] 플레이 씬 상태 감지: {sceneName}");
                        if (CustomAssetManager.ShouldInjectCustomContent())
                        {
                            ReloadCurrentAlbumAssets();
                            PlaySceneArtworkInjector.StartArtworkInjection();
                        }
                    }
                    else
                    {
                        BgmBgaInjector.StartInjection(isPlayScene: false);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(ex, "[SceneDetector]", "씬 처리 중 오류");
            }
        }

        private void HandleFairyModeScene()
        {
            SceneHandler.HandleFairyModeScene();
            HandlePlayScene("FairyModeScene");
        }

        private void HandlePlayMovieScene()
        {
            SceneHandler.HandlePlayMovieScene();
            HandlePlayScene("PlayMovieScene");
        }

        private void HandlePlayScene(string sceneName)
        {
            MelonLogger.Msg($"[SceneDetector] {sceneName} 로드 처리 시작");

            try
            {
                CustomBgmPlayer.Cleanup();
                MelonLogger.Msg("[SceneDetector] ✅ 플레이 씬 진입 - 커스텀 프리뷰 BGM 중지");

                if (CustomAssetManager.IsCustomChartSelected())
                {
                    ReloadCurrentAlbumAssets();
                    PlaySceneArtworkInjector.StartArtworkInjection();
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(ex, "[SceneDetector]", $"{sceneName} 처리 실패");
            }
        }
    }
}
