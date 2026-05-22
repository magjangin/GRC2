using GRC2.Core;
using GRC2.Harmony.Handlers;
using MelonLoader;
using System;
using System.Collections;
using UnityEngine;

namespace GRC2.Harmony.Hooks
{
    public static partial class GameFlowHooks
    {
        /// <summary>
        /// coOpenPreMusicStartWindow prefix - 곡 시작 전 윈도우 열기
        /// </summary>
        public static void CoOpenPreMusicStartWindowPrefix(object __instance)
        {
            try
            {
                if (CustomAssetManager.IsSceneWhereInjectionDisallowed())
                    return;

                LogPreStartWindowOpen(__instance);
                TryManipulatePreStartWindowMusicId(__instance);
                MelonLogger.Msg("===========================================");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] coOpenPreMusicStartWindow 오류: {ex.Message}");
                MelonLogger.Warning($"[GameFlowHooks] 스택 트레이스: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// coOpenPreMusicStartWindow postfix - 곡 시작 전 윈도우 열린 뒤 아트워크 적용
        /// (MusicID→FIRST·앨범 선택은 CustomChartHandler.coOpen에서 수행, 윈도우 아트워크만 여기서)
        /// </summary>
        public static void CoOpenPreMusicStartWindowPostfix(object __instance)
        {
            try
            {
                if (CustomAssetManager.IsSceneWhereInjectionDisallowed())
                    return;

                MelonLogger.Msg("[GameFlowHooks] 🪟 coOpenPreMusicStartWindow() 완료");
                TrySchedulePreStartWindowCustomArtwork();
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] coOpenPreMusicStartWindow Postfix 오류: {ex.Message}");
            }
        }

        private static void LogPreStartWindowOpen(object instance)
        {
            MelonLogger.Msg("===========================================");
            MelonLogger.Msg("[GameFlowHooks] 🪟 coOpenPreMusicStartWindow() 호출됨");
            MelonLogger.Msg($"[GameFlowHooks]   인스턴스: {instance?.GetType().Name ?? "null"}");
            MelonLogger.Msg($"[GameFlowHooks]   시간: {DateTime.Now:HH:mm:ss.fff}");
            MelonLogger.Msg("[GameFlowHooks]   설명: 곡 시작 전 윈도우 열기");
        }

        private static void TryManipulatePreStartWindowMusicId(object instance)
        {
            if (instance == null)
                return;

            Type instanceType = instance.GetType();
            object currentMusicID = AudioClipPatch.GetCurrentMusicIDFromInstance(instance);
            MelonLogger.Msg($"[GameFlowHooks]   📌 현재 MusicID: {currentMusicID ?? "null"} (타입: {currentMusicID?.GetType().Name ?? "null"})");

            bool musicIdChanged = TryManipulateMusicIdByArtist(instance, instanceType, currentMusicID);
            if (musicIdChanged)
            {
                MelonLogger.Msg("[GameFlowHooks]   ✅ 아티스트 ID 기반 MusicID 변경 완료");
            }
        }

        private static void TrySchedulePreStartWindowCustomArtwork()
        {
            if (!CustomAssetManager.IsCustomChartSelected())
                return;

            var imageFile = AlbumManager.GetCurrentImageFile();
            if (string.IsNullOrEmpty(imageFile) || !System.IO.File.Exists(imageFile))
                return;

            CustomAssetManager.LoadCustomArtwork(imageFile);
            var sprite = CustomAssetManager.GetCustomArtwork();
            if (sprite != null)
                MelonCoroutines.Start(ApplyPreStartWindowArtworkDelayed(sprite));
        }

        private static IEnumerator ApplyPreStartWindowArtworkDelayed(Sprite sprite)
        {
            yield return new WaitForSeconds(0.15f);
            try
            {
                if (sprite != null && ResultSceneInjector.ApplyArtworkToArtWorkObject(sprite))
                {
                    MelonLogger.Msg("[GameFlowHooks] ✅ 곡 시작 전 윈도우 アート워크 적용");
                }
                else
                {
                    PlaySceneArtworkInjector.StartArtworkInjection();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[GameFlowHooks] 곡 시작 전 윈도우 아트워크 적용 오류: {ex.Message}");
            }
        }
    }
}
