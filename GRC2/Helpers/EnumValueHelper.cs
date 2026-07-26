using GRC2.Parsers;
using IntiCreates.RythmGame;
using IntiCreates.RythmGame.FairyMode;

namespace GRC2.Helpers
{
    /// <summary>
    /// BMS 노트 속성을 게임 enum 값으로 변환하는 헬퍼 클래스
    /// </summary>
    public static class EnumValueHelper
    {
        public static NoteTypeId GetNoteTypeId(NoteType noteType)
        {
            switch (noteType)
            {
                case NoteType.Touch:
                    return NoteTypeId.Touch;
                case NoteType.Hold:
                case NoteType.HoldEnd:
                    return NoteTypeId.Hold;
                case NoteType.Flick:
                    return NoteTypeId.Flick;
                case NoteType.Fairy:
                case NoteType.FairyEnd:
                    return NoteTypeId.Fairy;
                default:
                    return NoteTypeId.Touch;
            }
        }

        public static NoteDirectionIndex GetDirectionIndex(NoteDirection direction)
        {
            switch (direction)
            {
                case NoteDirection.Left:
                    return NoteDirectionIndex.LEFT_MIDDLE;   // 03: 왼쪽 → 왼쪽 중간
                case NoteDirection.LeftUp:
                    return NoteDirectionIndex.LEFT_TOP;
                case NoteDirection.Up:
                    return NoteDirectionIndex.CENTER_TOP;
                case NoteDirection.RightUp:
                    return NoteDirectionIndex.RIGHT_TOP;
                case NoteDirection.Right:
                    return NoteDirectionIndex.RIGHT_MIDDLE;  // 07: 오른쪽 → 오른쪽 중간
                case NoteDirection.RightDown:
                    return NoteDirectionIndex.RIGHT_BOTTOM;
                case NoteDirection.Down:
                    return NoteDirectionIndex.CENTER_BOTTOM;
                case NoteDirection.LeftDown:
                    return NoteDirectionIndex.LEFT_BOTTOM;
                default:
                    return NoteDirectionIndex.CENTER_MIDDLE;
            }
        }

        /// <summary>
        /// 레인에 따라 direction을 결정합니다. Lane 0: Bottom, Lane 1: Middle, Lane 2: Top
        /// </summary>
        public static NoteDirectionIndex GetDirectionIndexFromLane(int lane, bool isLeft)
        {
            if (isLeft)
            {
                switch (lane)
                {
                    case 0: return NoteDirectionIndex.LEFT_BOTTOM;
                    case 2: return NoteDirectionIndex.LEFT_TOP;
                    default: return NoteDirectionIndex.LEFT_MIDDLE;
                }
            }

            switch (lane)
            {
                case 0: return NoteDirectionIndex.RIGHT_BOTTOM;
                case 2: return NoteDirectionIndex.RIGHT_TOP;
                default: return NoteDirectionIndex.RIGHT_MIDDLE;
            }
        }

        /// <summary>
        /// Lane 0-2를 Lane_1 ~ Lane_3으로 매핑합니다 (게임은 1-based).
        /// </summary>
        public static NoteSubLaneType GetSubLaneType(int lane)
        {
            switch (lane)
            {
                case 1: return NoteSubLaneType.Lane_2;
                case 2: return NoteSubLaneType.Lane_3;
                default: return NoteSubLaneType.Lane_1;
            }
        }

        public static NoteLaneLeftRight GetLaneLeftRight(bool isLeft)
        {
            return isLeft ? NoteLaneLeftRight.Left : NoteLaneLeftRight.Right;
        }

        /// <summary>
        /// subLaneID를 BMS 레인 인덱스(0-2)로 되돌립니다.
        /// </summary>
        public static int ToLaneIndex(NoteSubLaneType subLane)
        {
            switch (subLane)
            {
                case NoteSubLaneType.Lane_2: return 1;
                case NoteSubLaneType.Lane_3: return 2;
                default: return 0;
            }
        }

        /// <summary>
        /// noteTypeID를 BMS 노트 타입으로 되돌립니다. 대응하는 타입이 없으면 null입니다.
        /// </summary>
        public static NoteType? ToBmsNoteType(NoteTypeId noteTypeId)
        {
            switch (noteTypeId)
            {
                case NoteTypeId.Fairy:
                    return NoteType.Fairy;
                case NoteTypeId.Hold:
                case NoteTypeId.Hold_Middle:
                    return NoteType.Hold;
                case NoteTypeId.Touch:
                    return NoteType.Touch;
                case NoteTypeId.Flick:
                    return NoteType.Flick;
                default:
                    return null;
            }
        }

        /// <summary>
        /// directionIndex가 왼쪽 계열인지 판정합니다 (CENTER 계열은 오른쪽으로 취급).
        /// </summary>
        public static bool IsLeftDirection(NoteDirectionIndex direction)
        {
            return direction == NoteDirectionIndex.LEFT_BOTTOM
                || direction == NoteDirectionIndex.LEFT_MIDDLE
                || direction == NoteDirectionIndex.LEFT_TOP;
        }
    }
}
