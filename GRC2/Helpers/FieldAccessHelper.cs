using System;
using System.Collections.Generic;
using System.Reflection;
using GRC2.Loaders;
using MelonLoader;

namespace GRC2.Helpers
{
    /// <summary>
    /// 필드 접근 및 캐싱을 담당하는 헬퍼 클래스
    /// </summary>
    public static class FieldAccessHelper
    {
        // 필드 정보 캐시 (성능 개선)
        private static Dictionary<string, FieldInfo> _fieldCache = new Dictionary<string, FieldInfo>();

        // 필드 이름 상수
        public const string FIELD_PERFECT_SAMPLE = "perfectSample";
        public const string FIELD_LANE_LEFT_RIGHT_ID = "laneLeftRightID";
        public const string FIELD_SUB_LANE_ID = "subLaneID";
        public const string FIELD_NOTE_TYPE_ID = "noteTypeID";
        public const string FIELD_DIRECTION_INDEX = "directionIndex";
        public const string FIELD_TURN_DIRECTION = "turnDireciton";
        public const string FIELD_NOTE_SIZE = "noteSize";
        public const string FIELD_SLIDE_END_FLICK_DIRECTION = "slideEndFlickDirection";
        public const string FIELD_CONNECT_NODE_DATA_ARRAY = "connectNodeDataArray";
        public const string FIELD_IS_CRITICAL = "isCritical";
        public const string FIELD_IS_CREATED = "isCreated";
        public const string FIELD_IS_LAST = "isLast";
        public const string FIELD_IS_THIS_BOOST_START = "isThisBoostStart";
        public const string FIELD_IS_SAME_TIMING_WITH_FLICK = "isSameTimingWithFlick";
        public const string FIELD_IS_NEAR_BY_FRONT_FLICK4_TAP_OR_HOLD = "isNearByFrontFlick4TapOrHold";
        public const string FIELD_IS_NEAR_BY_FRONT_SLIDE_END4_FLICK = "isNearByFrontSlideEnd4Flick";

        /// <summary>
        /// 캐시된 필드 정보를 가져옵니다.
        /// </summary>
        public static FieldInfo GetCachedField(string fieldName)
        {
            if (GameTypeLoader.NoteCreateDataType == null)
            {
                return null;
            }

            // TryGetValue를 사용하여 ContainsKey + [] 패턴 최적화
            if (!_fieldCache.TryGetValue(fieldName, out var cachedField))
            {
                var field = GameTypeLoader.NoteCreateDataType.GetField(fieldName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                _fieldCache[fieldName] = field;
                return field;
            }

            return cachedField;
        }

        /// <summary>
        /// 필드 값을 설정합니다. 필드 정보는 캐시됩니다.
        /// </summary>
        public static void SetFieldValue(object obj, string fieldName, object value)
        {
            try
            {
                var field = GetCachedField(fieldName);
                if (field != null)
                {
                    field.SetValue(obj, value);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[FieldAccessHelper] 필드 '{fieldName}' 설정 실패: {ex.Message}");
            }
        }

        /// <summary>
        /// 필드 값을 가져옵니다.
        /// </summary>
        public static object GetFieldValue(object obj, string fieldName)
        {
            try
            {
                var field = GetCachedField(fieldName);
                if (field != null)
                {
                    return field.GetValue(obj);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"[FieldAccessHelper] 필드 '{fieldName}' 가져오기 실패: {ex.Message}");
            }
            return null;
        }
    }
}

