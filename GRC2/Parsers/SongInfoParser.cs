using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MelonLoader;

namespace GRC2.Parsers
{
    /// <summary>
    /// hwa 폴더의 txt 파일에서 곡 정보를 파싱하는 클래스
    /// </summary>
    public static class SongInfoParser
    {
        /// <summary>
        /// 크레딧 줄의 키와 저장할 칸입니다. 곡 선택 상세 패널의 칸 순서(아티스트·작사·작곡·편곡·채보·CD·원작 게임)를 따릅니다.
        /// 작곡가/composer는 예전에는 아티스트로 읽었지만, 상세 패널에 작곡 칸이 따로 있어 작곡으로 읽습니다.
        /// "artistid"는 아티스트 키 뒤에 구분자가 아닌 글자가 붙으므로 여기서 걸리지 않고 캐릭터 필드로 갑니다.
        /// 공백도 구분자라서 "원작 게임"보다 "원작"이 먼저 있으면 값이 "게임 : …"으로 읽힙니다. 여러 단어 키를 앞에 둡니다.
        /// </summary>
        private static readonly (string Label, Regex Pattern, Action<SongInfo, string> Assign)[] CreditFields =
        {
            ("아티스트", CreditPattern("아티스트|artist"), (info, value) => info.Artist = value),
            ("작사", CreditPattern("작사가|작사|lyricist|lyrics|作詞"), (info, value) => info.Lyricist = value),
            ("작곡", CreditPattern("작곡가|작곡|composer|作曲"), (info, value) => info.Composer = value),
            ("편곡", CreditPattern("편곡가|편곡|arranger|arrange|編曲"), (info, value) => info.Arranger = value),
            ("채보", CreditPattern("채보자|채보|charter|譜面制作者|譜面制作|譜面"), (info, value) => info.Charter = value),
            ("수록 CD", CreditPattern(@"수록\s*cd|수록\s*앨범|収録\s*cd|cd\s*title|cd"), (info, value) => info.CdTitle = value),
            ("원작 게임", CreditPattern(@"원작\s*게임|원작|게임|game\s*title|game|原作"), (info, value) => info.GameTitle = value)
        };

