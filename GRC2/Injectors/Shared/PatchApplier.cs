using System;
using HarmonyLib;
using GRC2.Harmony.Registration;
using MelonLoader;

namespace GRC2.Injectors
{
    /// <summary>
    /// Harmony 패치 적용 로직 - 간소화 버전
    /// 각 타입별 패치는 Patchers 폴더의 별도 클래스로 분리됨
    /// </summary>
    internal static class PatchApplier
    {
        private static HarmonyLib.Harmony _harmonyInstance;

        public static void Initialize(HarmonyLib.Harmony harmonyInstance)
        {
            _harmonyInstance = harmonyInstance;
            
            // 각 Patcher 초기화
            CoverImagePatcher.Initialize(harmonyInstance);
            AudioClipPatcher.Initialize(harmonyInstance);
            SelectingMusicUIPatcher.Initialize(harmonyInstance);
            TextPatcher.Initialize(harmonyInstance);
        }

        /// <summary>
        /// 커버 이미지 관련 타입 찾기 및 후킹
        /// </summary>
        public static void PatchCoverImageTypes()
        {
            CoverImagePatcher.Patch();
        }

        /// <summary>
        /// 오디오 클립 관련 타입 찾기 및 후킹
        /// </summary>
        public static void PatchAudioClipTypes()
        {
            AudioClipPatcher.Patch();
        }

        /// <summary>
        /// 곡 선택 UI 관련 타입 찾기 및 후킹
        /// </summary>
        public static void PatchSelectingMusicUITypes()
        {
            SelectingMusicUIPatcher.Patch();
        }

        /// <summary>
        /// 텍스트 설정 관련 타입 찾기 및 후킹
        /// </summary>
        public static void PatchTextTypes()
        {
            TextPatcher.Patch();
        }
    }
}
