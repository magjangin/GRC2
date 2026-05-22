using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GRC2.Helpers;
using MelonLoader;
using UnityEngine;

namespace GRC2.Loaders
{
    /// <summary>
    /// 게임 타입 로딩 및 초기화를 담당하는 클래스
    /// </summary>
    public static class GameTypeLoader
    {
        // 타입 캐시
        public static Type NoteCreateDataType { get; private set; }
        public static Type NoteTypeIdEnum { get; private set; }
        public static Type NoteLaneLeftRightEnum { get; private set; }
        public static Type NoteSubLaneTypeEnum { get; private set; }
        public static Type NoteDirectionIndexEnum { get; private set; }
        public static Type NoteSizeEnum { get; private set; }
        public static Type SlideEndFlickDirectionEnum { get; private set; }

        public static void Initialize()
        {
            try
            {
                var assembly = LoadGameAssembly();
                if (assembly == null)
                {
                    MelonLogger.Error("[GameTypeLoader] Assembly-CSharp를 찾을 수 없습니다.");
                    return;
                }

                // NoteCreateData 타입 찾기
                FindNoteCreateDataType(assembly);
                if (NoteCreateDataType == null)
                {
                    MelonLogger.Error("[GameTypeLoader] NoteCreateData 타입을 찾을 수 없습니다.");
                    return;
                }

                // Enum 타입들 찾기
                FindEnumTypes(assembly);

                // 초기화 완료 로그
                LogInitializationStatus();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[GameTypeLoader] 초기화 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 게임 Assembly를 로드합니다.
        /// </summary>
        private static Assembly LoadGameAssembly()
        {
            var assemblyPath = Path.Combine(
                Path.GetDirectoryName(Application.dataPath),
                "GUNVOLT_RECORDS_Cychronicle_Data",
                "Managed",
                "Assembly-CSharp.dll"
            );

            if (File.Exists(assemblyPath))
            {
                return Assembly.LoadFrom(assemblyPath);
            }

            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
        }

        /// <summary>
        /// NoteCreateData 타입을 찾습니다.
        /// </summary>
        private static void FindNoteCreateDataType(Assembly assembly)
        {
            // 1. 직접 타입 검색
            NoteCreateDataType = assembly.GetType("IntiCreates.RythmGame.FairyMode.NoteCreateData");
            
            if (NoteCreateDataType == null)
            {
                // 2. 이름으로 검색
                NoteCreateDataType = FindTypeByName(assembly, "NoteCreateData");
            }

            // 3. createNote 메서드에서 파라미터 타입 확인
            if (NoteCreateDataType == null)
            {
                NoteCreateDataType = FindNoteCreateDataTypeFromMethod(assembly);
            }
        }

        /// <summary>
        /// createNote 메서드나 mFairyNoteCreateDataArray 필드에서 NoteCreateData 타입을 찾습니다.
        /// </summary>
        private static Type FindNoteCreateDataTypeFromMethod(Assembly assembly)
        {
            try
            {
                var managerType = assembly.GetType("IntiCreates.cFairyModeNotesManager");
                if (managerType == null)
                {
                    return null;
                }

                // createNote 메서드에서 파라미터 타입 확인
                var createNoteMethod = managerType.GetMethod("createNote", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (createNoteMethod != null)
                {
                    var parameters = createNoteMethod.GetParameters();
                    if (parameters.Length > 0 && parameters[0].ParameterType.Name == "NoteCreateData")
                    {
                        return parameters[0].ParameterType;
                    }
                }
                
                // mFairyNoteCreateDataArray 필드 타입 확인
                var arrayField = managerType.GetField("mFairyNoteCreateDataArray",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (arrayField?.FieldType.IsArray == true)
                {
                    var elementType = arrayField.FieldType.GetElementType();
                    if (elementType?.Name == "NoteCreateData")
                    {
                        return elementType;
                    }
                }
            }
            catch
            {
                // 오류 발생 시 무시
            }

            return null;
        }

        /// <summary>
        /// NoteCreateData 필드에서 SlideEndFlickDirection Enum을 찾습니다.
        /// </summary>
        private static Type FindSlideEndFlickDirectionFromField(Assembly assembly)
        {
            try
            {
                var slideEndFlickField = NoteCreateDataType.GetField(FieldAccessHelper.FIELD_SLIDE_END_FLICK_DIRECTION,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (slideEndFlickField?.FieldType.IsEnum == true)
                {
                    var fieldType = slideEndFlickField.FieldType;
                    // SlideEndFlickDirection이거나 NoteDirectionIndex인 경우
                    if (fieldType.Name.Contains("SlideEndFlick") || fieldType.Name.Contains("FlickDirection"))
                    {
                        return fieldType;
                    }
                    // NoteDirectionIndex를 사용하는 경우 null 반환
                    if (fieldType.Name == "NoteDirectionIndex" || fieldType == NoteDirectionIndexEnum)
                    {
                        return null;
                    }
                }
            }
            catch
            {
                // 오류 발생 시 무시
            }
            
            return null;
        }

        /// <summary>
        /// 초기화 상태를 로그로 출력합니다.
        /// </summary>
        private static void LogInitializationStatus()
        {
            MelonLogger.Msg("[GameTypeLoader] 초기화 완료 - 로드된 타입:");
            
            if (NoteCreateDataType != null)
                MelonLogger.Msg($"  - {NoteCreateDataType.Name} ({NoteCreateDataType.Namespace})");
            
            if (NoteTypeIdEnum != null)
                MelonLogger.Msg($"  - {NoteTypeIdEnum.Name}");
            
            if (NoteLaneLeftRightEnum != null)
                MelonLogger.Msg($"  - {NoteLaneLeftRightEnum.Name}");
            
            if (NoteSubLaneTypeEnum != null)
                MelonLogger.Msg($"  - {NoteSubLaneTypeEnum.Name}");
            
            if (NoteDirectionIndexEnum != null)
                MelonLogger.Msg($"  - {NoteDirectionIndexEnum.Name}");
            
            if (NoteSizeEnum != null)
                MelonLogger.Msg($"  - {NoteSizeEnum.Name}");
            
            if (SlideEndFlickDirectionEnum != null)
                MelonLogger.Msg($"  - {SlideEndFlickDirectionEnum.Name}");
        }

        /// <summary>
        /// 모든 Enum 타입을 찾습니다.
        /// </summary>
        private static void FindEnumTypes(Assembly assembly)
        {
            NoteTypeIdEnum = assembly.GetType("IntiCreates.RythmGame.FairyMode.NoteTypeId") 
                ?? FindTypeByName(assembly, "NoteTypeId");

            NoteLaneLeftRightEnum = assembly.GetType("IntiCreates.RythmGame.FairyMode.NoteLaneLeftRight") 
                ?? FindTypeByName(assembly, "NoteLaneLeftRight");

            NoteSubLaneTypeEnum = assembly.GetType("IntiCreates.RythmGame.FairyMode.NoteSubLaneType") 
                ?? FindTypeByName(assembly, "NoteSubLaneType");

            NoteDirectionIndexEnum = assembly.GetType("IntiCreates.RythmGame.NoteDirectionIndex") 
                ?? FindTypeByName(assembly, "NoteDirectionIndex");

            NoteSizeEnum = assembly.GetType("IntiCreates.RythmGame.NoteSize") 
                ?? FindTypeByName(assembly, "NoteSize");

            SlideEndFlickDirectionEnum = assembly.GetType("IntiCreates.RythmGame.SlideEndFlickDirection") 
                ?? FindTypeByName(assembly, "SlideEndFlickDirection");
            
            // SlideEndFlickDirection을 찾지 못한 경우, NoteCreateData 필드에서 타입 확인
            if (SlideEndFlickDirectionEnum == null && NoteCreateDataType != null)
            {
                SlideEndFlickDirectionEnum = FindSlideEndFlickDirectionFromField(assembly);
            }
        }

        /// <summary>
        /// 이름으로 타입을 찾습니다.
        /// </summary>
        private static Type FindTypeByName(Assembly assembly, string typeName)
        {
            try
            {
                return assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
            }
            catch
            {
                return null;
            }
        }
    }
}

