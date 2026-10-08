using System.Text;
using GRC2.Parsers;
using Xunit;

namespace GRC2.Tests
{
    /// <summary>
    /// info.txt의 크레딧 줄(아티스트·작사·작곡·편곡·채보·수록 CD·원작 게임)이 곡 정보의 어느 칸으로 가는지 확인합니다.
    /// ParseTxtFile은 MelonLogger로 로그를 남겨 테스트에서 부를 수 없으므로, 줄 하나를 처리하는 TryApplyCreditLine을 봅니다.
    /// </summary>
    public class SongInfoParserTests
    {
        [Theory]
        [InlineData("아티스트 : 화영왕", "Artist", "화영왕")]
        [InlineData("artist=Someone", "Artist", "Someone")]
        [InlineData("작사 : 홍길동", "Lyricist", "홍길동")]
        [InlineData("작사가=홍길동", "Lyricist", "홍길동")]
        [InlineData("Lyricist: Someone", "Lyricist", "Someone")]
        [InlineData("作詞：誰か", "Lyricist", "誰か")]
        [InlineData("작곡 : 홍길동", "Composer", "홍길동")]
        [InlineData("작곡가 : 홍길동", "Composer", "홍길동")]
        [InlineData("#composer = Someone", "Composer", "Someone")]
        [InlineData("편곡 : 홍길동", "Arranger", "홍길동")]
        [InlineData("ARRANGER: Someone", "Arranger", "Someone")]
        [InlineData("채보 : 화영왕", "Charter", "화영왕")]
        [InlineData("채보자 화영왕", "Charter", "화영왕")]
        [InlineData("譜面制作：誰か", "Charter", "誰か")]
        [InlineData("수록 CD : 1st Album", "CdTitle", "1st Album")]
        [InlineData("CD: Single", "CdTitle", "Single")]
        [InlineData("CD title : Single", "CdTitle", "Single")]       // "cd"만 맞으면 값이 "title : Single"이 됩니다.
        [InlineData("원작 게임 : GUNVOLT 3", "GameTitle", "GUNVOLT 3")] // "원작"만 맞으면 값이 "게임 : …"이 됩니다.
        [InlineData("원작 : 건볼트", "GameTitle", "건볼트")]
        [InlineData("Game = GUNVOLT", "GameTitle", "GUNVOLT")]
        [InlineData("Game Title = GUNVOLT", "GameTitle", "GUNVOLT")]
        public void TryApplyCreditLine_PutsValueInMatchingField(string line, string property, string expected)
        {
            var info = new SongInfo();

            Assert.True(SongInfoParser.TryApplyCreditLine(line, info, out _, out string value));
            Assert.Equal(expected, value);
            Assert.Equal(expected, typeof(SongInfo).GetProperty(property).GetValue(info));
        }

        [Fact]
        public void TryApplyCreditLine_ComposerKeyNoLongerFillsArtist()
        {
            // 예전에는 작곡가/composer를 아티스트로 읽었습니다. 이제 작곡 칸이 따로 있습니다.
            var info = new SongInfo { Artist = "화영왕" };

            Assert.True(SongInfoParser.TryApplyCreditLine("작곡가 : 누군가", info, out string label, out _));
            Assert.Equal("작곡", label);
            Assert.Equal("누군가", info.Composer);
            Assert.Equal("화영왕", info.Artist);
        }

        [Theory]
        [InlineData("곡 제목 : shut up")]
        [InlineData("난이도 : easy, normal, hard, expert")]
        [InlineData("캐릭터 : 르호")]
        [InlineData("artistid : Roro")]   // 캐릭터 키입니다. 아티스트로 읽으면 안 됩니다.
        [InlineData("easy = 5")]
        [InlineData("작사")]              // 값이 없으면 크레딧 줄이 아닙니다.
        [InlineData("")]
        [InlineData(null)]
        public void TryApplyCreditLine_IgnoresOtherLines(string line)
        {
            var info = new SongInfo();

            Assert.False(SongInfoParser.TryApplyCreditLine(line, info, out _, out _));
            Assert.Equal("", info.Artist);
            Assert.Equal("", info.Lyricist);
            Assert.Equal("", info.Composer);
            Assert.Equal("", info.GameTitle);
        }

        [Fact]
        public void TryApplyCreditLine_NormalizesDecomposedHangul()
        {
            // macOS에서 저장한 파일은 한글이 자모로 풀려(NFD) 있을 수 있습니다. 제목처럼 NFC로 모아 둡니다.
            var info = new SongInfo();
            string decomposed = "홍길동".Normalize(NormalizationForm.FormD);

            Assert.True(SongInfoParser.TryApplyCreditLine("작사 : " + decomposed, info, out _, out _));
            Assert.Equal("홍길동", info.Lyricist);
        }
    }
}
