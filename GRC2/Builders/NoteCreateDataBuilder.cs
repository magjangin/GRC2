using System;
using System.Collections.Generic;
using GRC2.Parsers;
using GRC2.Helpers;
using IntiCreates;
using IntiCreates.RythmGame;
using MelonLoader;

namespace GRC2.Builders
{
    using NoteCreateData = FairyNoteEditorLoader.NoteCreateData;

    /// <summary>
    /// NoteCreateData 생성 및 필드 설정을 담당하는 클래스
    /// </summary>
    public static class NoteCreateDataBuilder
    {
        /// <summary>BMS 노트 조회 키: (perfectSample, 레인, 좌우, 노트 종류). 종류를 모르면 -1입니다. 문자열 키를 만들지 않습니다(P1).</summary>
        private static Dictionary<(int Sample, int Lane, bool IsLeft, int TypeKey), BmsNote> _noteLookupCache = null;
        private static List<BmsNote> _cachedBmsNotes = null;

        public static NoteCreateData CreateNoteCreateData(BmsNote bmsNote)
        {
            try
            {
                if (bmsNote == null)
                {
                    MelonLogger.Error("[NoteCreateDataBuilder] bmsNote가 null입니다.");
                    return null;
                }

                // NoteCreateData는 명시적 생성자가 없으므로 필드를 직접 채웁니다.
                // bool 필드들은 기본값이 false이므로 별도 초기화가 필요 없습니다.
                var noteCreateData = new NoteCreateData
                {
                    perfectSample = NoteSampleTime.ToSamples(bmsNote.Time),
                    laneLeftRightID = EnumValueHelper.GetLaneLeftRight(bmsNote.IsLeft),
                    subLaneID = EnumValueHelper.GetSubLaneType(bmsNote.Lane),
                    noteTypeID = EnumValueHelper.GetNoteTypeId(bmsNote.Type),
                    noteSize = NoteSize.Scale1,
                    // 디컴파일된 NoteCreateData 기본값은 NUM이며, 이 모드에서는 특별한 플릭/슬라이드 노트가 아니라면 그대로 유지합니다.
                    slideEndFlickDirection = NoteDirectionIndex.NUM
                };

                var directionIndexValue = NoteFieldInitializer.SetDirectionIndex(noteCreateData, bmsNote);

                // turnDireciton 설정 (페어리 노트용). FairyEnd는 1A/1B만 사용하므로 bmsNote 전달
                if (bmsNote.Type == NoteType.Fairy || bmsNote.Type == NoteType.FairyEnd)
                {
                    NoteFieldInitializer.SetTurnDirection(noteCreateData, directionIndexValue, bmsNote);
                }

                return noteCreateData;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(ex, "[NoteCreateDataBuilder]", "CreateNoteCreateData 오류");
                return null;
            }
        }

        /// <summary>
        /// NoteCreateData의 perfectSample을 역으로 계산하여 대응하는 BmsNote를 찾습니다.
        /// 성능 최적화: Dictionary를 사용하여 O(1) 검색
        /// </summary>
        public static BmsNote GetBmsNoteFromNoteCreateData(NoteCreateData noteCreateData, List<BmsNote> bmsNotes)
        {
            try
            {
                if (noteCreateData == null)
                {
                    return null;
                }

                bool isLeft = noteCreateData.laneLeftRightID == IntiCreates.RythmGame.FairyMode.NoteLaneLeftRight.Left;
                int lane = EnumValueHelper.ToLaneIndex(noteCreateData.subLaneID);
                NoteType? targetType = EnumValueHelper.ToBmsNoteType(noteCreateData.noteTypeID);
                EnsureBmsNoteLookupCache(bmsNotes);

                // perfectSample은 BMS 시각을 NoteSampleTime.ToSamples로 바꾼 값이라, 같은 식으로 만든 키와 정확히 맞습니다.
                // 예전에는 초를 소수 셋째 자리로 반올림한 문자열 키를 노트마다 새로 만들었습니다(P1).
                if (_noteLookupCache.TryGetValue((noteCreateData.perfectSample, lane, isLeft, TypeKeyOf(targetType)), out var cachedNote))
                {
                    return cachedNote;
                }

                // 캐시에 없을 때만(종류가 맞지 않는 경우 등) 선형 탐색합니다. 예전과 같습니다.
                float time = NoteSampleTime.ToSeconds(noteCreateData.perfectSample);
                return bmsNotes.Find(n =>
                    Math.Abs(n.Time - time) < 0.001f &&
                    n.Lane == lane &&
                    n.IsLeft == isLeft &&
                    (targetType == null || n.Type == targetType));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 캐시를 초기화합니다 (새로운 변환 시작 시 호출)
        /// </summary>
        public static void ClearCache()
        {
            _noteLookupCache = null;
            _cachedBmsNotes = null;
        }

        private static void EnsureBmsNoteLookupCache(List<BmsNote> bmsNotes)
        {
            bool needRebuild = _noteLookupCache == null ||
                _cachedBmsNotes == null ||
                !ReferenceEquals(_cachedBmsNotes, bmsNotes);

            if (!needRebuild)
            {
                return;
            }

            _noteLookupCache = new Dictionary<(int Sample, int Lane, bool IsLeft, int TypeKey), BmsNote>();
            _cachedBmsNotes = bmsNotes;

            foreach (var note in bmsNotes)
            {
                var key = (NoteSampleTime.ToSamples(note.Time), note.Lane, note.IsLeft, TypeKeyOf(note.Type));
                if (!_noteLookupCache.ContainsKey(key))
                {
                    _noteLookupCache[key] = note;
                }
            }
        }

        /// <summary>노트 종류를 키용 정수로 바꿉니다. 종류가 없으면 -1입니다.</summary>
        private static int TypeKeyOf(NoteType? noteType)
        {
            return noteType.HasValue ? (int)noteType.Value : -1;
        }
    }
}
