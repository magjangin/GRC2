namespace GRC2.Helpers
{
    /// <summary>
    /// 긴 글자를 옆으로 흘려 보여 줄 때의 위치 계산(Unity 없이 테스트 가능).
    /// 처음 위치에서 <see cref="StartDelay"/>초 멈춤 → 초당 <see cref="Speed"/>만큼 왼쪽으로 이동 →
    /// 끝이 보이면 <see cref="EndHold"/>초 멈춤 → 처음 위치로 돌아가 반복합니다.
    /// </summary>
    public sealed class MarqueeTimeline
    {
        public const float DefaultStartDelay = 1.5f;
        public const float DefaultEndHold = 1.5f;
        public const float DefaultSpeed = 80f;

        public float StartDelay { get; }
        public float EndHold { get; }
        public float Speed { get; }

        /// <summary>끝까지 보이려면 움직여야 하는 거리. 0 이하면 움직이지 않습니다.</summary>
        public float Distance { get; private set; }

        /// <summary>지금 왼쪽으로 옮긴 거리(0 ~ Distance).</summary>
        public float Offset { get; private set; }

        private int _phase; // 0: 시작 대기, 1: 이동, 2: 끝 대기
        private float _timer;

        public MarqueeTimeline(float startDelay = DefaultStartDelay, float endHold = DefaultEndHold, float speed = DefaultSpeed)
        {
            StartDelay = startDelay;
            EndHold = endHold;
            Speed = speed;
        }

        /// <summary>새 글자로 처음부터 다시 시작합니다.</summary>
        public void Reset(float distance)
        {
            Distance = distance > 0f ? distance : 0f;
            Offset = 0f;
            _phase = 0;
            _timer = 0f;
        }

        /// <summary>시간을 진행하고 새 Offset을 돌려줍니다.</summary>
        public float Advance(float deltaTime)
        {
            if (Distance <= 0f || deltaTime <= 0f)
                return Offset;

            switch (_phase)
            {
                case 0:
                    _timer += deltaTime;
                    if (_timer >= StartDelay)
                    {
                        float over = _timer - StartDelay;
                        _phase = 1;
                        _timer = 0f;
                        return Advance(over);
                    }
                    break;

                case 1:
                    Offset += Speed * deltaTime;
                    if (Offset >= Distance)
                    {
                        Offset = Distance;
                        _phase = 2;
                        _timer = 0f;
                    }
                    break;

                default:
                    _timer += deltaTime;
                    if (_timer >= EndHold)
                    {
                        Offset = 0f;
                        _phase = 0;
                        _timer = 0f;
                    }
                    break;
            }
            return Offset;
        }
    }
}