        private static Regex CreditPattern(string keys)
        {
            return new Regex(@"^#?(" + keys + @")\s*[:=：\s]\s*(.+)", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// txt 파일에서 곡 정보를 파싱
        /// </summary>
        /// <param name="filePath">txt 파일 경로</param>
        /// <returns>파싱된 곡 정보</returns>
        public static SongInfo ParseTxtFile(string filePath)
        {
            var songInfo = new SongInfo();

            if (!File.Exists(filePath))
            {
                MelonLogger.Warning($"[SongInfoParser] 파일을 찾을 수 없습니다: {filePath}");
                return songInfo;
            }

            try
            {
                MelonLogger.Msg($"[SongInfoParser] 곡 정보 파일 파싱 시작: {Path.GetFileName(filePath)}");

                string text = "";
                string encodingUsed = "Default/BOM Detect";

                // BOM이 있으면 그 인코딩을 따르고, 없으면 UTF-8로 읽습니다.
                // 예전 주석은 CP949라고 했지만 Unity Mono에서 Encoding.Default는 UTF-8입니다(알려진 문제 G). 그래서 명시합니다.
                using (var reader = new StreamReader(filePath, System.Text.Encoding.UTF8, true))
                {
                    text = reader.ReadToEnd();
                    encodingUsed = reader.CurrentEncoding.EncodingName;
                }
                
                MelonLogger.Msg($"[SongInfoParser] 인코딩 결정: {encodingUsed}");
                string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                
                foreach (var line in lines)
                {
                    string trimmedLine = line.Trim();
                    if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("//"))
                        continue;

                    // 인라인 주석 제거
                    int commentIndex = trimmedLine.IndexOf("//");
                    if (commentIndex >= 0)
                    {
                        trimmedLine = trimmedLine.Substring(0, commentIndex).Trim();
                    }

                    if (string.IsNullOrWhiteSpace(trimmedLine))
                        continue;

                    // 제목 필드
                    var titleMatch = Regex.Match(trimmedLine, @"^#?(곡\s*제목|곡명|제목|title|name)\s*[:=：\s]\s*(.+)", RegexOptions.IgnoreCase);
                    if (titleMatch.Success)
                    {
                        songInfo.Title = titleMatch.Groups[2].Value.Trim().Normalize(System.Text.NormalizationForm.FormC);
                        
                        // 디버깅 로그
                        MelonLogger.Msg($"[SongInfoParser] 곡 제목 파싱: '{songInfo.Title}'");
                        continue;
                    }

                    // 크레딧 필드 (아티스트, 작사, 작곡, 편곡, 채보, 수록 CD, 원작 게임)
                    if (TryApplyCreditLine(trimmedLine, songInfo, out string creditLabel, out string creditValue))
                    {
                        MelonLogger.Msg($"[SongInfoParser] {creditLabel}: {creditValue}");
                        continue;
                    }

                    // 난이도 필드
                    var difficultyMatch = Regex.Match(trimmedLine, @"^#?(난이도|difficulty|level)\s*[:=：\s]\s*(.+)", RegexOptions.IgnoreCase);
                    if (difficultyMatch.Success)
                    {
                        var difficultyStr = difficultyMatch.Groups[2].Value.Trim();
                        songInfo.Difficulties = difficultyStr.Split(new[] { ',', '，', '|', ';' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(d => d.Trim())
                            .Where(d => !string.IsNullOrEmpty(d))
                            .ToList();
                        MelonLogger.Msg($"[SongInfoParser] 난이도 목록: {string.Join(", ", songInfo.Difficulties)}");
                        continue;
                    }
                    
                    // 캐릭터/아티스트ID 필드
                    var characterMatch = Regex.Match(trimmedLine, @"^#?(캐릭터|character|artistid)\s*[:=：\s]\s*(.+)", RegexOptions.IgnoreCase);
                    if (characterMatch.Success)
                    {
                        songInfo.Character = characterMatch.Groups[2].Value.Trim();
                        MelonLogger.Msg($"[SongInfoParser] 캐릭터(ArtistID): {songInfo.Character}");
                        continue;
                    }
                    
                    // 난이도 숫자 매핑 (easy: 5 등)
                    var difficultyNumberMatch = Regex.Match(trimmedLine, @"^#?(easy|normal|hard|expert)\s*[:=：\s]\s*(\d+)", RegexOptions.IgnoreCase);
                    if (difficultyNumberMatch.Success)
                    {
                        var difficultyName = difficultyNumberMatch.Groups[1].Value.Trim().ToLower();
                        if (int.TryParse(difficultyNumberMatch.Groups[2].Value.Trim(), out int difficultyNumber))
                        {
                            songInfo.DifficultyNumbers[difficultyName] = difficultyNumber;
                            MelonLogger.Msg($"[SongInfoParser] 난이도 숫자 매핑: {difficultyName} = {difficultyNumber}");
                        }
                        continue;
                    }
                }

                // 난이도 숫자 매핑 로그
                var difficultyNumbersStr = string.Join(", ", songInfo.DifficultyNumbers.Select(kvp => $"{kvp.Key}={kvp.Value}"));
                MelonLogger.Msg($"[SongInfoParser] 곡 정보 파싱 완료 - 제목: {songInfo.Title}, 아티스트: {songInfo.Artist}, 난이도: {string.Join(", ", songInfo.Difficulties)}");
                if (!string.IsNullOrEmpty(difficultyNumbersStr))
                {
                    MelonLogger.Msg($"[SongInfoParser] 난이도 숫자 매핑: {difficultyNumbersStr}");
                }
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, "[SongInfoParser]", "곡 정보 파싱 오류");
            }

            return songInfo;
        }

        /// <summary>
        /// 크레딧 줄이면 값을 songInfo의 해당 칸에 넣고 true를 돌려줍니다.
        /// 주석과 앞뒤 공백을 걷어 낸 줄을 받으며, 로그는 남기지 않습니다(호출하는 쪽이 label/value로 남깁니다).
        /// </summary>
        public static bool TryApplyCreditLine(string line, SongInfo songInfo, out string label, out string value)
        {
            label = null;
            value = null;
            if (songInfo == null || string.IsNullOrWhiteSpace(line))
                return false;

            foreach (var field in CreditFields)
            {
                var match = field.Pattern.Match(line);
                if (!match.Success)
                    continue;

                label = field.Label;
                value = match.Groups[2].Value.Trim().Normalize(System.Text.NormalizationForm.FormC);
                field.Assign(songInfo, value);
                return true;
            }

            return false;
        }
    }
}
