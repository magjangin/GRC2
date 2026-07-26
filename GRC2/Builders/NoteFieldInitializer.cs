using GRC2.Parsers;
using GRC2.Helpers;
using IntiCreates;
using IntiCreates.RythmGame;

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
        public static NoteDirectionIndex SetDirectionIndex(FairyNoteEditorLoader.NoteCreateData noteCreateData, BmsNote bmsNote)
        {
            NoteDirectionIndex directionIndexValue;

            if (bmsNote.Type == NoteType.FairyEnd)
            {
                // 페어리 끝 노트 위치는 곡선 깊이를 위해 CENTER_TOP. 1A/1B는 turnDirection으로만 반영.
                directionIndexValue = NoteDirectionIndex.CENTER_TOP;
            }
            else if (bmsNote.Direction.HasValue)
            {
                directionIndexValue = EnumValueHelper.GetDirectionIndex(bmsNote.Direction.Value);
            }
            else if (bmsNote.Type == NoteType.Hold)
            {
                // 홀드 노트의 경우 레인에 따라 direction 설정
                directionIndexValue = EnumValueHelper.GetDirectionIndexFromLane(bmsNote.Lane, bmsNote.IsLeft);
            }
            else
            {
                // 기본값: CENTER_MIDDLE (홀드 끝 노트용)
                directionIndexValue = NoteDirectionIndex.CENTER_MIDDLE;
            }

            noteCreateData.directionIndex = directionIndexValue;
            return directionIndexValue;
        }

        /// <summary>
        /// turnDireciton을 설정합니다 (페어리 노트용).
        /// FairyEnd: 1A/1B(bmsNote.Direction)로만 설정. 그 외: directionIndex의 좌우 성분을 따름.
        /// </summary>
        public static void SetTurnDirection(
            FairyNoteEditorLoader.NoteCreateData noteCreateData,
            NoteDirectionIndex directionIndexValue,
            BmsNote bmsNote = null)
        {
            if (bmsNote != null && bmsNote.Type == NoteType.FairyEnd && bmsNote.Direction.HasValue)
            {
                // 페어리 끝: 1A=Left, 1B=Right만 사용 (레인 무관)
                noteCreateData.turnDireciton = ToTurnDirection(bmsNote.Direction.Value);
                return;
            }

            if (bmsNote != null && bmsNote.Type == NoteType.Fairy &&
                bmsNote.EndNote != null && bmsNote.EndNote.Direction.HasValue)
            {
                // 페어리 시작 노트는 끝 노트가 지시하는 턴 방향을 따라감
                noteCreateData.turnDireciton = ToTurnDirection(bmsNote.EndNote.Direction.Value);
                return;
            }

            noteCreateData.turnDireciton = EnumValueHelper.IsLeftDirection(directionIndexValue)
                ? TurnDirection.Left
                : TurnDirection.Right;
        }

        private static TurnDirection ToTurnDirection(NoteDirection direction)
        {
            return direction == NoteDirection.Left ? TurnDirection.Left : TurnDirection.Right;
        }
    }
}
