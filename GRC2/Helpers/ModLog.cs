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

        /// <summary>VerboseLog=1일 때만 남기는 상세 로그(보간 없는 고정 문자열용).</summary>
        public static void Verbose(string message)
        {
            if (IsVerbose)
                MelonLogger.Msg(message);
        }

        /// <summary>
        /// VerboseLog=1일 때만 남기는 상세 로그. <c>ModLog.Verbose($"...{x}...")</c>는 이 오버로드로 묶여,
        /// 꺼져 있으면 문자열을 아예 만들지 않습니다(<see cref="VerboseLogHandler"/>, 알려진_문제 P10).
        /// </summary>
        public static void Verbose(ref VerboseLogHandler message)
        {
            if (message.IsEnabled)
                MelonLogger.Msg(message.ToString());
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

        private static readonly Dictionary<string, int> _warningCountByKey = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>
        /// 매 프레임·매 판정·매 텍스트 설정처럼 자주 불리는 경로의 경고. 같은 key의 1·10·100·1000…번째만 남기고
        /// 나머지는 셉니다(2026-11-25, 알려진_문제 P16). 예전에는 OnGUI(프레임당 여러 번)나 <c>set_text</c> 훅에서
        /// 같은 예외가 반복되면 경고가 초당 수백 줄 찍혀, 콘솔 출력 자체가 게임을 느리게 만들었습니다.
        /// </summary>
        public static void WarningThrottled(string key, string message)
        {
            if (ShouldLogThrottled(key, out int count))
                MelonLogger.Warning(count == 1 ? message : $"{message} (같은 경고 {count}회째, 사이 생략)");
        }

        /// <summary><see cref="WarningThrottled"/>의 판정. 횟수를 올리고, 1 또는 10의 거듭제곱 번째면 true.</summary>
        internal static bool ShouldLogThrottled(string key, out int count)
        {
            _warningCountByKey.TryGetValue(key, out count);
            if (count < int.MaxValue)
                count++;
            _warningCountByKey[key] = count;

            int n = count;
            while (n >= 10 && n % 10 == 0)
                n /= 10;
            return n == 1;
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
