using System.IO;
using System.Text;

namespace GRC2.Helpers
{
    /// <summary>
    /// 곡 정보 txt를 읽습니다. BOM이 있으면 그 인코딩(UTF-8/UTF-16), 없으면 UTF-8입니다.
    ///
    /// 게임 폴더(Managed)에는 <c>I18N.CJK.dll</c>이 없어 CP949(한국어 ANSI)·Shift-JIS를 런타임에서 읽을 수 없습니다(2026-11-26 실측).
    /// 그래서 그런 파일은 고칠 수 없고, 대신 <see cref="IsValidUtf8"/>가 false가 되어 호출하는 쪽이 "UTF-8로 다시 저장하라"고
    /// 알릴 수 있게 합니다(알려진_문제 G2). 예전에는 제목이 � 투성이가 돼도 아무 로그가 없었습니다.
    /// </summary>
    public static class TextFileDecoder
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, throwOnInvalidBytes: true);

        public static string Decode(byte[] bytes, out bool isValidUtf8)
        {
            isValidUtf8 = true;
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            // UTF-16 BOM이면 StreamReader의 BOM 판별을 그대로 씁니다(UTF-8 검사 대상 아님).
            if (bytes.Length >= 2 && ((bytes[0] == 0xFF && bytes[1] == 0xFE) || (bytes[0] == 0xFE && bytes[1] == 0xFF)))
            {
                using (var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, true))
                    return reader.ReadToEnd();
            }

            int offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            try
            {
                return StrictUtf8.GetString(bytes, offset, bytes.Length - offset);
            }
            catch (DecoderFallbackException)
            {
                isValidUtf8 = false;
                return Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
            }
        }
    }
}
