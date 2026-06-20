namespace FApp.Testing;

[Fixture (8, "Poly diff coverage tests", "Poly.Diff")]
class PolyDiffTests {
   [Test (79, "Seg slice overlap computation tests")]
   void Test () {
      // Computation symmetry check [swap the participants]

      // Line-line
      Poly line = Poly.Line ((10, 10), (10 + 50, 10)); // Length: 50
      {
         Poly line2 = Poly.Line ((10, 10), (10 + 50, 10)); // Length: 50
         FreePoly fp = new (line), fp2 = new (line2);
         fp.GetFirst (out FreeSegSlice ss); fp2.GetFirst (out FreeSegSlice ss2);
         ss.ComputeOverlap (ss2, out Bound1 ovrA, out Bound1 ovrB);
         ovrA.EQ ((0, 1)).IsTrue (); ovrB.EQ ((0, 1)).IsTrue ();
      }

      {
         Poly line2 = Poly.Line ((10, 10), (10 + 40, 10)); // Length: 40
         FreePoly fp = new (line), fp2 = new (line2);
         fp.GetFirst (out FreeSegSlice ss); fp2.GetFirst (out FreeSegSlice ss2);
         ss.ComputeOverlap (ss2, out Bound1 ovrA, out Bound1 ovrB);
         ovrA.EQ ((0, 0.8)).IsTrue (); ovrB.EQ ((0, 1)).IsTrue ();
      }

      // Line-line (opposition)
      {
         //Poly line2 = Poly.Line ((10 + 50, 10), (10, 10)); // Length: 50
         //FreePoly fp = new (line), fp2 = new (line2);
         //fp.GetFirst (out FreeSegSlice ss); fp2.GetFirst (out FreeSegSlice ss2);
         //ss.ComputeOverlap (ss2, out Bound1 ovrA, out Bound1 ovrB);
         //ovrA.EQ ((0, 1)).IsTrue (); ovrB.EQ ((0, 1)).IsTrue ();
      }

      {
         //Poly line2 = Poly.Line ((10 + 40, 10), (10, 10)); // Length: 40
         //FreePoly fp = new (line), fp2 = new (line2);
         //fp.GetFirst (out FreeSegSlice ss); fp2.GetFirst (out FreeSegSlice ss2);
         //ss.ComputeOverlap (ss2, out Bound1 ovrA, out Bound1 ovrB);
         //ovrA.EQ ((0, 0.8)).IsTrue (); ovrB.EQ ((0, 1)).IsTrue ();
      }

      // Arc-arc
      Poly arc = Poly.Parse ("M100,0Q0,100,3");
      {
         Poly arc2 = Poly.Parse ("M100,0Q0,100,3"); // Full overlap
         FreePoly fp = new (arc), fp2 = new (arc2);
         fp.GetFirst (out FreeSegSlice ss); fp2.GetFirst (out FreeSegSlice ss2);
         ss.ComputeOverlap (ss2, out Bound1 ovrA, out Bound1 ovrB);
         ovrA.EQ ((0, 1)).IsTrue (); ovrB.EQ ((0, 1)).IsTrue ();
      }

      {
         Poly arc2 = Poly.Parse ("M100,0Q100,200,2"); // Partial overlap
         FreePoly fp = new (arc), fp2 = new (arc2);
         fp.GetFirst (out FreeSegSlice ss); fp2.GetFirst (out FreeSegSlice ss2);
         ss.ComputeOverlap (ss2, out Bound1 ovrA, out Bound1 ovrB);
         ovrA.EQ ((0, 2.0 / 3)).IsTrue (); ovrB.EQ ((0, 1)).IsTrue ();
      }

      {
         Poly arc2 = Poly.Parse ("M200,100Q0,100,2"); // Partial overlap
         FreePoly fp = new (arc), fp2 = new (arc2);
         fp.GetFirst (out FreeSegSlice ss); fp2.GetFirst (out FreeSegSlice ss2);
         ss.ComputeOverlap (ss2, out Bound1 ovrA, out Bound1 ovrB);
         ovrA.EQ ((1.0 / 3, 1)).IsTrue (); ovrB.EQ ((0, 1)).IsTrue ();
      }

      {
         Poly arc2 = Poly.Parse ("M200,100Q100,200,1"); // Partial overlap
         FreePoly fp = new (arc), fp2 = new (arc2);
         fp.GetFirst (out FreeSegSlice ss); fp2.GetFirst (out FreeSegSlice ss2);
         ss.ComputeOverlap (ss2, out Bound1 ovrA, out Bound1 ovrB);
         ovrA.EQ ((1.0 / 3, 2.0 / 3)).IsTrue (); ovrB.EQ ((0, 1)).IsTrue ();
      }

      // Arc-arc (opposition)
      {
         //Poly arc2 = Poly.Parse ("M0,100Q100,0,-3"); // Full overlap (opposition)
         //FreePoly fp = new (arc), fp2 = new (arc2);
         //fp.GetFirst (out FreeSegSlice ss); fp2.GetFirst (out FreeSegSlice ss2);
         //ss.ComputeOverlap (ss2, out Bound1 ovrA, out Bound1 ovrB).IsTrue ();
         //ovrA.EQ ((0, 1)).IsTrue (); ovrB.EQ ((0, 1)).IsTrue ();
      }

      // Circle-circle
      // Circle-circle (opposition)

      // Circle-arc
      // Circle-arc (opposition)
   }

   [Test (80, "Poly coverage (tracker) tests")]
   void Test2 () {
      // Note: Would perhaps need additional methods to access internal state (for testing purposes only!)
      // [ ] Closed v/s Open

      // GetFirst | GetNext - on uncovered (open) Poly
      Poly p = Poly.Parse ("M0,0H100V50H25"); // 3-Seg Open
      {
         FreePoly fp = new (p);
         fp.GetFirst (out FreeSegSlice ss).IsTrue (); ss.PolyExtent.EQ ((0, 1)).IsTrue ();
         fp.GetNext (ref ss).IsTrue (); ss.PolyExtent.EQ ((1, 2)).IsTrue ();
         fp.GetNext (ref ss).IsTrue (); ss.PolyExtent.EQ ((2, 3)).IsTrue ();
         fp.GetNext (ref ss).IsFalse ();
      }

      Poly p2 = Poly.Parse ("M0,0H100L50,40Z"); // 3-Seg Closed
      {
         FreePoly fp = new (p2);
         fp.GetFirst (out FreeSegSlice ss).IsTrue (); ss.PolyExtent.EQ ((0, 1)).IsTrue ();
         fp.GetNext (ref ss).IsTrue (); ss.PolyExtent.EQ ((1, 2)).IsTrue ();
         fp.GetNext (ref ss).IsTrue (); ss.PolyExtent.EQ ((2, 3)).IsTrue ();
         fp.GetNext (ref ss).IsFalse ();
      }

      // Coverage update test - verified via GetFirst | GetNext
      // GetFirst | GetNext - on partially covered Poly
      // GetFirst - on fully covered Poly
   }

   [Test (81, "Poly coverage interface test")]
   void Test3 () {
      // Trim test
      Poly rect = Poly.Parse ("M0,0H100V50H0Z");
      Poly trimmedRect = Poly.Parse ("M20,50H0V0H100V50H80");

      // Feedback poly computation
      var cvg = PolyCoverage.ComputeFeedback (rect, [trimmedRect]);
      List<Poly> polys = [.. cvg.GetFreeSlicePolys ()];
      polys.Count.Is (1);
      polys [0].Is ("M79.999995,50H20.000005"); // "M80,50H20"
   }
}
