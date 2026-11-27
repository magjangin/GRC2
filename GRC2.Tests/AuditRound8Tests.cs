using System.Linq;
using GRC2.Core;
using GRC2.Parsers;
using Xunit;

namespace GRC2.Tests
{
    public class AuditRound8Tests
    {
        static AuditRound8Tests()
        {
            TestAssemblyResolver.Ensure();
        }
        [Fact]
        public void BmsParser_ParseBmsText_ParsesHeaderAndNotesCorrectly()
        {
            var bmsText = @"
#TITLE Test BMS
#BPM 150
#00111:01000100
#00116:02000000
#00216:19000000
";
            var notes = BmsParser.ParseBmsText(bmsText, "test_bms", printSummary: false);

            Assert.NotEmpty(notes);
            // 00111: 01 (Touch) 2개
            // 00116: 02 (Hold 시작) 1개
            // 00216: 19 (Hold 끝) 1개
            var holdNote = notes.FirstOrDefault(n => n.Type == NoteType.Hold);
            Assert.NotNull(holdNote);
            Assert.True(holdNote.Duration > 0f);
            Assert.NotNull(holdNote.EndNote);
        }

        [Fact]
        public void BmsNoteDataParser_ParseHexData_PadsOddLengthTokensSafely()
        {
            // valueWidth=2인데 5글자: "01023" -> "01", "02", "30" (3이 상위 4비트로 패딩되어 0x30 = 48)
            var values = BmsNoteDataParser.ParseHexData("01023", valueWidth: 2);

            Assert.Equal(3, values.Count);
            Assert.Equal(0x01, values[0]);
            Assert.Equal(0x02, values[1]);
            Assert.Equal(0x30, values[2]);
        }

        [Fact]
        public void FieldContracts_ContainsPlayerMusicPlayDataKeyFields()
        {
            var saveFieldContracts = GameCompatibility.FieldContracts
                .Where(c => c.Owner.Name == "PlayerMusicPlayData")
                .Select(c => c.Name)
                .ToList();

            Assert.Contains("highScoreArray", saveFieldContracts);
            Assert.Contains("maxComboArray", saveFieldContracts);
            Assert.Contains("playCountArray", saveFieldContracts);
            Assert.Contains("playFlagArray", saveFieldContracts);
            Assert.Contains("isNew", saveFieldContracts);
        }
    }
}
