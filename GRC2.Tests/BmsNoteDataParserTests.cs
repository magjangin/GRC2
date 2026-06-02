using GRC2.Parsers;
using Xunit;

namespace GRC2.Tests
{
    public class BmsNoteDataParserTests
    {
        [Fact]
        public void DetectNoteDataValueWidth_UsesThreeCharacters_WhenThreeCharacterWavKeyExists()
        {
            var lines = new[]
            {
                "#WAV001 kick.wav",
                "#WAV00A flick.wav",
                "#WAV010 fairy.wav",
                "#00111:00100A010000"
            };

            int width = BmsParser.DetectNoteDataValueWidth(lines);

            Assert.Equal(3, width);
        }

        [Fact]
        public void ParseHexData_WithThreeCharacterWidth_ReadsThreeCharacterTokens()
        {
            var values = BmsNoteDataParser.ParseHexData("00100A010000", valueWidth: 3);

            Assert.Equal(new[] { 0x001, 0x00A, 0x010, 0x000 }, values);
        }

        [Fact]
        public void ParseNoteData_WithThreeCharacterWidth_UsesThreeCharacterSlotCount()
        {
            var notes = BmsNoteDataParser.ParseNoteData(1, 11, "00100000A", valueWidth: 3);

            Assert.Equal(2, notes.Count);
            Assert.Equal(1f, notes[0].Tick);
            Assert.Equal(1f + (2f / 3f), notes[1].Tick, precision: 5);
            Assert.Equal(NoteType.Touch, notes[0].Type);
            Assert.Equal(NoteType.Flick, notes[1].Type);
            Assert.Equal(NoteDirection.LeftDown, notes[1].Direction);
        }

        [Fact]
        public void DetectNoteDataValueWidth_KeepsTwoCharacters_WhenOnlyTwoCharacterWavKeysExist()
        {
            var lines = new[]
            {
                "#WAV01 kick.wav",
                "#WAV0A flick.wav",
                "#00111:010A00"
            };

            int width = BmsParser.DetectNoteDataValueWidth(lines);

            Assert.Equal(2, width);
        }
    }
}
