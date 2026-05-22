# Enum 및 타입 시스템 관리

## 개요

게임의 Enum 타입과 타입 시스템을 관리하는 메커니즘을 분석합니다.

## EnumValueHelper 클래스

### 구조

```csharp
public static class EnumValueHelper
{
    // Enum 값 캐시 (성능 최적화)
    private static Dictionary<string, object> _enumCache = new Dictionary<string, object>();
    
    // Enum 이름 상수
    public const string ENUM_TAP = "Tap";
    public const string ENUM_HOLD = "Hold";
    public const string ENUM_FLICK = "Flick";
    public const string ENUM_FAIRY = "Fairy";
    public const string ENUM_LEFT = "LEFT";
    public const string ENUM_RIGHT = "RIGHT";
    public const string ENUM_CENTER_MIDDLE = "CENTER_MIDDLE";
    public const string ENUM_SCALE_1 = "SCALE_1";
    public const string ENUM_NUM = "NUM";
}
```

### GetEnumValue 메서드 (캐싱)

```csharp
public static object GetEnumValue(Type enumType, string valueName)
{
    if (enumType == null || string.IsNullOrEmpty(valueName))
    {
        return null;
    }

    // ⚡ 캐시 키 생성
    var cacheKey = $"{enumType.FullName}.{valueName}";
    
    // ⚡ 캐시에서 먼저 검색
    if (_enumCache.TryGetValue(cacheKey, out var cachedValue))
    {
        return cachedValue;
    }

    try
    {
        // Enum 값 파싱
        var enumValue = Enum.Parse(enumType, valueName, ignoreCase: true);
        
        // ⚡ 캐시에 저장
        _enumCache[cacheKey] = enumValue;
        
        return enumValue;
    }
    catch
    {
        // ⚡ null도 캐시 (다음 호출 시 빠르게 반환)
        _enumCache[cacheKey] = null;
        return null;
    }
}
```

**성능 비교:**

| 호출 | 캐시 상태 | 시간 | 설명 |
|------|-----------|------|------|
| 1차 | 없음 | ~0.1ms | Enum.Parse 호출 |
| 2차 | 있음 | ~0.001ms | Dictionary 조회 (100배 빠름) |

### 노트 타입 변환

```csharp
public static object GetNoteTypeId(NoteType noteType)
{
    string enumName;
    
    switch (noteType)
    {
        case NoteType.Tap:
            enumName = ENUM_TAP;
            break;
        case NoteType.Hold:
        case NoteType.HoldEnd:
            enumName = ENUM_HOLD;
            break;
        case NoteType.Flick:
            enumName = ENUM_FLICK;
            break;
        case NoteType.Fairy:
        case NoteType.FairyEnd:
            enumName = ENUM_FAIRY;
            break;
        default:
            return null;
    }
    
    return GetEnumValue(GameTypeLoader.NoteTypeIdEnum, enumName);
}
```

### 방향 변환

```csharp
public static object GetDirectionIndex(NoteDirection direction)
{
    string enumName;
    
    switch (direction)
    {
        case NoteDirection.Left:
            enumName = "LEFT";
            break;
        case NoteDirection.LeftUp:
            enumName = "LEFT_TOP";
            break;
        case NoteDirection.Up:
            enumName = "CENTER_TOP";
            break;
        case NoteDirection.RightUp:
            enumName = "RIGHT_TOP";
            break;
        case NoteDirection.Right:
            enumName = "RIGHT";
            break;
        case NoteDirection.RightDown:
            enumName = "RIGHT_BOTTOM";
            break;
        case NoteDirection.Down:
            enumName = "CENTER_BOTTOM";
            break;
        case NoteDirection.LeftDown:
            enumName = "LEFT_BOTTOM";
            break;
        default:
            enumName = ENUM_CENTER_MIDDLE;
            break;
    }
    
    return GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, enumName);
}
```

### 레인 기반 방향 설정 (홀드 노트용)

