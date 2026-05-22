using System;
using System.Collections.Generic;
using System.Linq;
using GRC2.Parsers;
using GRC2.Processors;
using MelonLoader;

namespace GRC2.Converters
{
    /// <summary>
    /// BMS 노트를 게임 노트로 변환하는 메인 클래스
    /// </summary>
    public static partial class BmsNoteConverter
    {
        // 상세 로깅 플래그 (성능 최적화용, 기본 비활성화)
        private static readonly bool EnableDetailedHoldNoteLogging = false;

        public static void Initialize()
        {
            Loaders.GameTypeLoader.Initialize();
        }

        /// <summary>
        /// BMS 노트를 게임의 NoteCreateData 배열로 변환합니다.
        /// </summary>
        /// <param name="bmsNotes">변환할 BMS 노트 리스트</param>
        /// <returns>NoteCreateData 타입의 배열 (타입: NoteCreateData[]). 변환 실패 시 null 반환</returns>
        public static Array ConvertBmsNotesToNoteCreateData(List<BmsNote> bmsNotes)
        {
            // 타입 안전성: 초기화 검증
            if (Loaders.GameTypeLoader.NoteCreateDataType == null)
            {
                MelonLogger.Error("[BmsNoteConverter] 초기화되지 않았습니다. Initialize()를 먼저 호출하세요.");
                return null;
            }

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

                var noteList = new List<object>();
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

                // 페어리 끝 노트 체크: 매칭된 페어리 시작(Duration>0)에 대해 끝이 있는지만 검사
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

                // 마지막 노트 찾기 및 isLast 설정
                try
                {
                    SetLastNoteFlag(noteList);
                }
                catch (Exception ex)
                {
                    Helpers.ErrorLogger.LogWarning(ex, "[BmsNoteConverter]", "마지막 노트 설정 중 오류");
                }

                return CreateTypedNoteArray(noteList, bmsNotes);
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, "[BmsNoteConverter]", "변환 오류");
                return null;
            }
        }
    }
}
