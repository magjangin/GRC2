using MelonLoader;
using UnityEngine;
using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using GRC2.Injectors;
using GRC2.Harmony.Handlers;
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
                
                // IntiCreates.cMusicSelectScrollViewDataGetter 타입 찾기 및 메서드 출력
                Type dataGetterType = ReflectionHelper.FindType("IntiCreates.cMusicSelectScrollViewDataGetter");
                if (dataGetterType != null)
                {
                    MelonLogger.Msg($"[MusicInjector] ✅ cMusicSelectScrollViewDataGetter 타입 발견: {dataGetterType.FullName}");
                }
                else
                {
                    MelonLogger.Msg("[MusicInjector] ❌ cMusicSelectScrollViewDataGetter 타입을 찾을 수 없습니다.");
                }
                
                // IntiCreates.cMusicSelectScrollViewDataGetter.initalizeDatas 패치는 제거됨
                // (mCellHaviableMusicDataList에 직접 주입하므로 불필요)
                
                // getMusicTitle 메서드 후킹 (제목 테이블에서 제목을 가져올 때 커스텀 차트 처리)
                var getMusicTitleMethod = ReflectionHelper.FindMethod("IntiCreates.cMusicSelectScrollViewDataGetter", "getMusicTitle", 
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic, silent: true);
                if (getMusicTitleMethod != null)
                {
                    var getMusicTitlePostfixMethod = typeof(MusicTitlePatch).GetMethod("GetMusicTitlePostfix", BindingFlags.Static | BindingFlags.Public);
                    if (getMusicTitlePostfixMethod != null)
                    {
                        harmonyInstance.Patch(getMusicTitleMethod, null, new HarmonyMethod(getMusicTitlePostfixMethod));
                        MelonLogger.Msg("[MusicInjector] ✅ cMusicSelectScrollViewDataGetter.getMusicTitle 패치 성공");
                    }
                }
                
                // 커버 이미지 관련 타입 찾기 및 후킹
                PatchApplier.PatchCoverImageTypes();
                
                // 오디오 클립 관련 타입 찾기 및 후킹
                PatchApplier.PatchAudioClipTypes();
                
                // 곡 선택 UI 관련 타입 찾기 및 후킹
                PatchApplier.PatchSelectingMusicUITypes();
                
                // 텍스트 설정 관련 타입 찾기 및 후킹
                PatchApplier.PatchTextTypes();
                
                // cMusicSelectScrollViewItem 및 cMusicSelectScrollView 메서드 후킹
                PatchMusicScrollViewMethods();
                
                // (롤백) 캐릭터 로딩 패치는 잠시 비활성화.
                // (정리) startRythmGame() 시점 인스턴스 필드 덤프/그래프 탐색 기반 디버그 로직은 제거됨.

            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[MusicInjector] Harmony 패치 적용 실패: {ex.Message}");
                MelonLogger.Msg($"[MusicInjector] 스택 트레이스: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// cMusicSelectScrollViewItem 및 cMusicSelectScrollView 메서드 후킹
        /// </summary>
        private static void PatchMusicScrollViewMethods()
        {
            try
            {
                Type scrollViewType = ReflectionHelper.FindType("IntiCreates.cMusicSelectScrollView");
                if (scrollViewType != null)
                {
                    // initializeAllItemByCrrentMusicData 후킹
                    MethodInfo initMethod = ReflectionHelper.FindMethod("IntiCreates.cMusicSelectScrollView", "initializeAllItemByCrrentMusicData",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, silent: true);
                    if (initMethod != null)
                    {
                        var prefixMethod = typeof(MusicScrollViewHooks).GetMethod("InitializeAllItemByCrrentMusicDataPrefix", BindingFlags.Static | BindingFlags.Public);
                        if (prefixMethod != null)
                        {
                            harmonyInstance.Patch(initMethod, new HarmonyMethod(prefixMethod), null);
                            MelonLogger.Msg("[MusicInjector] ✅ cMusicSelectScrollView.initializeAllItemByCrrentMusicData 패치 성공");
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
