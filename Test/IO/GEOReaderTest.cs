// ╔═╦╗
// ║╬╠╬╦╗ GEOReaderTest.cs
// ║╔╣╠║╣ Tests for GEOReader
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using FApp.Testing;
namespace FApp;

[Fixture (2, "Basic GEO Tests", "GEO")]
class GEOTests {
   [Test (72, "Basic GEO load test")]
   void Test19 () {
      var dwg = GEOReader.Load (PT.File ("GEO/Basic.geo"));
      CurlWriter.Save (dwg, PT.TmpCurl);
      Assert.TextFilesEqual ("GEO/Out/Basic.curl", PT.TmpCurl);
      RoundTrip ("GEO/Out/Basic.curl");
   }

   static void RoundTrip (string file) {
      if (!Path.IsPathRooted (file)) file = PT.File (file);
      var obj = CurlReader.Load (file);
      CurlWriter.Save (obj, PT.TmpCurl);
      Assert.TextFilesEqual (file, PT.TmpCurl);
   }

   [Test (98, "GEO load test: FIL entity")]
   void LoadGeoWithFIL () {
      var dwg = GEOReader.Load (PT.File ("GEO/2mm_Profilhalte_Wa_03.geo"));
      CurlWriter.Save (dwg, PT.TmpCurl);
      Assert.TextFilesEqual ("GEO/Out/2mm_Profilhalte_Wa_03.curl", PT.TmpCurl);
   }
}
