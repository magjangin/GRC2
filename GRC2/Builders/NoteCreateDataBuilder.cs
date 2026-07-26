using System;
using System.Collections.Generic;
using GRC2.Parsers;
using GRC2.Helpers;
using GRC2.Loaders;
using MelonLoader;

namespace GRC2.Builders
{
    /// <summary>
    /// NoteCreateData 생성 및 필드 설정을 담당하는 클래스
    /// </summary>
    public static partial class NoteCreateDataBuilder
    {
        // 샘플레이트 (게임의 오디오 샘플레이트, 일반적으로 48000)
        private const int SAMPLE_RATE = 48000;
        
        private static Dictionary<string, BmsNote> _timeToBmsNoteCache = null;
        private static List<BmsNote> _cachedBmsNotes = null;

        public static object CreateNoteCreateData(BmsNote bmsNote)
        {
            try
            {
                if (GameTypeLoader.NoteCreateDataType == null)
                {
                    MelonLogger.Error("[NoteCreateDataBuilder] NoteCreateData 타입이 초기화되지 않았습니다.");
                    return null;
                }

                if (bmsNote == null)
                {
                    MelonLogger.Error("[NoteCreateDataBuilder] bmsNote가 null입니다.");
                    return null;
                }

                // perfectSample 계산
                var perfectSample = (int)(bmsNote.Time * SAMPLE_RATE);
                
                // 노트 타입 Enum 값 가져오기
                var noteTypeId = EnumValueHelper.GetNoteTypeId(bmsNote.Type);
                if (noteTypeId == null)
                {
                    MelonLogger.Error("[NoteCreateDataBuilder] noteTypeId를 가져올 수 없습니다.");
                    return null;
                }
                
                // laneLeftRight Enum 값 가져오기
                var laneLeftRight = EnumValueHelper.GetEnumValue(GameTypeLoader.NoteLaneLeftRightEnum, 
                    bmsNote.IsLeft ? EnumValueHelper.GetEnumLeft() : EnumValueHelper.GetEnumRight());
                if (laneLeftRight == null)
                {
                    MelonLogger.Error("[NoteCreateDataBuilder] laneLeftRightID Enum 값을 가져올 수 없습니다.");
                    return null;
                }
                
                // NoteCreateData는 명시적 생성자가 없으므로 기본 생성자로 만들고 필드를 채웁니다.
                object noteCreateData = Activator.CreateInstance(GameTypeLoader.NoteCreateDataType);
                if (noteCreateData == null)
                {
                    MelonLogger.Error("[NoteCreateDataBuilder] NoteCreateData 인스턴스 생성 실패");
                    return null;
                }

                // perfectSample 설정
                SetSampleFields(noteCreateData, perfectSample);

                // laneLeftRightID 설정
                FieldAccessHelper.SetFieldValue(noteCreateData, FieldAccessHelper.FIELD_LANE_LEFT_RIGHT_ID, laneLeftRight);

                // subLaneID 설정
                var subLane = EnumValueHelper.GetSubLaneType(bmsNote.Lane);
                if (subLane == null)
                {
                    MelonLogger.Error($"[NoteCreateDataBuilder] subLaneID를 가져올 수 없습니다. Lane={bmsNote.Lane}");
                    return null;
                }
                FieldAccessHelper.SetFieldValue(noteCreateData, FieldAccessHelper.FIELD_SUB_LANE_ID, subLane);

                // noteTypeID 설정
                FieldAccessHelper.SetFieldValue(noteCreateData, FieldAccessHelper.FIELD_NOTE_TYPE_ID, noteTypeId);

                // directionIndex 설정
                object directionIndexValue = NoteFieldInitializer.SetDirectionIndex(noteCreateData, bmsNote);

                // turnDireciton 설정 (페어리 노트용). FairyEnd는 1A/1B만 사용하므로 bmsNote 전달
                if (bmsNote.Type == NoteType.Fairy || bmsNote.Type == NoteType.FairyEnd)
                {
                    NoteFieldInitializer.SetTurnDirection(noteCreateData, directionIndexValue, bmsNote);
                }

                // noteSize 설정
                NoteFieldInitializer.SetNoteSize(noteCreateData);

                // slideEndFlickDirection 설정
                NoteFieldInitializer.SetSlideEndFlickDirection(noteCreateData);

                // 기타 Boolean 필드들
                NoteFieldInitializer.SetDefaultBooleanFields(noteCreateData);

                // 최종 검증
                ValidatePerfectSample(noteCreateData, perfectSample, bmsNote);

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

    public static partial class NoteCreateDataBuilder
    {
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
        }
    }
}
