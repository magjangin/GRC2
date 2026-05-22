# 에러 처리 및 디버깅 시스템

## 개요

프로젝트 전반의 에러 처리 패턴과 디버깅 도구를 분석합니다.

## ErrorLogger 클래스

### 구조

```csharp
public static class ErrorLogger
{
    public static void LogException(Exception ex, string context, string additionalMessage = null)
    public static void LogWarning(Exception ex, string context, string additionalMessage = null)
}
```

### LogException 메서드

**목적**: 예외를 표준 형식으로 로깅 (Error 레벨)

```csharp
public static void LogException(Exception ex, string context, string additionalMessage = null)
{
    if (ex == null)
    {
        MelonLogger.Error($"{context} 예외가 null입니다.");
        return;
    }

    var message = $"{context} 오류";
    if (!string.IsNullOrEmpty(additionalMessage))
    {
        message += $": {additionalMessage}";
    }

    MelonLogger.Error($"{message}");
    MelonLogger.Error($"예외 타입: {ex.GetType().Name}");
    MelonLogger.Error($"메시지: {ex.Message}");
    
    // InnerException 자동 처리
    if (ex.InnerException != null)
    {
        MelonLogger.Error($"내부 예외: {ex.InnerException.Message}");
    }
    
    // 스택 트레이스
    MelonLogger.Error($"스택 트레이스:\n{ex.StackTrace}");
}
```

### LogWarning 메서드

**목적**: 치명적이지 않은 오류를 경고 레벨로 로깅

```csharp
public static void LogWarning(Exception ex, string context, string additionalMessage = null)
{
    if (ex == null)
    {
        MelonLogger.Warning($"{context} 예외가 null입니다.");
        return;
    }

    var message = $"{context} 경고";
    if (!string.IsNullOrEmpty(additionalMessage))
    {
        message += $": {additionalMessage}";
    }

    MelonLogger.Warning($"{message}");
    MelonLogger.Warning($"예외 타입: {ex.GetType().Name}");
    MelonLogger.Warning($"메시지: {ex.Message}");
}
```

## 사용 패턴

### 1. 표준 try-catch 패턴

```csharp
// ✅ 좋은 예: ErrorLogger 사용
public static void SomeMethod()
{
    try
    {
        // 작업 수행
    }
    catch (Exception ex)
    {
        ErrorLogger.LogException(ex, "[ClassName]", "SomeMethod 실행 중 오류");
    }
}

// ❌ 나쁜 예: 직접 로깅
public static void SomeMethod()
{
    try
    {
        // 작업 수행
    }
    catch (Exception ex)
    {
        MelonLogger.Error($"오류: {ex.Message}"); // 일관성 없음
    }
}
```

### 2. 컨텍스트 정보 제공

```csharp
try
{
    var noteCreateData = CreateNoteCreateData(bmsNote);
}
catch (Exception ex)
{
    ErrorLogger.LogException(ex, "[NoteCreateDataBuilder]", 
        $"노트 생성 실패 (Time={bmsNote.Time}, Lane={bmsNote.Lane})");
}
```

### 3. 경고 레벨 사용

```csharp
try
{
    var constructor = noteCreateDataType.GetConstructor(types);
    if (constructor != null)
    {
        return constructor.Invoke(args);
    }
}
catch (Exception ex)
{
    // 치명적이지 않은 오류 (다른 생성자 시도 가능)
    ErrorLogger.LogWarning(ex, "[NoteConstructorHelper]", 
        "생성자 호출 실패 (다른 생성자 시도)");
}
```

## 디버깅 도구

### NoteArrayJsonDumper

**목적**: 노트 배열을 JSON으로 덤프하여 디버깅

```csharp
public static class NoteArrayJsonDumper
{
    private static bool _isEnabled = false;
    private static string _noteFolderPath;

    public static void Initialize(string noteFolderPath, bool isEnabled = false)
    {
        _isEnabled = isEnabled;
        _noteFolderPath = noteFolderPath;
        
        if (_isEnabled && !Directory.Exists(_noteFolderPath))
        {
            Directory.CreateDirectory(_noteFolderPath);
        }
    }

    public static void DumpNoteArray(Array noteArray, string fileName)
    {
        if (!_isEnabled) return;
        
        try
        {
            var json = SerializeNoteArray(noteArray);
            var formattedJson = FormatJson(json);
            
            var filePath = Path.Combine(_noteFolderPath, fileName);
            File.WriteAllText(filePath, formattedJson);
            
            MelonLogger.Msg($"[NoteArrayJsonDumper] JSON 덤프 완료: {filePath}");
        }
        catch (Exception ex)
        {
            ErrorLogger.LogException(ex, "[NoteArrayJsonDumper]", "JSON 덤프 실패");
        }
    }
}
```

**사용 예제:**

```csharp
// 초기화 (비활성화 상태)
NoteArrayJsonDumper.Initialize(noteFolderPath, isEnabled: false);

// 활성화하려면
NoteArrayJsonDumper.Initialize(noteFolderPath, isEnabled: true);

// 덤프
NoteArrayJsonDumper.DumpNoteArray(originalNotes, "original_notes.json");
NoteArrayJsonDumper.DumpNoteArray(injectedNotes, "injected_notes.json");
```

**현재 모드 기본값(중요)**:
- `NoteArrayHooks.Initialize(...)` 내부에서 `NoteArrayJsonDumper.Initialize(noteFolderPath, isEnabled: false)`로 호출되므로, 기본 설정으로는 JSON 파일이 생성되지 않습니다.

