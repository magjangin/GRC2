using System;

namespace GRC2.Helpers
{
    /// <summary>BGA 동기화 한 번(틱)에 할 일.</summary>
    public enum BgaSyncAction
    {
        /// <summary>그대로 둡니다(배속도 1로).</summary>
        None,
        /// <summary>BGM이 아직 흐르지 않으니 영상을 멈춰 둡니다.</summary>
        Hold,
        /// <summary>영상 시각을 <see cref="BgaSyncDecision.SeekTime"/>로 옮깁니다(멈춰 있었으면 다시 재생).</summary>
        Seek,
        /// <summary>시각은 두고 배속만 <see cref="BgaSyncDecision.PlaybackSpeed"/>로 바꿔 조금씩 따라잡습니다.</summary>
        AdjustSpeed,
    }

    public readonly struct BgaSyncDecision
    {
        public BgaSyncAction Action { get; }
        public double SeekTime { get; }
        public float PlaybackSpeed { get; }
        /// <summary>영상 시각 - 기대 시각(초, 루프 경계는 가까운 쪽으로 접음). 양수면 영상이 앞섬.</summary>
        public double Drift { get; }

        public BgaSyncDecision(BgaSyncAction action, double seekTime, float playbackSpeed, double drift)
        {
            Action = action;
            SeekTime = seekTime;
            PlaybackSpeed = playbackSpeed;
            Drift = drift;
        }
    }

    /// <summary>
    /// BGA(VideoPlayer)를 BGM에 맞추는 판단(Unity 없이 테스트 가능).
    ///
    /// 예전 방식은 0.1초 넘게 어긋나면 1초마다 <c>VideoPlayer.time</c>을 바꿔 되감았습니다. mp4 탐색은 키프레임부터 다시
    /// 디코딩하느라 그때마다 영상이 0.1~0.2초 밀리고, 밀린 만큼 또 0.1초를 넘어 1초마다 탐색이 반복됐습니다. 탐색 직후 영상이
    /// 1.7초 넘게 멈춘 적도 있습니다(2026-11-24 로그). 또 게임은 BGM을 <c>isPlaying</c>인 채 약 2초 늦게 흘려 보내는데,
    /// 영상은 그동안 먼저 재생되다가 2초를 되감겼습니다. 플레이 시작 때 화면이 끊기던 원인입니다.
    ///
    /// 그래서 (1) BGM 시각이 실제로 늘기 전에는 영상을 멈춰 두고, (2) 작은 차이는 배속으로 천천히 따라잡고,
    /// (3) 큰 차이만 탐색하되 탐색 뒤에는 디코더가 자리 잡을 시간을 두고 다시 판단합니다.
    /// </summary>
    public static class BgaSyncPolicy
    {
        /// <summary>이 이상 어긋나면 탐색합니다(초).</summary>
        public const double SeekThreshold = 0.5;
        /// <summary>이보다 작은 차이는 무시합니다(초). 영상 프레임 간격(30fps ≈ 0.033초) 정도.</summary>
        public const double DeadZone = 0.04;
        /// <summary>탐색 뒤 영상 시각이 자리 잡을 때까지 기다리는 시간(초). 그동안은 판단하지 않습니다.</summary>
        public const double SeekSettleSeconds = 1.0;
        /// <summary>차이 1초당 바꿀 배속. 0.15초 차이면 7.5% 빠르게/느리게 → 약 2초에 따라잡음.</summary>
        public const double SpeedGain = 0.5;
        public const float MinSpeed = 0.9f;
        public const float MaxSpeed = 1.1f;
        /// <summary>BGM 시각이 이만큼 늘어야 흐르는 것으로 봅니다(초).</summary>
        public const double AdvanceEpsilon = 0.001;

        /// <param name="bgmTime">지금 BGM 시각(초).</param>
        /// <param name="previousBgmTime">직전 틱의 BGM 시각. 첫 틱이면 null.</param>
        /// <param name="videoTime">지금 영상 시각(초).</param>
        /// <param name="videoLength">영상 길이(초). 0 이하면 루프를 고려하지 않습니다.</param>
        /// <param name="videoPlaying">영상이 재생 중인지.</param>
        /// <param name="secondsSinceSeek">마지막 탐색 뒤 지난 실제 시간(초). 탐색한 적이 없으면 큰 값.</param>
        /// <param name="canSetSpeed">이 VideoPlayer가 배속을 바꿀 수 있는지(<c>canSetPlaybackSpeed</c>).</param>
        public static BgaSyncDecision Decide(
            double bgmTime,
            double? previousBgmTime,
            double videoTime,
            double videoLength,
            bool videoPlaying,
            double secondsSinceSeek,
            bool canSetSpeed)
        {
            double expected = ExpectedVideoTime(bgmTime, videoLength);
            double drift = WrapDrift(videoTime - expected, videoLength);

            // 첫 틱은 BGM이 흐르는지 아직 모릅니다. 아무것도 하지 않고 다음 틱에 봅니다.
            if (previousBgmTime == null)
                return new BgaSyncDecision(BgaSyncAction.None, expected, 1f, drift);

            bool bgmAdvancing = bgmTime - previousBgmTime.Value > AdvanceEpsilon;
            if (!bgmAdvancing)
            {
                // BGM이 시작 전이거나 일시정지·종료. 영상만 앞서 나가지 않게 멈춰 둡니다.
                return new BgaSyncDecision(BgaSyncAction.Hold, expected, 1f, drift);
            }

            if (!videoPlaying)
            {
                // 멈춰 두었던 영상을 BGM 시각에서 다시 시작합니다.
                return new BgaSyncDecision(BgaSyncAction.Seek, expected, 1f, drift);
            }

            if (secondsSinceSeek < SeekSettleSeconds)
                return new BgaSyncDecision(BgaSyncAction.None, expected, 1f, drift);

            double absDrift = Math.Abs(drift);
            if (absDrift >= SeekThreshold || !canSetSpeed && absDrift >= SeekThreshold / 2)
                return new BgaSyncDecision(BgaSyncAction.Seek, expected, 1f, drift);

            if (absDrift < DeadZone || !canSetSpeed)
                return new BgaSyncDecision(BgaSyncAction.None, expected, 1f, drift);

            // 영상이 앞서면(drift > 0) 느리게, 뒤처지면 빠르게.
            float speed = (float)(1.0 - drift * SpeedGain);
            if (speed < MinSpeed) speed = MinSpeed;
            if (speed > MaxSpeed) speed = MaxSpeed;
            return new BgaSyncDecision(BgaSyncAction.AdjustSpeed, expected, speed, drift);
        }

        /// <summary>BGM 시각에 맞는 영상 시각. 영상이 BGM보다 짧으면 루프합니다.</summary>
        public static double ExpectedVideoTime(double bgmTime, double videoLength)
        {
            if (bgmTime < 0) bgmTime = 0;
            if (videoLength <= 0) return bgmTime;
            double t = bgmTime % videoLength;
            return t < 0 ? t + videoLength : t;
        }

        /// <summary>루프 경계를 사이에 둔 차이(예: 영상 끝 9.9초 vs 기대 0.1초)를 가까운 쪽(-0.2초)으로 접습니다.</summary>
        public static double WrapDrift(double drift, double videoLength)
        {
            if (videoLength <= 0) return drift;
            double half = videoLength / 2;
            while (drift > half) drift -= videoLength;
            while (drift < -half) drift += videoLength;
            return drift;
        }
    }
}
