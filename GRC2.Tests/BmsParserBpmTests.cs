using System.Globalization;
using System.Threading;
using GRC2.Parsers;
using Xunit;

namespace GRC2.Tests
{
    public class BmsParserBpmTests
    {
        [Theory]
        [InlineData("124", 124f)]
        [InlineData("150.5", 150.5f)]
        [InlineData("0.5", 0.5f)]
        [InlineData("200.", 200f)]
        public void TryParseBpm_ReadsPlainNumbers(string text, float expected)
        {
            Assert.True(BmsParser.TryParseBpm(text, out float bpm));
            Assert.Equal(expected, bpm, precision: 4);
        }

        [Theory]
        [InlineData("")]
        [InlineData(".")]
        [InlineData("1.2.3")]   // 정규식 [0-9.]+는 통과시키지만 숫자가 아님
        [InlineData("0")]       // BPM 0이면 박 길이가 무한대가 됨
        [InlineData("0.0")]
        [InlineData("-5")]
        [InlineData("abc")]
        [InlineData("1e999")]   // 오버플로 → 무한대
        [InlineData(null)]
        public void TryParseBpm_RejectsValuesThatWouldBreakTiming(string text)
        {
            Assert.False(BmsParser.TryParseBpm(text, out _));
        }

        [Theory]
        [InlineData("de-DE")]   // 소수점이 쉼표인 문화권
        [InlineData("fr-FR")]
        [InlineData("ko-KR")]
        public void TryParseBpm_IgnoresTheCurrentCulture(string cultureName)
        {
            CultureInfo original = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);

                Assert.True(BmsParser.TryParseBpm("150.5", out float bpm));
                Assert.Equal(150.5f, bpm, precision: 4);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = original;
            }
        }
    }
}
