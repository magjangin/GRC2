# Harmony 패칭 시스템 상세 가이드

## 개요

이 문서는 GRC2 모드에서 사용하는 Harmony 패칭 시스템의 내부 구조와 동작 원리를 상세히 설명합니다. Harmony는 런타임에 메서드를 가로채고 수정할 수 있는 강력한 라이브러리로, 게임 코드를 직접 수정하지 않고도 기능을 확장할 수 있게 해줍니다.

## 목차

1. [Harmony 패칭 아키텍처](#harmony-패칭-아키텍처)
2. [동적 패칭 vs 어트리뷰트 기반 패칭](#동적-패칭-vs-어트리뷰트-기반-패칭)
3. [PatchApplier 시스템](#patchapplier-시스템)
4. [Patcher 클래스 구조](#patcher-클래스-구조)
5. [Prefix와 Postfix 패턴](#prefix와-postfix-패턴)
6. [메서드 탐색 및 필터링](#메서드-탐색-및-필터링)
7. [실전 예제](#실전-예제)
8. [디버깅 및 문제 해결](#디버깅-및-문제-해결)

---

## Harmony 패칭 아키텍처

### 전체 구조

```
SceneDetector (진입점)
    ↓
MusicInjector.Initialize()
    ↓
PatchApplier.Initialize(harmonyInstance)
    ↓
각 Patcher 클래스 초기화
    ├─ CoverImagePatcher
    ├─ AudioClipPatcher
    ├─ SelectingMusicUIPatcher
    ├─ TextPatcher
    ├─ FairyModeNotesManagerPatcher
    └─ CharactorLoadPatcher
    ↓
DelayedPatch() 코루틴
    ↓
각 Patcher.Patch() 호출
    ↓
Harmony.Patch() 실행
```

### 핵심 컴포넌트

1. **HarmonyLib.Harmony**: Harmony 인스턴스 (패치 관리자)
2. **PatchApplier**: 패치 적용 조정자
3. **Patcher 클래스들**: 각 기능별 패치 로직
4. **Patch 클래스들**: 실제 Prefix/Postfix 메서드 구현

---

## 동적 패칭 vs 어트리뷰트 기반 패칭

### 어트리뷰트 기반 패칭 (사용하지 않음)

```csharp
// ❌ 이 프로젝트에서는 사용하지 않는 방식
[HarmonyPatch(typeof(SomeClass), "MethodName")]
public class SomePatch
{
    [HarmonyPrefix]
    public static bool Prefix()
    {
        // ...
    }
}
```

**단점:**
- 컴파일 타임에 타입이 결정되어야 함
- 게임 업데이트 시 타입 이름 변경에 취약
- 유연성 부족

### 동적 패칭 (프로젝트에서 사용)

```csharp
// ✅ 이 프로젝트에서 사용하는 방식
public static void Patch()
{
    Type targetType = ReflectionHelper.FindType("IntiCreates.cMusicSelectSceneUIUpdater");
    MethodInfo targetMethod = targetType.GetMethod("noticeChangedMusic", ...);
    MethodInfo prefixMethod = typeof(AudioClipPatch).GetMethod("NoticeChangedMusicPostfix", ...);
    
    _harmonyInstance.Patch(targetMethod, 
        prefix: null,
        postfix: new HarmonyMethod(prefixMethod));
}
```

**장점:**
- 런타임에 타입 탐색 가능
- 메서드 시그니처 변경에 유연하게 대응
- 조건부 패칭 가능
- 디버깅 정보 풍부

---

## PatchApplier 시스템

### PatchApplier.cs 구조 (`GRC2/Injectors/Shared/PatchApplier.cs`)

```csharp
internal static class PatchApplier
{
    private static HarmonyLib.Harmony _harmonyInstance;

    public static void Initialize(HarmonyLib.Harmony harmonyInstance)
    {
        _harmonyInstance = harmonyInstance;
        
        // 각 Patcher 초기화 (Harmony 인스턴스 전달)
        CoverImagePatcher.Initialize(harmonyInstance);
        AudioClipPatcher.Initialize(harmonyInstance);
        SelectingMusicUIPatcher.Initialize(harmonyInstance);
        TextPatcher.Initialize(harmonyInstance);
        FairyModeNotesManagerPatcher.Initialize(harmonyInstance);
        CharactorLoadPatcher.Initialize(harmonyInstance);
    }

    // 각 기능별 패치 메서드
    public static void PatchCoverImageTypes() => CoverImagePatcher.Patch();
    public static void PatchAudioClipTypes() => AudioClipPatcher.Patch();
    public static void PatchSelectingMusicUITypes() => SelectingMusicUIPatcher.Patch();
    public static void PatchTextTypes() => TextPatcher.Patch();
    public static void PatchFairyModeNotesManagerTypes() => FairyModeNotesManagerPatcher.Patch();
}
```

### 초기화 흐름

```csharp
// Core/Bootstrap/MusicInjector.cs
public static void Initialize()
{
    var harmonyInstance = new HarmonyLib.Harmony("com.grc2.mod");
    PatchApplier.Initialize(harmonyInstance);
    
    // 게임 어셈블리 로드 대기 후 패치 적용
    MelonCoroutines.Start(DelayedPatch());
}

private static IEnumerator DelayedPatch()
{
    // Assembly-CSharp.dll 로드 대기
    while (Assembly.GetExecutingAssembly().GetType("IntiCreates.cMusicSelectScrollView") == null)
    {
        yield return new WaitForSeconds(0.5f);
    }
    
    // 패치 적용
    PatchApplier.PatchCoverImageTypes();
    PatchApplier.PatchAudioClipTypes();
    // ...
}
```

---

## Patcher 클래스 구조

### 표준 Patcher 템플릿

```csharp
internal static class SomePatcher
{
    private static HarmonyLib.Harmony _harmonyInstance;

    // 1. 초기화 (Harmony 인스턴스 저장)
    public static void Initialize(HarmonyLib.Harmony harmonyInstance)
    {
        _harmonyInstance = harmonyInstance;
    }

    // 2. 패치 적용
    public static void Patch()
    {
        try
        {
            // 타입 찾기
            Type targetType = ReflectionHelper.FindType("Namespace.ClassName");
            if (targetType == null)
            {
                MelonLogger.Msg("[Patcher] ❌ 타입을 찾을 수 없습니다.");
                return;
            }
            
            // 메서드 찾기
            MethodInfo[] methods = targetType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            
            // 메서드 패치
            PatchMethod(methods, "methodName", typeof(PatchClass), "PrefixMethod", "PostfixMethod");
        }
        catch (Exception ex)
        {
            MelonLogger.Msg($"[Patcher] 패치 중 오류: {ex.Message}");
        }
    }

    // 3. 메서드 패치 헬퍼
    private static void PatchMethod(MethodInfo[] methods, string methodName, 
        Type patchType, string prefixMethodName, string postfixMethodName)
    {
        var method = methods.FirstOrDefault(m => m.Name == methodName);
        if (method == null) return;
        
        MethodInfo prefixMethod = patchType.GetMethod(prefixMethodName, BindingFlags.Static | BindingFlags.Public);
        MethodInfo postfixMethod = patchType.GetMethod(postfixMethodName, BindingFlags.Static | BindingFlags.Public);
        
        _harmonyInstance.Patch(method, 
            prefixMethod != null ? new HarmonyMethod(prefixMethod) : null,
            postfixMethod != null ? new HarmonyMethod(postfixMethod) : null);
    }
}
```

### AudioClipPatcher 실제 예제

```csharp
internal static class AudioClipPatcher
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
            // cMusicSelectSceneUIUpdater 타입 찾기
            Type uiUpdaterType = ReflectionHelper.FindType("IntiCreates.cMusicSelectSceneUIUpdater");
            if (uiUpdaterType != null)
            {
                MelonLogger.Msg($"[AudioClipPatcher] ✅ cMusicSelectSceneUIUpdater 타입 발견");
                
                MethodInfo[] methods = uiUpdaterType.GetMethods(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                
                // 여러 메서드 패치
                PatchMethod(methods, "noticeChangedMusic", typeof(AudioClipPatch), 
                    null, "NoticeChangedMusicPostfix");
                PatchMethod(methods, "changeDifficulty", typeof(AudioClipPatch), 
                    null, "ChangeDifficultyPostfix");
                PatchMethod(methods, "startRythmGame", typeof(GameFlowHooks), 
                    "StartRythmGamePrefix", null);
                // ...
            }
        }
        catch (Exception ex)
        {
            MelonLogger.Msg($"[AudioClipPatcher] 오류: {ex.Message}");
        }
    }
}
```

---

## Prefix와 Postfix 패턴

### Prefix 패턴

**목적**: 원본 메서드 실행 **전**에 코드 실행

```csharp
public static bool SomePrefix(/* 원본 메서드 파라미터 */)
{
    // 전처리 로직
    
    // return false: 원본 메서드 실행 취소
    // return true: 원본 메서드 계속 실행
    return true;
}
```

**사용 예제 - 메서드 실행 차단:**

```csharp
// Harmony/Hooks/GameFlowHooks.Navigation.cs (partial 분리)
public static bool CoOpenPreMusicStartWindowPrefix(object __instance)
{
    try
    {
        // 커스텀 차트인 경우 MusicID 변경
        TryManipulateMusicIdByArtist(__instance);
        
        // 원본 메서드 계속 실행
        return true;
    }
    catch (Exception ex)
    {
        ErrorLogger.LogException(ex, "[GameFlowHooks]", "CoOpenPreMusicStartWindowPrefix");
        return true;
    }
}
```

### Postfix 패턴

**목적**: 원본 메서드 실행 **후**에 코드 실행

```csharp
public static void SomePostfix(/* 원본 메서드 파라미터 */, ref ReturnType __result)
{
    // 후처리 로직
    // __result를 수정하여 반환값 변경 가능
}
```

**사용 예제 - 반환값 수정:**

```csharp
// Harmony/Handlers/MusicTitlePatch.cs
public static void GetMusicTitlePostfix(ref string __result)
{
    try
    {
        if (CustomAssetManager.IsCustomChartSelected())
        {
            var songInfo = AlbumManager.GetCurrentSongInfo();
            if (songInfo != null && !string.IsNullOrEmpty(songInfo.Title))
            {
                __result = songInfo.Title; // 반환값 변경
            }
        }
    }
    catch (Exception ex)
    {
        ErrorLogger.LogException(ex, "[MusicTitlePatch]", "GetMusicTitlePostfix");
    }
}
```

### 특수 파라미터

Harmony는 특수한 파라미터 이름을 인식합니다:

```csharp
public static void SomePostfix(
    object __instance,        // 원본 메서드의 인스턴스 (this)
    ref ReturnType __result,  // 원본 메서드의 반환값
    MethodBase __originalMethod, // 원본 메서드 정보
    object[] __args           // 원본 메서드의 모든 인자
)
```

---

## 메서드 탐색 및 필터링

### 기본 메서드 탐색

```csharp
private static void PatchMethod(MethodInfo[] methods, string methodName, 
    Type patchType, string prefixMethodName, string postfixMethodName)
{
    // 1. 메서드 이름으로 필터링
    var method = methods.FirstOrDefault(m => 
        m.Name == methodName &&
        !m.IsSpecialName &&  // 프로퍼티 getter/setter 제외
        m.DeclaringType != typeof(UnityEngine.MonoBehaviour) // Unity 기본 메서드 제외
    );
    
    if (method == null)
    {
        MelonLogger.Msg($"[Patcher] ⚠️ {methodName} 메서드를 찾을 수 없습니다!");
        return;
    }
    
    // 2. 메서드 정보 출력
    MelonLogger.Msg($"[Patcher] === {methodName} 메서드 발견 ===");
    MelonLogger.Msg($"[Patcher]   - {method.Name}({string.Join(", ", 
        method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
    
    // 3. 패치 적용
    // ...
}
```

### 커스텀 필터 사용

```csharp
private static void PatchMethod(MethodInfo[] methods, string methodName, 
    Type patchType, string prefixMethodName, string postfixMethodName, 
    Func<MethodInfo, bool> customFilter = null)
{
    Func<MethodInfo, bool> filter = m => 
        (customFilter != null ? customFilter(m) : m.Name == methodName) &&
        !m.IsSpecialName &&
        m.DeclaringType != typeof(UnityEngine.MonoBehaviour);
    
    var method = methods.FirstOrDefault(filter);
    // ...
}

// 사용 예제
PatchMethod(methods, "changeDifficulty", typeof(AudioClipPatch), 
    null, "ChangeDifficultyPostfix", 
    m => m.Name == "changeDifficulty" || m.Name.Contains("Difficulty"));
```

### 파라미터 기반 필터링

```csharp
// LoadAssets 메서드 찾기 (특정 파라미터 시그니처)
foreach (var m in methods)
{
    if (m.Name != "coLoadAssets" && !m.Name.Contains("LoadAssets"))
        continue;
    
    var ps = m.GetParameters();
    if (ps == null || ps.Length != 2)
        continue;
    
    var p0 = ps[0].ParameterType;
    var p1 = ps[1].ParameterType;
    
    // 첫 번째 파라미터가 Charactor 타입인지 확인
    if (!(p0.Name.Contains("Charactor") || p0.Name.Contains("Character")))
        continue;
    
    // 두 번째 파라미터가 MusicData 타입인지 확인
    if (!p1.Name.Contains("MusicData"))
        continue;
    
    // 패치 적용
    _harmonyInstance.Patch(m, new HarmonyMethod(prefixMethod), null);
}
```

---

## 실전 예제

### 예제 1: 곡 선택 변경 감지

```csharp
// Harmony/Registration/AudioClipPatcher.cs - 패치 설정
public static void Patch()
{
    Type uiUpdaterType = ReflectionHelper.FindType("IntiCreates.cMusicSelectSceneUIUpdater");
    MethodInfo[] methods = uiUpdaterType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
    
    PatchMethod(methods, "noticeChangedMusic", typeof(AudioClipPatch), 
        null, "NoticeChangedMusicPostfix");
}

// Harmony/Handlers/AudioClipPatch.cs - Postfix 구현
public static void NoticeChangedMusicPostfix(object __instance)
{
    try
    {
        // 현재 선택된 곡 정보 가져오기
        var musicIdField = __instance.GetType().GetField("mCurrentMusicID", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        var musicId = musicIdField?.GetValue(__instance);
        
        // 커스텀 차트인지 확인
        if (AlbumManager.IsCustomChartMusicID(musicId) (구현: AlbumManager.Mappings.cs))
        {
            // 앨범 선택
            AlbumManager.SelectAlbumByMusicID(musicId) (구현: AlbumManager.Mappings.cs);
            
            // 커스텀 BGM 주입
            CustomBgmPlayer.InjectCustomBgm();
            
            // 원본 프리뷰 중지
            PreviewAudioManager.StopPreviewAndAmbient();
            
            // 아트워크 업데이트
            ArtworkUpdater.UpdateArtwork();
        }
        else
        {
            // 일반 곡: 원본 복원
            CustomBgmPlayer.CleanupAndRestore();
        }
    }
    catch (Exception ex)
    {
        ErrorLogger.LogException(ex, "[AudioClipPatch]", "NoticeChangedMusicPostfix");
    }
}
```

### 예제 2: 텍스트 교체

```csharp
// Harmony/Registration/TextPatcher.cs - 패치 설정
public static void Patch()
{
    Type textType = ReflectionHelper.FindType("TMPro.TMP_Text");
    if (textType != null)
    {
        MethodInfo setTextMethod = textType.GetMethod("set_text", 
            BindingFlags.Public | BindingFlags.Instance);
        
        if (setTextMethod != null)
        {
            MethodInfo prefixMethod = typeof(TextPatch).GetMethod("SetTextPrefix", 
                BindingFlags.Static | BindingFlags.Public);
            
            _harmonyInstance.Patch(setTextMethod, new HarmonyMethod(prefixMethod), null);
        }
    }
}

// Harmony/Handlers/TextPatch.cs - Prefix 구현
private static bool _isTextReplacementEnabled = false;

public static void EnableTextReplacement(bool enable)
{
    _isTextReplacementEnabled = enable;
}

public static void SetTextPrefix(ref string value)
{
    try
    {
        // 스위치가 꺼져있으면 교체하지 않음
        if (!_isTextReplacementEnabled)
            return;
        
        // 플레이 씬에서만 작동
        string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (currentScene != "FairyModeScene" && currentScene != "PlayMovieScene")
            return;
        
        // 원본 제목 목록에 있는 텍스트만 교체
        if (MusicScrollViewHooks.GetAllOriginalTitles().Contains(value))
        {
            var songInfo = AlbumManager.GetCurrentSongInfo();
            if (songInfo != null && !string.IsNullOrEmpty(songInfo.Title))
            {
                value = songInfo.Title; // 텍스트 교체
            }
        }
    }
    catch (Exception ex)
    {
        ErrorLogger.LogException(ex, "[TextPatch]", "SetTextPrefix");
    }
}
```

### 예제 3: 노트 배열 주입

```csharp
// NoteArrayHooks.cs - 패치 설정
public static void Initialize()
{
    var harmonyInstance = new HarmonyLib.Harmony("com.grc2.notearray");
    
    Type notesManagerType = ReflectionHelper.FindType("IntiCreates.cFairyModeNotesManager");
    if (notesManagerType != null)
    {
        // createAllNote 메서드 후킹
        MethodInfo createAllNoteMethod = notesManagerType.GetMethod("createAllNote", 
            BindingFlags.Public | BindingFlags.Instance);
        
        if (createAllNoteMethod != null)
        {
            MethodInfo prefixMethod = typeof(NoteArrayHooks).GetMethod("CreateAllNotePrefix", 
                BindingFlags.Static | BindingFlags.Public);
            
            harmonyInstance.Patch(createAllNoteMethod, new HarmonyMethod(prefixMethod), null);
        }
    }
}

// NoteArrayHooks.cs - Prefix 구현
public static void CreateAllNotePrefix(object __instance)
{
    try
    {
        // 커스텀 차트가 아니면 주입하지 않음
        if (!CustomAssetManager.IsCustomChartSelected())
        {
            MelonLogger.Msg("[NoteArrayHooks] ⚠️ 커스텀 차트가 아닙니다. BMS 노트 주입을 건너뜁니다.");
            return;
        }
        
        // BMS 노트 주입
        TryInjectBmsNotes(__instance);
    }
    catch (Exception ex)
    {
        ErrorLogger.LogException(ex, "[NoteArrayHooks]", "CreateAllNotePrefix");
    }
}
```

---

## 디버깅 및 문제 해결

### 1. 패치 실패 디버깅

```csharp
public static void Patch()
{
    try
    {
        Type targetType = ReflectionHelper.FindType("IntiCreates.SomeClass");
        if (targetType == null)
        {
            MelonLogger.Msg("[Patcher] ❌ 타입을 찾을 수 없습니다: IntiCreates.SomeClass");
            
            // 모든 타입 출력 (디버깅용)
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                var types = assembly.GetTypes().Where(t => t.Name.Contains("Some"));
                foreach (var type in types)
                {
                    MelonLogger.Msg($"[Patcher] 발견된 타입: {type.FullName}");
                }
            }
            return;
        }
        
        // 메서드 목록 출력
        MethodInfo[] methods = targetType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
        MelonLogger.Msg($"[Patcher] {targetType.Name}의 메서드 목록:");
        foreach (var method in methods)
        {
            MelonLogger.Msg($"[Patcher]   - {method.Name}({string.Join(", ", 
                method.GetParameters().Select(p => p.ParameterType.Name))})");
        }
    }
    catch (Exception ex)
    {
        MelonLogger.Msg($"[Patcher] 패치 중 오류: {ex.Message}\n{ex.StackTrace}");
    }
}
```

### 2. 메서드 시그니처 확인

```csharp
private static void DebugMethodSignature(MethodInfo method)
{
    MelonLogger.Msg($"[Debug] 메서드: {method.Name}");
    MelonLogger.Msg($"[Debug] 반환 타입: {method.ReturnType.Name}");
    MelonLogger.Msg($"[Debug] 파라미터:");
    
    foreach (var param in method.GetParameters())
    {
        MelonLogger.Msg($"[Debug]   - {param.ParameterType.FullName} {param.Name}");
    }
    
    MelonLogger.Msg($"[Debug] 선언 타입: {method.DeclaringType.FullName}");
    MelonLogger.Msg($"[Debug] IsStatic: {method.IsStatic}");
    MelonLogger.Msg($"[Debug] IsPublic: {method.IsPublic}");
}
```

### 3. Harmony 패치 상태 확인

```csharp
public static void DebugPatchStatus()
{
    var harmonyInstance = new HarmonyLib.Harmony("com.grc2.mod");
    var patchedMethods = harmonyInstance.GetPatchedMethods();
    
    MelonLogger.Msg("[Debug] 패치된 메서드 목록:");
    foreach (var method in patchedMethods)
    {
        var patches = HarmonyLib.Harmony.GetPatchInfo(method);
        MelonLogger.Msg($"[Debug] {method.DeclaringType.Name}.{method.Name}");
        MelonLogger.Msg($"[Debug]   - Prefixes: {patches.Prefixes.Count}");
        MelonLogger.Msg($"[Debug]   - Postfixes: {patches.Postfixes.Count}");
    }
}
```

### 4. 일반적인 문제와 해결책

| 문제 | 원인 | 해결책 |
|------|------|--------|
| 타입을 찾을 수 없음 | 게임 어셈블리가 아직 로드되지 않음 | DelayedPatch 코루틴 사용 |
| 메서드를 찾을 수 없음 | 메서드 이름 또는 시그니처 변경 | 메서드 목록 출력 후 확인 |
| 패치가 작동하지 않음 | Prefix/Postfix 시그니처 불일치 | 파라미터 타입 확인 |
| 무한 루프 발생 | Prefix에서 원본 메서드 호출 | 재귀 호출 방지 로직 추가 |
| 성능 저하 | 프레임마다 호출되는 메서드 패치 | 패치 대상 신중히 선택 |

---

## 베스트 프랙티스

### 1. 패치 조직화

```csharp
// ✅ 좋은 예: 기능별로 Patcher 분리 (실제 경로: GRC2/Harmony/Registration/)
AudioClipPatcher.cs      // 오디오 관련
CoverImagePatcher.cs     // 이미지 관련
TextPatcher.cs           // 텍스트 관련

// ❌ 나쁜 예: 모든 패치를 하나의 파일에
AllPatches.cs
```

### 2. 에러 처리

```csharp
// ✅ 좋은 예: 모든 패치에 try-catch
public static void SomePostfix()
{
    try
    {
        // 패치 로직
    }
    catch (Exception ex)
    {
        ErrorLogger.LogException(ex, "[Patcher]", "SomePostfix");
    }
}

// ❌ 나쁜 예: 에러 처리 없음
public static void SomePostfix()
{
    // 패치 로직 (예외 발생 시 게임 크래시)
}
```

### 3. 로깅

```csharp
// ✅ 좋은 예: 명확한 로그 메시지
MelonLogger.Msg("[AudioClipPatcher] ✅ noticeChangedMusic 패치 성공!");
MelonLogger.Msg("[AudioClipPatcher] ⚠️ changeDifficulty 메서드를 찾을 수 없습니다!");

// ❌ 나쁜 예: 불명확한 로그
MelonLogger.Msg("Success");
MelonLogger.Msg("Error");
```

### 4. 성능 고려

```csharp
// ✅ 좋은 예: 조건 체크로 불필요한 작업 방지
public static void SomePostfix()
{
    if (!CustomAssetManager.IsCustomChartSelected())
        return; // 빠른 종료
    
    // 커스텀 차트 처리
}

// ❌ 나쁜 예: 항상 모든 로직 실행
public static void SomePostfix()
{
    // 항상 실행 (성능 낭비)
}
```

---

## 참고 자료

- [Harmony 공식 문서](https://harmony.pardeike.net/)
- [MelonLoader 문서](https://melonwiki.xyz/)
- 프로젝트 내 관련 파일:
  - `GRC2/Injectors/Shared/PatchApplier.cs`
  - `GRC2/Core/Bootstrap/MusicInjector.cs`
  - `GRC2/Harmony/README.md` (Hooks / Handlers / Registration 역할 요약)
  - `GRC2/Harmony/Registration/*.cs` (패치 등록 `*Patcher`)
  - `GRC2/Harmony/Handlers/*.cs` (Prefix·Postfix 본문)
  - `GRC2/Harmony/Hooks/*.cs` (훅 진입점 partial)
  - `GRC2/Core/Infrastructure/ReflectionHelper.cs`
