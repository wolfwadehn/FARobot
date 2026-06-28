// ╔═╦╗
// ║╬╠╬╦╗ TPixDwg.cs
// ║╔╣╠║╣ Tests for FRobot-Dwg
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using FRobot.Testing;
namespace FRobot;

[Fixture (10, "Freak Dwg Bound Issue Tests", "DXF")]
class BoundTests {
   [Test (96, "Empty E2Text entity empty bound issue", Skip = false)]
   void Test () {
      var dwg = DXFReader.Load (PT.File ("Dxf/Triceratops2.dxf")); // This helps recreate Empty E2Text (which results in Empty bound)
      double.IsFinite (dwg.Bound.Width).IsTrue ();
      double.IsFinite (dwg.Bound.Height).IsTrue ();
   }


}

[Fixture (11, "User reported issues", "User")]
class UserTests {
   [Test (99, "#415 Inch unit support")]
   void Test () {
      var dwg = DXFReader.Load (PT.File ("Dxf/8-CZS-H.dxf"));
      CurlWriter.Save (dwg, PT.TmpCurl);
      Assert.TextFilesEqual ("Dxf/Out/8-CZS-H.curl", PT.TmpCurl);
   }
}
