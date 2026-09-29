using System;
using System.Collections.Generic;
using System.Linq;
using GRC2.Parsers;
using GRC2.Processors;
using IntiCreates;
using IntiCreates.RythmGame.FairyMode;
using MelonLoader;

namespace GRC2.Converters
{
    using NoteCreateData = FairyNoteEditorLoader.NoteCreateData;

    /// <summary>
    /// BMS 노트를 게임 노트로 변환하는 메인 클래스
    /// </summary>
    public static class BmsNoteConverter
    {
        /// <summary>
        /// BMS 노트를 게임의 NoteCreateData 배열로 변환합니다.
        /// </summary>
        /// <param name="bmsNotes">변환할 BMS 노트 리스트</param>
        /// <returns>NoteCreateData 배열. 변환 실패 시 null 반환</returns>
        public static NoteCreateData[] ConvertBmsNotesToNoteCreateData(List<BmsNote> bmsNotes)
        {
            // 타입 안전성: 입력 검증
            if (bmsNotes == null)
            {
                MelonLogger.Error("[BmsNoteConverter] bmsNotes가 null입니다.");
                return null;
            }

            try
            {
                MelonLogger.Msg($"[BmsNoteConverter] BMS 노트 변환 시작: {bmsNotes.Count}개");

                // 성능 최적화: 캐시 초기화
                Builders.NoteCreateDataBuilder.ClearCache();

                var noteList = new List<NoteCreateData>();
                var holdEndNotes = new List<BmsNote>(); // 홀드 끝 노트 저장
                var fairyEndNotes = new List<BmsNote>(); // 페어리 끝 노트 저장

                // 성능 최적화: 한 번만 정렬 (Time 기준)
                var sortedBmsNotes = bmsNotes.OrderBy(n => n.Time).ToList();
                int convertedCount = 0;
                int skippedCount = 0;

                foreach (var bmsNote in sortedBmsNotes)
                {
                    try
                    {
                        // 홀드/페어리 끝 노트는 별도 처리
                        if (bmsNote.Type == NoteType.HoldEnd)
                        {
                            holdEndNotes.Add(bmsNote);
                            skippedCount++;
                            continue;
                        }
                        if (bmsNote.Type == NoteType.FairyEnd)
                        {
                            fairyEndNotes.Add(bmsNote);
                            skippedCount++;
                            continue;
                        }

                        var noteCreateData = Builders.NoteCreateDataBuilder.CreateNoteCreateData(bmsNote);
                        if (noteCreateData != null)
                        {
                            noteList.Add(noteCreateData);
                            convertedCount++;
                        }
                        else
                        {
                            MelonLogger.Warning($"[BmsNoteConverter] 노트 변환 실패: Time={bmsNote.Time}, Lane={bmsNote.Lane}, Type={bmsNote.Type}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Helpers.ErrorLogger.LogException(ex, "[BmsNoteConverter]", $"노트 변환 중 오류 (Time={bmsNote?.Time}, Type={bmsNote?.Type})");
                    }
                }

                // 홀드 끝 노트 체크: 홀드 시작 노트가 있는데 끝 노트가 없는 경우 주입 금지
                var missingHoldEnds = CheckMissingEndNotes(
                    bmsNotes.Where(n => n.Type == NoteType.Hold),
                    holdEndNotes,
                    "홀드",
                    "홀드 시작 노트(02 채널)",
                    "홀드 끝 노트(19 채널)");

                if (missingHoldEnds != null)
                {
                    return null; // 주입 금지
                }

                ProcessHoldEndNotes(noteList, holdEndNotes, bmsNotes);

                // 페어리 끝 노트 체크: 매칭된 페어리 시작(Duration>0)에 대해 끝이 있는지만 검사.
                // 짝을 아예 찾지 못해 Duration이 0으로 남은 페어리는 여기서 걸러지지 않으므로,
                // 아래 CheckIncompleteConnectedNotes가 최종 산출물 기준으로 다시 확인합니다.
                var missingFairyEnds = CheckMissingEndNotes(
                    bmsNotes.Where(n => n.Type == NoteType.Fairy && n.Duration > 0),
                    fairyEndNotes,
                    "페어리",
                    "페어리 시작 노트(11-18 채널)",
                    "페어리 끝 노트(1A-1B 채널)");

                if (missingFairyEnds != null)
                {
                    return null; // 주입 금지
                }

                ProcessFairyEndNotes(noteList, fairyEndNotes, bmsNotes);

                // 0초 더미 노트 필터링
                try
                {
                    FilterZeroTimeNotes(noteList);
                }
                catch (Exception ex)
                {
                    Helpers.ErrorLogger.LogWarning(ex, "[BmsNoteConverter]", "0초 노트 필터링 중 오류");
                }

                // 최종 방어선: 게임에 넘길 수 없는 노트가 남아 있으면 주입 자체를 취소합니다.
                if (CheckIncompleteConnectedNotes(noteList))
                {
                    return null; // 주입 금지
                }

                // 마지막 노트 찾기 및 isLast 설정
                try
                {
                    SetLastNoteFlag(noteList);
                }
                catch (Exception ex)
                {
                    Helpers.ErrorLogger.LogWarning(ex, "[BmsNoteConverter]", "마지막 노트 설정 중 오류");
                }

                var noteArray = noteList.ToArray();
                MelonLogger.Msg($"[BmsNoteConverter] 변환 완료: {noteArray.Length}개 노트");
                return noteArray;
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, "[BmsNoteConverter]", "변환 오류");
                return null;
            }
        }

        private static void ProcessHoldEndNotes(List<NoteCreateData> noteList, List<BmsNote> holdEndNotes, List<BmsNote> bmsNotes)
        {
            try
            {
                HoldNoteProcessor.ProcessHoldEndNotes(noteList, holdEndNotes, bmsNotes);
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, "[BmsNoteConverter]", "홀드 끝 노트 처리 중 오류");
            }
        }

