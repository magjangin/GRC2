using GRC2.Core;
using Xunit;

namespace GRC2.Tests
{
    public class CustomKeySettingsTests
    {
        [Theory]
        [InlineData("true", true)]
        [InlineData("트루", true)]
        [InlineData("참", true)]
        [InlineData("켜기", true)]
        [InlineData("켜짐", true)]
        [InlineData("활성화", true)]
        [InlineData("on", true)]
        [InlineData("ON", true)]
        [InlineData("1", true)]
        [InlineData("enable", true)]
        [InlineData("ENABLED", true)]
        [InlineData("y", true)]
        [InlineData("yes", true)]
        [InlineData("false", false)]
        [InlineData("폴스", false)]
        [InlineData("거짓", false)]
        [InlineData("비활성화", false)]
        [InlineData("끄기", false)]
        [InlineData("꺼짐", false)]
        [InlineData("off", false)]
        [InlineData("OFF", false)]
        [InlineData("0", false)]
        [InlineData("disable", false)]
        [InlineData("DISABLED", false)]
        [InlineData("n", false)]
        [InlineData("no", false)]
        public void ParseBool_SupportsExtendedKeywords(string valueRaw, bool expected)
        {
            // Act
            bool result = CustomKeySettings.ParseBool(valueRaw, fallback: !expected);

            // Assert
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("모름", true)]
        [InlineData("maybe", false)]
        [InlineData("2", true)]
        public void ParseBool_UnknownValueReturnsFallback(string valueRaw, bool fallback)
        {
            Assert.Equal(fallback, CustomKeySettings.ParseBool(valueRaw, fallback));
        }
    }
}
