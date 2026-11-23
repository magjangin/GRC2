using System.Globalization;
using System.Threading;
using GRC2.Helpers;
using Xunit;

namespace GRC2.Tests
{
    /// <summary>ModLog.VerboseOverride는 전역이라 다른 테스트와 겹치지 않게 혼자 돌립니다.</summary>
    [CollectionDefinition("ModLog 전역 상태", DisableParallelization = true)]
    public class ModLogCollection
    {
    }

    /// <summary>
    /// 상세 로그가 꺼져 있으면 보간 문자열을 만들지 않는지(알려진_문제 P6), 켜져 있으면 string.Format과 같은 결과인지 확인합니다.
    /// </summary>
    [Collection("ModLog 전역 상태")]
    public class ModLogTests
    {
        private int _evaluated;

        private int Touch()
        {
            _evaluated++;
            return 42;
        }

        [Fact]
        public void Verbose_WhenOff_DoesNotEvaluateInterpolationHoles()
        {
            ModLog.VerboseOverride = false;
            try
            {
                // string 오버로드로 묶였다면 Touch()가 먼저 실행됩니다. 처리기 오버로드로 묶여야 0번입니다.
                ModLog.Verbose($"[테스트] 값 {Touch()} / {Touch():F3}");
                Assert.Equal(0, _evaluated);
            }
            finally
            {
                ModLog.VerboseOverride = null;
            }
        }

        [Fact]
        public void Handler_WhenOn_FormatsLikeStringFormat()
        {
            var previousCulture = Thread.CurrentThread.CurrentCulture;
            ModLog.VerboseOverride = true;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                double time = 1.23456;
                string name = null;
                int lane = 3;

                VerboseLogHandler handler = $"t={time:F3}, n={name}, lane={lane,4}|{lane,-3}|{Touch()}";

                Assert.True(handler.IsEnabled);
                Assert.Equal(string.Format("t={0:F3}, n={1}, lane={2,4}|{2,-3}|{3}", time, name, lane, 42), handler.ToString());
                Assert.Equal(1, _evaluated);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previousCulture;
                ModLog.VerboseOverride = null;
            }
        }

        [Fact]
        public void ShouldLogOnChange_OnlyWhenMessageDiffers()
        {
            var key = System.Guid.NewGuid().ToString();
            Assert.True(ModLog.ShouldLogOnChange(key, "5개"));
            Assert.False(ModLog.ShouldLogOnChange(key, "5개"));
            Assert.True(ModLog.ShouldLogOnChange(key, "6개"));
        }

        [Fact]
        public void Handler_WhenOff_IsEmpty()
        {
            ModLog.VerboseOverride = false;
            try
            {
                VerboseLogHandler handler = $"x={Touch()}";
                Assert.False(handler.IsEnabled);
                Assert.Equal(string.Empty, handler.ToString());
                Assert.Equal(0, _evaluated);
            }
            finally
            {
                ModLog.VerboseOverride = null;
            }
        }
    }
}
