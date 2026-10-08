using System.Collections.Generic;
using GRC2.Parsers;
using GRC2.Processors;
using Xunit;

namespace GRC2.Tests
{
    public class HoldNoteProcessorTests
    {
        [Fact]
        public void MatchHoldNotes_OneEndNeverServesTwoHolds()
        {
            // 같은 레인의 홀드 두 개가 끝 하나를 나눠 쓰면 두 홀드가 겹칩니다(E3).
            // 이른 시작이 끝을 갖고, 나중 시작은 짝이 없어야 합니다.
            var firstStart = NewNote(NoteType.Hold, channel: 16, tick: 1f);
            var secondStart = NewNote(NoteType.Hold, channel: 16, tick: 1.5f);
            var end = NewNote(NoteType.HoldEnd, channel: 16, tick: 3f);
            var notes = new List<BmsNote> { secondStart, firstStart, end };

            HoldNoteProcessor.MatchHoldNotes(notes);

            Assert.Same(end, firstStart.EndNote);
            Assert.Equal(2f, firstStart.Duration, precision: 5);
            Assert.Null(secondStart.EndNote);
            Assert.Equal(0f, secondStart.Duration);
        }

        private static BmsNote NewNote(NoteType type, int channel, float tick)
        {
            return new BmsNote { Type = type, Channel = channel, Tick = tick, Lane = 0, IsLeft = true };
        }
    }
}
