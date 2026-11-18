using System;
using System.IO;
using GRC2.Core;
using Xunit;

namespace GRC2.Tests
{
    public class AlbumFolderSignatureTests : IDisposable
    {
        private readonly string _hwa;

        public AlbumFolderSignatureTests()
        {
            _hwa = Path.Combine(Path.GetTempPath(), "grc2-sig-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_hwa, "song a"));
            File.WriteAllText(Path.Combine(_hwa, "song a", "info.txt"), "곡 제목 : a");
        }

        public void Dispose()
        {
            try { Directory.Delete(_hwa, recursive: true); } catch { }
        }

        [Fact]
        public void SameFolder_SameSignature()
        {
            Assert.Equal(AlbumManager.ComputeFolderSignature(_hwa), AlbumManager.ComputeFolderSignature(_hwa));
        }

        [Fact]
        public void AddingAlbum_ChangesSignature()
        {
            ulong before = AlbumManager.ComputeFolderSignature(_hwa);
            Directory.CreateDirectory(Path.Combine(_hwa, "song b"));
            File.WriteAllText(Path.Combine(_hwa, "song b", "hwa.bms"), "#BPM 120");
            Assert.NotEqual(before, AlbumManager.ComputeFolderSignature(_hwa));
        }

        [Fact]
        public void EditingInfoTxt_ChangesSignature()
        {
            var info = Path.Combine(_hwa, "song a", "info.txt");
            ulong before = AlbumManager.ComputeFolderSignature(_hwa);
            File.WriteAllText(info, "곡 제목 : a (수정)");
            File.SetLastWriteTimeUtc(info, DateTime.UtcNow.AddMinutes(1));
            Assert.NotEqual(before, AlbumManager.ComputeFolderSignature(_hwa));
        }

        [Fact]
        public void FontsFolder_IsIgnored()
        {
            ulong before = AlbumManager.ComputeFolderSignature(_hwa);
            Directory.CreateDirectory(Path.Combine(_hwa, "fonts"));
            File.WriteAllText(Path.Combine(_hwa, "fonts", "LICENSE.txt"), "x");
            Assert.Equal(before, AlbumManager.ComputeFolderSignature(_hwa));
        }

        [Fact]
        public void MissingFolder_ReturnsZero()
        {
            Assert.Equal(0UL, AlbumManager.ComputeFolderSignature(Path.Combine(_hwa, "none")));
        }

        [Fact]
        public void GetAlbumKey_DistinguishesHwaRootFromSubfolderNamedHwa()
        {
            Assert.Equal("root", AlbumManager.GetAlbumKey(_hwa, _hwa));
            Assert.Equal("hwa", AlbumManager.GetAlbumKey(Path.Combine(_hwa, "hwa"), _hwa));
            Assert.Equal("song a", AlbumManager.GetAlbumKey(Path.Combine(_hwa, "song a"), _hwa));
        }
    }
}
