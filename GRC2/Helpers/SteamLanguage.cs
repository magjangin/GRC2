using System;
using System.Globalization;

namespace GRC2.Helpers
{
    /// <summary>
    /// OS 언어 → Steam 언어 API 이름. 스팀 없이(Init 실패) 실행하면 <c>SteamApps.GetCurrentGameLanguage</c>가 예외를 내므로
    /// <c>SteamApiHijacker</c>가 이 값으로 대신 답합니다(알려진_문제 A4). Unity·Steamworks 없이 테스트됩니다.
    ///
    /// 2026-11-26(A4b): 예전에는 한국어에 <c>"korean"</c>을 돌려줬는데 Steam API 이름은 <c>"koreana"</c>라, 게임
    /// (<c>sLocalizeDirector.getCurrentLocale</c>)이 모르는 값으로 보고 영어를 골랐습니다. 일본어·중국어 등은 아예 영어였습니다.
    /// </summary>
    public static class SteamLanguage
    {
        /// <summary>게임 <c>sLocalizeDirector.getCurrentLocale</c>가 아는 Steam 언어 이름 전부. 테스트가 게임 어셈블리 문자열과 대조합니다.</summary>
        public static readonly string[] GameKnownNames =
        {
            "japanese", "english", "french", "german", "italian", "brazilian", "spanish", "schinese", "tchinese", "koreana",
        };

        public static string FromCulture(CultureInfo culture)
        {
            if (culture == null)
                return "english";

            switch (culture.TwoLetterISOLanguageName)
            {
                case "ko": return "koreana";
                case "ja": return "japanese";
                case "fr": return "french";
                case "de": return "german";
                case "it": return "italian";
                case "pt": return "brazilian";
                case "es": return "spanish";
                case "zh": return IsTraditionalChinese(culture) ? "tchinese" : "schinese";
                default: return "english";
            }
        }

        /// <summary>zh-TW·zh-HK·zh-MO·zh-Hant(-*)는 번체, 나머지(zh-CN·zh-SG·zh-Hans)는 간체.</summary>
        private static bool IsTraditionalChinese(CultureInfo culture)
        {
            for (var c = culture; c != null && !string.IsNullOrEmpty(c.Name); c = c.Parent)
            {
                string name = c.Name;
                if (name.Equals("zh-Hant", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("zh-CHT", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("zh-TW", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("zh-HK", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("zh-MO", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (ReferenceEquals(c, c.Parent))
                    break;
            }
            return false;
        }
    }
}
