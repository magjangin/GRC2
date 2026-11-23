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
        public void RecordHookFailure_KeepsEachDescriptionOnce()
        {
            // PatchAll 실패 뒤 클래스별 재적용에서 TargetMethods가 다시 불려도 같은 실패가 두 번 세어지지 않아야 합니다.
            string description = "테스트 훅 " + Guid.NewGuid().ToString("N");
            int before = GameCompatibility.HookFailures.Count;

            GameCompatibility.RecordHookFailure(description);
            GameCompatibility.RecordHookFailure(description);
            GameCompatibility.RecordHookFailure(null);

            Assert.Equal(before + 1, GameCompatibility.HookFailures.Count);
            Assert.Contains(description, GameCompatibility.HookFailures);
        }

        [Fact]
        public void FieldContracts_ResolveAgainstReferencedGameAssembly()
        {
            var broken = GameCompatibility.FindBrokenFieldContracts();
            Assert.True(broken.Count == 0, "깨진 게임 필드:\n" + string.Join("\n", broken));
        }

        [Fact]
        public void ConstantContracts_MatchExpectedGameValues()
        {
            var broken = GameCompatibility.FindBrokenConstantContracts();
            Assert.True(broken.Count == 0, "깨진 게임 상수:\n" + string.Join("\n", broken));
        }

        [Fact]
        public void CustomMusicIdRange_StartsAfterOriginalSongsAndSkipsNamedEnumValues()
        {
            Assert.Equal(GameCompatibility.TestedCustomMusicIdStart, CustomMusicIdRegistry.Start);
            Assert.Equal(54, GameCompatibility.TestedCustomMusicIdStart);
            Assert.Equal(7999, CustomMusicIdRegistry.End);
            Assert.True(CustomMusicIdRegistry.IsReserved(512));
            Assert.True(CustomMusicIdRegistry.IsReserved(0));
            Assert.True(CustomMusicIdRegistry.IsReserved(53));
            Assert.False(CustomMusicIdRegistry.IsReserved(54));
        }

        [Fact]
        public void FieldContracts_CoverEveryNamedFieldRefInSource()
        {
            var root = FindRepoRoot();
            var modSources = Directory.GetFiles(Path.Combine(root, "GRC2"), "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Contains(@"\obj\") && !p.Contains("/obj/") && !p.EndsWith("GameCompatibility.cs"));

            var known = GameCompatibility.FieldContracts.Select(c => c.Name).ToHashSet();
            var missing = modSources
                .SelectMany(p => Regex.Matches(File.ReadAllText(p), @"(?:FieldRefAccess|LazyFieldRef)<[^>]+>>?\(""(\w+)""\)").Cast<Match>())
                .Select(m => m.Groups[1].Value)
                .Distinct()
                .Where(n => !known.Contains(n))
                .ToList();

            Assert.True(missing.Count == 0,
                "GameCompatibility.FieldContracts에 없는 FieldRefAccess 필드: " + string.Join(", ", missing));
        }

        /// <summary>
        /// 모드 소스가 쓰는 게임 enum 멤버(빌드 때 숫자로 박힘)가 전부 ConstantContracts에 있는지 확인합니다.
        /// 계약 표에 없는 멤버는 게임이 값을 바꿔도 시작 로그가 알려 주지 못합니다.
        /// </summary>
        [Fact]
        public void ConstantContracts_CoverEveryGameEnumMemberInSource()
        {
            var gameEnums = new[] { "NoteTypeId", "NoteDirectionIndex", "NoteSubLaneType", "NoteLaneLeftRight", "JudgeType", "MusicID" };
            var known = GameCompatibility.ConstantContracts.Select(c => c.Description).ToHashSet();

            var root = FindRepoRoot();
            var pattern = new Regex(@"\b(" + string.Join("|", gameEnums) + @")\.([A-Za-z_]\w*)\b");
            var missing = Directory.GetFiles(Path.Combine(root, "GRC2"), "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Contains(@"\obj\") && !p.Contains("/obj/") && !p.EndsWith("GameCompatibility.cs"))
                .SelectMany(p => pattern.Matches(StripComments(File.ReadAllText(p))).Cast<Match>())
                .Select(m => m.Groups[1].Value + "." + m.Groups[2].Value)
                .Distinct()
                .Where(n => !known.Contains(n))
                .ToList();

            Assert.True(missing.Count == 0,
                "GameCompatibility.ConstantContracts에 없는 게임 enum 멤버: " + string.Join(", ", missing));
        }

        [Fact]
        public void RuntimeLookups_ReadLoadedAssemblyAndReportMissingNames()
        {
            var musicId = Assembly.Load("Assembly-CSharp").GetType("IntiCreates.soRythmGameMusicDataMap+MusicID");
            Assert.Equal(8000, GameCompatibility.RuntimeEnumValue(musicId, "UNIQUE_TOP"));
            Assert.Null(GameCompatibility.RuntimeEnumValue(musicId, "NO_SUCH_MEMBER"));
            Assert.Equal(-7, GameCompatibility.RuntimeEnumValueOr(musicId, "NO_SUCH_MEMBER", -7));

            var beat = Assembly.Load("Assembly-CSharp").GetType("IntiCreates.cBGMBeatManager");
            Assert.Equal(48000, GameCompatibility.RuntimeConstValue(beat, "defaultFrequency"));
            Assert.Null(GameCompatibility.RuntimeConstValue(beat, "noSuchField"));

            var missing = new GameCompatibility.ConstantContract("X", "Y", 1, () => null);
            Assert.True(missing.IsBroken);
            Assert.Contains("현재 없음", missing.ToString());
        }

        private static string StripComments(string source)
        {
            source = Regex.Replace(source, @"/\*.*?\*/", "", RegexOptions.Singleline);
            return Regex.Replace(source, @"//[^\n]*", "");
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

        [Fact]
        public void CreditGetters_AllResolveOnMusicData()
        {
            var musicDataType = Assembly.Load("Assembly-CSharp").GetType("IntiCreates.soRythmGameMusicDataMap+MusicData");
            Assert.NotNull(musicDataType);
            var missing = GRC2.Harmony.CreditTextPatch.CreditByGetter.Keys
                .Where(n => AccessTools.Method(musicDataType, n) == null)
                .ToList();
            Assert.True(missing.Count == 0, "MusicData에 없는 크레딧 게터: " + string.Join(", ", missing));
        }

        [Fact]
        public void TmpSettings_FallbackFontAssetsFieldResolves()
        {
            var tmpSettingsType = Assembly.Load("Unity.TextMeshPro").GetType("TMPro.TMP_Settings");
            Assert.NotNull(tmpSettingsType);
            var field = AccessTools.Field(tmpSettingsType, "m_fallbackFontAssets");
            Assert.NotNull(field);
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

        /// <summary>
        /// 패치 메서드의 매개변수(__instance/__result/__state/___필드 제외)가 대상 메서드의 같은 이름·같은 타입 매개변수와 맞는지 확인합니다.
        /// 이름이 다르면 Harmony가 게임 안에서 패치 적용 시 예외를 냅니다.
        /// </summary>
        [Fact]
        public void PatchMethodParameters_MatchTargetMethodSignatures()
        {
            const BindingFlags all = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var problems = new System.Collections.Generic.List<string>();
            var patchNames = new[] { "Prefix", "Postfix", "Finalizer", "Transpiler" };
            int checkedParams = 0;

            foreach (var type in typeof(GRC2.Core.SceneDetector).Assembly.GetTypes())
            {
                var classAttrs = HarmonyInfos(type);
                var methods = type.GetMethods(all);
                if (classAttrs.Count == 0 && !methods.Any(m => HarmonyInfos(m).Count > 0))
                    continue;
                if (methods.Any(m => m.Name == "TargetMethod" || m.Name == "TargetMethods"))
                    continue;

                foreach (var m in methods)
                {
                    bool byAttr = m.GetCustomAttributes(typeof(HarmonyPrefix), false).Length > 0
                        || m.GetCustomAttributes(typeof(HarmonyPostfix), false).Length > 0
                        || m.GetCustomAttributes(typeof(HarmonyFinalizer), false).Length > 0
                        || m.GetCustomAttributes(typeof(HarmonyTranspiler), false).Length > 0;
                    if (!byAttr && !patchNames.Contains(m.Name))
                        continue;

                    var merged = HarmonyMethod.Merge(classAttrs.Concat(HarmonyInfos(m)).ToList());
                    if (merged.declaringType == null)
                        continue;
                    var target = ResolveOriginal(merged);
                    if (target == null)
                        continue;

                    var targetParams = target.GetParameters();
                    foreach (var p in m.GetParameters())
                    {
                        var name = p.Name;
                        checkedParams++;
                        if (name == "__instance" || name == "__result" || name == "__state" || name == "__originalMethod"
                            || name == "__runOriginal" || name.StartsWith("___") || name == "__args")
                            continue;
                        if (name.StartsWith("__") && int.TryParse(name.Substring(2), out var idx))
                        {
                            if (idx >= targetParams.Length)
                                problems.Add($"{type.Name}.{m.Name}: {name} 인덱스가 대상 매개변수 수({targetParams.Length})를 넘음");
                            continue;
                        }

                        var tp = targetParams.FirstOrDefault(x => x.Name == name);
                        if (tp == null)
                            problems.Add($"{type.Name}.{m.Name}: 매개변수 '{name}'이(가) {target.DeclaringType?.Name}.{target.Name}에 없음 (실제: {string.Join(", ", targetParams.Select(x => x.Name))})");
                        else if (tp.ParameterType.IsByRef ? tp.ParameterType.GetElementType() != (p.ParameterType.IsByRef ? p.ParameterType.GetElementType() : p.ParameterType)
                                                          : tp.ParameterType != (p.ParameterType.IsByRef ? p.ParameterType.GetElementType() : p.ParameterType))
                            problems.Add($"{type.Name}.{m.Name}: 매개변수 '{name}' 타입 {p.ParameterType.Name} != 대상 {tp.ParameterType.Name}");
                    }

                    // __result 타입 확인
                    var resultParam = m.GetParameters().FirstOrDefault(x => x.Name == "__result");
                    if (resultParam != null && target is MethodInfo mi)
                    {
                        var rt = resultParam.ParameterType.IsByRef ? resultParam.ParameterType.GetElementType() : resultParam.ParameterType;
                        if (rt != mi.ReturnType)
                            problems.Add($"{type.Name}.{m.Name}: __result 타입 {rt.Name} != 대상 반환형 {mi.ReturnType.Name}");
                    }

                    // ___필드 확인
                    foreach (var fp in m.GetParameters().Where(x => x.Name.StartsWith("___")))
                    {
                        var fieldName = fp.Name.Substring(3);
                        if (AccessTools.Field(target.DeclaringType, fieldName) == null)
                            problems.Add($"{type.Name}.{m.Name}: 필드 '{fieldName}'을(를) {target.DeclaringType?.Name}에서 찾지 못함");
                    }
                }
            }

            Assert.True(checkedParams > 10, "검사한 패치 매개변수가 너무 적습니다: " + checkedParams);
            Assert.True(problems.Count == 0, "패치 매개변수 불일치:" + Environment.NewLine + string.Join(Environment.NewLine, problems));
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
