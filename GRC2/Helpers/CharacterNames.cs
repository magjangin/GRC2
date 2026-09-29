using System;
using System.Collections.Generic;

namespace GRC2.Helpers
{
    /// <summary>
    /// info.txt의 캐릭터/아티스트 표기(한글 별칭 포함)를 게임 <c>PCD.MainCharactor</c> enum 이름으로 통일합니다.
    /// 예전에는 <c>AlbumManager</c>와 <c>MusicScrollViewHooks</c>가 같은 표를 따로 갖고 있었습니다.
    /// </summary>
    public static class CharacterNames
    {
        private static readonly Dictionary<string, string> Aliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "르호", "Morpho" },
                { "Morpho", "Morpho" },
                { "Roro", "Roro" },
                { "룩시아", "Luxair" },
                { "Luxair", "Luxair" }
            };

        /// <summary>
        /// 앞뒤 공백을 지우고 알려진 별칭이면 enum 이름으로 바꿉니다.
        /// 비어 있거나 공백뿐인 값은 그대로 돌려줍니다. 모르는 값은 공백만 지운 채 돌려줍니다.
        /// </summary>
        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;

            string trimmed = value.Trim();
            return Aliases.TryGetValue(trimmed, out string canonical) ? canonical : trimmed;
        }
    }
}
