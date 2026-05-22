using System;
using System.Collections.Generic;
using GRC2.Parsers;

namespace GRC2.Core
{
    /// <summary>
    /// 앨범별 파일 관리 클래스
    /// </summary>
    public class AlbumInfo
    {
        public string AlbumFolderPath { get; set; }
        public string AlbumName { get; set; }
        public List<string> BmsFiles { get; set; } = new List<string>();
        public List<string> ImageFiles { get; set; } = new List<string>();
        public List<string> BgaFiles { get; set; } = new List<string>();
        public List<string> BgmFiles { get; set; } = new List<string>();
        public string TxtFile { get; set; }
        public SongInfo SongInfo { get; set; }
    }

    /// <summary>
    /// 앨범 폴더 스캔 및 파일 매핑 관리
    /// </summary>
    public static partial class AlbumManager
    {
        private static Dictionary<string, AlbumInfo> _albums = new Dictionary<string, AlbumInfo>();
        private static AlbumInfo _currentAlbum = null;
        private static Dictionary<object, AlbumInfo> _musicIdToAlbumMap = new Dictionary<object, AlbumInfo>();
        private static Dictionary<object, string> _musicIdToOriginalTitleMap = new Dictionary<object, string>();
        
        /// <summary>
        /// 아티스트 ID별 첫 곡 정보 저장 (아티스트ID -> (MusicID, 제목))
        /// </summary>
        private static Dictionary<string, (object musicId, string title)> _artistIdToFirstSong = new Dictionary<string, (object, string)>();
        private static string _currentArtistId = null;

    }
}

