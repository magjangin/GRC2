using System;
using GRC2.Injectors;
using HarmonyLib;
using IntiCreates;
using MelonLoader;
using UnityEngine;

namespace GRC2.Harmony.Handlers
{
    /// <summary>
    /// cPlayMovieSceneManager.coUpdateMovieInARow()는 mLoadMoviePathList.Count &gt; 1인 곡에서
    /// 원본 클립이 거의 끝나갈 때(40프레임 이내, ~0.67초) mUseMovieClipList의 "다음 원본 클립"으로
    /// 자동 교체합니다(cFairyModeNotesManager와 무관한 별도 코루틴, requestPlay()에서 시작).
    ///
    /// BgaInjector가 VideoPlayer에 커스텀 BGA를 직접 주입해도 이 코루틴은 멈추지 않고 계속 돌기
    /// 때문에, 곡 막바지에 원본 BGA로 잠깐 되돌아갔다가 씬이 끝나는 현상이 있었습니다
    /// (mUseMovieClipList는 원본 게임이 로드한 실제 BGA 클립 목록이라 모드가 손대지 않음).
    ///
    /// 커스텀 BGA가 실제로 주입/재생 중일 때만 이 코루틴을 멈춰서 원본으로 되돌아가지 않게 합니다.
    /// 커스텀 BGA가 없는(원본 그대로 재생하는) 곡에서는 아무 것도 건드리지 않습니다.
    /// </summary>
    public static class BgaCrossfadePatch
    {
        private static readonly AccessTools.FieldRef<cPlayMovieSceneManager, Coroutine> SwapCoroutineRef =
            AccessTools.FieldRefAccess<cPlayMovieSceneManager, Coroutine>("mUpdateSwapCoroutine");

        [HarmonyPatch(typeof(cPlayMovieSceneManager), "requestPlay")]
        private static class RequestPlayPatch
        {
            /// <summary>
            /// 게임의 requestPlay()가 BgaInjector의 주입 완료보다 먼저 끝나 코루틴이 이미
            /// 시작된 경우를 대비한 경로입니다(순서가 반대인 경우는 BgaInjector가 직접 호출).
            /// </summary>
            [HarmonyPostfix]
            private static void Postfix(cPlayMovieSceneManager __instance)
            {
                TryStopSwapCoroutine(__instance);
            }
        }

        /// <summary>
        /// BgaInjector가 커스텀 BGA 주입/재생을 마친 직후에도 호출합니다.
        /// 이미 멈춰있거나 대상이 없으면 아무 일도 하지 않는 안전한 재호출 가능 메서드입니다.
        /// </summary>
        public static void TryStopSwapCoroutine(cPlayMovieSceneManager manager)
        {
            if (!BgaInjector.IsInjected || manager == null)
                return;

            try
            {
                var coroutine = SwapCoroutineRef(manager);
                if (coroutine == null)
                    return;

                manager.StopCoroutine(coroutine);
                SwapCoroutineRef(manager) = null;
                MelonLogger.Msg("[BgaCrossfadePatch] 게임 자체 BGA 크로스페이드 코루틴 정지 (커스텀 BGA 유지)");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning("[BgaCrossfadePatch] 크로스페이드 코루틴 정지 오류: " + ex.Message);
            }
        }
    }
}
