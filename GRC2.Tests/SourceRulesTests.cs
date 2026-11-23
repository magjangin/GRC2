using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace GRC2.Tests
{
    /// <summary>
    /// 모드 소스 전체에 지켜야 하는 규칙을 검사합니다. 사람이 리뷰에서 놓치기 쉬운 것만 둡니다.
    /// </summary>
    public class SourceRulesTests
    {
        // 문자열 인자 하나만 받는 오버로드는 현재 문화권으로 비교합니다(.NET Framework/Mono). 느리고(BMS·설정 파일의 줄마다 호출),
        // 문화권에 따라 결과가 달라질 수 있습니다. StringComparison.Ordinal(IgnoreCase)를 넘깁니다(2026-11-24, 알려진_문제 P14).
        private static readonly Regex CultureSensitiveCall = new Regex(
            @"\.(StartsWith|EndsWith|IndexOf|LastIndexOf)\(""(?:[^""\\]|\\.)*""\)|\bstring\.Compare\(\s*[^,()]+,\s*[^,()]+\)|\.ToLower\(\)|\.ToUpper\(\)",
            RegexOptions.Compiled);

        // ModLog.Verbose($"...")는 VerboseLog=0이면 구멍을 실행하지 않습니다(P10). 구멍에 ++/--나 대입이 있으면 동작이 설정에 따라 달라집니다.
        private static readonly Regex VerboseWithSideEffect = new Regex(
            @"ModLog\.Verbose\(\$""[^\n]*\{[^}\n]*(\+\+|--|[^=!<>]=[^=>])[^}\n]*\}",
            RegexOptions.Compiled);

        [Fact]
        public void ModSource_UsesOrdinalStringComparisons()
        {
            var violations = ScanModSource(CultureSensitiveCall);
            Assert.True(violations.Length == 0,
                "문화권 의존 문자열 비교가 있습니다. StringComparison.Ordinal(IgnoreCase)·ToLowerInvariant를 쓰세요:\n" + string.Join("\n", violations));
        }

        [Fact]
        public void VerboseLogHoles_HaveNoSideEffects()
        {
            var violations = ScanModSource(VerboseWithSideEffect);
            Assert.True(violations.Length == 0,
                "ModLog.Verbose 보간 구멍에 부작용 있는 식이 있습니다(꺼져 있으면 실행되지 않음):\n" + string.Join("\n", violations));
        }

        [Fact]
        public void Rules_CatchKnownBadPatterns()
        {
            // 규칙 정규식 자체가 동작하는지(아무것도 못 잡는 정규식이면 위 테스트가 늘 통과합니다).
            Assert.Matches(CultureSensitiveCall, "if (line.StartsWith(\"//\"))");
            Assert.Matches(CultureSensitiveCall, "var x = name.ToLower();");
            Assert.DoesNotMatch(CultureSensitiveCall, "line.StartsWith(\"//\", StringComparison.Ordinal)");
            Assert.DoesNotMatch(CultureSensitiveCall, "ext.ToLowerInvariant()");
            Assert.Matches(VerboseWithSideEffect, "ModLog.Verbose($\"n={count++}\");");
            Assert.DoesNotMatch(VerboseWithSideEffect, "ModLog.Verbose($\"n={count}, ok={a == b}\");");
        }

        /// <summary>GRC2/ 아래 .cs의 코드 줄(주석 줄 제외)에서 정규식에 걸리는 곳을 "파일:줄: 내용"으로 돌려줍니다.</summary>
        private static string[] ScanModSource(Regex rule)
        {
            var modDir = Path.Combine(FindRepoRoot(), "GRC2");
            return Directory.EnumerateFiles(modDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) &&
                            !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                .SelectMany(f => File.ReadAllLines(f).Select((line, i) => (File: f, Line: i + 1, Text: line)))
                .Where(x =>
                {
                    var trimmed = x.Text.TrimStart();
                    return !trimmed.StartsWith("//", StringComparison.Ordinal) && !trimmed.StartsWith("*", StringComparison.Ordinal);
                })
                .Where(x => rule.IsMatch(x.Text))
                .Select(x => $"{Path.GetFileName(x.File)}:{x.Line}: {x.Text.Trim()}")
                .ToArray();
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "GRC2.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }
    }
}
