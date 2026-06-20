// ╔═╦╗
// ║╬╠╬╦╗ TOverlap.cs
// ║╔╣╠║╣ Basic tests of overlap detection and ranging infrastructure
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
namespace FApp.Testing;

[Fixture (9, "Overlap detection and ranging", "Poly.Overlap")]
class OverlapTests {
   [Test (88, "Test Line overlap infrastructure")]
   void DetectLineOverlaps () {
      var dwg = new Dwg2 ();
      { // Line-Line (aligned) | Exact
         dwg.Ents.Clear ();
         Poly p = Poly.Line ((10, 10), (100, 10));
         Poly p2 = Poly.Line ((10, 10), (100, 10));
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is ("M10,10H100");
      }
      { // Line-Line (opposing) | Exact
         dwg.Ents.Clear ();
         Poly p = Poly.Line ((10, 10), (100, 10));
         Poly p2 = Poly.Line ((100, 10), (10, 10));
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is ("M10,10H100");
      }

      { // Line-Line (aligned) | Fully covered - Left aligned
         dwg.Ents.Clear ();
         Poly p = Poly.Line ((10, 10), (200, 10));
         Poly p2 = Poly.Line ((10, 10), (100, 10));
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is ("M10,10H100");
      }
      { // Line-Line (opposing) | Fully covered - Left aligned
         dwg.Ents.Clear ();
         Poly p = Poly.Line ((10, 10), (200, 10));
         Poly p2 = Poly.Line ((100, 10), (10, 10));
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is ("M10,10H100");
      }

      { // Line-Line (aligned) | Fully covered - Right aligned
         dwg.Ents.Clear ();
         Poly p = Poly.Line ((10, 10), (200, 10));
         Poly p2 = Poly.Line ((100, 10), (200, 10));
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is ("M100,10H200");
      }
      { // Line-Line (opposing) | Fully covered - Right aligned
         dwg.Ents.Clear ();
         Poly p = Poly.Line ((10, 10), (200, 10));
         Poly p2 = Poly.Line ((200, 10), (100, 10));
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is ("M100,10H200");
      }

      { // Line-Line (aligned) | Fully covered - Center aligned
         dwg.Ents.Clear ();
         Poly p = Poly.Line ((10, 10), (200, 10));
         Poly p2 = Poly.Line ((50, 10), (100, 10));
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is ("M50,10H100");
      }
      { // Line-Line (opposing) | Fully covered - Center aligned
         dwg.Ents.Clear ();
         Poly p = Poly.Line ((10, 10), (200, 10));
         Poly p2 = Poly.Line ((100, 10), (50, 10));
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is ("M50,10H100");
      }

      { // Line-Line (aligned) | Fully covered - Left crossover
         dwg.Ents.Clear ();
         Poly p = Poly.Line ((10, 10), (200, 10));
         Poly p2 = Poly.Line ((0, 10), (100, 10));
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is ("M10,10H100");
      }
      { // Line-Line (opposing) | Fully covered - Left crossover
         dwg.Ents.Clear ();
         Poly p = Poly.Line ((10, 10), (200, 10));
         Poly p2 = Poly.Line ((100, 10), (0, 10));
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is ("M10,10H100");
      }

      { // Line-Line (aligned) | Fully covered - Right crossover
         dwg.Ents.Clear ();
         Poly p = Poly.Line ((10, 10), (200, 10));
         Poly p2 = Poly.Line ((100, 10), (300, 10));
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is ("M100,10H200");
      }
      { // Line-Line (opposing) | Fully covered - Right crossover
         dwg.Ents.Clear ();
         Poly p = Poly.Line ((10, 10), (200, 10));
         Poly p2 = Poly.Line ((300, 10), (100, 10));
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is ("M100,10H200");
      }
   }

   [Test (89, "Test Line overlap infrastructure (extended)")]
   void DetectLineOverlaps2 () {
      var dwg = new Dwg2 ();
      { // 3 overlapping lines, nested in series
         dwg.Ents.Clear ();
         Poly p = Poly.Line ((10, 10), (100, 10));
         Poly p2 = Poly.Line ((20, 10), (90, 10));
         Poly p3 = Poly.Line ((30, 10), (50, 10));
         dwg.Add (p); dwg.Add (p2); dwg.Add (p3);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is ("M20,10H90");
      }
      {
         dwg.Ents.Clear ();
         Poly p = Poly.Line ((10, 10), (100, 10));
         Poly p2 = Poly.Line ((90, 10), (150, 10));
         Poly p3 = Poly.Line ((80, 10), (140, 10));
         dwg.Add (p); dwg.Add (p2); dwg.Add (p3);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is ("M80,10H140");
      }
      {
         dwg.Ents.Clear ();
         Poly p = Poly.Line ((10, 10), (100, 10));
         Poly p2 = Poly.Line ((90, 10), (150, 10));
         Poly p3 = Poly.Line ((80, 10), (160, 10));
         dwg.Add (p); dwg.Add (p2); dwg.Add (p3);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is ("M80,10H150");
      }
      { // Mega overlap: 4 lines
         dwg.Ents.Clear ();
         Poly p = Poly.Line ((10, 10), (100, 10));
         Poly p2 = Poly.Line ((20, 10), (50, 10));
         Poly p3 = Poly.Line ((30, 10), (60, 10));
         Poly p4 = Poly.Line ((55, 10), (120, 10));
         dwg.Add (p); dwg.Add (p2); dwg.Add (p3); dwg.Add (p4);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is ("M20,10H100");
      }
   }

