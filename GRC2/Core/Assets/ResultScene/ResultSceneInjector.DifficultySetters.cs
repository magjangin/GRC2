using System;
using System.Collections.Generic;
using GRC2.Helpers;

namespace GRC2.Core
{
    public static partial class ResultSceneInjector
    {
        private static bool TrySetDifficultyOnObject(object target, Type type, int[] lvArray, string logLabel)
        {
            if (SetMusicLVArray(target, lvArray))
            {
                MelonLoader.MelonLogger.Msg($"[ResultSceneInjector] ✅ 결과 씬 난이도 적용: {logLabel}");
                return true;
            }

            foreach (var field in type.GetFields(InstanceFieldFlags))
            {
                if (field.FieldType.IsClass && field.FieldType != typeof(string))
                {
                    object nested = field.GetValue(target);
                    if (nested != null && SetMusicLVArray(nested, lvArray))
                    {
                        MelonLoader.MelonLogger.Msg($"[ResultSceneInjector] ✅ 결과 씬 난이도 적용: {logLabel}.{field.Name}");
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool SetMusicLVArray(object obj, int[] lvArray)
        {
            if (obj == null || lvArray == null) return false;
            try
            {
                Type type = obj.GetType();
                foreach (string name in DifficultyArrayFieldNames)
                {
                    var field = type.GetField(name, InstanceFieldFlags);
                    if (field != null && field.FieldType == typeof(int[]))
                    {
                        field.SetValue(obj, lvArray);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[ResultSceneInjector] TrySetMusicLvArrayOnObject", "리플렉션 실패");
            }

            return false;
        }

        private static string FormatDifficultyString(int[] lvArray)
        {
            if (lvArray == null || lvArray.Length == 0) return "";
            string[] labels = { "EASY", "NORMAL", "HARD", "EXPERT" };
            var parts = new List<string>();
            for (int i = 0; i < lvArray.Length && i < labels.Length; i++)
                parts.Add($"{labels[i]} {lvArray[i]}");
            return string.Join("  /  ", parts);
        }

        private static bool TrySetDifficultyText(object updater, Type updaterType, string difficultyStr)
        {
            if (updater == null || string.IsNullOrEmpty(difficultyStr)) return false;
            try
            {
                var field = updaterType.GetField("mDifficultyText", InstanceFieldFlags);
                if (field == null) return false;
                object textComponent = field.GetValue(updater);
                if (textComponent == null) return false;
                var prop = textComponent.GetType().GetProperty("text", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (prop != null && prop.CanWrite)
                {
                    prop.SetValue(textComponent, difficultyStr);
                    return true;
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[ResultSceneInjector] TrySetDifficultyText", "리플렉션 실패");
            }

            return false;
        }
    }
}
