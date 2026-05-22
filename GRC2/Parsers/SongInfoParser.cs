using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MelonLoader;

namespace GRC2.Parsers
{
    /// <summary>
    /// 곡 정보를 저장하는 클래스
    /// </summary>
    public class SongInfo
    {
        public string Title { get; set; } = "custom chart";
        public string Artist { get; set; } = "";
        public string Character { get; set; } = ""; // 캐릭터 (아티스트 ID로 사용)
        public List<string> Difficulties { get; set; } = new List<string>();
        public Dictionary<string, int> DifficultyNumbers { get; set; } = new Dictionary<string, int>();
    }

    /// <summary>
    /// hwa 폴더의 txt 파일에서 곡 정보를 파싱하는 클래스
    /// </summary>
    public static class SongInfoParser
    {
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

                // 가장 표준적이고 안정적인 방식: StreamReader의 자동 감지 기능 사용
                // BOM이 있으면 UTF-8/UTF-16으로, 없으면 Encoding.Default(한국어 윈도우는 CP949)로 읽음
                using (var reader = new StreamReader(filePath, System.Text.Encoding.Default, true))
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

                    // 아티스트 필드 (아티스트, 작곡가, artist, composer 등)
                    var artistMatch = Regex.Match(trimmedLine, @"^#?(아티스트|작곡가|artist|composer)\s*[:=：\s]\s*(.+)", RegexOptions.IgnoreCase);
                    if (artistMatch.Success)
                    {
                        songInfo.Artist = artistMatch.Groups[2].Value.Trim();
                        MelonLogger.Msg($"[SongInfoParser] 아티스트: {songInfo.Artist}");
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
    }
}
