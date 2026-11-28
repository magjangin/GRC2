using System;
using System.Collections.Generic;
using System.Linq;
using GRC2.Core;
using GRC2.Parsers;
using Xunit;

namespace GRC2.Tests
{
    public class AuditRound10Tests
    {
        static AuditRound10Tests()
        {
            TestAssemblyResolver.Ensure();
        }

        [Fact]
        public void BmsParser_ExtractHeaderMetadata_ExtractsTitleAndArtist()
        {
            var bmsText = @"
// BMS Header Comment
#TITLE Freedom Dive [FOUR DIMENSIONS]
#ARTIST xi
#GENRE Hardcore
#BPM 222.22
#00111:0100
";
            var (title, artist) = BmsParser.ExtractHeaderMetadata(bmsText);

            Assert.Equal("Freedom Dive [FOUR DIMENSIONS]", title);
            Assert.Equal("xi", artist);
        }

        [Fact]
        public void BmsParser_ExtractHeaderMetadata_CaseInsensitiveAndColonSupport()
        {
            var bmsText = @"
#title: Conflict
#artist: SiQlo + wa.
#00111:0100
";
            var (title, artist) = BmsParser.ExtractHeaderMetadata(bmsText);

            Assert.Equal("Conflict", title);
            Assert.Equal("SiQlo + wa.", artist);
        }

        [Fact]
        public void BmsParser_ExtractHeaderMetadata_StopsAtMeasureData()
        {
            var bmsText = @"
#TITLE First Title
#00001:0000
#TITLE Second Title In Data Section
";
            var (title, artist) = BmsParser.ExtractHeaderMetadata(bmsText);

            Assert.Equal("First Title", title);
            Assert.Null(artist);
        }

        [Fact]
        public void FieldContracts_ContainsNoteCreateDataKeyFields()
        {
            var noteCreateDataContracts = GameCompatibility.FieldContracts
                .Where(c => c.Owner.Name == "NoteCreateData")
                .Select(c => c.Name)
                .ToList();

            Assert.Contains("perfectSample", noteCreateDataContracts);
            Assert.Contains("laneLeftRightID", noteCreateDataContracts);
            Assert.Contains("subLaneID", noteCreateDataContracts);
            Assert.Contains("noteTypeID", noteCreateDataContracts);
            Assert.Contains("directionIndex", noteCreateDataContracts);
            Assert.Contains("turnDireciton", noteCreateDataContracts);
            Assert.Contains("connectNodeDataArray", noteCreateDataContracts);
            Assert.Contains("simultaneouslyNote", noteCreateDataContracts);
            Assert.Contains("isLast", noteCreateDataContracts);
        }

        [Fact]
        public void FieldContracts_NoteCreateData_AllFieldsPassVerification()
        {
            var broken = GameCompatibility.FindBrokenFieldContracts();
            var brokenNoteCreateData = broken
                .Where(b => b.Contains("NoteCreateData"))
                .ToList();

            Assert.Empty(brokenNoteCreateData);
        }
    }
}
