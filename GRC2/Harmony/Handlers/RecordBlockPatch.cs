using System;
using GRC2.Core;
using GRC2.Helpers;
using HarmonyLib;
using IntiCreates;
using IntiCreates.RythmGame;
using MelonLoader;

namespace GRC2.Harmony.Handlers
{
    /// <summary>
    /// AutoPlay/판정조작(JudgePerfect)이 켜진 상태로 플레이한 결과가 베스트 스코어/콤보/클리어뱃지/
    /// 세이브 파일에 반영되지 않도록 차단합니다.
    /// </summary>
    public static class RecordBlockPatch
    {
        public static bool ShouldBlock => AutoPlayPatch.IsEnabled || JudgePerfectPatch.IsEnabled;

        private static readonly AccessTools.FieldRef<cRythmGameResultSceneUpdater, int> OldHighScoreRef =
            AccessTools.FieldRefAccess<cRythmGameResultSceneUpdater, int>("mOldPlayHighScore");

        private sealed class Snapshot
        {
            public int MusicId;
            public int Difficulty;
            public int HighScore;
            public int MaxCombo;
            public int PlayCount;
            public ResultClearBadge PlayFlag;
        }

        /// <summary>
        /// ResultSceneUpdaterPatch가 커스텀 곡일 때 sceneInitParam.musicData.id를 실제 커스텀 MusicID로
        /// 바꿔치기하므로, Harmony 프리픽스 실행 순서에 의존하지 않고 이 메서드에서 직접 동일한 방식으로
        /// 실제 MusicID를 계산합니다.
        /// </summary>
        private static int ResolveMusicId(soRythmGameMusicDataMap.MusicData musicData)
        {
            if (CustomAssetManager.IsCustomChartSelected() &&
                AlbumManager.GetCurrentMusicID() is soRythmGameMusicDataMap.MusicID customId)
            {
                return (int)customId;
            }

            return (int)musicData.id;
        }

        [HarmonyPatch(typeof(cRythmGameResultSceneUpdater), "initializePreFade")]
        private static class InitializePreFadePatch
        {
            [HarmonyPrefix]
            private static void Prefix(
                cRythmGameResultSceneManageObject.InitializeParam sceneInitParam,
                out Snapshot __state)
            {
                __state = null;
                if (!ShouldBlock || sceneInitParam == null)
                    return;

                try
                {
                    var saveDirector = SingletonMonoBehaviour<sSaveDataDirector>.Instance;
                    var playerMusicData = saveDirector?.getCurrentActiveGameData()?.playerMusicData;
                    if (playerMusicData == null)
                        return;

                    int id = ResolveMusicId(sceneInitParam.musicData);
                    int difficulty = (int)sceneInitParam.difficulty;
                    if (id < 0 || id >= playerMusicData.Length)
                        return;

                    var playData = playerMusicData[id];
                    if (playData == null || difficulty < 0 || difficulty >= playData.highScoreArray.Length)
                        return;

                    __state = new Snapshot
                    {
                        MusicId = id,
                        Difficulty = difficulty,
                        HighScore = playData.highScoreArray[difficulty],
                        MaxCombo = playData.maxComboArray[difficulty],
                        PlayCount = playData.playCountArray[difficulty],
                        PlayFlag = playData.playFlagArray[difficulty]
                    };
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogWarning(ex, "[RecordBlockPatch]", "스냅샷 캡처 오류");
                }
            }

            [HarmonyPostfix]
            private static void Postfix(cRythmGameResultSceneUpdater __instance, Snapshot __state)
            {
                if (!ShouldBlock)
                    return;

                OldHighScoreRef(__instance) = 0;

                if (__state == null)
                    return;

                try
                {
                    var saveDirector = SingletonMonoBehaviour<sSaveDataDirector>.Instance;
                    var playerMusicData = saveDirector?.getCurrentActiveGameData()?.playerMusicData;
                    if (playerMusicData == null || __state.MusicId < 0 || __state.MusicId >= playerMusicData.Length)
                        return;

                    var playData = playerMusicData[__state.MusicId];
                    if (playData == null)
                        return;

                    playData.highScoreArray[__state.Difficulty] = __state.HighScore;
                    playData.maxComboArray[__state.Difficulty] = __state.MaxCombo;
                    playData.playCountArray[__state.Difficulty] = __state.PlayCount;
                    playData.playFlagArray[__state.Difficulty] = __state.PlayFlag;

                    MelonLogger.Msg("[RecordBlockPatch] AutoPlay/판정조작 결과 기록 차단: 베스트/콤보/플레이수/클리어뱃지 원복");
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogException(ex, "[RecordBlockPatch]", "기록 원복 오류");
                }
            }
        }

        [HarmonyPatch(typeof(cRythmGameResultSceneUpdater), "coUpdateResultAnim")]
        private static class ResultAnimPatch
        {
            [HarmonyPrefix]
            private static void Prefix(cRythmGameResultSceneUpdater __instance)
            {
                if (ShouldBlock)
                    OldHighScoreRef(__instance) = 0;
            }
        }

        [HarmonyPatch(typeof(sSaveDataDirector), "requestGameDataSaveToFile")]
        private static class RequestSavePatch
        {
            [HarmonyPrefix]
            private static bool Prefix()
            {
                if (!ShouldBlock)
                    return true;

                MelonLogger.Msg("[RecordBlockPatch] AutoPlay/판정조작 결과 저장 차단: 세이브 파일 저장 요청 스킵");
                return false;
            }
        }
    }
}
