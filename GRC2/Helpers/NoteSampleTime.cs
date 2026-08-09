using System;

namespace GRC2.Helpers
{
    /// <summary>
    /// BMS 초 단위 시간과 게임 NoteCreateData.perfectSample 사이의 변환을 한 곳에 모읍니다.
    ///
    /// 예전에는 노트를 만들 때는 (int) 절삭을, 홀드/페어리 끝 노트를 되찾을 때는
    /// Math.Round(AwayFromZero)를 써서 같은 시각이 최대 1샘플 어긋났고,
    /// 그 오차를 프로세서 쪽 ±2 샘플 탐색이 대신 흡수하고 있었습니다.
    /// 변환식이 하나면 그 오차 자체가 생기지 않습니다.
    /// </summary>
    public static class NoteSampleTime
    {
        /// <summary>게임 오디오 샘플레이트입니다.</summary>
        public const int SampleRate = 48000;

        /// <summary>초 단위 시간을 perfectSample 값으로 변환합니다.</summary>
        public static int ToSamples(float seconds)
        {
            return (int)Math.Round(seconds * (double)SampleRate, MidpointRounding.AwayFromZero);
        }

        /// <summary>perfectSample 값을 초 단위 시간으로 되돌립니다.</summary>
        public static float ToSeconds(int samples)
        {
            return samples / (float)SampleRate;
        }
    }
}
