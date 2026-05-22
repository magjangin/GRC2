using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using GRC2.Core;
using GRC2.Helpers;
using GRC2.Parsers;

namespace GRC2.Harmony.Hooks
{
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
