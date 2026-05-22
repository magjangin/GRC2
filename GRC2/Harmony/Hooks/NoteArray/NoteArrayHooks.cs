using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using GRC2.Helpers;
using GRC2.Parsers;
using GRC2.Converters;

namespace GRC2.Harmony.Hooks
{
    public static partial class NoteArrayHooks
    {
        private static List<BmsNote> _bmsNotes = new List<BmsNote>();

        /// <summary>
        /// BMS 노트 업데이트 (앨범 변경 시 호출)
        /// </summary>
        public static void UpdateBmsNotes(List<BmsNote> bmsNotes)
        {
            if (bmsNotes != null)
            {
                _bmsNotes = bmsNotes;
                MelonLogger.Msg($"[NoteArrayHooks] BMS 노트 업데이트: {_bmsNotes.Count}개");
            }
        }

        private static void InjectBmsNotes(object instance)
        {
            try
            {
                if (_bmsNotes == null || _bmsNotes.Count == 0)
                {
                    return;
                }

                // 모든 BMS 노트 변환
                var noteCreateDataArray = BmsNoteConverter.ConvertBmsNotesToNoteCreateData(_bmsNotes);
                if (noteCreateDataArray == null)
                {
                    MelonLogger.Error("═══════════════════════════════════════════════════════════════");
                    MelonLogger.Error("[NoteArrayHooks] ❌ BMS 노트 주입이 취소되었습니다. 변환 결과가 null입니다. 이전 로그(BmsNoteConverter 등)와 BMS 파일을 확인하세요.");
                    MelonLogger.Error("═══════════════════════════════════════════════════════════════");
                    return; // 주입 금지
                }
                if (noteCreateDataArray.Length == 0)
                {
                    MelonLogger.Warning("[NoteArrayHooks] 변환된 노트가 없습니다.");
                    return;
                }

                // mFairyNoteCreateDataArray 필드 찾기
                var noteArrayField = instance.GetType().GetField("mFairyNoteCreateDataArray",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                if (noteArrayField == null)
                {
                    MelonLogger.Error("[NoteArrayHooks] mFairyNoteCreateDataArray 필드를 찾을 수 없습니다.");
                    return;
                }

                // BMS 노트 배열로 교체
                noteArrayField.SetValue(instance, noteCreateDataArray);
                MelonLogger.Msg($"[NoteArrayHooks] BMS 노트 교체 완료: {noteCreateDataArray.Length}개");
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, "[NoteArrayHooks]", "InjectBmsNotes 오류");
            }
        }


    }
}
