namespace GRC2.Parsers
{
    /// <summary>
    /// BMS 노트 데이터 모델
    /// </summary>
    public class BmsNote
    {
        public int Channel { get; set; }
        public float Tick { get; set; }  // measure 단위 (예: 1.5 = measure 1의 중간)
        public float Time { get; set; }  // 초 단위
        public int Lane { get; set; }    // 게임 레인 (1-6)
        public bool IsLeft { get; set; }  // 왼쪽 레인인지
        public NoteType Type { get; set; }
        public NoteDirection? Direction { get; set; }
        public float Duration { get; set; }  // 홀드 길이 (초 단위)
        
        // 홀드/페어리 노트 연결 참조
        public BmsNote StartNote { get; set; } // 끝 노트인 경우 시작 노트 참조
        public BmsNote EndNote { get; set; }   // 시작 노트인 경우 끝 노트 참조
        
        // BPM 정보 (매칭 시간 오차 범위 계산용)
        public float BaseBpm { get; set; } // 기본 BPM (곡의 초기 BPM)
    }

    /// <summary>
    /// 노트 타입 열거형
    /// </summary>
    public enum NoteType
    {
        Touch,      // 일반 노트 (01)
        Hold,       // 홀드 노트 (02)
        HoldEnd,    // 홀드 끝 노트 (19)
        Flick,      // 플릭 노트 (03-0A)
        Fairy,      // 페어리 노트 (11-18)
        FairyEnd    // 페어리 끝 노트 (1A-1B)
    }

    /// <summary>
    /// 노트 방향 열거형
    /// </summary>
    public enum NoteDirection
    {
        None,
        Left,           // 왼쪽
        LeftUp,         // 왼쪽 위
        Up,             // 위
        RightUp,        // 오른쪽 위
        Right,          // 오른쪽
        RightDown,      // 오른쪽 아래
        Down,           // 아래
        LeftDown        // 왼쪽 아래
    }

    /// <summary>
    /// BPM 변화 데이터 모델
    /// </summary>
    public class BpmChange
    {
        public float Tick { get; set; }  // measure 단위
        public float Bpm { get; set; }
        public float Freq { get; set; }  // 60 / BPM (1분음표 길이)
    }
}


























