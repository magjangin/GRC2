using System;
using System.Collections;
using System.Collections.Generic;

namespace GRC2.Injectors
{
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
}
