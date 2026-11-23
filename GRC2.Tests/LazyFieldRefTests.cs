using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using GRC2.Helpers;
using Xunit;

namespace GRC2.Tests
{
    /// <summary>
    /// 게임 필드 접근이 정적 초기화에서 던지지 않도록 바꾼 LazyFieldRef를 확인합니다.
    /// (필드를 못 찾는 경로는 MelonLogger를 쓰므로 테스트 호스트에서는 확인하지 않습니다.)
    /// </summary>
    public class LazyFieldRefTests
    {
        private class Sample
        {
#pragma warning disable 0649
            private int mValue;
#pragma warning restore 0649
            public int Value => mValue;
        }

        [Fact]
        public void ReadsAndWritesPrivateField()
        {
            var fieldRef = new LazyFieldRef<Sample, int>("mValue");
            var sample = new Sample();

            Assert.True(fieldRef.IsAvailable);
            fieldRef[sample] = 42;
            Assert.Equal(42, sample.Value);
            Assert.Equal(42, fieldRef[sample]);
        }

        /// <summary>
        /// static readonly 필드 초기화에서 FieldRefAccess를 바로 부르면, 필드 하나가 사라졌을 때
        /// 그 클래스의 정적 초기화가 던져 같은 클래스의 다른 기능까지 멈춥니다. 새 코드가 그 방식을 다시 쓰지 않는지 봅니다.
        /// </summary>
        [Fact]
        public void ModSourceDoesNotResolveGameFieldsInStaticInitializers()
        {
            var dir = new DirectoryInfo(System.AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "GRC2.sln")))
                dir = dir.Parent;
            Assert.NotNull(dir);

            var offenders = Directory.GetFiles(Path.Combine(dir.FullName, "GRC2"), "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Contains(@"\obj\") && !p.Contains("/obj/"))
                .Where(p => Regex.IsMatch(File.ReadAllText(p), @"static\s+readonly\s+AccessTools\.FieldRef<"))
                .Select(Path.GetFileName)
                .ToList();

            Assert.True(offenders.Count == 0,
                "static readonly AccessTools.FieldRef 대신 LazyFieldRef를 쓰세요: " + string.Join(", ", offenders));
        }
    }
}
