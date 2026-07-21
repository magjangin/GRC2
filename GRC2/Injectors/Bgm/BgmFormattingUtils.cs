using System;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace GRC2.Injectors
{
    /// <summary>
    /// BGM Injector 후킹에서 사용하는 포맷팅 유틸리티 클래스
    /// </summary>
    internal static partial class BgmFormattingUtils
    {
        public static string FormatArguments(object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return "";
            }
            
            var argValues = new List<string>();
            foreach (var arg in args)
            {
                if (arg is AudioClip clip)
                {
                    var clipName = string.IsNullOrEmpty(clip.name) ? "(이름 없음)" : clip.name;
                    argValues.Add($"AudioClip({clipName}, {clip.length:F3}초)");
                }
                else if (arg is float f)
                    argValues.Add($"{f:F3}");
                else if (arg is int n)
                    argValues.Add(n.ToString());
                else if (arg is string s)
                    argValues.Add($"\"{s}\"");
                else if (arg is bool b)
                    argValues.Add(b.ToString());
                else if (arg != null)
                    argValues.Add(arg.ToString());
                else
                    argValues.Add("null");
            }
            return string.Join(", ", argValues);
        }
        
        public static string FormatResult(object result)
        {
            if (result == null)
            {
                return "";
            }
            
            if (result is float rf)
                return $" → {rf:F3}";
            else if (result is int ri)
                return $" → {ri}";
            else if (result is AudioClip resultClip)
            {
                var resultClipName = string.IsNullOrEmpty(resultClip.name) ? "(이름 없음)" : resultClip.name;
                return $" → AudioClip({resultClipName}, {resultClip.length:F3}초)";
            }
            else
                return $" → {result}";
        }
        
        /// <summary>
        /// 필드 값을 포맷팅하여 문자열로 반환 (배열, 리스트, 복잡한 객체 지원)
        /// </summary>
        public static string FormatFieldValue(object value, Type fieldType, string fieldName)
        {
            if (value == null)
            {
                return "null";
            }
            
            if (fieldType == typeof(int))
            {
                return FormatIntField((int)value, fieldName);
            }
            else if (fieldType == typeof(float))
            {
                return FormatFloatField((float)value, fieldName);
            }
            else if (fieldType == typeof(bool))
            {
                return ((bool)value).ToString();
            }
            else if (fieldType == typeof(string))
            {
                return $"\"{value}\"";
            }
            else if (fieldType.IsArray)
            {
                return FormatArrayField(value, fieldType);
            }
            else if (TryFormatListField(value, out string listValue))
            {
                return listValue;
            }
            else if (TryFormatDictionaryField(value, out string dictionaryValue))
            {
                return dictionaryValue;
            }
            else if (value is AudioClip clip)
            {
                var clipName = string.IsNullOrEmpty(clip.name) ? "(이름 없음)" : clip.name;
                return $"AudioClip({clipName}, {clip.length:F3}초)";
            }
            else if (value is UnityEngine.Object unityObj)
            {
                return $"{value.GetType().Name}({unityObj.name ?? "null"})";
            }
            // 열거형 처리
            else if (fieldType.IsEnum)
            {
                return $"{value} ({Convert.ToInt32(value)})";
            }
            else if (fieldType.IsClass)
            {
                return FormatComplexObjectField(value);
            }
            else
            {
                return value.ToString();
            }
        }
    }

    internal static partial class BgmFormattingUtils
    {
        private const int MaxFormattedElements = 10;

        private static string FormatArrayField(object value, Type fieldType)
        {
            try
            {
                Array array = value as Array;
                if (array == null)
                    return "null";

                int length = array.Length;
                string elementTypeName = fieldType.GetElementType().Name;
                if (length == 0)
                    return $"{elementTypeName}[] (길이: 0)";

                var elements = FormatIndexedElements(length, i => array.GetValue(i));
                return FormatBoundedCollection($"{elementTypeName}[]", length, elements);
            }
            catch (Exception ex)
            {
                return $"{fieldType.Name}[] (읽기 실패: {ex.Message})";
            }
        }

        private static bool TryFormatListField(object value, out string formatted)
        {
            formatted = null;
            if (!(value is IList list))
                return false;

            try
            {
                int count = list.Count;
                if (count == 0)
                {
                    formatted = "IList (길이: 0)";
                    return true;
                }

                var elements = FormatIndexedElements(count, i => list[i]);
                formatted = FormatBoundedCollection("IList", count, elements);
                return true;
            }
            catch (Exception ex)
            {
                formatted = $"IList (읽기 실패: {ex.Message})";
                return true;
            }
        }

        private static bool TryFormatDictionaryField(object value, out string formatted)
        {
            formatted = null;
            if (!(value is IDictionary dict))
                return false;

            try
            {
                int count = dict.Count;
                if (count == 0)
                {
                    formatted = "IDictionary (길이: 0)";
                    return true;
                }

                var entries = new List<string>();
                int currentIndex = 0;
                foreach (DictionaryEntry entry in dict)
                {
                    if (currentIndex >= MaxFormattedElements)
                        break;

                    string keyStr = entry.Key?.ToString() ?? "null";
                    string valueStr = FormatFieldValue(entry.Value, entry.Value?.GetType() ?? typeof(object), "");
                    entries.Add($"{keyStr}: {valueStr}");
                    currentIndex++;
                }

                formatted = FormatBoundedCollection("IDictionary", count, entries);
                return true;
            }
            catch (Exception ex)
            {
                formatted = $"IDictionary (읽기 실패: {ex.Message})";
                return true;
            }
        }

        private static List<string> FormatIndexedElements(int count, Func<int, object> getValue)
        {
            var elements = new List<string>();
            int maxElements = Math.Min(count, MaxFormattedElements);
            for (int i = 0; i < maxElements; i++)
            {
                var element = getValue(i);
                if (element == null)
                {
                    elements.Add("null");
                }
                else
                {
                    string elementStr = FormatFieldValue(element, element.GetType(), "");
                    elements.Add($"[{i}] {elementStr}");
                }
            }

            return elements;
        }

        private static string FormatBoundedCollection(string label, int count, List<string> elements)
        {
            string result = $"{label} (길이: {count})";
            if (count > MaxFormattedElements)
            {
                return result + $" - 처음 {MaxFormattedElements}개: [{string.Join(", ", elements)}] ...";
            }

            return result + $" - [{string.Join(", ", elements)}]";
        }
    }

    internal static partial class BgmFormattingUtils
    {
        private static string FormatIntField(int intValue, string fieldName)
        {
            if (fieldName.Contains("Sample"))
            {
                float timeValue = intValue / 48000f;
                return $"{intValue} 샘플 ({timeValue:F3}초)";
            }

            return intValue.ToString();
        }

        private static string FormatFloatField(float floatValue, string fieldName)
        {
            if (fieldName.Contains("Sec") || fieldName.Contains("Time"))
            {
                return $"{floatValue:F3}초";
            }

            return floatValue.ToString("F3");
        }

        private static string FormatComplexObjectField(object value)
        {
            try
            {
                var objType = value.GetType();
                var importantFields = objType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(IsImportantObjectField)
                    .Take(5)
                    .ToList();

                var fieldValues = ReadImportantFieldValues(value, importantFields);
                if (fieldValues.Count > 0)
                {
                    return $"{objType.Name}({string.Join(", ", fieldValues)})";
                }

                return $"{objType.Name} 인스턴스";
            }
            catch
            {
                return $"{value.GetType().Name} 인스턴스";
            }
        }

        private static bool IsImportantObjectField(FieldInfo field)
        {
            return field.FieldType.IsPrimitive ||
                field.FieldType == typeof(string) ||
                field.FieldType == typeof(float) ||
                field.FieldType == typeof(int) ||
                field.FieldType == typeof(bool) ||
                field.Name.Contains("Sample") ||
                field.Name.Contains("Time") ||
                field.Name.Contains("Sec") ||
                field.Name.Contains("Length") ||
                field.Name.Contains("Count");
        }

        private static List<string> ReadImportantFieldValues(object value, List<FieldInfo> importantFields)
        {
            var fieldValues = new List<string>();
            foreach (var field in importantFields)
            {
                try
                {
                    var fieldValue = field.GetValue(value);
                    string fieldValueStr = FormatFieldValue(fieldValue, field.FieldType, field.Name);
                    fieldValues.Add($"{field.Name}={fieldValueStr}");
                }
                catch
                {
                    // 필드 읽기 실패는 무시
                }
            }

            return fieldValues;
        }
    }
}