### GameFlowDebugger

**목적**: 게임 플로우 디버깅

```csharp
public static class GameFlowDebugger
{
    public static void LogMusicSelection(object musicData)
    {
        try
        {
            var musicIdField = musicData.GetType().GetField("musicID");
            var titleField = musicData.GetType().GetField("title");
            
            var musicId = musicIdField?.GetValue(musicData);
            var title = titleField?.GetValue(musicData);
            
            MelonLogger.Msg($"[GameFlowDebugger] 곡 선택: ID={musicId}, Title={title}");
        }
        catch (Exception ex)
        {
            ErrorLogger.LogWarning(ex, "[GameFlowDebugger]", "곡 정보 로깅 실패");
        }
    }
}
```

### MusicInjectionDebugger

**목적**: 곡 주입 디버깅

```csharp
public static class MusicInjectionDebugger
{
    public static void LogInjectionStatus(List<object> musicList, int customCount)
    {
        MelonLogger.Msg("═══════════════════════════════════════");
        MelonLogger.Msg($"[MusicInjectionDebugger] 곡 주입 상태");
        MelonLogger.Msg($"  전체 곡 수: {musicList.Count}");
        MelonLogger.Msg($"  커스텀 곡 수: {customCount}");
        MelonLogger.Msg($"  원본 곡 수: {musicList.Count - customCount}");
        MelonLogger.Msg("═══════════════════════════════════════");
    }
}
```

## 로깅 레벨

### MelonLogger 레벨

```csharp
// Error: 치명적 오류
MelonLogger.Error("[Context] 치명적 오류 발생");

// Warning: 경고 (계속 진행 가능)
MelonLogger.Warning("[Context] 경고: 일부 기능이 작동하지 않을 수 있음");

// Msg: 일반 정보
MelonLogger.Msg("[Context] 작업 완료");
```

### 로깅 아이콘

```csharp
// ✅ 성공
MelonLogger.Msg("[Context] ✅ 작업 성공");

// ❌ 실패
MelonLogger.Error("[Context] ❌ 작업 실패");

// ⚠️ 경고
MelonLogger.Warning("[Context] ⚠️ 주의 필요");

// 🔍 검색/탐색
MelonLogger.Msg("[Context] 🔍 파일 검색 중...");

// 🔧 처리 중
MelonLogger.Msg("[Context] 🔧 처리 중...");
```

## 베스트 프랙티스

### 1. 항상 컨텍스트 제공

```csharp
// ✅ 좋은 예
ErrorLogger.LogException(ex, "[BmsParser]", "파일 파싱 중 오류");

// ❌ 나쁜 예
MelonLogger.Error(ex.Message);
```

### 2. 추가 정보 포함

```csharp
// ✅ 좋은 예
ErrorLogger.LogException(ex, "[NoteBuilder]", 
    $"노트 생성 실패 (Time={time}, Lane={lane}, Type={type})");

// ❌ 나쁜 예
ErrorLogger.LogException(ex, "[NoteBuilder]", "노트 생성 실패");
```

### 3. 적절한 레벨 사용

```csharp
// ✅ 좋은 예: 치명적 오류는 Error
if (noteCreateDataType == null)
{
    MelonLogger.Error("[Builder] NoteCreateData 타입이 null입니다.");
    return null;
}

// ✅ 좋은 예: 경고는 Warning
if (field == null)
{
    MelonLogger.Warning("[Builder] 필드를 찾을 수 없습니다. 기본값 사용.");
}

// ✅ 좋은 예: 정보는 Msg
MelonLogger.Msg("[Builder] 노트 생성 완료: 1000개");
```

### 4. 예외 재발생 금지

```csharp
// ✅ 좋은 예: 예외 처리 후 계속 진행
try
{
    ProcessNote(note);
}
catch (Exception ex)
{
    ErrorLogger.LogWarning(ex, "[Processor]", "노트 처리 실패 (건너뜀)");
    continue; // 다음 노트 처리
}

// ❌ 나쁜 예: 예외 재발생
try
{
    ProcessNote(note);
}
catch (Exception ex)
{
    ErrorLogger.LogException(ex, "[Processor]", "노트 처리 실패");
    throw; // 전체 프로세스 중단
}
```

## 디버깅 팁

### 1. JSON 덤프 활성화

```csharp
// GRC2/Harmony/Hooks/NoteArrayHooks.cs
NoteArrayJsonDumper.Initialize(noteFolderPath, isEnabled: true); // false → true
```

### 2. 상세 로깅 활성화

```csharp
// 각 Patcher에서 메서드 시그니처 출력
MelonLogger.Msg($"[Patcher] 메서드: {method.Name}");
MelonLogger.Msg($"[Patcher] 파라미터: {string.Join(", ", 
    method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))}");
```

### 3. 스택 트레이스 분석

```csharp
// 호출 스택 출력
var stackTrace = new System.Diagnostics.StackTrace(true);
MelonLogger.Msg($"[Debug] 호출 스택:\n{stackTrace}");
```

## 참고 자료

- `GRC2/Helpers/ErrorLogger.cs`
- `GRC2/Helpers/NoteArrayJsonDumper.cs`
- `GRC2/Helpers/GameFlowDebugger.cs`
- `GRC2/Helpers/MusicInjectionDebugger.cs`
