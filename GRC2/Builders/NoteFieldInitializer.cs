using System;
using GRC2.Parsers;
using GRC2.Helpers;
using GRC2.Loaders;
using MelonLoader;

namespace GRC2.Builders
{
    /// <summary>
    /// NoteCreateData 필드 초기화를 담당하는 클래스
    /// </summary>
    public static class NoteFieldInitializer
    {
        /// <summary>
        /// directionIndex를 설정합니다.
        /// 페어리 끝(1A/1B): 위치는 레인 기준, 회전(1A/1B)은 turnDirection에서만 사용.
        /// </summary>
        public static object SetDirectionIndex(object noteCreateData, BmsNote bmsNote)
        {
            object directionIndexValue = null;
            if (bmsNote.Type == NoteType.FairyEnd)
            {
                // 페어리 끝 노트 위치는 곡선 깊이를 위해 CENTER_TOP. 1A/1B는 turnDirection으로만 반영.
                directionIndexValue = EnumValueHelper.GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, EnumValueHelper.GetEnumCenterTop());
                if (directionIndexValue == null) directionIndexValue = EnumValueHelper.GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, EnumValueHelper.GetEnumCenterMiddle());
                FieldAccessHelper.SetFieldValue(noteCreateData, FieldAccessHelper.FIELD_DIRECTION_INDEX, directionIndexValue);
            }
            else if (bmsNote.Direction.HasValue)
            {
                directionIndexValue = EnumValueHelper.GetDirectionIndex(bmsNote.Direction.Value);
                FieldAccessHelper.SetFieldValue(noteCreateData, FieldAccessHelper.FIELD_DIRECTION_INDEX, directionIndexValue);
            }
            else
            {
                // 홀드 노트의 경우 레인에 따라 direction 설정
                if (bmsNote.Type == NoteType.Hold)
                {
                    directionIndexValue = EnumValueHelper.GetDirectionIndexFromLane(bmsNote.Lane, bmsNote.IsLeft);
                }
                else
                {
                    // 기본값: CENTER_MIDDLE (홀드 끝 노트용)
                    directionIndexValue = EnumValueHelper.GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, EnumValueHelper.GetEnumCenterMiddle());
                }
                FieldAccessHelper.SetFieldValue(noteCreateData, FieldAccessHelper.FIELD_DIRECTION_INDEX, directionIndexValue);
            }
            return directionIndexValue;
        }

        /// <summary>
        /// turnDireciton을 설정합니다 (페어리 노트용).
        /// FairyEnd: 1A/1B(bmsNote.Direction)로만 설정. 그 외: directionIndex 문자열에서 LEFT/RIGHT 추출.
        /// </summary>
        public static void SetTurnDirection(object noteCreateData, object directionIndexValue, BmsNote bmsNote = null)
        {
            var turnDirecitonField = FieldAccessHelper.GetCachedField(FieldAccessHelper.FIELD_TURN_DIRECTION);
            if (turnDirecitonField == null) return;

            string turnDirection = null;
            if (bmsNote != null && bmsNote.Type == NoteType.FairyEnd && bmsNote.Direction.HasValue)
            {
                // 페어리 끝: 1A=Left, 1B=Right만 사용 (레인 무관)
                turnDirection = bmsNote.Direction.Value == NoteDirection.Left
                    ? EnumValueHelper.GetEnumLeft()
                    : EnumValueHelper.GetEnumRight();
            }
            else if (bmsNote != null && bmsNote.Type == NoteType.Fairy && bmsNote.EndNote != null && bmsNote.EndNote.Direction.HasValue)
            {
                // 페어리 시작 노트는 끝 노트가 지시하는 턴 방향을 따라감
                turnDirection = bmsNote.EndNote.Direction.Value == NoteDirection.Left
                    ? EnumValueHelper.GetEnumLeft()
                    : EnumValueHelper.GetEnumRight();
            }
            else if (directionIndexValue != null)
            {
                var directionStr = directionIndexValue.ToString();
                turnDirection = EnumValueHelper.GetEnumRight();
                if (directionStr.IndexOf("LEFT", StringComparison.OrdinalIgnoreCase) >= 0)
                    turnDirection = EnumValueHelper.GetEnumLeft();
                else if (directionStr.IndexOf("RIGHT", StringComparison.OrdinalIgnoreCase) >= 0)
                    turnDirection = EnumValueHelper.GetEnumRight();
            }

            if (turnDirection == null) return;
            var fieldType = turnDirecitonField.FieldType;
            if (fieldType.IsEnum)
            {
                var turnDirectionEnum = EnumValueHelper.GetEnumValue(fieldType, turnDirection);
                if (turnDirectionEnum != null)
                    FieldAccessHelper.SetFieldValue(noteCreateData, FieldAccessHelper.FIELD_TURN_DIRECTION, turnDirectionEnum);
            }
            else if (fieldType == typeof(string))
            {
                FieldAccessHelper.SetFieldValue(noteCreateData, FieldAccessHelper.FIELD_TURN_DIRECTION, turnDirection);
            }
        }

        /// <summary>
        /// slideEndFlickDirection을 설정합니다.
        /// </summary>
        public static void SetSlideEndFlickDirection(object noteCreateData)
        {
            var slideEndFlickField = FieldAccessHelper.GetCachedField(FieldAccessHelper.FIELD_SLIDE_END_FLICK_DIRECTION);
            
            if (slideEndFlickField != null)
            {
                var fieldType = slideEndFlickField.FieldType;
                
                // 필드 타입이 NoteDirectionIndex인 경우
                if (fieldType == GameTypeLoader.NoteDirectionIndexEnum || fieldType.Name == "NoteDirectionIndex")
                {
                    // NoteDirectionIndex에서 CENTER_MIDDLE 사용 (플릭이 아닌 기본값)
                    var centerMiddle = EnumValueHelper.GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, EnumValueHelper.GetEnumCenterMiddle());
                    if (centerMiddle != null)
                    {
                        FieldAccessHelper.SetFieldValue(noteCreateData, FieldAccessHelper.FIELD_SLIDE_END_FLICK_DIRECTION, centerMiddle);
                    }
                    else
                    {
                        MelonLogger.Warning("[NoteFieldInitializer] slideEndFlickDirection에 CENTER_MIDDLE을 설정할 수 없습니다.");
                    }
                }
                // 필드 타입이 SlideEndFlickDirection Enum인 경우
                else if (GameTypeLoader.SlideEndFlickDirectionEnum != null && fieldType == GameTypeLoader.SlideEndFlickDirectionEnum)
                {
                    var numFlick = EnumValueHelper.GetEnumValue(GameTypeLoader.SlideEndFlickDirectionEnum, EnumValueHelper.GetEnumNum());
                    if (numFlick != null)
                    {
                        FieldAccessHelper.SetFieldValue(noteCreateData, FieldAccessHelper.FIELD_SLIDE_END_FLICK_DIRECTION, numFlick);
                    }
                    else
                    {
                        // NUM을 찾지 못한 경우 다른 값들 시도
                        var alternativeNames = new[] { "None", "NONE", "Num", "Default", "DEFAULT" };
                        object enumValue = null;
                        
                        foreach (var name in alternativeNames)
                        {
                            enumValue = EnumValueHelper.GetEnumValue(GameTypeLoader.SlideEndFlickDirectionEnum, name);
                            if (enumValue != null)
                            {
                                MelonLogger.Msg($"[NoteFieldInitializer] slideEndFlickDirection '{name}' 값 사용");
                                break;
                            }
                        }
                        
                        if (enumValue == null)
                        {
                            // 모든 대안 실패 시 첫 번째 값 사용
                            try
                            {
                                var values = Enum.GetValues(GameTypeLoader.SlideEndFlickDirectionEnum);
                                if (values.Length > 0)
                                {
                                    enumValue = values.GetValue(0);
                                    MelonLogger.Warning($"[NoteFieldInitializer] slideEndFlickDirection 'NUM'을 찾을 수 없어 첫 번째 값 사용: {enumValue}");
                                }
                            }
                            catch (Exception ex)
                            {
                                ErrorLogger.LogWarning(ex, "[NoteFieldInitializer]", "slideEndFlickDirection Enum 값 가져오기 실패");
                            }
                        }
                        
                        if (enumValue != null)
                        {
                            FieldAccessHelper.SetFieldValue(noteCreateData, FieldAccessHelper.FIELD_SLIDE_END_FLICK_DIRECTION, enumValue);
                        }
                    }
                }
                else
                {
                    MelonLogger.Warning($"[NoteFieldInitializer] slideEndFlickDirection 필드 타입을 인식할 수 없습니다: {fieldType?.FullName ?? "null"}");
                }
            }
        }

        /// <summary>
        /// 기타 Boolean 필드들을 기본값으로 설정합니다.
        /// </summary>
        public static void SetDefaultBooleanFields(object noteCreateData)
        {
            FieldAccessHelper.SetFieldValue(noteCreateData, FieldAccessHelper.FIELD_IS_CRITICAL, false);
            FieldAccessHelper.SetFieldValue(noteCreateData, FieldAccessHelper.FIELD_IS_CREATED, false);
            FieldAccessHelper.SetFieldValue(noteCreateData, FieldAccessHelper.FIELD_IS_LAST, false);
            FieldAccessHelper.SetFieldValue(noteCreateData, FieldAccessHelper.FIELD_IS_THIS_BOOST_START, false);
            FieldAccessHelper.SetFieldValue(noteCreateData, FieldAccessHelper.FIELD_IS_SAME_TIMING_WITH_FLICK, false);
            FieldAccessHelper.SetFieldValue(noteCreateData, FieldAccessHelper.FIELD_IS_NEAR_BY_FRONT_FLICK4_TAP_OR_HOLD, false);
            FieldAccessHelper.SetFieldValue(noteCreateData, FieldAccessHelper.FIELD_IS_NEAR_BY_FRONT_SLIDE_END4_FLICK, false);
        }

        /// <summary>
        /// noteSize 필드를 설정합니다.
        /// </summary>
        public static void SetNoteSize(object noteCreateData)
        {
            var noteSize = EnumValueHelper.GetEnumValue(GameTypeLoader.NoteSizeEnum, EnumValueHelper.GetEnumScale1());
            FieldAccessHelper.SetFieldValue(noteCreateData, FieldAccessHelper.FIELD_NOTE_SIZE, noteSize);
        }
    }
}



























