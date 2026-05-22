using System;
using System.IO;
using System.Linq;
using GRC2;
using MelonLoader;

namespace GRC2.Core
{
    /// <summary>
    /// 커스텀 에셋 로딩 로직
    /// </summary>
    internal static class AssetLoader
    {
        /// <summary>
        /// 커스텀 아트워크 및 프리뷰 BGM 로드 (앨범별)
        /// </summary>
        public static void LoadCustomAssets(string hwaFolderPath)
        {
            try
            {
                MelonLogger.Msg("[AssetLoader] 커스텀 아트워크 및 프리뷰 BGM 파일 스캔 시작");

                // 앨범별 이미지 파일 가져오기
                var imageFile = AlbumManager.GetCurrentImageFile();
                if (!string.IsNullOrEmpty(imageFile))
                {
                    MelonLogger.Msg($"[AssetLoader] 커스텀 아트워크 파일 발견: {Path.GetFileName(imageFile)}");
                    CustomAssetManager.LoadCustomArtwork(imageFile);
                }
                else
                {
                    MelonLogger.Msg("[AssetLoader] 현재 선택된 앨범에 커스텀 아트워크 이미지 파일이 없습니다.");
                }

                // music.ogg 파일 검색 (프리뷰 BGM) - 현재 앨범 폴더에서 검색
                var currentBgmFile = AlbumManager.GetCurrentBgmFile();
                if (!string.IsNullOrEmpty(currentBgmFile) && File.Exists(currentBgmFile))
                {
                    MelonLogger.Msg($"[AssetLoader] 프리뷰 BGM 파일 발견: {Path.GetFileName(currentBgmFile)}");
                    CustomAssetManager.LoadCustomPreviewBGM(currentBgmFile);
                }
                else
                {
                    MelonLogger.Msg("[AssetLoader] 프리뷰 BGM 파일(music.ogg)을 찾을 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, "[AssetLoader]", "커스텀 아트워크 및 프리뷰 BGM 로드 오류");
            }
        }
    }
}



