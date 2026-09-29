using System.IO;
using GRC2.Helpers;
using GRC2.Parsers;
using GRC2.Processors;
using Xunit;

namespace GRC2.Tests
{
    /// <summary>
    /// 중복을 한 곳으로 모은 헬퍼(CharacterNames, PathHelper, NoteProcessorHelper.CalculateTimeTolerance,
    /// SongInfo.DifficultyKeys)가 정리 전 각 구현과 같은 값을 내는지 확인합니다.
    /// </summary>
    public class SharedHelperTests
    {
        [Theory]
        [InlineData("르호", "Morpho")]
        [InlineData("Morpho", "Morpho")]
        [InlineData("morpho", "Morpho")]
        [InlineData("  MORPHO  ", "Morpho")]
        [InlineData("Roro", "Roro")]
        [InlineData("roro", "Roro")]
        [InlineData("룩시아", "Luxair")]
        [InlineData("Luxair", "Luxair")]
        [InlineData("luxair", "Luxair")]
        public void CharacterNames_Normalize_MapsKnownAliases(string raw, string expected)
        {
            Assert.Equal(expected, CharacterNames.Normalize(raw));
        }

        [Fact]
        public void CharacterNames_Normalize_TrimsUnknownValuesAndKeepsCase()
        {
            Assert.Equal("Gunvolt", CharacterNames.Normalize("  Gunvolt "));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CharacterNames_Normalize_ReturnsBlankInputUnchanged(string raw)
        {
            Assert.Equal(raw, CharacterNames.Normalize(raw));
        }

        [Theory]
        [InlineData(120f, 0.05f)]
        [InlineData(240f, 0.025f)]
        [InlineData(60f, 0.10f)]
        [InlineData(1000f, 0.02f)]  // 0.006초 → 최소값 0.02초로 제한
        [InlineData(10f, 0.15f)]    // 0.6초 → 최대값 0.15초로 제한
        [InlineData(0f, 0.05f)]     // BPM이 없으면 기준 BPM(120)으로 계산
        [InlineData(-5f, 0.05f)]
        public void CalculateTimeTolerance_ScalesInverselyWithBpmAndClamps(float bpm, float expected)
        {
            Assert.Equal(expected, NoteProcessorHelper.CalculateTimeTolerance(bpm), precision: 5);
        }

        [Fact]
        public void DifficultyKeys_AreInGameDifficultyOrder()
        {
            Assert.Equal(new[] { "easy", "normal", "hard", "expert" }, SongInfo.DifficultyKeys);
        }

        [Fact]
        public void PathHelper_ReturnsNullForBlankOrInvalidPaths()
        {
            Assert.Null(PathHelper.GetFullPathOrNull(null));
            Assert.Null(PathHelper.GetFullPathOrNull("   "));
            Assert.Null(PathHelper.GetFullPathOrNull("bad\0path"));
            Assert.Null(PathHelper.GetExistingFullPathOrNull("bad\0path"));
        }

        [Fact]
        public void PathHelper_GetExistingFullPath_RequiresTheFileToExist()
        {
            string dir = Path.Combine(Path.GetTempPath(), "GRC2Tests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string missing = Path.Combine(dir, "missing.ogg");
                string present = Path.Combine(dir, "music.ogg");
                File.WriteAllBytes(present, new byte[] { 1 });

                Assert.Equal(missing, PathHelper.GetFullPathOrNull(missing)); // 없어도 절대 경로는 돌려줌
                Assert.Null(PathHelper.GetExistingFullPathOrNull(missing));
                Assert.Equal(present, PathHelper.GetExistingFullPathOrNull(present));
            }
            finally
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
