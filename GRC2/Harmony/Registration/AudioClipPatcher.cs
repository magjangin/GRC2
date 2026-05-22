using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using GRC2.Core;
using GRC2.Harmony.Hooks;
using GRC2.Harmony.Handlers;
using MelonLoader;

namespace GRC2.Harmony.Registration
{
    /// <summary>
    /// 오디오 클립 관련 타입 패치
    /// </summary>
    internal static class AudioClipPatcher
    {
        private static HarmonyLib.Harmony _harmonyInstance;

        public static void Initialize(HarmonyLib.Harmony harmonyInstance)
        {
            _harmonyInstance = harmonyInstance;
        }

        /// <summary>
        /// 오디오 클립 관련 타입 찾기 및 후킹
        /// </summary>
        public static void Patch()
        {
            try
            {
                // cMusicSelectSceneUIUpdater 타입 찾기
                Type uiUpdaterType = ReflectionHelper.FindType("IntiCreates.cMusicSelectSceneUIUpdater");
                if (uiUpdaterType != null)
                {
                    MelonLogger.Msg($"[AudioClipPatcher] ✅ cMusicSelectSceneUIUpdater 타입 발견");
                    
                    MethodInfo[] methods = uiUpdaterType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                    
                    // noticeChangedMusic 메서드 후킹
                    PatchMethod(methods, "noticeChangedMusic", typeof(AudioClipPatch), "NoticeChangedMusicPostfix", null);
                    
                    // changeDifficulty 메서드 후킹
                    PatchMethod(methods, "changeDifficulty", typeof(AudioClipPatch), "ChangeDifficultyPostfix", null, 
                        m => m.Name == "changeDifficulty" || m.Name.Contains("Difficulty"));
                    
                    // startRythmGame 메서드 후킹
                    PatchMethod(methods, "startRythmGame", typeof(GameFlowHooks), "StartRythmGamePrefix", null);
                    
                    // coStartRythmGame 메서드 후킹
                    PatchMethod(methods, "coStartRythmGame", typeof(GameFlowHooks), "CoStartRythmGamePrefix", null);
                    
                    // coOpenPreMusicStartWindow 메서드 후킹
                    PatchMethod(methods, "coOpenPreMusicStartWindow", typeof(GameFlowHooks), 
                        "CoOpenPreMusicStartWindowPrefix", "CoOpenPreMusicStartWindowPostfix");
                    
                    // backToPreScreen 메서드 후킹
                    PatchMethod(methods, "backToPreScreen", typeof(GameFlowHooks), "BackToPreScreenPrefix", null);
                    
                    // openSortWindow 메서드 후킹
                    PatchMethod(methods, "openSortWindow", typeof(GameFlowHooks), "OpenSortWindowPrefix", null);
                    
                    // openFilterWindow 메서드 후킹
                    PatchMethod(methods, "openFilterWindow", typeof(GameFlowHooks), "OpenFilterWindowPrefix", null);
                    
                    // LoadAssets 메서드 후킹
                    PatchLoadAssetsMethods(methods, uiUpdaterType);
                }
                else
                {
                    MelonLogger.Msg("[AudioClipPatcher] ❌ cMusicSelectSceneUIUpdater 타입을 찾을 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[AudioClipPatcher] 오디오 클립 타입 패치 중 오류: {ex.Message}");
            }
        }

        private static void PatchMethod(MethodInfo[] methods, string methodName, Type patchType, 
            string prefixMethodName, string postfixMethodName, Func<MethodInfo, bool> customFilter = null)
        {
            try
            {
                MelonLogger.Msg($"[AudioClipPatcher] 🔍 {methodName} 메서드 검색 중...");
                
                Func<MethodInfo, bool> filter = m => 
                    (customFilter != null ? customFilter(m) : m.Name == methodName) &&
                    !m.IsSpecialName &&
                    m.DeclaringType != typeof(UnityEngine.MonoBehaviour);
                
                var method = methods.FirstOrDefault(filter);
                
                if (method != null)
                {
                    MelonLogger.Msg($"[AudioClipPatcher] === {methodName} 메서드 발견 ===");
                    MelonLogger.Msg($"[AudioClipPatcher]   - {method.Name}({string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
                    
                    MethodInfo prefixMethod = null;
                    MethodInfo postfixMethod = null;
                    
                    if (!string.IsNullOrEmpty(prefixMethodName))
                    {
                        prefixMethod = patchType.GetMethod(prefixMethodName, BindingFlags.Static | BindingFlags.Public);
                    }
                    
                    if (!string.IsNullOrEmpty(postfixMethodName))
                    {
                        postfixMethod = patchType.GetMethod(postfixMethodName, BindingFlags.Static | BindingFlags.Public);
                    }
                    
                    if (prefixMethod != null || postfixMethod != null)
                    {
                        _harmonyInstance.Patch(method, 
                            prefixMethod != null ? new HarmonyMethod(prefixMethod) : null,
                            postfixMethod != null ? new HarmonyMethod(postfixMethod) : null);
                        MelonLogger.Msg($"[AudioClipPatcher] ✅ {methodName} 패치 성공!");
                    }
                }
                else
                {
                    MelonLogger.Msg($"[AudioClipPatcher] ⚠️ {methodName} 메서드를 찾을 수 없습니다!");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[AudioClipPatcher] ⚠️ {methodName} 패치 실패: {ex.Message}");
            }
        }

        private static void PatchLoadAssetsMethods(MethodInfo[] methods, Type uiUpdaterType)
        {
            try
            {
                int loadAssetsCandidate = 0;
                int loadAssetsPatched = 0;
                
                MelonLogger.Msg("[AudioClipPatcher] 🔧 cMusicSelectSceneUIUpdater LoadAssets(coLoadAssets) 후보 검색/패치 중...");
                
                foreach (var m in methods)
                {
                    if (m == null) continue;
                    if (m.IsSpecialName) continue;
                    
                    if (m.Name != "coLoadAssets" && !m.Name.Contains("LoadAssets"))
                        continue;
                    
                    var ps = m.GetParameters();
                    if (ps == null || ps.Length != 2)
                        continue;
                    
                    var p0 = ps[0].ParameterType;
                    var p1 = ps[1].ParameterType;
                    if (p0 == null || p1 == null) continue;
                    
                    var p0Name = p0.Name ?? "";
                    var p1Name = p1.Name ?? "";
                    
                    if (!(p0Name.Contains("Charactor") || p0Name.Contains("Character")))
                        continue;
                    
                    loadAssetsCandidate++;
                    MelonLogger.Msg($"[AudioClipPatcher]   - 후보: {uiUpdaterType.Name}.{m.Name}({p0Name}, {p1Name})");
                    
                    if (!p1Name.Contains("MusicData"))
                        continue;
                    
                    var prefix = CharactorLoadPatcher.CreateCharactorPrefixMethodInfo(p0, p1, m);
                    if (prefix == null)
                        continue;
                    
                    _harmonyInstance.Patch(m, new HarmonyMethod(prefix), null);
                    loadAssetsPatched++;
                }
                
                MelonLogger.Msg($"[AudioClipPatcher] ✅ UIUpdater LoadAssets 패치 결과: 후보 {loadAssetsCandidate}개 중 {loadAssetsPatched}개 패치");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[AudioClipPatcher] ⚠️ UIUpdater LoadAssets 패치 중 오류: {ex.Message}");
            }
        }
    }
}













