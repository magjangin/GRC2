using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GRC2.Parsers;
using GRC2.Loaders;
using MelonLoader;

namespace GRC2.Helpers
{
    /// <summary>
    /// Enum 값 가져오기 및 변환을 담당하는 헬퍼 클래스
    /// </summary>
    public static class EnumValueHelper
    {
        // Enum 값 이름 상수
        private const string ENUM_LEFT = "Left";
        private const string ENUM_RIGHT = "Right";
        private const string ENUM_CENTER_MIDDLE = "CENTER_MIDDLE";
        private const string ENUM_CENTER_TOP = "CENTER_TOP";
        private const string ENUM_CENTER_BOTTOM = "CENTER_BOTTOM";
        private const string ENUM_LEFT_BOTTOM = "LEFT_BOTTOM";
        private const string ENUM_LEFT_MIDDLE = "LEFT_MIDDLE";
        private const string ENUM_LEFT_TOP = "LEFT_TOP";
        private const string ENUM_RIGHT_BOTTOM = "RIGHT_BOTTOM";
        private const string ENUM_RIGHT_MIDDLE = "RIGHT_MIDDLE";
        private const string ENUM_RIGHT_TOP = "RIGHT_TOP";
        private const string ENUM_TOUCH = "Touch";
        private const string ENUM_HOLD = "Hold";
        private const string ENUM_FLICK = "Flick";
        private const string ENUM_FAIRY = "Fairy";
        private const string ENUM_SCALE1 = "Scale1";
        private const string ENUM_NUM = "NUM";

        // 성능 최적화: Enum 값 캐시 (Type + valueName을 키로 사용)
        private static Dictionary<string, object> _enumValueCache = new Dictionary<string, object>();

        public static object GetEnumValue(Type enumType, string valueName)
        {
            try
            {
                if (enumType == null)
                {
                    MelonLogger.Error($"[EnumValueHelper] Enum 타입이 null입니다. '{valueName}' 값을 가져올 수 없습니다.");
                    return null;
                }

                if (!enumType.IsEnum)
                {
                    MelonLogger.Error($"[EnumValueHelper] {enumType.Name}은 Enum 타입이 아닙니다.");
                    return null;
                }

                // 성능 최적화: 캐시 키 생성 (Type.FullName + valueName)
                var cacheKey = $"{enumType.FullName}:{valueName}";
                
                // 캐시에서 먼저 확인
                if (_enumValueCache.TryGetValue(cacheKey, out var cachedValue))
                {
                    return cachedValue;
                }

                // 캐시에 없으면 파싱하고 캐시에 저장
                var enumValue = Enum.Parse(enumType, valueName);
                _enumValueCache[cacheKey] = enumValue;
                return enumValue;
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[EnumValueHelper] Enum 값 '{valueName}'을 {enumType?.Name ?? "NULL"}에서 찾을 수 없습니다: {ex.Message}");
                if (enumType != null && enumType.IsEnum)
                {
                    try
                    {
                        var values = Enum.GetValues(enumType);
                        var availableValues = new List<string>();
                        foreach (var val in values)
                        {
                            availableValues.Add(val.ToString());
                        }
                        MelonLogger.Msg($"[EnumValueHelper] {enumType.Name}에서 사용 가능한 값들: {string.Join(", ", availableValues)}");
                        
                        if (values.Length > 0)
                        {
                            var fallbackValue = values.GetValue(0);
                            MelonLogger.Warning($"[EnumValueHelper] 대체 값으로 '{fallbackValue}' 사용");
                            return fallbackValue; // 첫 번째 값 반환
                        }
                    }
                    catch (Exception ex2)
                    {
                        MelonLogger.Error($"[EnumValueHelper] Enum 값 목록 가져오기 실패: {ex2.Message}");
                    }
                }
                return null;
            }
        }

        public static object GetNoteTypeId(NoteType noteType)
        {
            switch (noteType)
            {
                case NoteType.Touch:
                    return GetEnumValue(GameTypeLoader.NoteTypeIdEnum, ENUM_TOUCH);
                case NoteType.Hold:
                case NoteType.HoldEnd:
                    return GetEnumValue(GameTypeLoader.NoteTypeIdEnum, ENUM_HOLD);
                case NoteType.Flick:
                    return GetEnumValue(GameTypeLoader.NoteTypeIdEnum, ENUM_FLICK);
                case NoteType.Fairy:
                case NoteType.FairyEnd:
                    return GetEnumValue(GameTypeLoader.NoteTypeIdEnum, ENUM_FAIRY);
                default:
                    return GetEnumValue(GameTypeLoader.NoteTypeIdEnum, ENUM_TOUCH);
            }
        }

        public static object GetDirectionIndex(NoteDirection direction)
        {
            switch (direction)
            {
                case NoteDirection.Left:
                    return GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, ENUM_LEFT_MIDDLE);  // 03: 왼쪽 → 왼쪽 중간
                case NoteDirection.LeftUp:
                    return GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, ENUM_LEFT_TOP);
                case NoteDirection.Up:
                    return GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, ENUM_CENTER_TOP);
                case NoteDirection.RightUp:
                    return GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, ENUM_RIGHT_TOP);
                case NoteDirection.Right:
                    return GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, ENUM_RIGHT_MIDDLE);  // 07: 오른쪽 → 오른쪽 중간
                case NoteDirection.RightDown:
                    return GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, ENUM_RIGHT_BOTTOM);
                case NoteDirection.Down:
                    return GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, ENUM_CENTER_BOTTOM);
                case NoteDirection.LeftDown:
                    return GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, ENUM_LEFT_BOTTOM);
                default:
                    return GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, ENUM_CENTER_MIDDLE);
            }
        }

        public static object GetDirectionIndexFromLane(int lane, bool isLeft)
        {
            // 레인에 따라 direction 결정
            // Lane 0: Bottom, Lane 1: Middle, Lane 2: Top
            if (isLeft)
            {
                switch (lane)
                {
                    case 0: return GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, ENUM_LEFT_BOTTOM);
                    case 1: return GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, ENUM_LEFT_MIDDLE);
                    case 2: return GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, ENUM_LEFT_TOP);
                    default: return GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, ENUM_LEFT_MIDDLE);
                }
            }
            else
            {
                switch (lane)
                {
                    case 0: return GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, ENUM_RIGHT_BOTTOM);
                    case 1: return GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, ENUM_RIGHT_MIDDLE);
                    case 2: return GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, ENUM_RIGHT_TOP);
                    default: return GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, ENUM_RIGHT_MIDDLE);
                }
            }
        }

        public static object GetSubLaneType(int lane)
        {
            // Lane에 따라 SubLane 결정
            // Lane 0-2는 Lane_1 ~ Lane_3로 매핑 (게임은 1-based)
            var subLaneName = $"Lane_{lane + 1}";
            return GetEnumValue(GameTypeLoader.NoteSubLaneTypeEnum, subLaneName);
        }

        // 상수 접근을 위한 public 메서드
        public static string GetEnumLeft() => ENUM_LEFT;
        public static string GetEnumRight() => ENUM_RIGHT;
        public static string GetEnumCenterMiddle() => ENUM_CENTER_MIDDLE;
        public static string GetEnumCenterTop() => ENUM_CENTER_TOP;
        public static string GetEnumScale1() => ENUM_SCALE1;
        public static string GetEnumNum() => ENUM_NUM;
    }
}

