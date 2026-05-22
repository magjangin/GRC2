using System;
using System.Reflection;
using GRC2.Parsers;
using MelonLoader;

namespace GRC2.Processors
{
    /// <summary>
    /// 노트 프로세서 공통 유틸리티 클래스
    /// </summary>
    public static class NoteProcessorHelper
    {
        /// <summary>
        /// 끝 노트를 connectNodeDataArray에 추가합니다.
        /// </summary>
        public static void AddEndNoteToConnectNodeArray(
            object startNote,
            BmsNote endNote,
            Type noteCreateDataType,
            Type noteDirectionIndexEnum,
            Type noteSizeEnum,
            Func<BmsNote, object> createNoteCreateData,
            Func<Type, string, object> getEnumValue,
            Action<object, string, object> setFieldValue,
            string endDirection,
            string processorName,
            bool copyTurnDirection = false)
        {
            try
            {
                if (startNote == null)
                {
                    MelonLogger.Warning($"[{processorName}] startNote가 null입니다. 끝 노트 추가 실패.");
                    return;
                }

                var connectNodeArrayField = noteCreateDataType.GetField("connectNodeDataArray",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (connectNodeArrayField == null)
                {
                    MelonLogger.Warning($"[{processorName}] connectNodeDataArray 필드를 찾을 수 없습니다.");
                    return;
                }

                var existingArray = connectNodeArrayField.GetValue(startNote) as Array;
                int existingLength = existingArray != null ? existingArray.Length : 0;
                int newLength = existingLength + 1;
                
                MelonLogger.Msg($"[{processorName}] connectNodeDataArray에 끝 노트 추가 시도: 기존 길이={existingLength}, 새 길이={newLength}");

                var elementType = noteCreateDataType;
                var newArray = Array.CreateInstance(elementType, newLength);

                if (existingArray != null)
                {
                    Array.Copy(existingArray, newArray, existingArray.Length);
                }

                // 끝 노트 생성
                var endNoteData = createNoteCreateData(endNote);
                if (endNoteData == null)
                {
                    MelonLogger.Warning($"[{processorName}] 끝 노트 생성 실패: Time={endNote.Time:F3}, Lane={endNote.Lane}");
                    return;
                }
                
                // 시작 노트의 필드들을 끝 노트에 복사 (laneLeftRightID, subLaneID 등)
                var laneLeftRightField = noteCreateDataType.GetField("laneLeftRightID",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (laneLeftRightField != null)
                {
                    var startLaneLeftRight = laneLeftRightField.GetValue(startNote);
                    if (startLaneLeftRight != null)
                    {
                        setFieldValue(endNoteData, "laneLeftRightID", startLaneLeftRight);
                        MelonLogger.Msg($"[{processorName}] 끝 노트 laneLeftRightID 복사: {startLaneLeftRight}");
                    }
                }
                
                var subLaneField = noteCreateDataType.GetField("subLaneID",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (subLaneField != null)
                {
                    var startSubLane = subLaneField.GetValue(startNote);
                    if (startSubLane != null)
                    {
                        setFieldValue(endNoteData, "subLaneID", startSubLane);
                        MelonLogger.Msg($"[{processorName}] 끝 노트 subLaneID 복사: {startSubLane}");
                    }
                }
                
                // noteTypeID도 명시적으로 Hold로 설정 (이미 createNoteCreateData에서 설정되었지만 확실히)
                var noteTypeIdField = noteCreateDataType.GetField("noteTypeID",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (noteTypeIdField != null)
                {
                    var holdTypeId = getEnumValue(Loaders.GameTypeLoader.NoteTypeIdEnum, "Hold");
                    if (holdTypeId != null)
                    {
                        setFieldValue(endNoteData, "noteTypeID", holdTypeId);
                        MelonLogger.Msg($"[{processorName}] 끝 노트 noteTypeID 설정: Hold");
                    }
                }
                
                // direction 설정
                // 페어리 끝(1A/1B)도 곡선의 물리적 길이(깊이)를 갖기 위해 전달된 endDirection(CENTER_TOP)을 적용
                var directionValue = getEnumValue(noteDirectionIndexEnum, endDirection);
                if (directionValue == null)
                {
                    MelonLogger.Warning($"[{processorName}] {endDirection}을 찾을 수 없어 CENTER_MIDDLE 사용");
                    directionValue = getEnumValue(noteDirectionIndexEnum, "CENTER_MIDDLE");
                }
                setFieldValue(endNoteData, "directionIndex", directionValue);
                
                var scale1 = getEnumValue(noteSizeEnum, "Scale1");
                setFieldValue(endNoteData, "noteSize", scale1);
                
                // Boolean 필드들도 설정 (끝 노트는 메인 배열에 없으므로 isLast는 false)
                setFieldValue(endNoteData, "isLast", false);
                setFieldValue(endNoteData, "isCritical", false);
                setFieldValue(endNoteData, "isCreated", false);
                
                // perfectSample이 제대로 설정되었는지 확인 및 로그
                var perfectSampleField = noteCreateDataType.GetField("perfectSample",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (perfectSampleField != null)
                {
                    var endPerfectSample = perfectSampleField.GetValue(endNoteData);
                    var startPerfectSample = perfectSampleField.GetValue(startNote);
                    MelonLogger.Msg($"[{processorName}] 끝 노트 perfectSample: {endPerfectSample} (시작 노트: {startPerfectSample}, 끝 시간: {endNote.Time:F3})");
                }

                // turnDirection 복사 (홀드 끝 등). 페어리 끝(1A/1B)은 createNoteCreateData에서 이미 턴 방향 적용 — 덮어쓰지 않음
                if (copyTurnDirection && endNote.Type != NoteType.FairyEnd)
                {
                    var startTurnDirecitonField = noteCreateDataType.GetField("turnDireciton",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (startTurnDirecitonField != null)
                    {
                        var startTurnDireciton = startTurnDirecitonField.GetValue(startNote);
                        if (startTurnDireciton != null)
                        {
                            setFieldValue(endNoteData, "turnDireciton", startTurnDireciton);
                        }
                    }
                }
                else if (endNote.Type == NoteType.FairyEnd)
                {
                    var turnField = noteCreateDataType.GetField("turnDireciton", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    var turnVal = turnField?.GetValue(endNoteData);
                    MelonLogger.Msg($"[{processorName}][디버그] 페어리 끝 노트 turnDirection 유지(덮어쓰지 않음): {turnVal} (1A=Left, 1B=Right)");
                }

                newArray.SetValue(endNoteData, newLength - 1);
                connectNodeArrayField.SetValue(startNote, newArray);
                
                // 검증: 실제로 추가되었는지 확인
                var verifyArray = connectNodeArrayField.GetValue(startNote) as Array;
                if (verifyArray != null && verifyArray.Length == newLength)
                {
                    MelonLogger.Msg($"[{processorName}] ✓ connectNodeDataArray에 끝 노트 추가 성공: 최종 길이={verifyArray.Length}");
                    
                    // 끝 노트의 상세 정보 확인
                    var endNoteInArray = verifyArray.GetValue(newLength - 1);
                    if (endNoteInArray != null)
                    {
                        var endNoteTypeField = noteCreateDataType.GetField("noteTypeID",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        var endPerfectSampleField = noteCreateDataType.GetField("perfectSample",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        var endDirectionField = noteCreateDataType.GetField("directionIndex",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        
                        var endNoteType = endNoteTypeField?.GetValue(endNoteInArray);
                        var endPerfectSample = endPerfectSampleField?.GetValue(endNoteInArray);
                        var endDirectionValue = endDirectionField?.GetValue(endNoteInArray);
                        
                        MelonLogger.Msg($"[{processorName}] 끝 노트 상세: noteTypeID={endNoteType}, perfectSample={endPerfectSample}, directionIndex={endDirectionValue}");
                    }
                }
                else
                {
                    MelonLogger.Warning($"[{processorName}] ⚠️ connectNodeDataArray 추가 검증 실패: 예상 길이={newLength}, 실제 길이={verifyArray?.Length ?? 0}");
                }
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, $"[{processorName}]", "AddEndNoteToConnectNodeArray 오류");
            }
        }
    }
}


















