using System;
using HarmonyLib;
using IntiCreates;
using MelonLoader;
using UnityEngine;

namespace GRC2.Helpers
{
    /// <summary>
    /// 플레이 씬에서 스페이스바(Space) 및 ESC 키 입력 시 일시정지(Pause) 메뉴를 여닫는 핸들러
    /// </summary>
    public static class PauseKeyHandler
    {
        private static readonly AccessTools.FieldRef<cRythmGameManager, cRythmGamePauseMenuHud> PauseMenuWorkRef =
            AccessTools.FieldRefAccess<cRythmGameManager, cRythmGamePauseMenuHud>("mPauseMenuWork");

        // setPauseButtonPusable은 private이므로 열린 인스턴스 델리게이트로 한 번만 바인딩합니다.
        private static readonly Action<cRythmGameManager, bool> SetPauseButtonPusable =
            AccessTools.MethodDelegate<Action<cRythmGameManager, bool>>(
                AccessTools.Method(typeof(cRythmGameManager), "setPauseButtonPusable"));

        /// <summary>
        /// 플레이 씬 OnUpdate에서 호출되어 Space / ESC 키 입력을 감지하고 일시정지 메뉴를 전환합니다.
        /// </summary>
        public static void HandlePauseKeyInput()
        {
            if (!Input.GetKeyDown(KeyCode.Space) && !Input.GetKeyDown(KeyCode.Escape))
            {
                return;
            }

            try
            {
                var manager = UnityEngine.Object.FindObjectOfType<cRythmGameManager>();
                if (manager == null)
                    return;

                bool isPausing = manager.mIsPausing;
                var pauseMenuWork = PauseMenuWorkRef(manager);

                if (isPausing && pauseMenuWork != null)
                {
                    // 이미 일시정지 메뉴가 열린 상태라면 계속하기(Unpause) 시도
                    if (pauseMenuWork.getState() == cRythmGamePauseMenuHud.State.Active)
                    {
                        pauseMenuWork.requestPushContinueButton();
                        MelonLogger.Msg("[PauseKeyHandler] ⏯️ 키 입력 (Space/ESC) -> 일시정지 해제 (Continue)");
                        return;
                    }
                }

                if (!isPausing)
                {
                    // 일시정지 버튼 활성화 상태 강제 후 메뉴 열기
                    SetPauseButtonPusable?.Invoke(manager, true);
                    manager.requestPause();
                    MelonLogger.Msg("[PauseKeyHandler] ⏸️ 키 입력 (Space/ESC) -> 일시정지 메뉴 오픈 (requestPause)");
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(ex, "[PauseKeyHandler]", "Pause 키 처리 오류");
            }
        }
    }
}
