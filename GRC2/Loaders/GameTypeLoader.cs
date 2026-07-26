using System;
using IntiCreates;
using IntiCreates.RythmGame;
using IntiCreates.RythmGame.FairyMode;

namespace GRC2.Loaders
{
    /// <summary>
    /// Assembly-CSharp에 직접 참조된 게임 노트 타입을 제공합니다.
    /// </summary>
    public static class GameTypeLoader
    {
        public static Type NoteCreateDataType { get; } =
            typeof(FairyNoteEditorLoader.NoteCreateData);
        public static Type NoteTypeIdEnum { get; } = typeof(NoteTypeId);
        public static Type NoteLaneLeftRightEnum { get; } =
            typeof(NoteLaneLeftRight);
        public static Type NoteSubLaneTypeEnum { get; } =
            typeof(NoteSubLaneType);
        public static Type NoteDirectionIndexEnum { get; } =
            typeof(NoteDirectionIndex);
        public static Type NoteSizeEnum { get; } = typeof(NoteSize);
    }
}
