using System;
using System.Collections.Generic;
using GRC2.Parsers;
using GRC2.Processors;

namespace GRC2.Converters
{
    public static partial class BmsNoteConverter
    {
        private static void ProcessHoldEndNotes(List<object> noteList, List<BmsNote> holdEndNotes, List<BmsNote> bmsNotes)
        {
            try
            {
                HoldNoteProcessor.ProcessHoldEndNotes(
                    noteList,
                    holdEndNotes,
                    bmsNotes,
                    Loaders.GameTypeLoader.NoteCreateDataType,
                    Loaders.GameTypeLoader.NoteDirectionIndexEnum,
                    Loaders.GameTypeLoader.NoteSizeEnum,
                    Builders.NoteCreateDataBuilder.GetBmsNoteFromNoteCreateData,
                    Builders.NoteCreateDataBuilder.CreateNoteCreateData,
                    Helpers.EnumValueHelper.GetEnumValue,
                    Helpers.FieldAccessHelper.SetFieldValue);
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, "[BmsNoteConverter]", "홀드 끝 노트 처리 중 오류");
            }
        }

        private static void ProcessFairyEndNotes(List<object> noteList, List<BmsNote> fairyEndNotes, List<BmsNote> bmsNotes)
        {
            try
            {
                FairyNoteProcessor.ProcessFairyEndNotes(
                    noteList,
                    fairyEndNotes,
                    bmsNotes,
                    Loaders.GameTypeLoader.NoteCreateDataType,
                    Loaders.GameTypeLoader.NoteDirectionIndexEnum,
                    Loaders.GameTypeLoader.NoteSizeEnum,
                    Builders.NoteCreateDataBuilder.GetBmsNoteFromNoteCreateData,
                    Builders.NoteCreateDataBuilder.CreateNoteCreateData,
                    Helpers.EnumValueHelper.GetEnumValue,
                    Helpers.FieldAccessHelper.SetFieldValue);
            }
            catch (Exception ex)
            {
                Helpers.ErrorLogger.LogException(ex, "[BmsNoteConverter]", "페어리 끝 노트 처리 중 오류");
            }
        }
    }
}
