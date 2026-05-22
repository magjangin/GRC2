using System;
using System.Collections.Generic;
using System.Linq;
using GRC2.Parsers;

namespace GRC2.Core
{
    public static partial class AlbumManager
    {
        /// <summary>
        /// 현재 선택된 앨범의 BMS 파일 가져오기
        /// </summary>
        public static string GetCurrentBmsFile()
        {
            if (_currentAlbum == null || _currentAlbum.BmsFiles.Count == 0)
                return null;
            return _currentAlbum.BmsFiles[0];
        }

        /// <summary>
        /// 현재 선택된 앨범의 BMS 파일 목록 가져오기
        /// </summary>
        public static List<string> GetCurrentBmsFiles()
        {
            if (_currentAlbum == null || _currentAlbum.BmsFiles == null || _currentAlbum.BmsFiles.Count == 0)
                return new List<string>();
            return _currentAlbum.BmsFiles.ToList();
        }

        /// <summary>
        /// 현재 선택된 앨범의 이미지 파일 가져오기
        /// </summary>
        public static string GetCurrentImageFile()
        {
            if (_currentAlbum == null || _currentAlbum.ImageFiles.Count == 0)
                return null;
            return _currentAlbum.ImageFiles[0];
        }

        /// <summary>
        /// 현재 선택된 앨범의 BGA 파일 가져오기
        /// </summary>
        public static string GetCurrentBgaFile()
        {
            if (_currentAlbum == null || _currentAlbum.BgaFiles.Count == 0)
                return null;
            return _currentAlbum.BgaFiles[0];
        }

        /// <summary>
        /// 현재 선택된 앨범의 BGM 파일 가져오기 (OGG 우선)
        /// </summary>
        public static string GetCurrentBgmFile()
        {
            if (_currentAlbum == null || _currentAlbum.BgmFiles.Count == 0)
                return null;

            var oggFile = _currentAlbum.BgmFiles.FirstOrDefault(f =>
                f.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase));
            return oggFile ?? _currentAlbum.BgmFiles[0];
        }

        /// <summary>
        /// 현재 선택된 앨범의 곡 정보 가져오기
        /// </summary>
        public static SongInfo GetCurrentSongInfo()
        {
            return _currentAlbum?.SongInfo;
        }

        /// <summary>
        /// 현재 선택된 앨범 정보 가져오기
        /// </summary>
        public static AlbumInfo GetCurrentAlbum()
        {
            return _currentAlbum;
        }
    }
}
