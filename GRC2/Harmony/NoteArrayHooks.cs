using System;
using System.Collections.Generic;
using MelonLoader;
using GRC2.Helpers;
using GRC2.Parsers;
using GRC2.Converters;
using HarmonyLib;
using GRC2.Core;
using GRC2.Injectors;
using IntiCreates;

namespace GRC2.Harmony
{
    [HarmonyPatch(typeof(cFairyModeNotesManager), "createAllNote")]
    public static class NoteArrayHooks
    {
        private static readonly AccessTools.FieldRef<cFairyModeNotesManager, FairyNoteEditorLoader.NoteCreateData[]> NoteArrayRef =
            AccessTools.FieldRefAccess<cFairyModeNotesManager, FairyNoteEditorLoader.NoteCreateData[]>("mFairyNoteCreateDataArray");

        /// <summary>주입이 취소됐을 때 플레이 화면에 경고를 띄우는 시간(초)입니다.</summary>
        private const float WarningSeconds = 8f;

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

        private static void InjectBmsNotes(cFairyModeNotesManager instance)
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
                    GameHud.ShowWarning("BMS 차트 주입 취소: 원본 차트가 나옵니다 (로그 확인)", WarningSeconds);
                    return; // 주입 금지
                }
                if (noteCreateDataArray.Length == 0)
                {
                    MelonLogger.Warning("[NoteArrayHooks] 변환된 노트가 없습니다.");
                    GameHud.ShowWarning("BMS 차트에 노트가 없어 원본 차트가 나옵니다 (로그 확인)", WarningSeconds);
                    return;
                }

                // BMS 노트 배열로 교체
                NoteArrayRef(instance) = noteCreateDataArray;
                MelonLogger.Msg($"[NoteArrayHooks] BMS 노트 교체 완료: {noteCreateDataArray.Length}개");
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, "[NoteArrayHooks]", "InjectBmsNotes 오류");
            }
        }
        [HarmonyPrefix]
        public static void CreateAllNotePrefix(cFairyModeNotesManager __instance)
        {
            TryInjectBmsNotes(__instance, "CreateAllNotePrefix");
        }

        private static void TryInjectBmsNotes(cFairyModeNotesManager instance, string methodName)
        {
            try
            {
                // createAllNote는 플레이 씬 말고 곡 선택 씬의 옵션 미리보기 창
                // (cMusicSelectPreviewWindowManager.coUpdateNote)에서도 열 때와 루프마다 불립니다.
                // 그 노트 배열은 게임의 샘플 노트이고, 이 시점의 _bmsNotes는 선택한 앨범이 아니라
                // 마지막으로 적재된 앨범의 차트라 갈아끼우면 엉뚱한 차트가 나옵니다.
                // 플레이 씬은 SceneDetector가 FairyModeScene 로드 때 플레이 씬 상태를 켜 두므로
                // 플레이의 createAllNote는 항상 이 검사를 통과합니다.
                if (!BgmBgaInjector.IsPlayScene())
                {
                    return;
                }

                if (!CustomAssetManager.ShouldInjectCustomContent())
                {
                    MelonLogger.Msg($"[NoteArrayHooks] ⚠️ BMS 노트 주입 건너뜀 (메서드: {methodName}, 씬 금지 또는 커스텀 미선택)");
                    return;
                }

                if (_bmsNotes != null && _bmsNotes.Count > 0)
                {
                    InjectBmsNotes(instance);
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(ex, "[NoteArrayHooks]", $"{methodName} 오류");
            }
        }
    }
}
