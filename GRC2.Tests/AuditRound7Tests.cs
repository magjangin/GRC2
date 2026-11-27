using System.Linq;
using GRC2.Core;
using GRC2.Parsers;
using Xunit;

namespace GRC2.Tests
{
    public class AuditRound7Tests
    {
        [Fact]
        public void SongInfoParser_ParsesViaStreamWithoutErrors()
        {
            var text = @"
#TITLE Test Song Title
#ARTIST Test Artist
// comment line
#LYRICS Test Lyricist // inline comment
#COMPOSER Test Composer
#EASY 3
#NORMAL 7
";
            var info = SongInfoParser.ParseText(text);

            Assert.Equal("Test Song Title", info.Title);
            Assert.Equal("Test Artist", info.Artist);
            Assert.Equal("Test Lyricist", info.Lyricist);
            Assert.Equal("Test Composer", info.Composer);
            Assert.Equal(3, info.DifficultyNumbers["easy"]);
            Assert.Equal(7, info.DifficultyNumbers["normal"]);
        }

        [Fact]
        public void SongInfoParser_HandlesVariousLineEndings()
        {
            var crlf = "title=Song1\r\nartist=Artist1\r\n";
            var lf = "title=Song2\nartist=Artist2\n";
            var cr = "title=Song3\rartist=Artist3\r";

            var info1 = SongInfoParser.ParseText(crlf);
            var info2 = SongInfoParser.ParseText(lf);
            var info3 = SongInfoParser.ParseText(cr);

            Assert.Equal("Song1", info1.Title);
            Assert.Equal("Artist1", info1.Artist);
            Assert.Equal("Song2", info2.Title);
            Assert.Equal("Artist2", info2.Artist);
            Assert.Equal("Song3", info3.Title);
            Assert.Equal("Artist3", info3.Artist);
        }

        [Fact]
        public void ConstantContracts_ContainsTurnDirectionAndNoteSize()
        {
            var turnDirectionContracts = GameCompatibility.ConstantContracts
                .Where(c => c.OwnerName == "TurnDirection")
                .ToList();

            var noteSizeContracts = GameCompatibility.ConstantContracts
                .Where(c => c.OwnerName == "NoteSize")
                .ToList();

            Assert.Equal(2, turnDirectionContracts.Count);
            Assert.Contains(turnDirectionContracts, c => c.MemberName == "Right" && c.Expected == 0);
            Assert.Contains(turnDirectionContracts, c => c.MemberName == "Left" && c.Expected == 1);

            Assert.Single(noteSizeContracts);
            Assert.Contains(noteSizeContracts, c => c.MemberName == "Scale1" && c.Expected == 0);
        }
    }
}
