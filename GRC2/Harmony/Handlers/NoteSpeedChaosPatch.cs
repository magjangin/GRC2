using System;
using System.Collections.Generic;
using GRC2.Core;
using HarmonyLib;
using IntiCreates;
using IntiCreates.RythmGame.FairyMode;

namespace GRC2.Harmony.Handlers
{
    /// <summary>
    /// cNotecWorkBase.getNoteSpeed()는 virtual이 아닌 단일 정의라서 Touch/Flick/Hold/Hold_Middle/
    /// Slide/SlideGuide 노트 워크 전부가 이 메서드 하나만 호출합니다. 여기서 반환값에 배율만
    /// 곱하므로, 노트 위치(mCurrentPos)만 바뀌고 판정 시간(perfectSample)에는 영향이 없습니다.
    ///
    /// 같은 노트에 대해 매 프레임 같은 배율이 나와야 시각적으로 튀지 않으므로, 노트/레인별 배율을
    /// 캐시합니다. 캐시는 createAllNote가 새로 호출될 때(곡 시작)마다 비웁니다.
    /// </summary>
    public static class NoteSpeedChaosPatch
    {
        private static readonly Random Rng = new Random();

        // NoteCreateData는 참조 타입이고 노트 하나당 인스턴스 하나이므로, 기본 참조 동등성으로 충분합니다.
        private static readonly Dictionary<FairyNoteEditorLoader.NoteCreateData, float> NoteMultipliers =
            new Dictionary<FairyNoteEditorLoader.NoteCreateData, float>();

        private static readonly Dictionary<(NoteLaneLeftRight, NoteSubLaneType), float> LaneMultipliers =
            new Dictionary<(NoteLaneLeftRight, NoteSubLaneType), float>();

        [HarmonyPatch(typeof(cNotecWorkBase), "getNoteSpeed")]
        private static class GetNoteSpeedPatch
        {
            [HarmonyPostfix]
            private static void Postfix(cNotecWorkBase __instance, ref float __result)
            {
                if (!CustomKeySettings.NoteSpeedChaos)
                    return;

                var createData = __instance?.getCreateParam()?.createData;
                if (createData == null)
                    return;

                float multiplier = CustomKeySettings.NoteSpeedChaosPerLane
                    ? GetLaneMultiplier(createData.laneLeftRightID, createData.subLaneID)
                    : GetNoteMultiplier(createData);

                __result *= multiplier;
            }
        }

        /// <summary>곡이 새로 시작될 때(createAllNote) 레인/노트 배율 캐시를 비워 다시 무작위화합니다.</summary>
        [HarmonyPatch(typeof(cFairyModeNotesManager), "createAllNote")]
        private static class ResetOnNewSongPatch
        {
            [HarmonyPrefix]
            private static void Prefix()
            {
                if (!CustomKeySettings.NoteSpeedChaos)
                    return;

                NoteMultipliers.Clear();
                LaneMultipliers.Clear();
            }
        }

        private static float GetLaneMultiplier(NoteLaneLeftRight lane, NoteSubLaneType subLane)
        {
            var key = (lane, subLane);
            if (!LaneMultipliers.TryGetValue(key, out var multiplier))
            {
                multiplier = RandomInRange();
                LaneMultipliers[key] = multiplier;
            }

            return multiplier;
        }

        private static float GetNoteMultiplier(FairyNoteEditorLoader.NoteCreateData createData)
        {
            if (!NoteMultipliers.TryGetValue(createData, out var multiplier))
            {
                multiplier = RandomInRange();
                NoteMultipliers[createData] = multiplier;
            }

            return multiplier;
        }

        private static float RandomInRange()
        {
            float min = CustomKeySettings.NoteSpeedChaosMin;
            float max = CustomKeySettings.NoteSpeedChaosMax;
            if (max < min)
                (min, max) = (max, min);

            return min + (float)Rng.NextDouble() * (max - min);
        }
    }
}
