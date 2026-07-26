using System;
using GRC2.Builders;
using GRC2.Parsers;
using IntiCreates;
using IntiCreates.RythmGame;
using IntiCreates.RythmGame.FairyMode;
using MelonLoader;

namespace GRC2.Processors
{
    using NoteCreateData = FairyNoteEditorLoader.NoteCreateData;

    /// <summary>
    /// 노트 프로세서 공통 유틸리티 클래스
    /// </summary>
    public static class NoteProcessorHelper
    {
        /// <summary>
        /// 끝 노트를 시작 노트의 connectNodeDataArray에 추가합니다.
        /// </summary>
        public static void AddEndNoteToConnectNodeArray(
            NoteCreateData startNote,
            BmsNote endNote,
            NoteDirectionIndex endDirection,
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

                var endNoteData = NoteCreateDataBuilder.CreateNoteCreateData(endNote);
                if (endNoteData == null)
                {
                    MelonLogger.Warning($"[{processorName}] 끝 노트 생성 실패: Time={endNote.Time:F3}, Lane={endNote.Lane}");
                    return;
                }

                // 시작 노트의 레인 정보를 끝 노트에 복사
                endNoteData.laneLeftRightID = startNote.laneLeftRightID;
                endNoteData.subLaneID = startNote.subLaneID;
                endNoteData.noteTypeID = NoteTypeId.Hold;
                endNoteData.directionIndex = endDirection;
                endNoteData.noteSize = NoteSize.Scale1;

                // turnDirection 복사 (홀드 끝 등).
                // 페어리 끝(1A/1B)은 CreateNoteCreateData에서 이미 턴 방향이 적용되어 있으므로 덮어쓰지 않습니다.
                if (copyTurnDirection && endNote.Type != NoteType.FairyEnd)
                {
                    endNoteData.turnDireciton = startNote.turnDireciton;
                }

                var existingArray = startNote.connectNodeDataArray;
                int existingLength = existingArray?.Length ?? 0;
                var newArray = new NoteCreateData[existingLength + 1];
                if (existingLength > 0)
                {
                    Array.Copy(existingArray, newArray, existingLength);
                }

                newArray[existingLength] = endNoteData;
                startNote.connectNodeDataArray = newArray;
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, $"[{processorName}]", "AddEndNoteToConnectNodeArray 오류");
            }
        }
    }
}
