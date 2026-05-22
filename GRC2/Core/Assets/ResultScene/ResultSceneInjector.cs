using System;
using System.Collections;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace GRC2.Core
{
    /// <summary>
    /// 결과 씬(RythmGameResultScene)에서 커스텀 아트워크와 난이도 표시를 주입합니다.
    /// </summary>
    public static partial class ResultSceneInjector
    {
        private const BindingFlags InstanceFieldFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private const int ArtworkApplyMaxAttempts = 10;
        private const float ArtworkApplyRetryInterval = 0.25f;
        private const string ResultSceneCanvasName = "ResultSceneCanvas";
        private const string SongNameBackgroundPath = "楽曲名下地";
        private const string ArtworkObjectName = "アートワーク";
        private const string GenericArtworkObjectName = "ArtWork";
        private const string SongInfoObjectName = "楽曲情報";
        private const string MusicLvObjectName = "musicLV";

        private static readonly string[] ArtworkSearchPaths =
        {
            "ResultSceneCanvas/楽曲名下地/アートワーク",
            "楽曲名下地/アートワーク",
            "アートワーク",
            "楽曲情報/アートワーク",
            "PreMusicStartWindow/ウインドウ/楽曲情報/アートワーク"
        };

        private static readonly string[] DifficultyOrder = { "easy", "normal", "hard", "expert" };

        private static readonly string[] DifficultyArrayFieldNames =
        {
            "musicLVArray",
            "mMusicLVArray",
            "levelArray",
            "mLevelArray",
            "musicLevelArray",
            "mLvArray",
            "lvArray",
            "mMusicLvArray",
            "levels",
            "mLevels"
        };

        private static object _coroutineRef;

        /// <summary>
        /// 결과 씬 로드 시 호출. 커스텀 차트이면 아트워크·난이도 주입 코루틴을 시작합니다.
        /// </summary>
        public static void StartInjection()
        {
            if (!CustomAssetManager.IsCustomChartSelected())
                return;

            if (_coroutineRef != null)
            {
                MelonLoader.MelonCoroutines.Stop(_coroutineRef);
                _coroutineRef = null;
            }

            _coroutineRef = MelonLoader.MelonCoroutines.Start(InjectArtworkAndDifficultyCoroutine());
        }

        private static IEnumerator InjectArtworkAndDifficultyCoroutine()
        {
            yield return new WaitForSeconds(0.15f);

            Sprite customSprite = null;
            try
            {
                LogResultSceneArtworkPathCheck();
                HideLvPartsInResultScene();
                var imageFile = AlbumManager.GetCurrentImageFile();
                if (!string.IsNullOrEmpty(imageFile) && System.IO.File.Exists(imageFile))
                {
                    CustomAssetManager.LoadCustomArtwork(imageFile);
                    customSprite = CustomAssetManager.GetCustomArtwork();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ResultSceneInjector] 주입 준비 오류: {ex.Message}");
            }

            if (customSprite != null)
            {
                for (int attempt = 1; attempt <= ArtworkApplyMaxAttempts; attempt++)
                {
                    try
                    {
                        bool applied = ApplyArtworkToResultScene(customSprite);
                        if (applied)
                            MelonLogger.Msg($"[ResultSceneInjector] ✅ 결과 씬 아트워크 적용 (시도 {attempt}/{ArtworkApplyMaxAttempts})");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[ResultSceneInjector] 아트워크 적용 오류: {ex.Message}");
                    }

                    if (attempt < ArtworkApplyMaxAttempts)
                        yield return new WaitForSeconds(ArtworkApplyRetryInterval);
                }
            }

            try
            {
                ApplyDifficultyToResultScene();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ResultSceneInjector] 난이도 적용 오류: {ex.Message}");
            }
            finally
            {
                _coroutineRef = null;
            }
        }
    }
}
