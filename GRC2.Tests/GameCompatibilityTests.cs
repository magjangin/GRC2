using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using GRC2.Core;
using HarmonyLib;
using Xunit;

namespace GRC2.Tests
{
    /// <summary>
    /// 게임 업데이트 뒤 test_debug.bat만 돌려도 "어느 훅·필드가 깨졌는지" 알 수 있게 하는 계약 테스트입니다.
    /// 참조하는 Assembly-CSharp.dll이 새 게임 버전이면 여기서 먼저 실패합니다.
    /// </summary>
    public class GameCompatibilityTests
    {
        static GameCompatibilityTests()
        {
            // 게임 어셈블리는 빌드 출력에 복사되지 않으므로 설치 폴더에서 직접 찾습니다(GRC2_GAME_PATH 또는 기본 설치 경로).
            var gamePath = Environment.GetEnvironmentVariable("GRC2_GAME_PATH")
                ?? @"H:\steam\steamapps\common\GUNVOLT RECORDS Cychronicle";
            var dirs = new[]
            {
                Path.Combine(gamePath, "GUNVOLT_RECORDS_Cychronicle_Data", "Managed"),
                Path.Combine(gamePath, "MelonLoader", "net35"),
                Path.Combine(gamePath, "MelonLoader", "net6"),
            };
            AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
            {
                var name = new AssemblyName(args.Name).Name + ".dll";
                foreach (var dir in dirs)
                {
                    var path = Path.Combine(dir, name);
                    if (File.Exists(path))
                        return Assembly.LoadFrom(path);
                }
                return null;
            };
        }

        [Fact]
        public void FieldContracts_ResolveAgainstReferencedGameAssembly()
        {
            var broken = GameCompatibility.FindBrokenFieldContracts();
            Assert.True(broken.Count == 0, "깨진 게임 필드:\n" + string.Join("\n", broken));
        }

        [Fact]
        public void FieldContracts_CoverEveryNamedFieldRefInSource()
        {
            var root = FindRepoRoot();
            var modSources = Directory.GetFiles(Path.Combine(root, "GRC2"), "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Contains(@"\obj\") && !p.Contains("/obj/") && !p.EndsWith("GameCompatibility.cs"));

            var known = GameCompatibility.FieldContracts.Select(c => c.Name).ToHashSet();
            var missing = modSources
                .SelectMany(p => Regex.Matches(File.ReadAllText(p), @"FieldRefAccess<[^>]+>>?\(""(\w+)""\)").Cast<Match>())
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .Where(n => !known.Contains(n))
                .ToList();

            Assert.True(missing.Count == 0,
                "GameCompatibility.FieldContracts에 없는 FieldRefAccess 필드: " + string.Join(", ", missing));
        }

        /// <summary>
        /// 패치 대상(원본 메서드)이 게임 어셈블리에 아직 존재하는지 확인합니다. 테스트 호스트에서는 Unity 내부 호출 때문에
        /// 실제 패치를 적용할 수 없으므로, Harmony가 하는 대상 해석만 그대로 따라 합니다.
        /// </summary>
        [Fact]
        public void EveryPatchTarget_ResolvesInReferencedGameAssembly()
        {
            const BindingFlags all = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var unresolved = new System.Collections.Generic.List<string>();
            int checkedTargets = 0;

            foreach (var type in typeof(GRC2.Core.SceneDetector).Assembly.GetTypes())
            {
                var classAttrs = HarmonyInfos(type);
                var methods = type.GetMethods(all);
                bool isPatchClass = classAttrs.Count > 0 || methods.Any(m => HarmonyInfos(m).Count > 0);
                if (!isPatchClass)
                    continue;

                try
                {
                    var targetMethod = methods.FirstOrDefault(m => m.Name == "TargetMethod" && m.GetParameters().Length == 0);
                    var targetMethods = methods.FirstOrDefault(m => m.Name == "TargetMethods" && m.GetParameters().Length == 0);
                    if (targetMethod != null)
                    {
                        checkedTargets++;
                        if (targetMethod.Invoke(null, null) == null)
                            unresolved.Add($"{type.FullName}.TargetMethod() -> null");
                    }
                    else if (targetMethods != null)
                    {
                        var list = ((System.Collections.IEnumerable)targetMethods.Invoke(null, null)).Cast<object>().ToList();
                        checkedTargets += list.Count;
                        if (list.Count == 0 || list.Any(m => m == null))
                            unresolved.Add($"{type.FullName}.TargetMethods() -> 비었거나 null 포함");
                    }
                    else
                    {
                        foreach (var m in methods)
                        {
                            bool isPatchMethod = HarmonyInfos(m).Count > 0
                                || m.Name == "Prefix" || m.Name == "Postfix" || m.Name == "Finalizer" || m.Name == "Transpiler";
                            if (!isPatchMethod)
                                continue;

                            var merged = HarmonyMethod.Merge(classAttrs.Concat(HarmonyInfos(m)).ToList());
                            if (merged.declaringType == null)
                                continue;

                            checkedTargets++;
                            if (ResolveOriginal(merged) == null)
                                unresolved.Add($"{type.FullName}.{m.Name} -> {merged.declaringType.Name}.{merged.methodName} 대상 없음");
                        }
                    }
                }
                catch (Exception ex)
                {
                    unresolved.Add($"{type.FullName}: {ex.GetBaseException().Message}");
                }
            }

            Assert.True(checkedTargets > 0, "검사한 패치 대상이 하나도 없습니다(테스트가 헛돌고 있음).");
            Assert.True(unresolved.Count == 0, "게임에서 찾지 못한 패치 대상:" + Environment.NewLine + string.Join(Environment.NewLine, unresolved));
        }

        [Theory]
        [InlineData("\"AppState\"\n{\n\t\"buildid\"\t\t\"13530823\"\n}", "13530823")]
        [InlineData("\"BuildID\" \"42\"", "42")]
        [InlineData("no build here", null)]
        [InlineData("", null)]
        [InlineData(null, null)]
        public void ParseBuildId_ReadsAcfValue(string text, string expected)
        {
            Assert.Equal(expected, SteamBuildInfo.ParseBuildId(text));
        }

        private static System.Collections.Generic.List<HarmonyMethod> HarmonyInfos(MemberInfo member)
        {
            return member.GetCustomAttributes(typeof(HarmonyAttribute), false)
                .Cast<HarmonyAttribute>()
                .Select(a => a.info)
                .ToList();
        }

        private static MethodBase ResolveOriginal(HarmonyMethod info)
        {
            switch (info.methodType)
            {
                case MethodType.Getter: return AccessTools.DeclaredPropertyGetter(info.declaringType, info.methodName);
                case MethodType.Setter: return AccessTools.DeclaredPropertySetter(info.declaringType, info.methodName);
                case MethodType.Constructor: return AccessTools.DeclaredConstructor(info.declaringType, info.argumentTypes);
                default: return AccessTools.DeclaredMethod(info.declaringType, info.methodName, info.argumentTypes);
            }
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
