using System.Linq;
using System.Globalization;
using GRC2.Helpers;
using Xunit;

namespace GRC2.Tests
{
    /// <summary>2026-11-26 5차 감사: 곡 정보 txt 인코딩 판별(알려진_문제 G2).</summary>
    public class TextFileDecoderTests
    {
        [Fact]
        public void Utf8WithAndWithoutBom_IsValid()
        {
            var body = System.Text.Encoding.UTF8.GetBytes("Title: 麦吉 한글");
            Assert.Equal("Title: 麦吉 한글", TextFileDecoder.Decode(body, out bool ok1));
            Assert.True(ok1);

            var withBom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(body).ToArray();
            Assert.Equal("Title: 麦吉 한글", TextFileDecoder.Decode(withBom, out bool ok2));
            Assert.True(ok2);
        }

        [Fact]
        public void Cp949Bytes_AreFlaggedNotUtf8()
        {
            // "한글"의 CP949 바이트(C7 D1 B1 DB)는 올바른 UTF-8이 아닙니다.
            var cp949 = new byte[] { 0x54, 0x3A, 0x20, 0xC7, 0xD1, 0xB1, 0xDB };
            TextFileDecoder.Decode(cp949, out bool ok);
            Assert.False(ok);
        }

        [Fact]
        public void Utf16Bom_IsDecoded()
        {
            var utf16 = System.Text.Encoding.Unicode.GetPreamble().Concat(System.Text.Encoding.Unicode.GetBytes("제목")).ToArray();
            Assert.Equal("제목", TextFileDecoder.Decode(utf16, out bool ok));
            Assert.True(ok);
        }

        [Fact]
        public void Empty_IsEmpty()
        {
            Assert.Equal(string.Empty, TextFileDecoder.Decode(new byte[0], out bool ok));
            Assert.True(ok);
        }
    }

    /// <summary>2026-11-26 5차 감사: 스팀 없이 실행할 때의 언어 이름(알려진_문제 A4b).</summary>
    public class SteamLanguageTests
    {
        [Theory]
        [InlineData("ko-KR", "koreana")]   // 예전 버그: "korean"을 돌려 게임이 영어를 골랐음
        [InlineData("ja-JP", "japanese")]
        [InlineData("zh-CN", "schinese")]
        [InlineData("zh-SG", "schinese")]
        [InlineData("zh-TW", "tchinese")]
        [InlineData("zh-HK", "tchinese")]
        [InlineData("pt-BR", "brazilian")]
        [InlineData("es-ES", "spanish")]
        [InlineData("fr-FR", "french")]
        [InlineData("de-DE", "german")]
        [InlineData("it-IT", "italian")]
        [InlineData("en-US", "english")]
        [InlineData("ru-RU", "english")]   // 게임이 모르는 언어는 영어
        public void FromCulture_ReturnsSteamApiName(string culture, string expected)
        {
            Assert.Equal(expected, SteamLanguage.FromCulture(new CultureInfo(culture)));
        }

        [Fact]
        public void FromCulture_Null_IsEnglish()
        {
            Assert.Equal("english", SteamLanguage.FromCulture(null));
        }

        [Fact]
        public void EveryReturnedName_IsKnownToGame()
        {
            foreach (var culture in CultureInfo.GetCultures(CultureTypes.AllCultures))
                Assert.Contains(SteamLanguage.FromCulture(culture), SteamLanguage.GameKnownNames);
        }
    }
}
