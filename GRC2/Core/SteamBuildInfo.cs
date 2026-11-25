using System;
using System.IO;
using System.Text.RegularExpressions;

namespace GRC2.Core
{
    /// <summary>Steam이 기록한 설치 빌드 정보 읽기. 게임 타입을 참조하지 않아 게임 어셈블리 없이도 테스트할 수 있습니다.</summary>
    public static class SteamBuildInfo
    {
        private const string SteamAppId = "2585040";

        /// <summary>appmanifest_*.acf 본문에서 "buildid" 값을 뽑습니다. 못 찾으면 null.</summary>
        public static string ParseBuildId(string acfText)
        {
            if (string.IsNullOrEmpty(acfText))
                return null;

            var match = Regex.Match(acfText, @"""buildid""\s+""([0-9]+)""", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>게임 폴더(.../steamapps/common/&lt;게임&gt;)에서 Steam이 기록한 현재 Build ID를 읽습니다. Steam 설치가 아니면 null.</summary>
        public static string ReadInstalledBuildId(string gameFolder)
        {
            try
            {
                var steamApps = Directory.GetParent(gameFolder)?.Parent;
                if (steamApps == null)
                    return null;

                var acf = Path.Combine(steamApps.FullName, $"appmanifest_{SteamAppId}.acf");
                return File.Exists(acf) ? ParseBuildId(File.ReadAllText(acf)) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