   [Test (90, "Test Arc overlap infrastructure")]
   void DetectArcOverlaps () {
      var dwg = new Dwg2 ();
      { // Arc-Arc (aligned) | Exact
         dwg.Ents.Clear ();
         Poly p = Poly.Arc ((100, -100), 45.D2R (), (100, 100));
         Poly p2 = Poly.Arc ((100, -100), 45.D2R (), (100, 100));
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is ("M100,-100Q100,100,1");
      }
      { // Arc-Arc (opposing) | Exact
         dwg.Ents.Clear ();
         Poly p = Poly.Arc ((100, -100), 45.D2R (), (100, 100));
         Poly p2 = Poly.Arc ((100, 100), -45.D2R (), (100, -100));
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is ("M100,-100Q100,100,1");
      }

      { // Arc-Arc (aligned) | | Fully covered - Left aligned
         dwg.Ents.Clear ();
         Poly p = Poly.Arc ((200, 200), 100, -60.D2R (), 60.D2R (), ccw: true);
         Poly p2 = Poly.Arc ((200, 200), 100, -60.D2R (), 30.D2R (), ccw: true);
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is (Poly.Arc ((200, 200), 100, -60.D2R (), 30.D2R (), ccw: true).ToString ());
      }
      { // Arc-Arc (opposing) | | Fully covered - Left aligned
         dwg.Ents.Clear ();
         Poly p = Poly.Arc ((200, 200), 100, -60.D2R (), 60.D2R (), ccw: true);
         Poly p2 = Poly.Arc ((200, 200), 100, 30.D2R (), -60.D2R (), ccw: false);
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is (Poly.Arc ((200, 200), 100, -60.D2R (), 30.D2R (), ccw: true).ToString ());
      }

      { // Arc-Arc (aligned) | | Fully covered - Right aligned
         dwg.Ents.Clear ();
         Poly p = Poly.Arc ((200, 200), 100, -60.D2R (), 60.D2R (), ccw: true);
         Poly p2 = Poly.Arc ((200, 200), 100, -30.D2R (), 60.D2R (), ccw: true);
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is (Poly.Arc ((200, 200), 100, -30.D2R (), 60.D2R (), ccw: true).ToString ());
      }
      { // Arc-Arc (opposing) | | Fully covered - Right aligned
         dwg.Ents.Clear ();
         Poly p = Poly.Arc ((200, 200), 100, -60.D2R (), 60.D2R (), ccw: true);
         Poly p2 = Poly.Arc ((200, 200), 100, 60.D2R (), -30.D2R (), ccw: false);
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is (Poly.Arc ((200, 200), 100, -30.D2R (), 60.D2R (), ccw: true).ToString ());
      }

      { // Arc-Arc (aligned) | | Fully covered - Center aligned
         dwg.Ents.Clear ();
         Poly p = Poly.Arc ((200, 200), 100, -60.D2R (), 60.D2R (), ccw: true);
         Poly p2 = Poly.Arc ((200, 200), 100, -30.D2R (), 30.D2R (), ccw: true);
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is (Poly.Arc ((200, 200), 100, -30.D2R (), 30.D2R (), ccw: true).ToString ());
      }
      { // Arc-Arc (opposing) | | Fully covered - Center aligned
         dwg.Ents.Clear ();
         Poly p = Poly.Arc ((200, 200), 100, -60.D2R (), 60.D2R (), ccw: true);
         Poly p2 = Poly.Arc ((200, 200), 100, 30.D2R (), -30.D2R (), ccw: false);
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is (Poly.Arc ((200, 200), 100, -30.D2R (), 30.D2R (), ccw: true).ToString ());
      }

      { // Arc-Arc (aligned) | | Fully covered - Left crossover (Shifted by -20)
         dwg.Ents.Clear ();
         Poly p = Poly.Arc ((200, 200), 100, -60.D2R (), 60.D2R (), ccw: true);
         Poly p2 = Poly.Arc ((200, 200), 100, -80.D2R (), 40.D2R (), ccw: true);
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is (Poly.Arc ((200, 200), 100, -60.D2R (), 40.D2R (), ccw: true).ToString ());
      }
      { // Arc-Arc (opposing) | | Fully covered - Left crossover (Shifted by -20)
         dwg.Ents.Clear ();
         Poly p = Poly.Arc ((200, 200), 100, -60.D2R (), 60.D2R (), ccw: true);
         Poly p2 = Poly.Arc ((200, 200), 100, 40.D2R (), -80.D2R (), ccw: false);
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is (Poly.Arc ((200, 200), 100, -60.D2R (), 40.D2R (), ccw: true).ToString ());
      }

      { // Arc-Arc (aligned) | | Fully covered - Right crossover (Shifted by +20)
         dwg.Ents.Clear ();
         Poly p = Poly.Arc ((200, 200), 100, -60.D2R (), 60.D2R (), ccw: true);
         Poly p2 = Poly.Arc ((200, 200), 100, -40.D2R (), 80.D2R (), ccw: true);
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is (Poly.Arc ((200, 200), 100, -40.D2R (), 60.D2R (), ccw: true).ToString ());
      }
      { // Arc-Arc (opposing) | | Fully covered - Right crossover (Shifted by +20)
         dwg.Ents.Clear ();
         Poly p = Poly.Arc ((200, 200), 100, -60.D2R (), 60.D2R (), ccw: true);
         Poly p2 = Poly.Arc ((200, 200), 100, 80.D2R (), -40.D2R (), ccw: false);
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is (Poly.Arc ((200, 200), 100, -40.D2R (), 60.D2R (), ccw: true).ToString ());
      }
   }

