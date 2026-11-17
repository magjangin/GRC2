using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GRC2.Helpers
{
    /// <summary>
    /// 게임 폰트에 없는 글자를 대신 그릴 폰트 파일 후보를 정합니다(Unity 없이 테스트 가능한 부분).
    /// 순서가 곧 TMP fallback 검색 순서입니다: 사용자가 hwa/fonts에 넣은 폰트가 먼저, 그다음 윈도우 기본 폰트.
    /// </summary>
    public static class FallbackFontCandidates
    {
        /// <summary>사용자 폰트 폴더 이름(게임 폴더/hwa/fonts).</summary>
        public const string UserFontFolderName = "fonts";

        /// <summary>
        /// 윈도우 기본 폰트 파일. 커스텀 곡 제목에 흔한 간체 → 번체 → 일본어 → 한국어 → 기호 순입니다.
        /// 게임 폰트에 이미 있는 글자에는 쓰이지 않으므로, 실제로는 게임 폰트가 못 그리는 글자만 여기로 옵니다.
        /// </summary>
        public static readonly string[] SystemFontFiles =
        {
            "msyh.ttc",     // Microsoft YaHei (간체)
            "msjh.ttc",     // Microsoft JhengHei (번체)
            "YuGothM.ttc",  // Yu Gothic Medium (일본어)
            "meiryo.ttc",   // Meiryo (Yu Gothic이 없는 구버전 윈도우)
            "malgun.ttf",   // 맑은 고딕 (한국어)
            "seguisym.ttf", // Segoe UI Symbol (기호)
        };

        private static readonly string[] UserFontExtensions = { ".ttf", ".otf", ".ttc" };

        /// <summary>
        /// 실제로 있는 폰트 파일 경로를 검색 순서대로 돌려줍니다. 같은 파일은 한 번만 넣습니다.
        /// </summary>
        /// <param name="userFontFolder">사용자 폰트 폴더(없어도 됨).</param>
        /// <param name="systemFontFolder">윈도우 폰트 폴더(보통 C:\Windows\Fonts).</param>
        /// <param name="listFiles">폴더의 파일 목록(테스트용 주입).</param>
        /// <param name="fileExists">파일 존재 여부(테스트용 주입).</param>
        public static List<string> Resolve(
            string userFontFolder,
            string systemFontFolder,
            Func<string, IEnumerable<string>> listFiles,
            Func<string, bool> fileExists)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Add(string path)
            {
                if (!string.IsNullOrEmpty(path) && seen.Add(path))
                    result.Add(path);
            }

            if (!string.IsNullOrEmpty(userFontFolder))
            {
                IEnumerable<string> userFiles;
                try { userFiles = listFiles(userFontFolder) ?? Enumerable.Empty<string>(); }
                catch { userFiles = Enumerable.Empty<string>(); }

                foreach (var file in userFiles
                    .Where(f => UserFontExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                    .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
                {
                    Add(file);
                }
            }

            if (!string.IsNullOrEmpty(systemFontFolder))
            {
                foreach (var name in SystemFontFiles)
                {
                    var path = Path.Combine(systemFontFolder, name);
                    if (fileExists(path))
                        Add(path);
                }
            }

            return result;
        }

        /// <summary>
        /// 진단 로그용: 문자열들에서 ASCII가 아닌 글자(서로게이트 쌍은 코드 포인트 하나로)를 중복 없이 모읍니다.
        /// </summary>
        public static List<int> CollectNonAsciiCodePoints(IEnumerable<string> texts)
        {
            var seen = new HashSet<int>();
            var result = new List<int>();
            foreach (var text in texts)
            {
                if (string.IsNullOrEmpty(text)) continue;
                for (int i = 0; i < text.Length; i++)
                {
                    int cp;
                    if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                    {
                        cp = char.ConvertToUtf32(text[i], text[i + 1]);
                        i++;
                    }
                    else
                    {
                        cp = text[i];
                    }

                    if (cp > 0x7F && !char.IsWhiteSpace((char)Math.Min(cp, 0xFFFF)) && seen.Add(cp))
                        result.Add(cp);
                }
            }
            return result;
        }
    }
}
