using MelonLoader;
using UnityEngine;
using System;
using System.Reflection;
using HarmonyLib;
using GRC2.Injectors;
using GRC2.Harmony.Hooks;

namespace GRC2.Core
{
    /// <summary>
    /// Harmony 패치 초기화 및 관리
    /// </summary>
    public class MusicInjector
    {
        internal static HarmonyLib.Harmony harmonyInstance = null;

        public static void Initialize()
        {
            if (harmonyInstance != null) return;
            
            MelonLogger.Msg("[MusicInjector] 초기화 중...");
            
            try
            {
                harmonyInstance = new HarmonyLib.Harmony("GRC2.MusicInjector");
                PatchApplier.Initialize(harmonyInstance);
                
                // PatchAll()은 즉시 실행되므로, 어셈블리가 로드될 때까지 기다린 후 패치
                MelonLoader.MelonCoroutines.Start(DelayedPatch());
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[MusicInjector] Harmony 패치 적용 실패: {ex.Message}");
            }
        }

        private static System.Collections.IEnumerator DelayedPatch()
        {
            // 게임 어셈블리가 로드될 때까지 대기
            yield return new WaitForSeconds(1.0f);

            try
            {
                MelonLogger.Msg("[MusicInjector] === Harmony 패치 시작 ===");
                
                // 커버 이미지 관련 타입 찾기 및 후킹
                PatchApplier.PatchCoverImageTypes();
                
                // 오디오 클립 관련 타입 찾기 및 후킹
                PatchApplier.PatchAudioClipTypes();
                
                // 곡 시작 전 팝업의 아트워크 직접 후킹
                PatchApplier.PatchPreMusicStartWindow();
                
                // 텍스트 설정 관련 타입 찾기 및 후킹
                PatchApplier.PatchTextTypes();

                // 결과 씬 아트워크/곡 제목 직접 주입 후킹
                PatchApplier.PatchResultSceneUpdater();
                
                // 원본 곡 목록 초기화 직후 커스텀 곡 주입
                PatchMusicScrollViewMethods();

            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[MusicInjector] Harmony 패치 적용 실패: {ex.Message}");
                MelonLogger.Msg($"[MusicInjector] 스택 트레이스: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// cMusicSelectScrollView의 기본 목록 재생성 직후를 후킹
        /// </summary>
        private static void PatchMusicScrollViewMethods()
        {
            try
            {
                Type scrollViewType = ReflectionHelper.FindType("IntiCreates.cMusicSelectScrollView");
                if (scrollViewType != null)
                {
                    MethodInfo initMethod = ReflectionHelper.FindMethod("IntiCreates.cMusicSelectScrollView", "initializeMusicDataByDefault",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, silent: true);
                    if (initMethod != null)
                    {
                        var postfixMethod = typeof(MusicScrollViewHooks).GetMethod(
                            nameof(MusicScrollViewHooks.InitializeMusicDataByDefaultPostfix),
                            BindingFlags.Static | BindingFlags.Public);
                        if (postfixMethod != null)
                        {
                            harmonyInstance.Patch(initMethod, null, new HarmonyMethod(postfixMethod));
                            MelonLogger.Msg("[MusicInjector] ✅ cMusicSelectScrollView.initializeMusicDataByDefault 패치 성공");
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[MusicInjector] MusicScrollView 메서드 패치 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 커스텀 곡 주입 (씬 이름으로 호출)
        /// </summary>
        public static void InjectCustomMusic(string sceneName)
        {
            MelonLogger.Msg($"[MusicInjector] 씬 로드됨: {sceneName}");
        }
    }
}
