using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using GRC2.Helpers;

namespace GRC2.Injectors
{
    /// <summary>
    /// BGM 주입 메인 클래스 - 코루틴 및 상태 관리
    /// </summary>
    internal static class BgmInjector
    {
        private static bool _bgmInjected = false;
        private static int _bgmAttemptCount = 0;
        private static bool _bgmLogShown = false;
        private static object _bgmBeatManagerInstance = null;
        private static float _bgmLength = 0f; // BGM 길이 (초)

        public static bool IsInjected => _bgmInjected;
        public static int AttemptCount => _bgmAttemptCount;
        public static bool LogShown => _bgmLogShown;
        public static object BgmBeatManagerInstance 
        { 
            get => _bgmBeatManagerInstance; 
            set => _bgmBeatManagerInstance = value; 
        }

        public static void Initialize()
        {
            BgmInjectorHooks.Initialize();
        }

        public static void Reset()
        {
            _bgmInjected = false;
            _bgmAttemptCount = 0;
            _bgmLogShown = false;
            _bgmBeatManagerInstance = null;
            _bgmLength = 0f;
        }
        
        /// <summary>
        /// BGM 길이 설정
        /// </summary>
        public static void SetBgmLength(float length)
        {
            _bgmLength = length;
            MelonLogger.Msg($"[BgmInjector] BGM 길이 설정: {length:F3}초");
        }
        
        /// <summary>
        /// BGM 길이 가져오기
        /// </summary>
        public static float GetBgmLength()
        {
            return _bgmLength;
        }

        public static void IncrementAttemptCount()
        {
            _bgmAttemptCount++;
        }

        public static void SetLogShown(bool value)
        {
            _bgmLogShown = value;
        }

        public static void SetInjected(bool value)
        {
            _bgmInjected = value;
        }

        public static IEnumerator TryInjectBgmCoroutine(string bgmFilePath, Type bgmBeatManagerType, bool isPlayScene)
        {
            MelonLogger.Msg("[BgmInjector] TryInjectBgmCoroutine 시작");
            
            // 시도 횟수 제한
            _bgmAttemptCount++;
            if (_bgmAttemptCount > 10)
            {
                if (!_bgmLogShown)
                {
                    MelonLogger.Warning("[BgmInjector] BGM 주입 시도 횟수 초과. cBGMBeatManager를 찾을 수 없습니다.");
                    _bgmLogShown = true;
                }
                yield break;
            }

            MelonLogger.Msg($"[BgmInjector] BGM 주입 시도 횟수: {_bgmAttemptCount}/10");

            // ⚡ 성능 최적화: 캐시된 인스턴스 확인 (이미 발견되었으면 재검색 생략)
            if (bgmBeatManagerType != null && _bgmBeatManagerInstance == null)
            {
                try
                {
                    if (BgmSearcher.TryFindBeatManager(bgmBeatManagerType, out var beatManager))
                    {
                        _bgmBeatManagerInstance = beatManager;
                        MelonLogger.Msg("[BgmInjector] cBGMBeatManager 인스턴스 발견");
                        BgmSearcher.LogOriginalAudioInfo(bgmBeatManagerType, _bgmBeatManagerInstance, "BgmInjector");
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogWarning(ex, "[BgmInjector] FindBgmBeatManagerCoroutine", "FindObjectOfType(cBGMBeatManager) 실패");
                }
            }

            // cBGMBeatManager를 찾지 못한 경우, AudioSource에서만 찾기 (단 한 번)
            if (_bgmBeatManagerInstance == null)
            {
                try
                {
                    if (BgmSearcher.TryFindBeatManagerFromAudioSource(bgmBeatManagerType, out var beatManagerFromAudio))
                    {
                        _bgmBeatManagerInstance = beatManagerFromAudio;
                        MelonLogger.Msg("[BgmInjector] AudioSource에서 cBGMBeatManager 발견");
                        BgmSearcher.LogOriginalAudioInfo(bgmBeatManagerType, _bgmBeatManagerInstance, "BgmInjector");
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogWarning(ex, "[BgmInjector] FindBgmBeatManagerCoroutine", "AudioSource 경로 검색 실패");
                }
            }

            // cBGMBeatManager 메서드로 BGM 주입 시도 (이미 주입되지 않은 경우만)
            if (!_bgmInjected && _bgmBeatManagerInstance != null && bgmBeatManagerType != null)
            {
                yield return TryInjectBgmToManager(bgmFilePath, bgmBeatManagerType);
            }
        }

        private static IEnumerator TryInjectBgmToManager(string bgmFilePath, Type bgmBeatManagerType)
        {
            // 이미 주입되었으면 중복 주입 방지
            if (_bgmInjected)
            {
                yield break;
            }
            
            if (_bgmBeatManagerInstance == null || bgmBeatManagerType == null)
            {
                yield break;
            }

            // setClip 메서드 찾기
            MethodInfo setClipMethod = null;
            try
            {
                setClipMethod = bgmBeatManagerType.GetMethod("setClip", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            }
            catch (Exception ex)
            {
                if (!_bgmLogShown)
                {
                    MelonLogger.Warning($"[BgmInjector] setClip 메서드 찾기 실패: {ex.Message}");
                    _bgmLogShown = true;
                }
            }
            
            if (setClipMethod != null)
            {
                MelonLogger.Msg("[BgmInjector] setClip 메서드 발견, BGM 로드 시도");
                
                var parameters = setClipMethod.GetParameters();
                
                // 파일 경로를 받는지 확인
                if (parameters.Length > 0 && parameters[0].ParameterType == typeof(string))
                {
                    // 파일 경로를 직접 전달
                    try
                    {
                        if (parameters.Length == 2 && parameters[1].ParameterType == typeof(bool))
                        {
                            setClipMethod.Invoke(_bgmBeatManagerInstance, new object[] { bgmFilePath, false });
                        }
                        else
                        {
                            setClipMethod.Invoke(_bgmBeatManagerInstance, new object[] { bgmFilePath });
                        }
                        MelonLogger.Msg("[BgmInjector] BGM 주입 성공 (setClip with file path)");
                        _bgmInjected = true;
                        yield break;
                    }
                    catch (Exception ex)
                    {
                        if (!_bgmLogShown)
                        {
                            MelonLogger.Warning($"[BgmInjector] setClip 호출 실패: {ex.Message}");
                            _bgmLogShown = true;
                        }
                    }
                }
                else if (parameters.Length > 0 && parameters[0].ParameterType == typeof(AudioClip))
                {
                    // AudioClip을 받는 경우 - BgmLoader로 위임
                    yield return BgmLoader.LoadAndInjectAudioClip(
                        bgmFilePath, 
                        setClipMethod, 
                        parameters, 
                        bgmBeatManagerType,
                        _bgmBeatManagerInstance,
                        (injected) => _bgmInjected = injected);
                }
            }
            else
            {
                // setClip이 없으면 _sorce 필드에 직접 접근 시도
                yield return BgmLoader.TryInjectViaSorceField(
                    bgmFilePath, 
                    bgmBeatManagerType,
                    _bgmBeatManagerInstance,
                    (injected) => _bgmInjected = injected,
                    (shown) => _bgmLogShown = shown);
            }
        }

    }
}
