using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using GRC2.Core;
using GRC2.Harmony.Hooks;
using GRC2.Harmony.Handlers;
using MelonLoader;
using System.Reflection.Emit;

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

    /// <summary>
    /// 곡 선택 UI 관련 타입 패치
    /// </summary>
    internal static class SelectingMusicUIPatcher
    {
        private static HarmonyLib.Harmony _harmonyInstance;

        public static void Initialize(HarmonyLib.Harmony harmonyInstance)
        {
            _harmonyInstance = harmonyInstance;
        }

        /// <summary>
        /// 곡 선택 UI 관련 타입 찾기 및 후킹
        /// </summary>
        public static void Patch()
        {
            try
            {
                // cMusicSelectSceneSelectingMusicUI 타입 찾기
                Type selectingMusicUIType = ReflectionHelper.FindType("IntiCreates.cMusicSelectSceneSelectingMusicUI");
                if (selectingMusicUIType != null)
                {
                    MelonLogger.Msg($"[SelectingMusicUIPatcher] ✅ cMusicSelectSceneSelectingMusicUI 타입 발견");
                    
                    MethodInfo[] methods = selectingMusicUIType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

                    // coOpen 메서드 후킹
                    PatchMethod(methods, "coOpen", typeof(GameFlowHooks), "CoOpenPrefix", null);
                    
                    // coClose 메서드 후킹
                    PatchMethod(methods, "coClose", typeof(GameFlowHooks), "CoClosePrefix", null);
                }
                else
                {
                    MelonLogger.Msg("[SelectingMusicUIPatcher] ❌ cMusicSelectSceneSelectingMusicUI 타입을 찾을 수 없습니다.");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[SelectingMusicUIPatcher] 곡 선택 UI 타입 패치 중 오류: {ex.Message}");
            }
        }

        private static void PatchMethod(MethodInfo[] methods, string methodName, Type patchType, 
            string prefixMethodName, string postfixMethodName)
        {
            try
            {
                MelonLogger.Msg($"[SelectingMusicUIPatcher] 🔍 {methodName} 메서드 검색 중...");
                
                var method = methods.FirstOrDefault(m => 
                    m.Name == methodName &&
                    !m.IsSpecialName &&
                    m.DeclaringType != typeof(UnityEngine.MonoBehaviour));
                
                if (method != null)
                {
                    MelonLogger.Msg($"[SelectingMusicUIPatcher] === {methodName} 메서드 발견 ===");
                    MelonLogger.Msg($"[SelectingMusicUIPatcher]   - {method.Name}({string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
                    
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
                        MelonLogger.Msg($"[SelectingMusicUIPatcher] ✅ {methodName} 패치 성공!");
                    }
                }
                else
                {
                    MelonLogger.Msg($"[SelectingMusicUIPatcher] ⚠️ {methodName} 메서드를 찾을 수 없습니다!");
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[SelectingMusicUIPatcher] ⚠️ {methodName} 패치 실패: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 커버 이미지 관련 타입 패치
    /// </summary>
    internal static class CoverImagePatcher
    {
        private static HarmonyLib.Harmony _harmonyInstance;

        public static void Initialize(HarmonyLib.Harmony harmonyInstance)
        {
            _harmonyInstance = harmonyInstance;
        }

        /// <summary>
        /// 커버 이미지 관련 타입 찾기 및 후킹
        /// </summary>
        public static void Patch()
        {
            try
            {
                // cMusicSelectArtWork 타입 찾기 및 후킹
                Type artWorkType = ReflectionHelper.FindType("IntiCreates.cMusicSelectArtWork");
                if (artWorkType != null)
                {
                    MelonLogger.Msg($"[CoverImagePatcher] ✅ cMusicSelectArtWork 타입 발견");
                    
                    // requestSetArtworkSprite 메서드 후킹
                    var setArtworkMethod = ReflectionHelper.FindMethod("IntiCreates.cMusicSelectArtWork", "requestSetArtworkSprite", 
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic, silent: true);
                    if (setArtworkMethod != null)
                    {
                        var prefixMethod = typeof(ArtWorkPatch).GetMethod("RequestSetArtworkSpritePrefix", BindingFlags.Static | BindingFlags.Public);
                        var postfixMethod = typeof(ArtWorkPatch).GetMethod("RequestSetArtworkSpritePostfix", BindingFlags.Static | BindingFlags.Public);
                        if (prefixMethod != null && postfixMethod != null)
                        {
                            _harmonyInstance.Patch(setArtworkMethod, new HarmonyMethod(prefixMethod), new HarmonyMethod(postfixMethod));
                            MelonLogger.Msg("[CoverImagePatcher] ✅ cMusicSelectArtWork.requestSetArtworkSprite 패치 성공");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[CoverImagePatcher] 커버 이미지 타입 패치 중 오류: {ex.Message}");
            }
        }
    }

    internal static class CharactorLoadPatcher
    {
        private static ModuleBuilder _dynamicPatchModule;
        private static int _dynamicPatchCounter = 0;

        private static void EnsureDynamicPatchModule()
        {
            if (_dynamicPatchModule != null) return;
            var asmName = new AssemblyName("GRC2.DynamicPatches");
            var asm = AssemblyBuilder.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.Run);
            _dynamicPatchModule = asm.DefineDynamicModule("GRC2.DynamicPatches.Module");
        }
        
        public static MethodInfo CreateCharactorPrefixMethodInfo(Type charactorType, Type musicDataType, MethodInfo original)
        {
            try
            {
                EnsureDynamicPatchModule();
                
                int id = System.Threading.Interlocked.Increment(ref _dynamicPatchCounter);
                var tb = _dynamicPatchModule.DefineType(
                    $"GRC2_CharactorPrefixType_{id}",
                    TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
                
                // static void Prefix(ref charactorType __0, musicDataType __1)
                var mb = tb.DefineMethod(
                    "Prefix",
                    MethodAttributes.Public | MethodAttributes.Static,
                    typeof(void),
                    new[] { charactorType.MakeByRefType(), musicDataType });
                
                mb.DefineParameter(1, ParameterAttributes.None, "__0");
                mb.DefineParameter(2, ParameterAttributes.None, "__1");
                
                var il = mb.GetILGenerator();
                
                // locals: object result, object boxedMusic
                var locResult = il.DeclareLocal(typeof(object)); // 0
                var locMusic = il.DeclareLocal(typeof(object));  // 1
                
                // locMusic = (object)__1
                il.Emit(OpCodes.Ldarg_1);
                if (musicDataType.IsValueType)
                    il.Emit(OpCodes.Box, musicDataType);
                il.Emit(OpCodes.Stloc, locMusic);
                
                // result = CharactorLoadPatch.ComputePatchedCharactor(locMusic, typeof(charactorType))
                il.Emit(OpCodes.Ldloc, locMusic);
                il.Emit(OpCodes.Ldtoken, charactorType);
                il.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle", BindingFlags.Public | BindingFlags.Static));
                
                var compute = typeof(CharactorLoadPatch).GetMethod("ComputePatchedCharactor", BindingFlags.Public | BindingFlags.Static);
                if (compute == null)
                    return null;
                
                il.Emit(OpCodes.Call, compute);
                il.Emit(OpCodes.Stloc, locResult);
                
                // if (result == null) return;
                var lblRet = il.DefineLabel();
                il.Emit(OpCodes.Ldloc, locResult);
                il.Emit(OpCodes.Brfalse_S, lblRet);
                
                // write back to ref __0
                il.Emit(OpCodes.Ldarg_0);      // ref charactor
                il.Emit(OpCodes.Ldloc, locResult);
                
                if (charactorType.IsValueType)
                {
                    il.Emit(OpCodes.Unbox_Any, charactorType);
                    il.Emit(OpCodes.Stobj, charactorType);
                }
                else
                {
                    il.Emit(OpCodes.Castclass, charactorType);
                    il.Emit(OpCodes.Stind_Ref);
                }
                
                il.MarkLabel(lblRet);
                il.Emit(OpCodes.Ret);
                
                var created = tb.CreateType();
                return created?.GetMethod("Prefix", BindingFlags.Public | BindingFlags.Static);
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[CharactorLoadPatcher] ⚠️ 캐릭터 prefix(TypeBuilder) 생성 실패: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }
    }

    /// <summary>
    /// cRythmGameResultSceneUpdater.initializePreFade 후킹 등록
    /// </summary>
    internal static class ResultSceneUpdaterPatcher
    {
        private static HarmonyLib.Harmony _harmonyInstance;

        public static void Initialize(HarmonyLib.Harmony harmonyInstance)
        {
            _harmonyInstance = harmonyInstance;
        }

        public static void Patch()
        {
            try
            {
                var method = ReflectionHelper.FindMethod(
                    "IntiCreates.cRythmGameResultSceneUpdater",
                    "initializePreFade",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    silent: true);

                if (method == null)
                {
                    MelonLogger.Warning("[ResultSceneUpdaterPatcher] cRythmGameResultSceneUpdater.initializePreFade 메서드를 찾을 수 없습니다.");
                    return;
                }

                var postfixMethod = typeof(ResultSceneUpdaterPatch).GetMethod(
                    nameof(ResultSceneUpdaterPatch.InitializePreFadePostfix),
                    BindingFlags.Static | BindingFlags.Public);

                if (postfixMethod == null)
                {
                    MelonLogger.Warning("[ResultSceneUpdaterPatcher] InitializePreFadePostfix 메서드를 찾을 수 없습니다.");
                    return;
                }

                _harmonyInstance.Patch(method, null, new HarmonyMethod(postfixMethod));
                MelonLogger.Msg("[ResultSceneUpdaterPatcher] ✅ cRythmGameResultSceneUpdater.initializePreFade 패치 성공");
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[ResultSceneUpdaterPatcher] 패치 중 오류: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 텍스트 설정 관련 타입 패치
    /// </summary>
    internal static class TextPatcher
    {
        private static HarmonyLib.Harmony _harmonyInstance;

        public static void Initialize(HarmonyLib.Harmony harmonyInstance)
        {
            _harmonyInstance = harmonyInstance;
        }

        /// <summary>
        /// 텍스트 설정 관련 타입 찾기 및 후킹
        /// </summary>
        public static void Patch()
        {
            try
            {
                // UnityEngine.UI.Text.set_text 후킹
                PatchTextType(typeof(UnityEngine.UI.Text));
                
                // TMPro.TextMeshProUGUI.set_text 후킹
                Type tmpUGUIType = ReflectionHelper.FindType("TMPro.TextMeshProUGUI");
                if (tmpUGUIType != null)
                {
                    PatchTextType(tmpUGUIType);
                }
                
                // TMPro.TextMeshPro.set_text 후킹
                Type tmpType = ReflectionHelper.FindType("TMPro.TextMeshPro");
                if (tmpType != null)
                {
                    PatchTextType(tmpType);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[TextPatcher] 텍스트 타입 패치 중 오류: {ex.Message}");
            }
        }

        private static void PatchTextType(Type textType)
        {
            try
            {
                PropertyInfo textProperty = textType.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                if (textProperty != null)
                {
                    MethodInfo setter = textProperty.GetSetMethod();
                    if (setter != null)
                    {
                        var prefixMethod = typeof(TextPatch).GetMethod("SetTextPrefix", BindingFlags.Static | BindingFlags.Public);
                        if (prefixMethod != null)
                        {
                            _harmonyInstance.Patch(setter, new HarmonyMethod(prefixMethod), null);
                            MelonLogger.Msg($"[TextPatcher] ✅ {textType.Name}.set_text 패치 성공");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[TextPatcher] ⚠️ {textType.Name} 패치 실패: {ex.Message}");
            }
        }
    }
}
