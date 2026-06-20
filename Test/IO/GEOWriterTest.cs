// ╔═╦╗
// ║╬╠╬╦╗ GEOWriterTest
// ║╔╣╠║╣ Tests for GEOWriter
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────

using FApp.Testing;
namespace FApp;

[Fixture (7, "GEOWriter tests", "GEO")]
class GeoWriterTests {
   [Test (69, "Basic test to write a geo file")]
   void Test20 () {
      GEOWriter.Save (Load ("IO/GEO/Basic.dxf"), PT.TmpGEO);
      Assert.TextFilesEqual ("IO/GEO/OUT/Basic.geo", PT.TmpGEO);
   }

   [Test (70, "Test for bend lines entities")]
   void Test21 () {
      GEOWriter.Save (Load ("IO/GEO/BasicBend.dxf"), PT.TmpGEO);
      Assert.TextFilesEqual ("IO/GEO/OUT/BasicBend.geo", PT.TmpGEO);
   }

   [Test (71, "Test for bend lines with text entities")]
   void Test22 () {
      GEOWriter.Save (Load ("IO/GEO/Bend-10.dxf"), PT.TmpGEO);
      Assert.TextFilesEqual ("IO/GEO/OUT/Bend-10.geo", PT.TmpGEO);
   }

   static Dwg2 Load (string file)
      => new DXFReader (PT.File (file)) { WhiteToBlack = true, DarkenColors = true, StitchThreshold = 0.0001 }.Load ();
}