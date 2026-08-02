using System;
using System.Collections.Generic;
using System.IO;
using GRC2.Helpers;
using MelonLoader;

namespace GRC2.Core
{
    /// <summary>
    /// savecustomkey 폴더의 설정 파일에서 AutoPlay/판정조작 on/off 값을 읽습니다.
    /// 단축키 토글 없이, 파일 값이 곧 적용값입니다(수정 후 게임 재시작 시 반영).
    /// </summary>
    public static class CustomKeySettings
    {
        private const string FolderName = "savecustomkey";
        private const string FileName = "config.txt";

        public static bool AutoPlayEnabled { get; private set; }
        public static bool JudgePerfectEnabled { get; private set; }

        public static void Initialize(string gameFolder)
        {
            try
            {
                var folder = Path.Combine(gameFolder, FolderName);
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                    MelonLogger.Msg($"[CustomKeySettings] {FolderName} 폴더 생성 완료: {folder}");
                }

                var filePath = Path.Combine(folder, FileName);
                if (!File.Exists(filePath))
                {
                    File.WriteAllLines(filePath, new[]
                    {
                        "autoplay_enabled=false",
                        "judge_perfect_enabled=false"
                    });
                    MelonLogger.Msg($"[CustomKeySettings] 기본 설정 파일 생성: {filePath}");
                }

                Load(filePath);
                MelonLogger.Msg(
                    $"[CustomKeySettings] 로드 완료: autoplay_enabled={AutoPlayEnabled}, judge_perfect_enabled={JudgePerfectEnabled}");
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(ex, "[CustomKeySettings]", "초기화 오류");
            }
        }

        private static void Load(string filePath)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in File.ReadAllLines(filePath))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith("#"))
                    continue;

                var idx = trimmed.IndexOf('=');
                if (idx <= 0)
                    continue;

                values[trimmed.Substring(0, idx).Trim()] = trimmed.Substring(idx + 1).Trim();
            }

            AutoPlayEnabled = ParseBool(values, "autoplay_enabled", false);
            JudgePerfectEnabled = ParseBool(values, "judge_perfect_enabled", false);
        }

        private static bool ParseBool(Dictionary<string, string> values, string key, bool fallback)
        {
            if (!values.TryGetValue(key, out var raw))
                return fallback;

            raw = raw.Trim();
            if (bool.TryParse(raw, out var parsed))
                return parsed;

            if (raw == "0" || string.Equals(raw, "비활성화", StringComparison.Ordinal))
                return false;
            if (raw == "1" || string.Equals(raw, "활성화", StringComparison.Ordinal))
                return true;

            return fallback;
        }
    }
}
