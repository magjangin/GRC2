using System;
using System.Linq;
using System.Reflection;
using MelonLoader;
using UnityEngine;

namespace GRC2.Injectors
{
    /// <summary>
    /// 게임 종료 모니터링 관련 후킹을 담당하는 클래스
    /// </summary>
    internal static partial class BgmGameEndMonitor
    {
        private static int _monitorGameEndCallCount = 0;
        
        // Reflection 필드 정보 캐싱 (성능 최적화)
        private static FieldInfo _cachedMusicDataField = null;
        private static FieldInfo _cachedFadeOutEndSampleField = null;
        private static FieldInfo _cachedFadeOutStartSampleField = null;
        private static FieldInfo _cachedScreenFadeOutStartSampleField = null;
        private static FieldInfo _cachedScreenFadeOutEndSampleField = null;
        private static FieldInfo _cachedKudosBoostEndSampleField = null;
        private static Type _cachedMusicDataType = null;
        private static Type _cachedInstanceType = null;
        
        // 마지막 조정된 값 캐싱 (불필요한 SetValue 방지)
        private static int _lastTargetSample = -1;
        private static int _lastFadeOutStartSample = -1;
        private static int _lastScreenFadeOutStartSample = -1;
        
        // 인스턴스 캐싱 (성능 최적화)
        private static object _cachedBgmManagerInstance = null;
        private static Type _cachedBgmManagerType = null;
        
        // 메서드 정보 캐싱 (성능 최적화)
        private static MethodInfo _cachedGetAudioClipMethod = null;
        private static MethodInfo _cachedGetCurrentSampleMethod = null;
        
        public static int MonitorGameEndCallCount => _monitorGameEndCallCount;
        
        public static void IncrementMonitorGameEndCallCount()
        {
            _monitorGameEndCallCount++;
        }
        
        /// <summary>
        /// Reflection 필드 정보를 캐싱 (성능 최적화)
        /// </summary>
        private static void CacheFieldInfo(Type instanceType, object musicData)
        {
            if (_cachedInstanceType == instanceType && _cachedMusicDataType == musicData?.GetType())
            {
                return; // 이미 캐싱됨
            }
            
            _cachedInstanceType = instanceType;
            _cachedMusicDataField = instanceType.GetField("mRythmGameMusicData",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (musicData != null)
            {
                _cachedMusicDataType = musicData.GetType();
                _cachedFadeOutEndSampleField = _cachedMusicDataType.GetField("musicFadeOutEndSample",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                _cachedFadeOutStartSampleField = _cachedMusicDataType.GetField("musicFadeOutStartSample",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                _cachedScreenFadeOutStartSampleField = _cachedMusicDataType.GetField("screenFadeOutStartSample",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                _cachedScreenFadeOutEndSampleField = _cachedMusicDataType.GetField("screenFadeOutEndSample",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            }
        }
        
        /// <summary>
        /// 캐시 초기화 (씬 변경 시 호출)
        /// </summary>
        public static void ClearCache()
        {
            _cachedMusicDataField = null;
            _cachedFadeOutEndSampleField = null;
            _cachedFadeOutStartSampleField = null;
            _cachedScreenFadeOutStartSampleField = null;
            _cachedScreenFadeOutEndSampleField = null;
            _cachedKudosBoostEndSampleField = null;
            _cachedMusicDataType = null;
            _cachedInstanceType = null;
            _lastTargetSample = -1;
            _lastFadeOutStartSample = -1;
            _lastScreenFadeOutStartSample = -1;
            
            // 인스턴스 및 메서드 캐시 초기화
            _cachedBgmManagerInstance = null;
            _cachedBgmManagerType = null;
            _cachedGetAudioClipMethod = null;
            _cachedGetCurrentSampleMethod = null;
        }
        
        /// <summary>
        /// BGM Manager 인스턴스 가져오기 (캐싱)
        /// </summary>
        private static object GetBgmManagerInstance(Type instanceType)
        {
            // 캐시된 인스턴스가 유효한지 확인
            if (_cachedBgmManagerInstance != null && _cachedBgmManagerType != null)
            {
                // 인스턴스가 여전히 유효한지 확인 (null 체크)
                if (!ReferenceEquals(_cachedBgmManagerInstance, null))
                {
                    return _cachedBgmManagerInstance;
                }
            }
            
            // 캐시가 없거나 무효한 경우 새로 찾기
            var assembly = instanceType.Assembly;
            var bgmManagerType = assembly.GetType("IntiCreates.cBGMBeatManager");
            if (bgmManagerType == null)
            {
                return null;
            }
            
            var bgmManagers = UnityEngine.Object.FindObjectsOfType(bgmManagerType);
            if (bgmManagers != null && bgmManagers.Length > 0)
            {
                _cachedBgmManagerInstance = bgmManagers[0];
                _cachedBgmManagerType = bgmManagerType;
                return _cachedBgmManagerInstance;
            }
            
            return null;
        }
        
        /// <summary>
        /// 메서드 정보 가져오기 (캐싱)
        /// </summary>
        private static MethodInfo GetCachedMethod(Type type, string methodName, BindingFlags flags)
        {
            // getAudioClip 메서드 캐싱
            if (methodName == "getAudioClip" && _cachedGetAudioClipMethod != null && _cachedBgmManagerType == type)
            {
                return _cachedGetAudioClipMethod;
            }
            
            // getCurrentSample 메서드 캐싱
            if (methodName == "getCurrentSample" && _cachedGetCurrentSampleMethod != null && _cachedBgmManagerType == type)
            {
                return _cachedGetCurrentSampleMethod;
            }
            
            // 메서드 찾기
            var method = type.GetMethod(methodName, flags);
            
            // 캐시에 저장
            if (methodName == "getAudioClip")
            {
                _cachedGetAudioClipMethod = method;
                _cachedBgmManagerType = type;
            }
            else if (methodName == "getCurrentSample")
            {
                _cachedGetCurrentSampleMethod = method;
                _cachedBgmManagerType = type;
            }
            
            return method;
        }
        
        public static bool MonitorGameEndPrefix(object __instance)
        {
            try
            {
                _monitorGameEndCallCount++;
                
                // 고빈도 코루틴 훅에서 과도한 로그/포맷팅을 피하기 위해 300회마다만 로그
                if (_monitorGameEndCallCount % 300 == 1)
                {
                    MelonLogger.Msg("[BgmGameEndMonitor] coMonitorGameEnd() 호출 감지 - 무한 플레이 모드 유지");
                }
                
                // mRythmGameMusicData를 BGM 길이에 맞게 조정 (필드 값은 유지)
                AdjustMusicDataForBgmLength(__instance, logAdjustment: false);
                
                // 원본 메서드 실행 허용 (테스트용)
                // TODO: 테스트 완료 후 다시 차단하도록 변경
                return true; // false에서 true로 변경
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[BgmGameEndMonitor] MonitorGameEndPrefix 오류: {ex.Message}");
                // 오류 발생 시에는 원본 실행 허용
                return true; // false에서 true로 변경
            }
        }
        
        /// <summary>
        /// 플레이 씬 로드 시 4개 필드를 조정하는 공개 메서드
        /// </summary>
        
        /// <summary>
        /// mRythmGameMusicData를 BGM 길이에 맞게 조정하고 BGA 관련 필드도 조정
        /// </summary>
        
        /// <summary>
        /// BGA/비디오 관련 필드 조정
        /// </summary>
        
        /// <summary>
        /// 일반적인 게임 종료/클리어 관련 코루틴 후킹용 prefix
        /// </summary>
    }
}

