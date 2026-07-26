using System;
using System.Reflection;
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
        private static readonly Type RythmGameManagerType =
            typeof(cRythmGameManager);
        private static MethodInfo _requestPauseMethod = null;
        private static MethodInfo _setPauseButtonPushableMethod = null;
        private static FieldInfo _pauseMenuWorkField = null;
        private static FieldInfo _isPausingField = null;
        private static MethodInfo _getStateMethod = null;
        private static MethodInfo _requestPushContinueButtonMethod = null;

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
                var managers =
                    UnityEngine.Object.FindObjectsOfType(RythmGameManagerType);
                if (managers == null || managers.Length == 0)
                    return;

                object managerInstance = managers[0];

                if (_isPausingField == null)
                {
                    _isPausingField = RythmGameManagerType.GetField("mIsPausing", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                if (_pauseMenuWorkField == null)
                {
                    _pauseMenuWorkField = RythmGameManagerType.GetField("mPauseMenuWork", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                bool isPausing = false;
                if (_isPausingField != null && _isPausingField.GetValue(managerInstance) is bool b)
                {
                    isPausing = b;
                }

                object pauseMenuWork = _pauseMenuWorkField?.GetValue(managerInstance);

                if (isPausing && pauseMenuWork != null)
                {
                    // 이미 일시정지 메뉴가 열린 상태라면 계속하기(Unpause) 시도
                    if (_getStateMethod == null)
                    {
                        _getStateMethod = pauseMenuWork.GetType().GetMethod("getState", BindingFlags.Public | BindingFlags.Instance);
                    }

                    int stateInt = 0;
                    if (_getStateMethod != null)
                    {
                        object stateObj = _getStateMethod.Invoke(pauseMenuWork, null);
                        if (stateObj != null)
                        {
                            stateInt = Convert.ToInt32(stateObj);
                        }
                    }

                    // cRythmGamePauseMenuHud.State.Active == 2
                    if (stateInt == 2)
                    {
                        if (_requestPushContinueButtonMethod == null)
                        {
                            _requestPushContinueButtonMethod = pauseMenuWork.GetType().GetMethod("requestPushContinueButton", BindingFlags.Public | BindingFlags.Instance);
                        }

                        if (_requestPushContinueButtonMethod != null)
                        {
                            _requestPushContinueButtonMethod.Invoke(pauseMenuWork, null);
                            MelonLogger.Msg("[PauseKeyHandler] ⏯️ 키 입력 (Space/ESC) -> 일시정지 해제 (Continue)");
                            return;
                        }
                    }
                }

                if (!isPausing)
                {
                    // 일시정지 메뉴 열기 시도
                    if (_setPauseButtonPushableMethod == null)
                    {
                        _setPauseButtonPushableMethod = RythmGameManagerType.GetMethod("setPauseButtonPusable", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    }

                    if (_requestPauseMethod == null)
                    {
                        _requestPauseMethod = RythmGameManagerType.GetMethod("requestPause", BindingFlags.Public | BindingFlags.Instance);
                    }

                    // 일시정지 버튼 활성화 상태 강제
                    if (_setPauseButtonPushableMethod != null)
                    {
                        _setPauseButtonPushableMethod.Invoke(managerInstance, new object[] { true });
                    }

                    if (_requestPauseMethod != null)
                    {
                        _requestPauseMethod.Invoke(managerInstance, null);
                        MelonLogger.Msg("[PauseKeyHandler] ⏸️ 키 입력 (Space/ESC) -> 일시정지 메뉴 오픈 (requestPause)");
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(ex, "[PauseKeyHandler]", "Pause 키 처리 오류");
            }
        }
    }
}