```csharp
public static object GetDirectionIndexFromLane(int lane, bool isLeft)
{
    string enumName;
    
    // 레인과 Left/Right에 따라 방향 결정
    if (lane == 0)
    {
        enumName = isLeft ? "LEFT" : "LEFT_TOP";
    }
    else if (lane == 1)
    {
        enumName = isLeft ? "LEFT_TOP" : "CENTER_TOP";
    }
    else if (lane == 2)
    {
        enumName = "CENTER_MIDDLE";
    }
    else if (lane == 3)
    {
        enumName = isLeft ? "CENTER_TOP" : "RIGHT_TOP";
    }
    else if (lane == 4)
    {
        enumName = isLeft ? "RIGHT_TOP" : "RIGHT";
    }
    else
    {
        enumName = ENUM_CENTER_MIDDLE;
    }
    
    return GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, enumName);
}
```

## GameTypeLoader 클래스

### 타입 로딩

```csharp
public static class GameTypeLoader
{
    // 로드된 타입들
    public static Type NoteCreateDataType { get; private set; }
    public static Type NoteTypeIdEnum { get; private set; }
    public static Type NoteLaneLeftRightEnum { get; private set; }
    public static Type NoteSubLaneTypeEnum { get; private set; }
    public static Type NoteDirectionIndexEnum { get; private set; }
    public static Type NoteSizeEnum { get; private set; }
    public static Type SlideEndFlickDirectionEnum { get; private set; }

    public static void Initialize()
    {
        try
        {
            MelonLogger.Msg("[GameTypeLoader] 게임 타입 로딩 시작...");
            
            // Assembly-CSharp.dll에서 타입 찾기
            var assembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            
            if (assembly == null)
            {
                MelonLogger.Error("[GameTypeLoader] Assembly-CSharp를 찾을 수 없습니다.");
                return;
            }

            // 타입 로드
            NoteCreateDataType = assembly.GetType("IntiCreates.NoteCreateData");
            NoteTypeIdEnum = assembly.GetType("IntiCreates.NoteTypeId");
            NoteLaneLeftRightEnum = assembly.GetType("IntiCreates.NoteLaneLeftRight");
            NoteSubLaneTypeEnum = assembly.GetType("IntiCreates.NoteSubLaneType");
            NoteDirectionIndexEnum = assembly.GetType("IntiCreates.NoteDirectionIndex");
            NoteSizeEnum = assembly.GetType("IntiCreates.NoteSize");
            SlideEndFlickDirectionEnum = assembly.GetType("IntiCreates.SlideEndFlickDirection");

            // 로드 결과 출력
            LogLoadedTypes();
            
            MelonLogger.Msg("[GameTypeLoader] 게임 타입 로딩 완료!");
        }
        catch (Exception ex)
        {
            ErrorLogger.LogException(ex, "[GameTypeLoader]", "타입 로딩 중 오류");
        }
    }

    private static void LogLoadedTypes()
    {
        MelonLogger.Msg($"[GameTypeLoader] NoteCreateData: {NoteCreateDataType?.Name ?? "null"}");
        MelonLogger.Msg($"[GameTypeLoader] NoteTypeId: {NoteTypeIdEnum?.Name ?? "null"}");
        MelonLogger.Msg($"[GameTypeLoader] NoteLaneLeftRight: {NoteLaneLeftRightEnum?.Name ?? "null"}");
        MelonLogger.Msg($"[GameTypeLoader] NoteSubLaneType: {NoteSubLaneTypeEnum?.Name ?? "null"}");
        MelonLogger.Msg($"[GameTypeLoader] NoteDirectionIndex: {NoteDirectionIndexEnum?.Name ?? "null"}");
        MelonLogger.Msg($"[GameTypeLoader] NoteSize: {NoteSizeEnum?.Name ?? "null"}");
        MelonLogger.Msg($"[GameTypeLoader] SlideEndFlickDirection: {SlideEndFlickDirectionEnum?.Name ?? "null"}");
    }
}
```

## GameTypeInspector 클래스

> 현재 코드 기준: `GameTypeInspector` 구현은 2026-05-12 정리에서 제거되었습니다.
> 아래 코드는 과거 리버스 엔지니어링/진단용 예시로만 참고하세요. 현재 런타임 타입 로딩은 `GameTypeLoader`가 담당합니다.

### 타입 검증

