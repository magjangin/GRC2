using System;
using System.Collections.Generic;
using System.Linq;
using GRC2.Helpers;
using Xunit;

namespace GRC2.Tests
{
    public class CustomMusicIdAllocatorTests
    {
        private static readonly Func<int, bool> Reserve512 = v => v == 512;

        private static Dictionary<string, int> Map(params (string key, int id)[] items)
            => items.ToDictionary(i => i.key, i => i.id, StringComparer.OrdinalIgnoreCase);

        [Fact]
        public void FirstRun_MatchesLegacyNameOrderFrom54()
        {
            // 맵 파일이 없을 때는 예전 방식(이름순 54부터)과 같아야 기존 기록이 이어집니다.
            var r = CustomMusicIdAllocator.Allocate(new[] { "zero two", "Apsana", "shut up" }, Map(), 54, 7999, Reserve512);

            Assert.Equal(54, r.Assigned["Apsana"]);
            Assert.Equal(55, r.Assigned["shut up"]);
            Assert.Equal(56, r.Assigned["zero two"]);
            Assert.True(r.Changed);
        }

        [Fact]
        public void AddingFolder_KeepsExistingIds()
        {
            var existing = Map(("Apsana", 54), ("shut up", 55), ("zero two", 56));

            // "B"는 이름순으로 중간에 오지만 기존 곡의 ID를 밀어내지 않고 빈 번호(57)를 받습니다.
            var r = CustomMusicIdAllocator.Allocate(new[] { "Apsana", "B", "shut up", "zero two" }, existing, 54, 7999, Reserve512);

            Assert.Equal(55, r.Assigned["shut up"]);
            Assert.Equal(56, r.Assigned["zero two"]);
            Assert.Equal(57, r.Assigned["B"]);
        }

        [Fact]
        public void RemovedFolder_KeepsReservationSoNoOneInheritsItsRecords()
        {
            var existing = Map(("Apsana", 54), ("shut up", 55));

            var r = CustomMusicIdAllocator.Allocate(new[] { "shut up", "new" }, existing, 54, 7999, Reserve512);

            Assert.Equal(56, r.Assigned["new"]);
            Assert.False(r.Assigned.ContainsKey("Apsana"));
            Assert.Equal(54, r.FullMap["Apsana"]);
        }

        [Fact]
        public void Unchanged_DoesNotRequestRewrite()
        {
            var existing = Map(("a", 54), ("b", 55));
            var r = CustomMusicIdAllocator.Allocate(new[] { "b", "a" }, existing, 54, 7999, Reserve512);
            Assert.False(r.Changed);
        }

        [Fact]
        public void SkipsReservedValues_AndGoesPastOldLimit511()
        {
            var keys = Enumerable.Range(0, 460).Select(i => $"song{i:D4}").ToList();
            var r = CustomMusicIdAllocator.Allocate(keys, Map(), 54, 7999, Reserve512);

            Assert.Empty(r.Skipped);
            Assert.DoesNotContain(512, r.Assigned.Values);
            Assert.Equal(511, r.Assigned["song0457"]);
            Assert.Equal(513, r.Assigned["song0458"]); // 512(SAVEABLE_ID_END)는 건너뜀
        }

        [Fact]
        public void ReportsSkippedWhenRangeIsFull()
        {
            var r = CustomMusicIdAllocator.Allocate(new[] { "a", "b", "c" }, Map(), 54, 55, Reserve512);
            Assert.Equal(new[] { "c" }, r.Skipped);
        }

        [Fact]
        public void DropsInvalidOrDuplicateEntries()
        {
            var existing = Map(("a", 54), ("b", 54), ("c", 512), ("d", 9000));
            var r = CustomMusicIdAllocator.Allocate(new[] { "a", "b", "c", "d" }, existing, 54, 7999, Reserve512);

            Assert.Equal(54, r.Assigned["a"]);
            Assert.Equal(new[] { 54, 55, 56, 57 }, r.Assigned.Values.OrderBy(v => v).ToArray());
            Assert.True(r.Changed);
        }

        [Fact]
        public void ParseAndFormat_RoundTrip_KeyMayContainEquals()
        {
            var map = Map(("a=b", 60), ("한글 폴더", 54));
            var parsed = CustomMusicIdAllocator.Parse(CustomMusicIdAllocator.Format(map));

            Assert.Equal(60, parsed["a=b"]);
            Assert.Equal(54, parsed["한글 폴더"]);
            Assert.Equal(2, parsed.Count);
        }
    }

    public class ModLogTests
    {
        [Fact]
        public void ShouldLogOnChange_OnlyWhenMessageDiffers()
        {
            var key = Guid.NewGuid().ToString();
            Assert.True(ModLog.ShouldLogOnChange(key, "5개"));
            Assert.False(ModLog.ShouldLogOnChange(key, "5개"));
            Assert.True(ModLog.ShouldLogOnChange(key, "6개"));
        }
    }
}
