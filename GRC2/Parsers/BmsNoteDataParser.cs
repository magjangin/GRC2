using System.Collections.Generic;

namespace GRC2.Parsers
{
    /// <summary>
    /// BMS 노트 데이터 파싱 유틸리티
    /// </summary>
    public static class BmsNoteDataParser
    {
        // BMS 채널 → 게임 레인 매핑
        // 16, 11, 12는 왼쪽 레인 1, 2, 3
        // 14, 15, 18은 오른쪽 레인 1, 2, 3
        public static readonly Dictionary<int, (int Lane, bool IsLeft)> ChannelToLaneMap = new Dictionary<int, (int, bool)>
        {
            { 16, (0, true) },   // Left 1 (Lane 0)
            { 11, (1, true) },   // Left 2 (Lane 1)
            { 12, (2, true) },   // Left 3 (Lane 2)
            { 14, (0, false) }, // Right 1 (Lane 0)
            { 15, (1, false) }, // Right 2 (Lane 1)
            { 18, (2, false) }  // Right 3 (Lane 2)
        };

        /// <summary>
        /// 노트 데이터 파싱
        /// </summary>
        public static List<BmsNote> ParseNoteData(int measure, int channel, string data)
        {
            var notes = new List<BmsNote>();
            
            if (!ChannelToLaneMap.ContainsKey(channel))
                return notes;

            var (lane, isLeft) = ChannelToLaneMap[channel];
            var hexValues = ParseHexData(data);
            var measureLength = hexValues.Count;

            for (int i = 0; i < hexValues.Count; i++)
            {
                var value = hexValues[i];
                if (value == 0) continue;

                // measure 내 위치 계산 (0.0 ~ 1.0)
                var positionInMeasure = (float)i / measureLength;
                var tick = measure + positionInMeasure;

                NoteDirection? direction;
                var noteType = GetNoteType(value, out direction);
                
                var note = new BmsNote
                {
                    Channel = channel,
                    Tick = tick,
                    Lane = lane,
                    IsLeft = isLeft,
                    Type = noteType,
                    Direction = direction
                };

                notes.Add(note);
            }

            return notes;
        }

        /// <summary>
        /// 16진수 데이터 파싱
        /// </summary>
        public static List<int> ParseHexData(string data)
        {
            var values = new List<int>();
            var trimmed = data.Trim();
            
            // 2자리씩 16진수 파싱
            for (int i = 0; i < trimmed.Length; i += 2)
            {
                if (i + 1 < trimmed.Length)
                {
                    var hex = trimmed.Substring(i, 2);
                    if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out int value))
                    {
                        values.Add(value);
                    }
                }
            }

            return values;
        }

        /// <summary>
        /// WAV 인덱스로 노트 타입 결정
        /// </summary>
        public static NoteType GetNoteType(int value, out NoteDirection? direction)
        {
            direction = null;
            
            // WAV 인덱스 기반 노트 타입 구분
            if (value == 0x01) return NoteType.Touch;      // touch
            if (value == 0x02) return NoteType.Hold;       // Hold
            if (value == 0x19) return NoteType.HoldEnd;    // Hold end
            if (value == 0x1A) { direction = NoteDirection.Left; return NoteType.FairyEnd; }  // fairy 끝노트 (1A=Left 턴)
            if (value == 0x1B) { direction = NoteDirection.Right; return NoteType.FairyEnd; } // fairy 끝노트 (1B=Right 턴)
            
            // Flick (03-0A)
            if (value >= 0x03 && value <= 0x0A)
            {
                direction = GetFlickDirection(value);
                return NoteType.Flick;
            }
            
            // Fairy (11-18)
            if (value >= 0x11 && value <= 0x18)
            {
                direction = GetFairyDirection(value);
                return NoteType.Fairy;
            }
            
            return NoteType.Touch;  // 기본값
        }

        /// <summary>
        /// 플릭 방향 결정
        /// </summary>
        public static NoteDirection GetFlickDirection(int value)
        {
            // 03: 왼쪽, 04: 왼쪽위, 05: 위, 06: 오른쪽위, 07: 오른쪽, 08: 오른쪽아래, 09: 아래, 0A: 왼쪽아래
            switch (value)
            {
                case 0x03: return NoteDirection.Left;
                case 0x04: return NoteDirection.LeftUp;
                case 0x05: return NoteDirection.Up;
                case 0x06: return NoteDirection.RightUp;
                case 0x07: return NoteDirection.Right;
                case 0x08: return NoteDirection.RightDown;
                case 0x09: return NoteDirection.Down;
                case 0x0A: return NoteDirection.LeftDown;
                default: return NoteDirection.None;
            }
        }

        /// <summary>
        /// 페어리 방향 결정 (11~18 = 시계 반대 방향: Left→LeftUp→Up→RightUp→Right→RightDown→Down→LeftDown)
        /// </summary>
        public static NoteDirection GetFairyDirection(int value)
        {
            // 11: 왼쪽, 12: 왼쪽위, 13: 위, 14: 오른쪽위, 15: 오른쪽, 16: 오른쪽아래, 17: 아래, 18: 왼쪽아래 (반시계)
            switch (value)
            {
                case 0x11: return NoteDirection.Left;
                case 0x12: return NoteDirection.LeftUp;
                case 0x13: return NoteDirection.Up;
                case 0x14: return NoteDirection.RightUp;
                case 0x15: return NoteDirection.Right;
                case 0x16: return NoteDirection.RightDown;
                case 0x17: return NoteDirection.Down;
                case 0x18: return NoteDirection.LeftDown;
                default: return NoteDirection.None;
            }
        }
    }
}


























