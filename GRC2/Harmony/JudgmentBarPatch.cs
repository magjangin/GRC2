using GRC2.Core;
using GRC2.Helpers;
using HarmonyLib;
using IntiCreates;
using IntiCreates.RythmGame;
using IntiCreates.RythmGame.FairyMode;
using MelonLoader;

namespace GRC2.Harmony
{
    /// <summary>
    /// cNotecWorkBase.onJudgeMent를 후킹해 판정 결과(등급/시간오차)를 GameHud의 판정바로 전달합니다.
    /// JudgePerfectPatch와 같은 대상 메서드를 각자 독립적인 Prefix/Postfix로 패치하므로 서로 간섭하지
    /// 않습니다(Harmony는 같은 메서드에 여러 패치를 허용).
    ///
    /// onJudgeMent에는 실제로 친 타이밍이 아닌 판정도 들어오므로 걸러서 넘깁니다(원본 cFairyModeNotesManager 기준).
    /// - MISS: 원본이 subSample을 0으로 넘기므로 틱 없이 라벨만 띄웁니다.
    /// - 롱노트 중간 지점(Hold_Middle), 롱노트 유지·끝 판정: 누르고 있는지만 보고 오차는 프레임 지연뿐이라 건너뜁니다.
    /// - 슬라이드 시작: 커서 위치로만 판정하고 subSample이 항상 0이라 건너뜁니다.
    /// - 슬라이드 끝: 판정 범위(300~450ms)가 막대(탭 BAD 경계)보다 훨씬 넓어 라벨만 띄웁니다.
    /// 나머지(탭, 플릭, 롱노트 시작)는 막대에 틱을 찍습니다.
    /// </summary>
    [HarmonyPatch(typeof(cNotecWorkBase), "onJudgeMent")]
    public static class JudgmentBarPatch
    {
        // 두 플래그 모두 각 override가 base.onJudgeMent 호출 뒤에 켜므로, 베이스의 Postfix에서는 이번 호출 전 값이 보입니다.
        private static readonly AccessTools.FieldRef<cFairyHoldNoteWork, bool> HoldJudgedFirstRef =
            AccessTools.FieldRefAccess<cFairyHoldNoteWork, bool>("mIsJudgedFirst");
        private static readonly AccessTools.FieldRef<cFairySlideNoteWork, bool> SlideJudgedFirstNoteRef =
            AccessTools.FieldRefAccess<cFairySlideNoteWork, bool>("mIsJudgedFirstNote");

        // 판정 범위는 인스펙터 값([SerializeField])이라 디컴파일 기본값과 다를 수 있어 실제 인스턴스에서 읽습니다.
        private static readonly AccessTools.FieldRef<cFairyModeNotesManager, float> TapPerfectRangeRef =
            AccessTools.FieldRefAccess<cFairyModeNotesManager, float>("tap_perfectJudgeRange");
        private static readonly AccessTools.FieldRef<cFairyModeNotesManager, int> TapGreatRangeRef =
            AccessTools.FieldRefAccess<cFairyModeNotesManager, int>("tap_greatJudgeRange");
        private static readonly AccessTools.FieldRef<cFairyModeNotesManager, int> TapGoodRangeRef =
            AccessTools.FieldRefAccess<cFairyModeNotesManager, int>("tap_goodJudgeRange");
        private static readonly AccessTools.FieldRef<cFairyModeNotesManager, int> TapBadRangeRef =
            AccessTools.FieldRefAccess<cFairyModeNotesManager, int>("tap_badJudgeRange");

        private static cFairyModeNotesManager _rangeSource;

        /// <summary>AllPerfect(JudgePerfectPatch)가 등급을 PERFECT로 바꾸기 전에 실제 등급을 잡아 둡니다.</summary>
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(cNotecWorkBase.OnJudgeParam judgeParam, out JudgeType __state)
        {
            __state = judgeParam != null ? judgeParam.judgeType : JudgeType.INVALID;
        }

        [HarmonyPostfix]
        private static void Postfix(cNotecWorkBase __instance, cNotecWorkBase.OnJudgeParam judgeParam, JudgeType __state)
        {
            if (!CustomKeySettings.EnableJudgmentBar || judgeParam == null)
                return;

            try
            {
                var createParam = __instance.getCreateParam();
                if (createParam == null || createParam.createData == null)
                    return;

                RefreshJudgeRanges(createParam.noteManager);

                if (__state == JudgeType.MISS)
                {
                    GameHud.ReportMiss();
                    return;
                }

                if (__state < JudgeType.PERFECT || __state >= JudgeType.MISS || judgeParam.isHoldContinueJudge)
                    return;

                bool showTick;
                switch (createParam.createData.noteTypeID)
                {
                    case NoteTypeId.Hold_Middle:
                        return;
                    case NoteTypeId.Hold:
                        var hold = __instance as cFairyHoldNoteWork;
                        if (hold != null && HoldJudgedFirstRef(hold))
                            return;
                        showTick = true;
                        break;
                    case NoteTypeId.Fairy:
                        var slide = __instance as cFairySlideNoteWork;
                        if (slide == null || !SlideJudgedFirstNoteRef(slide))
                            return;
                        showTick = false;
                        break;
                    default:
                        showTick = true;
                        break;
                }

                GameHud.ReportHit((int)__state, judgeParam.subSample, NoteSampleTime.SampleRate, showTick);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning("[JudgmentBarPatch] 판정바 갱신 오류: " + ex.Message);
            }
        }

        private static void RefreshJudgeRanges(cFairyModeNotesManager manager)
        {
            if (manager == null || ReferenceEquals(manager, _rangeSource))
                return;

            _rangeSource = manager;
            GameHud.SetJudgeRanges(
                TapPerfectRangeRef(manager),
                TapGreatRangeRef(manager),
                TapGoodRangeRef(manager),
                TapBadRangeRef(manager));
        }
    }
}