```csharp
public static class GameTypeInspector
{
    public static void InspectNoteCreateDataType()
    {
        if (GameTypeLoader.NoteCreateDataType == null)
        {
            MelonLogger.Error("[GameTypeInspector] NoteCreateData 타입이 null입니다.");
            return;
        }

        MelonLogger.Msg("[GameTypeInspector] NoteCreateData 타입 검사:");
        
        // 필드 목록
        var fields = GameTypeLoader.NoteCreateDataType.GetFields(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        
        MelonLogger.Msg($"[GameTypeInspector] 필드 수: {fields.Length}");
        
        foreach (var field in fields)
        {
            MelonLogger.Msg($"[GameTypeInspector]   - {field.FieldType.Name} {field.Name}");
        }
        
        // 생성자 목록
        var constructors = GameTypeLoader.NoteCreateDataType.GetConstructors(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        
        MelonLogger.Msg($"[GameTypeInspector] 생성자 수: {constructors.Length}");
        
        foreach (var constructor in constructors)
        {
            var parameters = constructor.GetParameters();
            var paramStr = string.Join(", ", parameters.Select(p => $"{p.ParameterType.Name} {p.Name}"));
            MelonLogger.Msg($"[GameTypeInspector]   - ({paramStr})");
        }
    }

    public static void InspectEnumType(Type enumType, string enumName)
    {
        if (enumType == null)
        {
            MelonLogger.Error($"[GameTypeInspector] {enumName} 타입이 null입니다.");
            return;
        }

        MelonLogger.Msg($"[GameTypeInspector] {enumName} Enum 값:");
        
        var values = Enum.GetValues(enumType);
        foreach (var value in values)
        {
            MelonLogger.Msg($"[GameTypeInspector]   - {value}");
        }
    }
}
```

## 타입 안전성

### 1. Null 체크

```csharp
// ✅ 좋은 예: 타입 사용 전 null 체크
if (GameTypeLoader.NoteCreateDataType == null)
{
    MelonLogger.Error("NoteCreateData 타입이 초기화되지 않았습니다.");
    return null;
}

var instance = Activator.CreateInstance(GameTypeLoader.NoteCreateDataType);
```

### 2. 타입 검증

```csharp
// ✅ 좋은 예: 타입 일치 검증
if (!GameTypeLoader.NoteCreateDataType.IsInstanceOfType(noteObj))
{
    MelonLogger.Error($"타입 불일치: 예상={GameTypeLoader.NoteCreateDataType.Name}, " +
                     $"실제={noteObj.GetType().Name}");
    return;
}
```

### 3. Enum 값 검증

```csharp
// ✅ 좋은 예: Enum 값 존재 확인
var enumValue = EnumValueHelper.GetEnumValue(enumType, valueName);
if (enumValue == null)
{
    MelonLogger.Warning($"Enum 값을 찾을 수 없습니다: {valueName}");
    // 기본값 사용 또는 대체 로직
}
```

## 성능 최적화

### Enum 캐싱 효과

```csharp
// 1000번 호출 시
// ❌ 캐싱 없음: ~100ms (Enum.Parse 1000번)
// ✅ 캐싱 사용: ~1ms (Dictionary 조회 1000번)

for (int i = 0; i < 1000; i++)
{
    var noteType = EnumValueHelper.GetNoteTypeId(NoteType.Tap);
}
```

## 베스트 프랙티스

### 1. 상수 사용

```csharp
// ✅ 좋은 예: 상수 사용
var enumValue = EnumValueHelper.GetEnumValue(enumType, EnumValueHelper.ENUM_TAP);

// ❌ 나쁜 예: 문자열 리터럴
var enumValue = EnumValueHelper.GetEnumValue(enumType, "Tap");
```

### 2. 초기화 순서

```csharp
// ✅ 좋은 예: BmsNoteConverter.Initialize()가 내부에서 GameTypeLoader.Initialize()까지 보장
BmsNoteConverter.Initialize();

// ❌ 나쁜 예: 순서 무시
BmsNoteConverter.Initialize(); // GameTypeLoader.NoteCreateDataType이 null!
```

### 3. 에러 처리

```csharp
// ✅ 좋은 예: null 체크 및 대체 로직
var enumValue = EnumValueHelper.GetEnumValue(enumType, valueName);
if (enumValue == null)
{
    // 대체 값 사용
    enumValue = EnumValueHelper.GetEnumValue(enumType, "Default");
}
```

## 참고 자료

- `GRC2/Helpers/EnumValueHelper.cs`
- `GRC2/Loaders/GameTypeLoader.cs`
- 과거 진단 도구: `GameTypeInspector` (현재 소스 기준 제거됨)
