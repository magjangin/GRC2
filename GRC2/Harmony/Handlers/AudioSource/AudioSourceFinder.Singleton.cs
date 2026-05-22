using MelonLoader;
using System;
using System.Reflection;
using UnityEngine;

namespace GRC2.Harmony.Handlers
{
    public static partial class AudioSourceFinder
    {
        /// <summary>
        /// 싱글톤 인스턴스 찾기
        /// </summary>
        public static object FindSingletonInstance(Type type)
        {
            if (type == null)
                return null;

            try
            {
                object instance = FindSingletonPropertyValue(type);
                if (instance != null)
                    return instance;

                return FindSingletonFieldValue(type);
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[AudioSourceFinder] 싱글톤 인스턴스 찾기 오류: {ex.Message}");
            }

            return null;
        }

        private static object FindSingletonPropertyValue(Type type)
        {
            foreach (string propertyName in SingletonPropertyNames)
            {
                PropertyInfo instanceProp = type.GetProperty(propertyName, InstanceLookupFlags);
                if (instanceProp != null && instanceProp.CanRead)
                    return instanceProp.GetValue(null);
            }

            return null;
        }

        private static object FindSingletonFieldValue(Type type)
        {
            foreach (string fieldName in SingletonFieldNames)
            {
                FieldInfo instanceField = type.GetField(fieldName, InstanceLookupFlags);
                if (instanceField != null)
                    return instanceField.GetValue(null);
            }

            return null;
        }

        private static object FindSoundManagerInstance(Type soundManagerType)
        {
            object soundManagerInstance = FindSingletonInstance(soundManagerType);
            if (soundManagerInstance != null)
            {
                MelonLogger.Msg("[AudioSourceFinder] ✅ sSoundManager2D 인스턴스 발견 (싱글톤)");
                return soundManagerInstance;
            }

            try
            {
                UnityEngine.Object[] instances = UnityEngine.Object.FindObjectsOfType(soundManagerType);
                if (instances != null && instances.Length > 0)
                {
                    MelonLogger.Msg($"[AudioSourceFinder] ✅ sSoundManager2D 인스턴스 발견 (FindObjectsOfType): {instances.Length}개");
                    return instances[0];
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[AudioSourceFinder] ❌ FindObjectsOfType 오류: {ex.Message}");
            }

            return null;
        }
    }
}
