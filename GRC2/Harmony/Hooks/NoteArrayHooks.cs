using System;
using System.Collections.Generic;
using System.Reflection;
using MelonLoader;
using GRC2.Helpers;
using GRC2.Parsers;
using GRC2.Converters;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using GRC2.Core;

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

    public static partial class NoteArrayHooks
    {
    }

    public static partial class NoteArrayHooks
    {
        public static void Initialize(List<BmsNote> bmsNotes = null)
        {
            if (bmsNotes != null)
            {
                _bmsNotes = bmsNotes;
                MelonLogger.Msg($"[NoteArrayHooks] BMS 노트 로드: {_bmsNotes.Count}개");
            }

            var harmony = new HarmonyLib.Harmony("GUNVOLT_RECORDS_Cychronicle.NoteArrayHooks");
            var assemblyPath = Path.Combine(
                Path.GetDirectoryName(Application.dataPath),
                "GUNVOLT_RECORDS_Cychronicle_Data",
                "Managed",
                "Assembly-CSharp.dll");

            Assembly assembly = null;
            if (File.Exists(assemblyPath))
            {
                assembly = Assembly.LoadFrom(assemblyPath);
            }
            else
            {
                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                assembly = loadedAssemblies.FirstOrDefault(a => a.GetName().Name == "Assembly-CSharp");
            }

            Type managerType = null;
            if (assembly != null)
            {
                managerType = assembly.GetType("IntiCreates.cFairyModeNotesManager");
                MelonLogger.Msg($"[NoteArrayHooks] Assembly 로드: {assembly.GetName().Name}");
            }
            else
            {
                MelonLogger.Warning("[NoteArrayHooks] Assembly-CSharp를 찾을 수 없습니다.");
            }

            if (managerType == null)
            {
                MelonLogger.Warning("[NoteArrayHooks] cFairyModeNotesManager 타입을 찾을 수 없습니다.");
                return;
            }

            MelonLogger.Msg($"[NoteArrayHooks] cFairyModeNotesManager 타입 발견: {managerType.FullName}");
            PatchMethodWithPrefix(harmony, managerType, "createAllNote", nameof(CreateAllNotePrefix));
            PatchMethodWithPrefix(harmony, managerType, "loadFairyNoteDatasJsonToArray", nameof(LoadFairyNoteDatasJsonToArrayPrefix));
        }

        public static void CreateAllNotePrefix(object __instance)
        {
            TryInjectBmsNotes(__instance, "CreateAllNotePrefix");
        }

        public static void LoadFairyNoteDatasJsonToArrayPrefix(object __instance)
        {
            TryInjectBmsNotes(__instance, "LoadFairyNoteDatasJsonToArrayPrefix");
        }

        private static void TryInjectBmsNotes(object instance, string methodName)
        {
            try
            {
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

        private static void PatchMethodWithPrefix(HarmonyLib.Harmony harmony, Type managerType, string methodName, string prefixName)
        {
            var method = managerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null)
            {
                MelonLogger.Warning($"[NoteArrayHooks] {methodName} 메서드를 찾을 수 없습니다.");
                return;
            }

            var prefix = new HarmonyMethod(typeof(NoteArrayHooks).GetMethod(prefixName));
            harmony.Patch(method, prefix);
            MelonLogger.Msg($"[NoteArrayHooks] {methodName} 후킹 성공");
        }
    }
}
