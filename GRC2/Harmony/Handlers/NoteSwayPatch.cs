using GRC2.Core;
using HarmonyLib;
using IntiCreates;
using UnityEngine;

namespace GRC2.Harmony.Handlers
{
    /// <summary>
    /// cNotecWorkBase.simulate가 매 프레임 계산하는 mCurrentPos는 노트의 세로(레인 진행) 위치만
    /// 나타내며, 판정(cFairyModeNotesManager.getNearestJudgeNote/judgeTapAndHoldStartNote 등)은
    /// laneLeftRightID/subLaneID(고정 레인 인덱스)와 perfectSample(시각)만 비교할 뿐 화면 좌표를
    /// 전혀 보지 않습니다. 따라서 렌더링된 transform.localPosition.x에만 오프셋을 더하는 이 패치는
    /// 판정 정확도에 영향을 주지 않는 순수 시각 효과입니다.
    ///
    /// simulate를 오버라이드하면서 base.simulate를 호출하지 않는 슬라이드/페어리 노트
    /// (cFairySlideNoteWork/cFairySlideNoteGuideWork/cOptionLanePreviewNoteWork)와, 터치된 이후의
    /// 홀드 노트(cFairyHoldNoteWork)는 이 패치가 적용되지 않습니다. 이 노트들은 이미 고유한
    /// 커서 추종/곡선 이동 로직을 갖고 있어 "직선으로 내려오는 노트"라는 전제가 맞지 않습니다.
    /// </summary>
    [HarmonyPatch(typeof(cNotecWorkBase), "simulate")]
    public static class NoteSwayPatch
    {
        private const float SAMPLE_RATE = 48000f;

        [HarmonyPostfix]
        private static void Postfix(cNotecWorkBase __instance, int currentSample)
        {
            if (!CustomKeySettings.NoteSway || __instance == null)
                return;

            var createParam = __instance.getCreateParam();
            if (createParam?.createData == null)
                return;

            float secondsUntilHit = (createParam.createData.perfectSample - currentSample) / SAMPLE_RATE;

            float damping = 1f;
            if (CustomKeySettings.NoteSwayDamping)
            {
                float dampingTime = Mathf.Max(0.0001f, CustomKeySettings.NoteSwayDampingTime);
                damping = Mathf.Clamp01(secondsUntilHit / dampingTime);
            }

            float offset = CustomKeySettings.NoteSwayAmplitude * damping *
                Mathf.Sin(2f * Mathf.PI * CustomKeySettings.NoteSwaySpeed * secondsUntilHit);

            var transform = __instance.transform;
            var pos = transform.localPosition;
            pos.x += offset * 0.01f; // 픽셀 근사치를 로컬 좌표 단위로 환산 (해상도/카메라 배율에 따라 오차 있음)
            transform.localPosition = pos;
        }
    }
}
