using System.Collections.Generic;
using System.Linq;
using GRC2.Helpers;
using Xunit;

namespace GRC2.Tests
{
    public class FallbackFontCandidatesTests
    {
        private const string UserDir = @"G:\Game\hwa\fonts";
        private const string SysDir = @"C:\Windows\Fonts";

        [Fact]
        public void Resolve_PutsUserFontsFirstSortedByName_ThenExistingSystemFontsInOrder()
        {
            var userFiles = new[] { UserDir + @"\b.otf", UserDir + @"\readme.txt", UserDir + @"\A.ttf" };
            var existing = new HashSet<string> { SysDir + @"\malgun.ttf", SysDir + @"\msyh.ttc" };

            var result = FallbackFontCandidates.Resolve(UserDir, SysDir, _ => userFiles, existing.Contains);

            Assert.Equal(new[]
            {
                UserDir + @"\A.ttf",
                UserDir + @"\b.otf",
                SysDir + @"\msyh.ttc",
                SysDir + @"\malgun.ttf",
            }, result);
        }

        [Fact]
        public void Resolve_IgnoresMissingOrBrokenUserFolder()
        {
            var result = FallbackFontCandidates.Resolve(UserDir, SysDir,
                _ => throw new System.IO.DirectoryNotFoundException(),
                p => p.EndsWith("msyh.ttc"));

            Assert.Equal(new[] { SysDir + @"\msyh.ttc" }, result);
        }

        [Fact]
        public void CollectNonAsciiCodePoints_DedupesAndSkipsAsciiAndWhitespace()
        {
            var result = FallbackFontCandidates.CollectNonAsciiCodePoints(new[]
            {
                "麦吉_Maggie x 盖盖Nyan",
                "\u3000한 麦",
                null,
                "\U0001F3B5",
            });

            Assert.Equal(new[] { 0x9EA6, 0x5409, 0x76D6, 0xD55C, 0x1F3B5 }, result.ToArray());
        }
    }
}
