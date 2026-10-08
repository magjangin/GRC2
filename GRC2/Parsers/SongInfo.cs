using System.Collections.Generic;

namespace GRC2.Parsers
{
    /// <summary>
    /// 곡 정보를 저장하는 클래스 (info.txt 파싱 결과)
    /// </summary>
    public class SongInfo
    {
        /// <summary>
        /// 난이도 키. 순서가 게임의 난이도 인덱스(easy=0 ... expert=3)와 같으므로
        /// 곡 목록의 레벨 배열과 결과 화면의 레벨 표시가 모두 이 순서를 씁니다.
        /// </summary>
        public static readonly string[] DifficultyKeys = { "easy", "normal", "hard", "expert" };

        public string Title { get; set; } = "custom chart";
        public string Artist { get; set; } = ""; // 아티스트 (상세 패널의 아티스트 칸, 캐릭터가 없을 때 아티스트 ID로도 사용)
        public string Character { get; set; } = ""; // 캐릭터 (아티스트 ID로 사용)

        // 크레딧. 곡 선택 상세 패널과 곡 시작 화면에 표시하고(CreditTextPatch), 비어 있으면 "-"로 보입니다.
        public string Lyricist { get; set; } = "";  // 작사
        public string Composer { get; set; } = "";  // 작곡
        public string Arranger { get; set; } = "";  // 편곡
        public string Charter { get; set; } = "";   // 채보 (게임의 譜面制作者 칸)
        public string CdTitle { get; set; } = "";   // 수록 CD
        public string GameTitle { get; set; } = ""; // 원작 게임

        public List<string> Difficulties { get; set; } = new List<string>();
        public Dictionary<string, int> DifficultyNumbers { get; set; } = new Dictionary<string, int>();
    }
}
