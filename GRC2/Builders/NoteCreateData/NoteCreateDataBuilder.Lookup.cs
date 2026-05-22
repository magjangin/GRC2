using System;
using System.Collections.Generic;
using GRC2.Helpers;
using GRC2.Parsers;

namespace GRC2.Builders
{
    public static partial class NoteCreateDataBuilder
    {
        /// <summary>
        /// BmsNote에서 NoteCreateData의 perfectSample을 역으로 계산하여 BmsNote를 찾습니다.
        /// 성능 최적화: Dictionary를 사용하여 O(1) 검색
        /// </summary>
        public static BmsNote GetBmsNoteFromNoteCreateData(object noteCreateData, List<BmsNote> bmsNotes)
        {
            try
            {
                var perfectSample = FieldAccessHelper.GetFieldValue(noteCreateData, FieldAccessHelper.FIELD_PERFECT_SAMPLE);
                if (perfectSample == null)
                {
                    return null;
                }

                bool isLeft = ReadIsLeft(noteCreateData);
                int lane = ReadLane(noteCreateData);
                NoteType? targetType = ReadTargetNoteType(noteCreateData);
                float time = (int)perfectSample / (float)SAMPLE_RATE;

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

        private static bool ReadIsLeft(object noteCreateData)
        {
            var laneLeftRightObj = FieldAccessHelper.GetFieldValue(noteCreateData, FieldAccessHelper.FIELD_LANE_LEFT_RIGHT_ID);
            if (laneLeftRightObj == null)
            {
                return true;
            }

            string laneLeftRight = laneLeftRightObj.ToString();
            return laneLeftRight.Contains("Left") || laneLeftRight == "LEFT";
        }

        private static int ReadLane(object noteCreateData)
        {
            var subLaneObj = FieldAccessHelper.GetFieldValue(noteCreateData, FieldAccessHelper.FIELD_SUB_LANE_ID);
            if (subLaneObj == null)
            {
                return 0;
            }

            string subLane = subLaneObj.ToString();
            if (subLane.Contains("2")) return 1;
            if (subLane.Contains("3")) return 2;
            return 0;
        }

        private static NoteType? ReadTargetNoteType(object noteCreateData)
        {
            var noteTypeIdObj = FieldAccessHelper.GetFieldValue(noteCreateData, "noteTypeID");
            string typeStr = noteTypeIdObj?.ToString() ?? "";

            if (typeStr.Contains("Fairy")) return NoteType.Fairy;
            if (typeStr.Contains("Hold")) return NoteType.Hold;
            if (typeStr.Contains("Touch")) return NoteType.Touch;
            if (typeStr.Contains("Flick")) return NoteType.Flick;
            return null;
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
