using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;

namespace GRC2.Core
{
    /// <summary>
    /// 리플렉션 헬퍼 메서드 - 성능 최적화: 타입 및 메서드 캐싱
    /// </summary>
    public static class ReflectionHelper
    {
        // ⚡ 성능 최적화: 타입 캐시 (문자열 → Type)
        private static Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();
        
        // ⚡ 성능 최적화: 메서드 캐시 (typeName.methodName → MethodInfo)
        private static Dictionary<string, MethodInfo> _methodCache = new Dictionary<string, MethodInfo>();
        
        // ⚡ 어셈블리 배열 캐시 (반복 호출 시 배열 할당 감소)
        private static Assembly[] _cachedAssemblies = null;
        
        /// <summary>
        /// 캐시된 어셈블리 배열 가져오기
        /// </summary>
        private static Assembly[] GetCachedAssemblies()
        {
            if (_cachedAssemblies == null)
            {
                _cachedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            }
            return _cachedAssemblies;
        }

        /// <summary>
        /// 타입과 메서드를 찾는 헬퍼 메서드
        /// </summary>
        public static MethodInfo FindMethod(string typeName, string methodName, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic, bool silent = false)
        {
            var cacheKey = $"{typeName}.{methodName}";
            
            // ⚡ 캐시에서 먼저 검색
            if (_methodCache.TryGetValue(cacheKey, out var cachedMethod))
            {
                return cachedMethod;
            }

            var assemblies = GetCachedAssemblies();
            foreach (var assembly in assemblies)
            {
                try
                {
                    var type = assembly.GetType(typeName);
                    if (type != null)
                    {
                        var method = type.GetMethod(methodName, flags);
                        if (method != null)
                        {
                            // ⚡ 캐시에 저장
                            _methodCache[cacheKey] = method;
                            
                            if (!silent)
                            {
                                MelonLogger.Msg($"[ReflectionHelper] 메서드 발견: {typeName}.{methodName}");
                            }
                            return method;
                        }
                    }
                }
                catch (Exception)
                {
                    // 메서드 탐색 실패 시 다음 후보 어셈블리/타입 시도
                }
            }
            
            // ⚡ 캐시에 null 저장 (다음 호출 시 빠르게 반환)
            _methodCache[cacheKey] = null;
            
            if (!silent)
            {
                MelonLogger.Msg($"[ReflectionHelper] 메서드를 찾을 수 없습니다: {typeName}.{methodName}");
            }
            return null;
        }

        /// <summary>
        /// 타입을 찾는 헬퍼 메서드 - 캐싱 적용
        /// </summary>
        public static Type FindType(string typeName)
        {
            // ⚡ 캐시에서 먼저 검색
            if (_typeCache.TryGetValue(typeName, out var cachedType))
            {
                return cachedType;
            }

            var assemblies = GetCachedAssemblies();
            foreach (var assembly in assemblies)
            {
                try
                {
                    var type = assembly.GetType(typeName);
                    if (type != null)
                    {
                        // ⚡ 캐시에 저장
                        _typeCache[typeName] = type;
                        return type;
                    }
                }
                catch (Exception)
                {
                    // GetType 실패 시 다음 어셈블리 시도
                }
            }
            
            // ⚡ 캐시에 null 저장 (다음 호출 시 빠르게 반환)
            _typeCache[typeName] = null;
            return null;
        }
        
        /// <summary>
        /// 캐시 초기화 (메모리 정리 필요 시)
        /// </summary>
        public static void ClearCache()
        {
            _typeCache.Clear();
            _methodCache.Clear();
            _cachedAssemblies = null;
        }
    }
}





















































