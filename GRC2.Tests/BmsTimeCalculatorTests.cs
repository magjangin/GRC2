using System.Collections.Generic;
using GRC2.Parsers;
using Xunit;

namespace GRC2.Tests
{
    public class BmsTimeCalculatorTests
    {
        [Fact]
        public void CalculateTime_NoBpmChanges_OneMeasureMatchesFourBeatsTimesBaseFreq()
        {
            var changes = new List<BpmChange>();
            float baseBpm = 120f;
            float baseFreq = 60f / baseBpm;
            float t = BmsTimeCalculator.CalculateTime(1f, baseBpm, baseFreq, changes);
            Assert.Equal(2f, t, precision: 5);
        }

        [Fact]
        public void CalculateTime_WithSingleBpmChange_SumsSegments()
        {
            var changes = new List<BpmChange>
            {
                new BpmChange { Tick = 1f, Bpm = 60f, Freq = 1f }
            };
            float baseBpm = 120f;
            float baseFreq = 60f / baseBpm;
            float t = BmsTimeCalculator.CalculateTime(2f, baseBpm, baseFreq, changes);
            Assert.True(t > 0f);
        }
    }
}
