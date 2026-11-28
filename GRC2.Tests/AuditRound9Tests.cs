using System;
using System.Linq;
using GRC2.Core;
using GRC2.Parsers;
using Xunit;

namespace GRC2.Tests
{
    public class AuditRound9Tests
    {
        static AuditRound9Tests()
        {
            TestAssemblyResolver.Ensure();
        }

        [Fact]
        public void BmsParser_MeasureLength_AppliesScaleCorrectly()
        {
            // 000 마디 길이: 0.75 (3/4 박자)
            // 001 마디 첫 박에 터치 노트 (#00111:01)
            var bmsText = @"
#TITLE Test Measure Length
#BPM 120
#00002:0.75
#00111:01000000
";
            var notes = BmsParser.ParseBmsText(bmsText, "test_measure_length");

            Assert.Single(notes);
            var note = notes[0];
            // 0번 마디 시작: 0.0, 길이 0.75 -> 1번 마디 시작 Tick은 0.75!
            Assert.Equal(0.75f, note.Tick, 3);
        }

        [Fact]
        public void BmsParser_OutOrderMeasures_SortedCorrectly()
        {
            // 마디 002가 먼저 나오고 001이 뒤에 나옴
            var bmsText = @"
#TITLE Test Out Order
#BPM 120
#00211:01000000
#00111:01000000
";
            var notes = BmsParser.ParseBmsText(bmsText, "test_out_order");

            Assert.Equal(2, notes.Count);
            // Tick 순서대로 정렬되어 있어야 함 (1.0f가 2.0f보다 앞섬)
            Assert.True(notes[0].Tick < notes[1].Tick);
            Assert.Equal(1.0f, notes[0].Tick, 3);
            Assert.Equal(2.0f, notes[1].Tick, 3);
        }

        [Fact]
        public void FieldContracts_ContainsMusicDataKeyFields()
        {
            var musicDataContracts = GameCompatibility.FieldContracts
                .Where(c => c.Owner.Name == "MusicData")
                .Select(c => c.Name)
                .ToList();

            Assert.Contains("id", musicDataContracts);
            Assert.Contains("charactorID", musicDataContracts);
            Assert.Contains("movieClipFileName", musicDataContracts);
        }
    }
}
