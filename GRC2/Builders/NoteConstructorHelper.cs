using System;
using System.Reflection;
using GRC2.Parsers;
using GRC2.Helpers;
using GRC2.Loaders;
using MelonLoader;

namespace GRC2.Builders
{
    /// <summary>
    /// NoteCreateData 생성자를 찾고 인스턴스를 생성하는 헬퍼 클래스
    /// </summary>
    public static class NoteConstructorHelper
    {
        // 생성자 탐색/호출 성공 로그를 제어하는 플래그 (성능 최적화용, 기본 비활성화)
        private static readonly bool EnableConstructorLogging = false;

        private const BindingFlags CONSTRUCTOR_FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        /// <summary>
        /// 생성자를 찾아서 NoteCreateData 인스턴스를 생성합니다.
        /// </summary>
        public static object TryFindConstructor(
            Type noteCreateDataType,
            BmsNote bmsNote,
            int perfectSample,
            object noteTypeId,
            bool isLeftBool)
        {
            object noteCreateData = null;
            
            // 생성자 시그니처 정의 (우선순위 순서)
            var constructorSignatures = new[]
            {
                // 2개 파라미터
                new { Types = new Type[] { typeof(float), typeof(int) }, Args = new object[] { bmsNote.Time, bmsNote.Lane }, Desc = "2개 파라미터 (time, lane)" },
                new { Types = new Type[] { typeof(int), typeof(int) }, Args = new object[] { perfectSample, bmsNote.Lane }, Desc = "2개 파라미터 (sample, lane)" },
                
                // 3개 파라미터
                new { Types = new Type[] { typeof(float), typeof(int), noteTypeId.GetType() }, Args = new object[] { bmsNote.Time, bmsNote.Lane, noteTypeId }, Desc = "3개 파라미터 (time, lane, type)" },
                new { Types = new Type[] { typeof(int), typeof(int), noteTypeId.GetType() }, Args = new object[] { perfectSample, bmsNote.Lane, noteTypeId }, Desc = "3개 파라미터 (sample, lane, type)" },
                
                // 4개 파라미터
                new { Types = new Type[] { typeof(float), typeof(int), noteTypeId.GetType(), typeof(bool) }, Args = new object[] { bmsNote.Time, bmsNote.Lane, noteTypeId, isLeftBool }, Desc = "4개 파라미터 (time, lane, type, leftRight)" },
                new { Types = new Type[] { typeof(int), typeof(int), noteTypeId.GetType(), typeof(bool) }, Args = new object[] { perfectSample, bmsNote.Lane, noteTypeId, isLeftBool }, Desc = "4개 파라미터 (sample, lane, type, leftRight)" },
                
                // 5개 파라미터
                new { Types = new Type[] { typeof(int), typeof(float), typeof(int), noteTypeId.GetType(), typeof(bool) }, Args = new object[] { bmsNote.Lane, bmsNote.Time, perfectSample, noteTypeId, isLeftBool }, Desc = "5개 파라미터 (lane, time, sample, type, leftRight)" },
            };
            
            // 일반 생성자 시도
            foreach (var sig in constructorSignatures)
            {
                if (noteCreateData != null) break;
                
                try
                {
                    var constructor = noteCreateDataType.GetConstructor(CONSTRUCTOR_FLAGS, null, sig.Types, null);
                    if (constructor != null)
                    {
                        noteCreateData = constructor.Invoke(sig.Args);
                        if (EnableConstructorLogging)
                        {
                            MelonLogger.Msg($"[NoteConstructorHelper] {sig.Desc} 생성자 찾기 성공: {sig.Types.Length}개 파라미터");
                        }
                        break;
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogWarning(ex, "[NoteConstructorHelper]", $"{sig.Desc} 생성자 호출 실패");
                }
            }
            
            // 6개 파라미터 생성자 시도 (Direction 추가)
            if (noteCreateData == null && bmsNote.Direction.HasValue)
            {
                noteCreateData = TryCreateWith6ParamsWithDirection(noteCreateDataType, bmsNote, perfectSample, noteTypeId, isLeftBool);
            }
            
            // 6개 파라미터 생성자 시도 (기본 Direction)
            if (noteCreateData == null)
            {
                noteCreateData = TryCreateWith6ParamsDefaultDirection(noteCreateDataType, bmsNote, perfectSample, noteTypeId, isLeftBool);
            }
            
            return noteCreateData;
        }

        /// <summary>
        /// 6개 파라미터 생성자 시도 (Direction 포함)
        /// </summary>
        private static object TryCreateWith6ParamsWithDirection(
            Type noteCreateDataType,
            BmsNote bmsNote,
            int perfectSample,
            object noteTypeId,
            bool isLeftBool)
        {
            try
            {
                var directionIndex = EnumValueHelper.GetDirectionIndex(bmsNote.Direction.Value);
                if (directionIndex != null)
                {
                    var directionType = directionIndex.GetType();
                    var constructor6 = noteCreateDataType.GetConstructor(
                        CONSTRUCTOR_FLAGS,
                        null,
                        new Type[] { typeof(int), typeof(float), typeof(int), noteTypeId.GetType(), typeof(bool), directionType },
                        null);
                    
                    if (constructor6 != null)
                    {
                        var result = constructor6.Invoke(new object[] { bmsNote.Lane, bmsNote.Time, perfectSample, noteTypeId, isLeftBool, directionIndex });
                        if (EnableConstructorLogging)
                        {
                            MelonLogger.Msg("[NoteConstructorHelper] 6개 파라미터 생성자 찾기 성공: 6개 파라미터 (Direction 포함)");
                        }
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[NoteConstructorHelper]", "6개 파라미터 생성자 호출 실패 (Direction 포함)");
            }
            return null;
        }

        /// <summary>
        /// 6개 파라미터 생성자 시도 (기본 Direction)
        /// </summary>
        private static object TryCreateWith6ParamsDefaultDirection(
            Type noteCreateDataType,
            BmsNote bmsNote,
            int perfectSample,
            object noteTypeId,
            bool isLeftBool)
        {
            try
            {
                var directionIndex = EnumValueHelper.GetEnumValue(GameTypeLoader.NoteDirectionIndexEnum, EnumValueHelper.GetEnumCenterMiddle());
                if (directionIndex != null)
                {
                    var directionType = directionIndex.GetType();
                    var constructor6 = noteCreateDataType.GetConstructor(
                        CONSTRUCTOR_FLAGS,
                        null,
                        new Type[] { typeof(int), typeof(float), typeof(int), noteTypeId.GetType(), typeof(bool), directionType },
                        null);
                    
                    if (constructor6 != null)
                    {
                        var result = constructor6.Invoke(new object[] { bmsNote.Lane, bmsNote.Time, perfectSample, noteTypeId, isLeftBool, directionIndex });
                        if (EnableConstructorLogging)
                        {
                            MelonLogger.Msg("[NoteConstructorHelper] 6개 파라미터 생성자 찾기 성공: 6개 파라미터 (기본 Direction)");
                        }
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogWarning(ex, "[NoteConstructorHelper]", "6개 파라미터 생성자 호출 실패 (기본 Direction)");
            }
            return null;
        }
    }
}



























