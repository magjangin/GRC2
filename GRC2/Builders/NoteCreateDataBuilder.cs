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
    public static partial class NoteCreateDataBuilder
    {
        // 샘플레이트 (게임의 오디오 샘플레이트, 일반적으로 48000)
        private const int SAMPLE_RATE = 48000;

        private static Dictionary<string, BmsNote> _timeToBmsNoteCache = null;
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
                    perfectSample = (int)(bmsNote.Time * SAMPLE_RATE),
                    laneLeftRightID = EnumValueHelper.GetLaneLeftRight(bmsNote.IsLeft),
                    subLaneID = EnumValueHelper.GetSubLaneType(bmsNote.Lane),
                    noteTypeID = EnumValueHelper.GetNoteTypeId(bmsNote.Type),
                    noteSize = NoteSize.Scale1,
                    // 게임 기본값은 NUM이지만, 플릭이 아닌 노트는 CENTER_MIDDLE을 사용합니다.
                    slideEndFlickDirection = NoteDirectionIndex.CENTER_MIDDLE
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
    }

    public static partial class NoteCreateDataBuilder
    {
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
                float time = noteCreateData.perfectSample / (float)SAMPLE_RATE;

                EnsureBmsNoteLookupCache(bmsNotes);
                string searchKey = BuildBmsNoteLookupKey(time, lane, isLeft, targetType);

                if (_timeToBmsNoteCache.TryGetValue(searchKey, out var cachedNote) &&
                    Math.Abs(cachedNote.Time - time) < 0.001f)
                {
                    return cachedNote;
                }

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
            _timeToBmsNoteCache = null;
            _cachedBmsNotes = null;
        }

        private static void EnsureBmsNoteLookupCache(List<BmsNote> bmsNotes)
        {
            bool needRebuild = _timeToBmsNoteCache == null ||
                _cachedBmsNotes == null ||
                !ReferenceEquals(_cachedBmsNotes, bmsNotes);

            if (!needRebuild)
            {
                return;
            }

            _timeToBmsNoteCache = new Dictionary<string, BmsNote>();
            _cachedBmsNotes = bmsNotes;

            foreach (var note in bmsNotes)
            {
                string key = BuildBmsNoteLookupKey(note.Time, note.Lane, note.IsLeft, note.Type);
                if (!_timeToBmsNoteCache.TryGetValue(key, out _))
                {
                    _timeToBmsNoteCache[key] = note;
                }
            }
        }

        private static string BuildBmsNoteLookupKey(float time, int lane, bool isLeft, NoteType? noteType)
        {
            var timeKey = (float)Math.Round(time, 3);
            return $"{timeKey}_{lane}_{isLeft}_{noteType}";
        }
    }
}
