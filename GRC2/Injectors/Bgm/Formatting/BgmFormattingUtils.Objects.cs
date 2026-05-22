using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace GRC2.Injectors
{
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
