using System;
using System.Collections.Generic;
using GRC2.Parsers;
using GRC2.Helpers;
using GRC2.Loaders;
using MelonLoader;

namespace GRC2.Builders
{
    /// <summary>
    /// NoteCreateData 생성 및 필드 설정을 담당하는 클래스
    /// </summary>
    public static partial class NoteCreateDataBuilder
    {
        // 샘플레이트 (게임의 오디오 샘플레이트, 일반적으로 48000)
        private const int SAMPLE_RATE = 48000;
        
        private static Dictionary<string, BmsNote> _timeToBmsNoteCache = null;
        private static List<BmsNote> _cachedBmsNotes = null;

        public static object CreateNoteCreateData(BmsNote bmsNote)
        {
// ... (skipping for a moment to replace the actual method content instead of the whole file, wait I should use line numbers correctly)
            try
            {
                if (GameTypeLoader.NoteCreateDataType == null)
                {
                    MelonLogger.Error("[NoteCreateDataBuilder] NoteCreateData 타입이 초기화되지 않았습니다.");
                    return null;
                }

                if (bmsNote == null)
                {
                    MelonLogger.Error("[NoteCreateDataBuilder] bmsNote가 null입니다.");
                    return null;
                }

                // perfectSample 계산
                var perfectSample = (int)(bmsNote.Time * SAMPLE_RATE);
                
                // 노트 타입 Enum 값 가져오기
                var noteTypeId = EnumValueHelper.GetNoteTypeId(bmsNote.Type);
                if (noteTypeId == null)
                {
                    MelonLogger.Error("[NoteCreateDataBuilder] noteTypeId를 가져올 수 없습니다.");
                    return null;
                }
                
                // laneLeftRight Enum 값 가져오기
                var laneLeftRight = EnumValueHelper.GetEnumValue(GameTypeLoader.NoteLaneLeftRightEnum, 
                    bmsNote.IsLeft ? EnumValueHelper.GetEnumLeft() : EnumValueHelper.GetEnumRight());
                if (laneLeftRight == null)
                {
                    MelonLogger.Error("[NoteCreateDataBuilder] laneLeftRightID Enum 값을 가져올 수 없습니다.");
                    return null;
                }
                
                // Boolean 값 (Left/Right)
                bool isLeftBool = bmsNote.IsLeft;
                
                // 생성자 찾기 시도
                object noteCreateData = NoteConstructorHelper.TryFindConstructor(
                    GameTypeLoader.NoteCreateDataType,
                    bmsNote,
                    perfectSample,
                    noteTypeId,
                    isLeftBool);
                
                // 기본 생성자 사용 여부 추적
                bool usedDefaultConstructor = false;
                
                // 생성자를 찾지 못한 경우 기본 생성자 사용
                if (noteCreateData == null)
                {
                    noteCreateData = Activator.CreateInstance(GameTypeLoader.NoteCreateDataType);
                    if (noteCreateData == null)
                    {
                        MelonLogger.Error("[NoteCreateDataBuilder] NoteCreateData 인스턴스 생성 실패");
                        return null;
                    }
                    usedDefaultConstructor = true;
                }

                // perfectSample 설정
                SetPerfectSample(noteCreateData, perfectSample, usedDefaultConstructor);

                // laneLeftRightID 설정
                FieldAccessHelper.SetFieldValue(noteCreateData, FieldAccessHelper.FIELD_LANE_LEFT_RIGHT_ID, laneLeftRight);

                // subLaneID 설정
                var subLane = EnumValueHelper.GetSubLaneType(bmsNote.Lane);
                if (subLane == null)
                {
                    MelonLogger.Error($"[NoteCreateDataBuilder] subLaneID를 가져올 수 없습니다. Lane={bmsNote.Lane}");
                    return null;
                }
                FieldAccessHelper.SetFieldValue(noteCreateData, FieldAccessHelper.FIELD_SUB_LANE_ID, subLane);

                // noteTypeID 설정
                FieldAccessHelper.SetFieldValue(noteCreateData, FieldAccessHelper.FIELD_NOTE_TYPE_ID, noteTypeId);

                // directionIndex 설정
                object directionIndexValue = NoteFieldInitializer.SetDirectionIndex(noteCreateData, bmsNote);

                // turnDireciton 설정 (페어리 노트용). FairyEnd는 1A/1B만 사용하므로 bmsNote 전달
                if (bmsNote.Type == NoteType.Fairy || bmsNote.Type == NoteType.FairyEnd)
                {
                    NoteFieldInitializer.SetTurnDirection(noteCreateData, directionIndexValue, bmsNote);
                }

                // noteSize 설정
                NoteFieldInitializer.SetNoteSize(noteCreateData);

                // slideEndFlickDirection 설정
                NoteFieldInitializer.SetSlideEndFlickDirection(noteCreateData);

                // 기타 Boolean 필드들
                NoteFieldInitializer.SetDefaultBooleanFields(noteCreateData);

                // 최종 검증
                ValidatePerfectSample(noteCreateData, perfectSample, bmsNote);

                return noteCreateData;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(ex, "[NoteCreateDataBuilder]", "CreateNoteCreateData 오류");
                return null;
            }
        }

    }
}
