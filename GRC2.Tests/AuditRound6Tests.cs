using System.Collections.Generic;
using GRC2.Core;
using GRC2.Helpers;
using Xunit;

namespace GRC2.Tests
{
    /// <summary>2026-11-26 6차 감사: BGM 단일 패스 선택(알려진_문제 P22).</summary>
    public class AlbumManagerChooseBgmFileTests
    {
        [Fact]
        public void ChooseBgmFile_EmptyOrNull_ReturnsNull()
        {
            Assert.Null(AlbumManager.ChooseBgmFile(null));
            Assert.Null(AlbumManager.ChooseBgmFile(new List<string>()));
            Assert.Null(AlbumManager.ChooseBgmFile(new[] { "" }));
        }

        [Fact]
        public void ChooseBgmFile_PrefersOggOverMp3AndWav()
        {
            var files = new[]
            {
                @"C:\songs\track_z.mp3",
                @"C:\songs\music_a.wav",
                @"C:\songs\sound_b.ogg",
                @"C:\songs\sound_a.ogg"
            };

            // ogg가 가장 우선이며, 같은 ogg 중 파일명 오름차순으로 가장 앞선 sound_a.ogg 선택
            var chosen = AlbumManager.ChooseBgmFile(files);
            Assert.Equal(@"C:\songs\sound_a.ogg", chosen);
        }

        [Fact]
        public void ChooseBgmFile_PrefersMp3OverWavWhenNoOgg()
        {
            var files = new[]
            {
                @"C:\songs\music_01.wav",
                @"C:\songs\bgm_second.mp3",
                @"C:\songs\bgm_first.mp3"
            };

            var chosen = AlbumManager.ChooseBgmFile(files);
            Assert.Equal(@"C:\songs\bgm_first.mp3", chosen);
        }

        [Fact]
        public void ChooseBgmFile_Wav_FiltersOutKeySounds_AndSelectsAllowedNames()
        {
            var keySoundsOnly = new[]
            {
                @"C:\songs\Hold end.wav",
                @"C:\songs\tap.wav",
                @"C:\songs\flick_left.wav"
            };
            Assert.Null(AlbumManager.ChooseBgmFile(keySoundsOnly));

            var withBgmWav = new[]
            {
                @"C:\songs\Hold end.wav",
                @"C:\songs\song_main.wav",
                @"C:\songs\music_theme.wav"
            };

            // music*, bgm*, song* 중 파일명 기준 오름차순 앞선 music_theme.wav 선택
            var chosen = AlbumManager.ChooseBgmFile(withBgmWav);
            Assert.Equal(@"C:\songs\music_theme.wav", chosen);
        }
    }

    /// <summary>2026-11-26 6차 감사: RecentKeys.Remove 및 LRU 관리(알려진_문제 P20).</summary>
    public class RecentKeysRemoveTests
    {
        [Fact]
        public void Remove_ExistingKey_ReturnsTrueAndDecreasesCount()
        {
            var keys = new RecentKeys(3, System.StringComparer.OrdinalIgnoreCase);
            keys.Touch("a");
            keys.Touch("b");
            Assert.Equal(2, keys.Count);

            Assert.True(keys.Remove("a"));
            Assert.Equal(1, keys.Count);
            Assert.False(keys.Remove("a"));
            Assert.False(keys.Remove(null));
        }
    }
}
