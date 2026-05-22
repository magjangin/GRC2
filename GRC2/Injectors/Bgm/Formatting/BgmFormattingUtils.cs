using System;
using System.Collections.Generic;
using UnityEngine;

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
}

