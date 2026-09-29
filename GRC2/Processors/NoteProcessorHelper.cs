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
        /// <summary>BMS에 <c>#BPM</c>이 없거나 0 이하일 때 허용 오차 계산에 쓰는 기준 BPM입니다.</summary>
        public const float DefaultBpm = 120f;

        private const float BaseToleranceSeconds = 0.05f; // DefaultBpm에서의 허용 오차
        private const float MinToleranceSeconds = 0.02f;
        private const float MaxToleranceSeconds = 0.15f;

        /// <summary>
        /// BPM 기반 시간 오차 허용 범위(초)를 계산합니다.
        /// BPM이 높을수록 타이밍이 까다로우므로 범위를 줄이고, 낮을수록 늘립니다.
        /// 공식: 0.05 × (120 / BPM), 결과는 0.02~0.15초로 제한합니다.
        /// 예: BPM 240 = 0.025초, BPM 120 = 0.05초, BPM 60 = 0.10초.
        /// </summary>
        public static float CalculateTimeTolerance(float bpm)
        {
            if (bpm <= 0f) bpm = DefaultBpm;

            float tolerance = BaseToleranceSeconds * (DefaultBpm / bpm);
            return Math.Max(MinToleranceSeconds, Math.Min(MaxToleranceSeconds, tolerance));
        }

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