   [Test (91, "Test Arc & Circle overlap infrastructure (extended)")]
   void DetectArcOverlapsExtreme () {
      var dwg = new Dwg2 ();
      { // 3 overlapping arcs, nested in series
         dwg.Ents.Clear ();
         Poly p = Poly.Arc ((200, 200), 100, 10.D2R (), 80.D2R (), ccw: true);
         Poly p2 = Poly.Arc ((200, 200), 100, 20.D2R (), 70.D2R (), ccw: true);
         Poly p3 = Poly.Arc ((200, 200), 100, 30.D2R (), 60.D2R (), ccw: true);
         dwg.Add (p); dwg.Add (p2); dwg.Add (p3);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is (Poly.Arc ((200, 200), 100, 20.D2R (), 70.D2R (), ccw: true).ToString ());
      }
      { // 3 overlapping arcs
         dwg.Ents.Clear ();
         Poly p = Poly.Arc ((200, 200), 100, 10.D2R (), 80.D2R (), ccw: true);
         Poly p2 = Poly.Arc ((200, 200), 100, 20.D2R (), 50.D2R (), ccw: true);
         Poly p3 = Poly.Arc ((200, 200), 100, 40.D2R (), 60.D2R (), ccw: true);
         dwg.Add (p); dwg.Add (p2); dwg.Add (p3);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is (Poly.Arc ((200, 200), 100, 20.D2R (), 60.D2R (), ccw: true).ToString ());
      }
   }

   [Test (92, "Test Circle overlap infrastructure")]
   void DetectCircleOverlaps () {
      var dwg = new Dwg2 ();
      { // Circle-Circle
         dwg.Ents.Clear ();
         Poly p = Poly.Circle ((200, 200), 100);
         Poly p2 = Poly.Circle ((200, 200), 100);
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is ("C200,200,100");
      }

      // Following tests are marked "Basic" or "Edge case" due to issues arising
      //    from the way the lies are measured along Circle (always CCW) and Arc (maybe CCW or CW)

      { // Arc-Circle (aligned) | Basic
         dwg.Ents.Clear ();
         Poly p = Poly.Circle ((200, 200), 100);
         Poly p2 = Poly.Arc ((200, 200), 100, 30.D2R (), 60.D2R (), ccw: true);
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [..  GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is (Poly.Arc ((200, 200), 100, 30.D2R (), 60.D2R (), ccw: true).ToString ());
      }
      { // Arc-Circle (opposing) | Basic
         dwg.Ents.Clear ();
         Poly p = Poly.Circle ((200, 200), 100);
         Poly p2 = Poly.Arc ((200, 200), 100, 60.D2R (), 30.D2R (), ccw: false);
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is (Poly.Arc ((200, 200), 100, 30.D2R (), 60.D2R (), ccw: true).ToString ());
      }

      { // Arc-Circle (aligned) | Edge case (Crossing over 0 degree mark)
         dwg.Ents.Clear ();
         Poly p = Poly.Circle ((200, 200), 100);
         Poly p2 = Poly.Arc ((200, 200), 100, -60.D2R (), 60.D2R (), ccw: true);
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is (Poly.Arc ((200, 200), 100, -60.D2R (), 60.D2R (), ccw: true).ToString ());
      }
      { // Arc-Circle (opposing) | Edge case (Crossing over 0 degree mark)
         dwg.Ents.Clear ();
         Poly p = Poly.Circle ((200, 200), 100);
         Poly p2 = Poly.Arc ((200, 200), 100, 60.D2R (), -60.D2R (), ccw: false);
         dwg.Add (p); dwg.Add (p2);
         List<Poly> polys = [.. GetOverlappingSections (dwg)];
         polys.Count.Is (1);
         polys[0].Is (Poly.Arc ((200, 200), 100, -60.D2R (), 60.D2R (), ccw: true).ToString ());
      }
   }

   static IEnumerable<Poly> GetOverlappingSections (Dwg2 dwg) {
      var ovr = new DwgOverlap (dwg);
      return ovr.GetOverlapSectionRepPoly ();
   }
}
