using System;
using System.Collections.Generic;
using GRC2.Core;
using MelonLoader;

namespace GRC2.Helpers
{
    /// <summary>
    /// 로그 양 조절. 곡·노트·매핑마다 찍히던 상세 로그는 <see cref="Verbose"/>로 보내
    /// savecustomkey/config.txt의 <c>VerboseLog=1</c>일 때만 남깁니다(기본 꺼짐).
    /// 곡이 수백 개면 상세 로그만 수천 줄이 되고, 콘솔 출력이 시작·곡 목록 갱신을 느리게 만들기 때문입니다.
    /// 경고·오류는 항상 MelonLogger.Warning/Error로 직접 남깁니다.
    /// </summary>
    public static class ModLog
    {
        /// <summary>테스트나 설정 로드 전처럼 CustomKeySettings를 거치지 않고 켜고 끌 때 씁니다.</summary>
        internal static bool? VerboseOverride;

        public static bool IsVerbose => VerboseOverride ?? CustomKeySettings.VerboseLog;

        /// <summary>항상 남기는 일반 로그.</summary>
        public static void Info(string message) => MelonLogger.Msg(message);

        /// <summary>VerboseLog=1일 때만 남기는 상세 로그.</summary>
        public static void Verbose(string message)
        {
            if (IsVerbose)
                MelonLogger.Msg(message);
        }

        private static readonly Dictionary<string, string> _lastByKey = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// 같은 key로 마지막에 남긴 내용과 다를 때만 남깁니다. 같은 상태를 반복해서 알리는 로그
        /// (예: 곡 목록이 다시 만들어질 때마다 찍히는 "주입 완료 N개")를 한 번으로 줄입니다.
        /// </summary>
        public static void InfoOnChange(string key, string message)
        {
            if (ShouldLogOnChange(key, message))
                MelonLogger.Msg(message);
        }

        /// <summary><see cref="InfoOnChange"/>의 판정(마지막 내용 갱신 포함).</summary>
        internal static bool ShouldLogOnChange(string key, string message)
        {
            if (_lastByKey.TryGetValue(key, out var last) && last == message)
                return false;
            _lastByKey[key] = message;
            return true;
        }
    }
}
