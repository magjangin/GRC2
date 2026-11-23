using System;
using System.Runtime.CompilerServices;
using System.Text;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// C# 10 보간 문자열 처리기 표시. net472에는 이 특성이 없어 컴파일러가 찾을 수 있게 직접 둡니다(이름·네임스페이스가 계약).
    /// 런타임에는 아무 역할이 없으므로 Unity Mono에서도 그대로 동작합니다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    internal sealed class InterpolatedStringHandlerAttribute : Attribute
    {
    }
}

namespace GRC2.Helpers
{
    /// <summary>
    /// <see cref="ModLog.Verbose(ref VerboseLogHandler)"/>용 보간 문자열 처리기입니다.
    ///
    /// 예전 <c>ModLog.Verbose(string)</c>은 <c>VerboseLog=0</c>(기본)이어도 호출하는 쪽이 <c>$"..."</c> 문자열을 먼저 다 만들었습니다.
    /// 홀드 끝 노트마다(<c>HoldNoteProcessor</c>), 곡마다(<c>SongInfoParser</c>·<c>AlbumManager</c>), 곡 목록이 다시 만들어질 때마다
    /// 소수 포맷·<c>string.Join</c>이 버려지는 문자열을 만들었습니다(알려진_문제 P10). 이 처리기는 꺼져 있으면 생성자에서
    /// <c>isEnabled=false</c>를 돌려줘 컴파일러가 구멍(<c>{...}</c>) 계산과 이어 붙이기를 통째로 건너뛰게 합니다.
    ///
    /// 주의: 꺼져 있으면 구멍 안의 식이 <b>실행되지 않습니다</b>. 구멍에 부작용 있는 식(<c>{i++}</c>, 상태를 바꾸는 메서드)을 쓰지 마세요.
    /// </summary>
    [InterpolatedStringHandler]
    public struct VerboseLogHandler
    {
        private readonly StringBuilder _builder;

        public VerboseLogHandler(int literalLength, int formattedCount, out bool isEnabled)
        {
            isEnabled = ModLog.IsVerbose;
            _builder = isEnabled ? new StringBuilder(literalLength + formattedCount * 8) : null;
        }

        public bool IsEnabled => _builder != null;

        public void AppendLiteral(string value) => _builder?.Append(value);

        public void AppendFormatted(string value) => _builder?.Append(value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, 0, null);

        public void AppendFormatted<T>(T value, string format) => AppendFormatted(value, 0, format);

        public void AppendFormatted<T>(T value, int alignment) => AppendFormatted(value, alignment, null);

        /// <summary>string.Format과 같은 규칙(현재 문화권, IFormattable이면 format 적용, 음수 정렬은 왼쪽 맞춤)으로 붙입니다.</summary>
        public void AppendFormatted<T>(T value, int alignment, string format)
        {
            if (_builder == null)
                return;

            string text = value is IFormattable formattable
                ? formattable.ToString(format, null)
                : value?.ToString();
            text = text ?? string.Empty;

            int pad = Math.Abs(alignment) - text.Length;
            if (pad > 0 && alignment > 0)
                _builder.Append(' ', pad);
            _builder.Append(text);
            if (pad > 0 && alignment < 0)
                _builder.Append(' ', pad);
        }

        public override string ToString() => _builder?.ToString() ?? string.Empty;
    }
}