        private static void ProcessFairyEndNotes(List<NoteCreateData> noteList, List<BmsNote> fairyEndNotes, List<BmsNote> bmsNotes)
        {
            try
            {
                FairyNoteProcessor.ProcessFairyEndNotes(noteList, fairyEndNotes, bmsNotes);
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, "[BmsNoteConverter]", "페어리 끝 노트 처리 중 오류");
            }
        }

        private static void FilterZeroTimeNotes(List<NoteCreateData> noteList)
        {
            if (noteList == null || noteList.Count == 0) return;

            try
            {
                int removedCount = 0;
                for (int i = noteList.Count - 1; i >= 0; i--)
                {
                    if (noteList[i] == null || noteList[i].perfectSample == 0)
                    {
                        noteList.RemoveAt(i);
                        removedCount++;
                    }
                }

                if (removedCount > 0)
                    MelonLogger.Warning($"[BmsNoteConverter] 0초 더미 노트 {removedCount}개 제거됨 (남은 노트: {noteList.Count}개)");
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, "[BmsNoteConverter]", "FilterZeroTimeNotes 오류");
            }
        }

        /// <summary>
        /// 게임의 cFairyModeNotesManager.createNote는 Fairy/Hold 노트에서 null 검사 없이
        /// connectNodeDataArray[0]을 읽습니다. 끝 노트가 붙지 않은 채로 배열을 넘기면
        /// createAllNote 안에서 NullReferenceException이 나고 곡 로딩이 통째로 깨집니다.
        ///
        /// 앞선 CheckMissingEndNotes는 BMS 원본 노트 기준이라
        /// (a) 짝을 못 찾아 Duration이 0으로 남은 페어리 시작,
        /// (b) 사전 검사는 통과했지만 Hold/FairyNoteProcessor의 매칭 단계에서 실패한 노트
        /// 두 경우를 놓칩니다. 여기서 실제로 게임에 넘길 배열을 기준으로 다시 확인합니다.
        /// </summary>
        /// <returns>넘길 수 없는 노트가 있으면 true (주입 금지)</returns>
        private static bool CheckIncompleteConnectedNotes(List<NoteCreateData> noteList)
        {
            if (noteList == null || noteList.Count == 0)
            {
                return false;
            }

            var incomplete = noteList
                .Where(note => note != null && RequiresConnectNode(note.noteTypeID) &&
                    (note.connectNodeDataArray == null || note.connectNodeDataArray.Length == 0))
                .ToList();

            if (incomplete.Count == 0)
            {
                return false;
            }

            MelonLogger.Error("");
            MelonLogger.Error("═══════════════════════════════════════════════════════════════");
            MelonLogger.Error("❌❌❌ BMS 노트 주입 실패: 끝 노트가 연결되지 않은 노트가 있습니다! ❌❌❌");
            MelonLogger.Error("═══════════════════════════════════════════════════════════════");
            MelonLogger.Error($"홀드/페어리 노트 {incomplete.Count}개에 끝 노트가 연결되지 않았습니다.");
            MelonLogger.Error("이 상태로 주입하면 게임이 노트를 만드는 도중 크래시합니다.");
            MelonLogger.Error("");
            MelonLogger.Error("🔍 BMS 파일을 다시 확인해보세요!");
            MelonLogger.Error("   - 페어리 시작 노트(11-18 채널)에는 끝 노트(1A/1B 채널)가,");
            MelonLogger.Error("   - 홀드 시작 노트(02 채널)에는 끝 노트(19 채널)가 필요합니다.");
            MelonLogger.Error("");
            foreach (var note in incomplete)
            {
                MelonLogger.Error(
                    $"   {note.noteTypeID}: Time={Helpers.NoteSampleTime.ToSeconds(note.perfectSample):F3}초, " +
                    $"perfectSample={note.perfectSample}, Lane={note.laneLeftRightID}/{note.subLaneID}");
            }
            MelonLogger.Error("═══════════════════════════════════════════════════════════════");
            MelonLogger.Error("");
            return true;
        }

        /// <summary>
        /// 게임이 connectNodeDataArray[0]을 반드시 읽는 노트 타입인지 판정합니다.
        /// </summary>
        private static bool RequiresConnectNode(NoteTypeId noteTypeId)
        {
            return noteTypeId == NoteTypeId.Fairy || noteTypeId == NoteTypeId.Hold;
        }

        private static List<BmsNote> CheckMissingEndNotes(
            IEnumerable<BmsNote> startNotes,
            List<BmsNote> endNotes,
            string noteTypeName,
            string startNoteDescription,
            string endNoteDescription)
        {
            var missingEnds = new List<BmsNote>();
            const float TIME_TOLERANCE = 0.01f; // 시간 오차 허용 범위

            foreach (var startNote in startNotes)
            {
                var expectedEndTime = startNote.Time + startNote.Duration;
                var hasEndNote = endNotes.Any(end =>
                    Math.Abs(end.Time - expectedEndTime) < TIME_TOLERANCE &&
                    end.Lane == startNote.Lane &&
                    end.IsLeft == startNote.IsLeft);

                if (!hasEndNote)
                {
                    missingEnds.Add(startNote);
                }
            }

            if (missingEnds.Count > 0)
            {
                MelonLogger.Error("");
                MelonLogger.Error("═══════════════════════════════════════════════════════════════");
                MelonLogger.Error($"❌❌❌ BMS 노트 주입 실패: {noteTypeName} 끝 노트가 없습니다! ❌❌❌");
                MelonLogger.Error("═══════════════════════════════════════════════════════════════");
                MelonLogger.Error($"{noteTypeName} 시작 노트 {missingEnds.Count}개에 대해 끝 노트가 BMS 파일에 없습니다!");
                MelonLogger.Error("");
                MelonLogger.Error("🔍 BMS 파일을 다시 확인해보세요!");
                MelonLogger.Error($"   - {startNoteDescription}에 대응하는");
                MelonLogger.Error($"   - {endNoteDescription}가 있는지 확인하세요.");
                MelonLogger.Error("");
                foreach (var startNote in missingEnds)
                {
                    MelonLogger.Error($"   {noteTypeName} 시작: Time={startNote.Time:F3}초, Lane={startNote.Lane}, IsLeft={startNote.IsLeft}, Duration={startNote.Duration:F3}초");
                    MelonLogger.Error($"   예상 끝 시간: {startNote.Time + startNote.Duration:F3}초");
                }
                MelonLogger.Error("═══════════════════════════════════════════════════════════════");
                MelonLogger.Error("");
                return missingEnds; // 주입 금지
            }

            return null; // 모든 끝 노트가 있음
        }

        private static void SetLastNoteFlag(List<NoteCreateData> noteList)
        {
            if (noteList == null || noteList.Count == 0)
            {
                return;
            }

            try
            {
                int maxPerfectSample = int.MinValue;
                NoteCreateData lastNote = null;

                // 모든 노트와 connectNodeDataArray의 끝 노트를 확인하여 가장 큰 perfectSample 찾기
                foreach (var noteObj in noteList)
                {
                    if (noteObj.perfectSample > maxPerfectSample)
                    {
                        maxPerfectSample = noteObj.perfectSample;
                        lastNote = noteObj;
                    }

                    var connectArray = noteObj.connectNodeDataArray;
                    if (connectArray == null) continue;

                    foreach (var connectNode in connectArray)
                    {
                        if (connectNode != null && connectNode.perfectSample > maxPerfectSample)
                        {
                            maxPerfectSample = connectNode.perfectSample;
                            lastNote = connectNode;
                        }
                    }
                }

                // 마지막 노트에 isLast 설정
                if (lastNote != null)
                {
                    lastNote.isLast = true;
                }
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, "[BmsNoteConverter]", "SetLastNoteFlag 오류");
            }
        }
    }
}
