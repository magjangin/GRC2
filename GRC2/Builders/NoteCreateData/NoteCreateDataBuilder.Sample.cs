using GRC2.Helpers;
using GRC2.Parsers;
using MelonLoader;

namespace GRC2.Builders
{
    public static partial class NoteCreateDataBuilder
    {
        /// <summary>
        /// perfectSample 필드를 설정합니다.
        /// </summary>
        private static void SetPerfectSample(object noteCreateData, int perfectSample, bool usedDefaultConstructor)
        {
            var existingPerfectSample = FieldAccessHelper.GetFieldValue(noteCreateData, FieldAccessHelper.FIELD_PERFECT_SAMPLE);
            var existingMSample = FieldAccessHelper.GetFieldValue(noteCreateData, "mSample");

            bool needsManualSet = usedDefaultConstructor ||
                (existingPerfectSample == null && existingMSample == null) ||
                (existingPerfectSample != null && (int)existingPerfectSample == 0) ||
                (existingMSample != null && (int)existingMSample == 0);

            if (needsManualSet)
            {
                SetSampleFields(noteCreateData, perfectSample);
                return;
            }

            var actualSample = existingPerfectSample ?? existingMSample;
            if (actualSample != null && !actualSample.Equals(perfectSample))
            {
                MelonLogger.Warning($"[NoteCreateDataBuilder] perfectSample 불일치: 생성자={actualSample}, 예상={perfectSample}");
            }
        }

        /// <summary>
        /// 최종 perfectSample 값을 검증합니다.
        /// </summary>
        private static void ValidatePerfectSample(object noteCreateData, int perfectSample, BmsNote bmsNote)
        {
            var finalPerfectSample = FieldAccessHelper.GetFieldValue(noteCreateData, FieldAccessHelper.FIELD_PERFECT_SAMPLE);

            if (finalPerfectSample == null)
            {
                MelonLogger.Error($"[NoteCreateDataBuilder] perfectSample이 null입니다! Time={bmsNote.Time}, Lane={bmsNote.Lane}, Type={bmsNote.Type}");
                SetSampleFields(noteCreateData, perfectSample);
                return;
            }

            int finalSampleInt = (int)finalPerfectSample;
            if (finalSampleInt == 0)
            {
                MelonLogger.Error($"[NoteCreateDataBuilder] perfectSample이 0입니다! Time={bmsNote.Time}, Lane={bmsNote.Lane}, Type={bmsNote.Type}");
                SetSampleFields(noteCreateData, perfectSample);
            }
        }

        private static void SetSampleFields(object noteCreateData, int perfectSample)
        {
            FieldAccessHelper.SetFieldValue(noteCreateData, FieldAccessHelper.FIELD_PERFECT_SAMPLE, perfectSample);
            FieldAccessHelper.SetFieldValue(noteCreateData, "mSample", perfectSample);
            FieldAccessHelper.SetFieldValue(noteCreateData, "sample", perfectSample);
        }
    }
}
