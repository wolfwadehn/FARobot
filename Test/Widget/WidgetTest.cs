// ╔═╦╗
// ║╬╠╬╦╗ WidgetTest
// ║╔╣╠║╣ Basic tests of the Widgets
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using FApp.Widgets;
namespace FApp.Testing;
using static DwgHub;

[Fixture (1, "Basic widget tests", "Widget")]
class WidgetTests {
   [Test (1, "Test LINE widget")]
   void Line () {
      var dwg = Begin (ECmd.Line);
      OnClick ((10, 10)); OnClick ((14, 13));
      OnClick ((11, 10), true);

      KeyboardMode = true;
      OnTypeIn (0, "16,13"); OnTypeIn (1, "21,8"); Enter ();
      OnTypeIn (0, "17,13"); OnTypeIn (1, "@4,-4"); Enter ();
      OnTypeIn (0, "18,13"); OnTypeIn (1, "@3<-45"); Enter ();
      End (dwg, "Line.dxf");
   }

   [Test (2, "Test CIRCLE widget")]
   void Circle () {
      var dwg = Begin (ECmd.Circle);
      OnClick ((10, 10)); OnClick ((15, 10));
      OnClick ((21, 10), true);

      KeyboardMode = true;
      OnTypeIn (0, "10,20"); OnTypeIn (1, "4"); Enter ();
      End (dwg, "Circle.dxf");
   }

   [Test (3, "Test ARC widget")]
   void Arc () {
      var dwg = Begin (ECmd.Arc);
      OnClick ((5, 5)); OnClick ((10, 15)); OnClick ((0, 5), true); // cw
      OnClick ((10, 10)); OnClick ((15, 10)); OnClick ((8, 12));    // ccw
      OnClick ((13, 13), true);

      KeyboardMode = true;
      OnTypeIn (0, "10,0"); OnTypeIn (1, "3"); OnTypeIn (2, "0"); OnTypeIn (3, "-135"); Enter (iCtrl: true); // cw
      OnTypeIn (0, "10,0"); OnTypeIn (1, "4"); OnTypeIn (2, "90"); OnTypeIn (3, "135"); Enter (); // ccw
      End (dwg, "Arc.dxf");
   }

   [Test (4, "Test RECT widget")]
   void Rect () {
      var dwg = Begin (ECmd.Rect);
      OnClick ((5, 5)); OnClick ((50, 25));
      OnClick ((0, 30), true);

      KeyboardMode = true;
      OnTypeIn (0, "-25,-50"); OnTypeIn (1, "0,0"); Enter ();
      OnTypeIn (0, "100,50"); OnTypeIn (1, "@-45,-20"); Enter ();
      End (dwg, "Rect.dxf");
   }

   [Test (5, "Test RECTCENTER widget")]
   void RectCenter () {
      var dwg = Begin (ECmd.RectCenter);
      OnClick ((30, 20)); OnClick ((60, 40)); OnClick ((0, 30), true);

      KeyboardMode = true;
      OnTypeIn (0, "0,0"); OnTypeIn (1, "25,15"); Enter ();
      OnTypeIn (0, "100,50"); OnTypeIn (1, "@30,20"); Enter ();
      End (dwg, "RectCenter.dxf");
   }

   [Test (6, "Test POINT widget")]
   void Point () {
      var dwg = Begin (ECmd.Point);
      OnClick ((30, 20));

      KeyboardMode = true;
      OnTypeIn (0, "25,15"); Enter ();
      OnTypeIn (0, "@3<45"); Enter ();
      End (dwg, "Point.dxf");
   }

   [Test (7, "Test CHAMFER widget")]
   void Chamfer () {
      Test ("Chamfer1.dxf", 10, 4, false);
      Test ("Chamfer2.dxf", 10, 2, true);
      Test ("Chamfer3.dxf", 6, 6, false);
      Test ("Chamfer4.dxf", 10, 2, true);
      Test ("Chamfer5.dxf", 10, 5, true); // Issue.140: Disable mirroring for non-rectangular polygons.

      static void Test (string file, double d1, double d2, bool iCtrl) {
         var dwg = Begin (ECmd.Chamfer, file);
         OnTypeIn (0, d1.S6 ()); OnTypeIn (1, d2.S6 ());
         foreach (var pt in dwg.Points.ToList ())
            OnClick (pt, iCtrl);
         End (dwg, file);
      }
   }

   [Test (8, "Test INFILLET widget")]
   void InFillet () {
      Test ("InFillet1.dxf", 20, false);
      Test ("InFillet2.dxf", 20, true);
      Test ("InFillet3.dxf", 20, true);
      Test ("InFillet4.dxf", 20, true);
      Test ("InFillet5.dxf", 20, true);

      static void Test (string file, double r, bool iCtrl) {
         var dwg = Begin (ECmd.InFillet, file);
         OnTypeIn (0, r.S6 ());
         foreach (var pt in dwg.Points.ToList ())
            OnClick (pt, iCtrl);
         End (dwg, file);
      }
   }

   [Test (9, "Test POLYINSCRIBE widget")]
   void PolyInscribe () {
      var dwg = Begin (ECmd.PolyInscribe);
      OnTypeIn (0, "6"); OnClick (new (0, 0)); OnClick (new (0, 5));
      OnClick (new (10, 0), true);

      KeyboardMode = true;
      OnTypeIn (0, "6"); OnTypeIn (1, "5,10"); OnTypeIn (2, "6"); OnTypeIn (3, "45"); Enter ();
      End (dwg, "PolyInscribe.dxf");
   }

   [Test (10, "Test POLYCIRCUMSCRIBE widget")]
   void PolyCircumscribe () {
      var dwg = Begin (ECmd.PolyCircumscribe);
      OnClick ((10, 10)); OnClick ((30, 10));

      KeyboardMode = true;
      OnTypeIn (0, "5"); OnTypeIn (1, "50,35"); OnTypeIn (2, "10"); OnTypeIn (3, "45"); Enter ();
      End (dwg, "PolyCircumscribe.dxf");
   }

   [Test (11, "Test CIRCLE2P widget")]
   void Circle2P () {
      var dwg = Begin (ECmd.Circle2P);
      OnTypeIn (0, "25"); OnClick ((10, 10)); OnClick ((20, 20), true);
      OnClick ((0, 50), true);
      KeyboardMode = true;
      OnTypeIn (0, "20"); OnTypeIn (1, "25,25"); OnTypeIn (2, "45,45"); Enter ();
      End (dwg, "Circle2P.dxf");
   }

   [Test (12, "Test ARC2P widget")]
   void Arc2P () {
      var dwg = Begin (ECmd.Arc2P);
      OnTypeIn (0, "10"); OnClick ((10, 10)); OnClick ((20, 20), iCtrl: true);  // cw
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M10,10Q20,20,-1");

      OnTypeIn (0, "10"); OnClick ((11, 10)); OnClick ((21, 20), iShift: true); // ccw
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M11,10Q21,20,3");

      OnClick ((30, 20), iCtrl: true); // Repeat last cmd
      ((E2Poly)dwg.Ents[2]).Poly.Is ("M30,20Q40,30,3");

      KeyboardMode = true;
      OnTypeIn (0, "5"); OnTypeIn (1, "50,50"); OnTypeIn (2, "60,60"); Enter ();  // ccw
      ((E2Poly)dwg.Ents[3]).Poly.Is ("M50,50Q60,60,2");

      OnTypeIn (0, "10"); OnTypeIn (1, "70,70"); OnTypeIn (2, "80,80"); Enter (); // ccw
      ((E2Poly)dwg.Ents[4]).Poly.Is ("M70,70Q80,80,1");

      dwg.Ents.Count.Is (5);
   }

   [Test (13, "Test ARC3P widget")]
   void Arc3P () {
      var dwg = Begin (ECmd.Arc3P);
      OnClick ((10, 10)); OnClick ((20, 30)); OnClick ((30, 10)); // cw
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M10,10Q30,10,-2.819331");

      OnClick ((5, 5)); OnClick ((10, 3)); OnClick ((20, 10));    // ccw
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M5,5Q20,10,1.262076");

      OnClick ((0, 30), true); // Repeat last cmd
      ((E2Poly)dwg.Ents[2]).Poly.Is ("M0,30Q15,35,1.262076");

      KeyboardMode = true;
      OnTypeIn (0, "40,10"); OnTypeIn (1, "40,20"); OnTypeIn (2, "40,15"); Enter (); // line
      ((E2Poly)dwg.Ents[3]).Poly.Is ("M40,10V15");

      OnTypeIn (0, "0,0"); OnTypeIn (1, "10,20"); OnTypeIn (2, "@20,0"); Enter ();   // cw
      ((E2Poly)dwg.Ents[4]).Poly.Is ("M0,0Q30,20,-1.409666");

      OnTypeIn (0, "1,0"); OnTypeIn (1, "11,20"); OnTypeIn (2, "@-20,10"); Enter (); // ccw
      ((E2Poly)dwg.Ents[5]).Poly.Is ("M1,0Q-9,30,2");

      dwg.Ents.Count.Is (6);
   }

   [Test (14, "Test CIRCLE3P widget")]
   void Circle3P () {
      var dwg = Begin (ECmd.Circle3P);
      OnClick ((100, 10)); OnClick ((30, 10)); OnClick ((70, 50));
      OnClick ((0, 0)); OnClick ((200, 0)); OnClick ((150, 0));
      OnClick ((0, 0)); OnClick ((200, 0)); OnClick ((150, 0));

      KeyboardMode = true;
      OnTypeIn (0, "100, 10"); OnTypeIn (1, "@-70, 10"); OnTypeIn (2, "0, 10"); Enter ();
      End (dwg, "Circle3P.dxf");
   }

   [Test (15, "Test CORNERSTEP widget")]
   void CornerStep () {
      Test ("CornerStep1.dxf", 20, 10, false);
      Test ("CornerStep2.dxf", 20, 10, false);
      Test ("CornerStep3.dxf", 20, 10, false);
      Test ("CornerStep4.dxf", 20, 10, false);
      Test ("CornerStep5.dxf", 60, 40, false);
      Test ("CornerStep6.dxf", 20, 10, true);
      Test ("CornerStep7.dxf", 20, 10, true);
      Test ("CornerStep8.dxf", 10, 5, true); // Issue.140: Disable mirroring for non-rectangular polygons.

      static void Test (string file, double d1, double d2, bool iCtrl) {
         var dwg = Begin (ECmd.CornerStep, file);
         OnTypeIn (0, d1.S6 ()); OnTypeIn (1, d2.S6 ());
         foreach (var pt in dwg.Points.ToList ())
            OnClick (pt, iCtrl);
         End (dwg, file);
      }
   }

   [Test (16, "Test POLYEDGE widget")]
   void PolyEdge () {
      var dwg = Begin (ECmd.PolyEdge);
      OnTypeIn (0, "6"); OnClick (new (0, 0)); OnClick (new (0, 5));
      OnClick (new Point2 (0, 15), true);

      KeyboardMode = true;
      OnTypeIn (0, "3"); OnTypeIn (1, "5,10"); OnTypeIn (2, "10,10"); Enter ();
      OnTypeIn (0, "4"); OnTypeIn (1, "15,0"); OnTypeIn (2, "20,5"); Enter ();
      OnTypeIn (0, "6"); OnTypeIn (1, "2,0"); OnTypeIn (2, "4,0"); Enter ();
      End (dwg, "PolyEdge.dxf");
   }

   [Test (17, "Test ARCTANGENT widget")]
   void ArcTangent () {
      var dwg = Begin (ECmd.ArcTangent);
      OnClick (new (0, 0)); OnClick (new (0, 10)); OnClick (new (10, 0)); // cw
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M0,0Q10,0,-2");

      OnClick (new (20, 20), true); // Repeat last cmd
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M20,20Q30,20,-2");

      OnClick (new (5, 0)); OnClick (new (5, -10)); OnClick (new (15, 0)); // ccw
      ((E2Poly)dwg.Ents[2]).Poly.Is ("M5,0Q15,0,2");

      OnClick (new (10, 10)); OnClick (new (10, 15)); OnClick (new (10, 25)); // line
      ((E2Poly)dwg.Ents[3]).Poly.Is ("M10,10V25");

      KeyboardMode = true;
      OnTypeIn (0, "20,10"); OnTypeIn (1, "135"); OnTypeIn (2, "30,10"); Enter ();  // cw
      ((E2Poly)dwg.Ents[4]).Poly.Is ("M20,10Q30,10,-3");

      OnTypeIn (0, "50,10"); OnTypeIn (1, "-135"); OnTypeIn (2, "65,10"); Enter (); // ccw
      ((E2Poly)dwg.Ents[5]).Poly.Is ("M50,10Q65,10,3");

      OnTypeIn (0, "11,11"); OnTypeIn (1, "45"); OnTypeIn (2, "15,15"); Enter ();  // line
      ((E2Poly)dwg.Ents[6]).Poly.Is ("M11,11L15,15");

      dwg.Ents.Count.Is (7);
   }

   [Test (18, "Test FILLET widget")]
   void Fillet () {
      Test ("Fillet1.dxf", 5, false);
      Test ("Fillet2.dxf", 5, true);
      Test ("Fillet3.dxf", 10, false);
      Test ("Fillet4.dxf", 10, true);
      Test ("Fillet5.dxf", 5, false);
      Test ("Fillet6.dxf", 5, true);
      Test ("Fillet7.dxf", 5, false);
      Test ("Fillet8.dxf", 5, true); // CTRL option with OPEN Poly!

      static void Test (string file, double radius, bool iCtrl) {
         var dwg = Begin (ECmd.Fillet, file);
         OnTypeIn (0, radius.S6 ());
         foreach (var pt in dwg.Points.ToList ())
            OnClick (pt, iCtrl);
         End (dwg, file);
      }
   }

   [Test (19, "Test EDGERECESS widget")]
   void EdgeRecess () {
      Test ("EdgeRecess1.dxf");
      Test ("EdgeRecess2.dxf");

      static void Test (string file) {
         var dwg = Begin (ECmd.EdgeRecess, file);
         Point2[] pts = [.. dwg.Ents.OfType<E2Point> ().Select (e => e.Pt)];
         pts.ForEach (pt => OnClick (pt)); // Two steps: To avoid Ents modified runtime error.
         End (dwg, file);
      }
   }

   [Test (25, "Test POLAR-ARRAY widget")]
   void PolarArray () {
      var dwg = Begin (ECmd.PolarArray);
      OnTypeIn (0, "4");
      dwg.Add (Poly.Line ((20, 10), (20, -10)));
      dwg.Ents.First ().IsSelected = true;

      // CCW direction spread
      OnClick ((0, 0)); OnClick ((10, 0)); OnClick ((0, 10));
      dwg.Ents.Count.Is (4);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M20,10V-10");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M-10,20H10");
      ((E2Poly)dwg.Ents[2]).Poly.Is ("M-20,-10V10");
      ((E2Poly)dwg.Ents[3]).Poly.Is ("M10,-20H-10");

      // CW direction spread
      dwg.RemoveOrdered ([.. dwg.Ents.Skip (1)]); // Purge copies made earlier!
      OnClick ((0, 0)); OnClick ((10, 0)); OnClick ((0, -10));
      dwg.Ents.Count.Is (4);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M20,10V-10");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M10,-20H-10");
      ((E2Poly)dwg.Ents[2]).Poly.Is ("M-20,-10V10");
      ((E2Poly)dwg.Ents[3]).Poly.Is ("M-10,20H10");

      KeyboardMode = true;
      dwg.RemoveOrdered ([.. dwg.Ents.Skip (1)]); // Purge copies made earlier!
      OnTypeIn (1, "0, 0"); OnTypeIn (2, "90"); Enter ();
      dwg.Ents.Count.Is (4);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M20,10V-10");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M-10,20H10");
      ((E2Poly)dwg.Ents[2]).Poly.Is ("M-20,-10V10");
      ((E2Poly)dwg.Ents[3]).Poly.Is ("M10,-20H-10");
   }

   [Test (26, "Test RECTARRAY widget")]
   void RectArray () {
      var dwg = Begin (ECmd.RectArray);
      OnTypeIn (0, "2"); OnTypeIn (1, "2"); OnTypeIn (2, "30"); OnTypeIn (3, "40"); OnTypeIn (4, "0");
      dwg.Add (Poly.Circle (new (10, 10), 15));
      dwg.Ents.First ().IsSelected = true;

      // Mouse mode
      OnClick ((10, 10)); OnClick ((50, 50));
      dwg.Ents.Count.Is (4);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("C10,10,15");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("C10,50,15");
      ((E2Poly)dwg.Ents[2]).Poly.Is ("C50,10,15");
      ((E2Poly)dwg.Ents[3]).Poly.Is ("C50,50,15");

      // Keyboard mode
      KeyboardMode = true;
      dwg.RemoveOrdered ([.. dwg.Ents.Skip (1)]);
      OnTypeIn (2, "40"); OnTypeIn (3, "40"); Enter ();
      dwg.Ents.Count.Is (4);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("C10,10,15");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("C10,50,15");
      ((E2Poly)dwg.Ents[2]).Poly.Is ("C50,10,15");
      ((E2Poly)dwg.Ents[3]).Poly.Is ("C50,50,15");
   }

   [Test (20, "Test EDGEU widget")]
   void EdgeU () {
      Test ("EdgeU1.dxf");
      Test ("EdgeU2.dxf");

      static void Test (string file) {
         var dwg = Begin (ECmd.EdgeU, file);
         Point2[] pts = [.. dwg.Ents.OfType<E2Point> ().Select (e => e.Pt)];
         pts.ForEach (pt => OnClick (pt)); // Two steps: To avoid Ents modified runtime error.
         End (dwg, file);
      }
   }

   [Test (21, "Test EDGEV widget")]
   void EdgeV () {
      Test ("EdgeV1.dxf");
      Test ("EdgeV2.dxf");
      static void Test (string file) {
         var dwg = Begin (ECmd.EdgeV, file);
         Point2[] pts = [.. dwg.Ents.OfType<E2Point> ().Select (e => e.Pt)];
         pts.ForEach (pt => OnClick (pt)); // Two steps: To avoid Ents modified runtime error.
         End (dwg, file);
      }
   }

   [Test (27, "Test for Line Parallel widget")]
   void LineParallel () {
      Dwg2 dwg = Begin (ECmd.LineParallel);

      // Circle
      dwg.Ents.Clear ();
      dwg.Add (Poly.Circle ((100, 100), 50));
      OnTypeIn (0, "0"); OnClick ((150, 100)); OnClick ((130, 100));
      dwg.Ents.Count.Is (2);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("C100,100,50");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("C100,100,30");

      dwg.Ents.Clear ();
      dwg.Add (Poly.Circle ((100, 100), 50));
      OnTypeIn (0, "0"); OnClick ((150, 100)); OnClick ((130, 100), iCtrl: true);
      dwg.Ents.Count.Is (3);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("C100,100,50");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("C100,100,30");
      ((E2Poly)dwg.Ents[2]).Poly.Is ("C100,100,70");

      dwg.Ents.Clear ();
      dwg.Add (Poly.Circle ((100, 100), 50));
      OnTypeIn (0, "10"); OnClick ((150, 100)); OnClick ((130, 100));
      dwg.Ents.Count.Is (2);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("C100,100,50");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("C100,100,40");

      dwg.Ents.Clear ();
      dwg.Add (Poly.Circle ((100, 100), 50));
      OnTypeIn (0, "10"); OnClick ((150, 100)); OnClick ((130, 100), iCtrl: true);
      dwg.Ents.Count.Is (3);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("C100,100,50");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("C100,100,40");
      ((E2Poly)dwg.Ents[2]).Poly.Is ("C100,100,60");

      // Arc
      dwg.Ents.Clear ();
      dwg.Add (Poly.Arc ((100, 50), 0, (100, 150)));
      OnTypeIn (0, "0"); OnClick ((150, 100)); OnClick ((130, 100));
      dwg.Ents.Count.Is (2);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M100,50Q100,150,2");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M100,70Q100,130,2");

      dwg.Ents.Clear ();
      dwg.Add (Poly.Arc ((100, 50), 0, (100, 150)));
      OnTypeIn (0, "0"); OnClick ((150, 100)); OnClick ((130, 100), iCtrl: true);
      dwg.Ents.Count.Is (3);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M100,50Q100,150,2");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M100,70Q100,130,2");
      ((E2Poly)dwg.Ents[2]).Poly.Is ("M100,30Q100,170,2");

      dwg.Ents.Clear ();
      dwg.Add (Poly.Arc ((100, 50), 0, (100, 150)));
      OnTypeIn (0, "10"); OnClick ((150, 100)); OnClick ((130, 100));
      dwg.Ents.Count.Is (2);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M100,50Q100,150,2");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M100,60Q100,140,2");

      dwg.Ents.Clear ();
      dwg.Add (Poly.Arc ((100, 50), 0, (100, 150)));
      OnTypeIn (0, "10"); OnClick ((150, 100)); OnClick ((130, 100), iCtrl: true);
      dwg.Ents.Count.Is (3);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M100,50Q100,150,2");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M100,60Q100,140,2");
      ((E2Poly)dwg.Ents[2]).Poly.Is ("M100,40Q100,160,2");

      // Line
      dwg.Ents.Clear ();
      dwg.Add (Poly.Line ((100, 100), (200, 100)));
      OnTypeIn (0, "0"); OnClick ((150, 100)); OnClick ((150, 20));
      dwg.Ents.Count.Is (2);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M100,100H200");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M100,20H200");
      OnTypeIn (0, "0"); OnClick ((150, 100)); OnClick ((150, 120));
      dwg.Ents.Count.Is (3);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M100,100H200");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M100,20H200");
      ((E2Poly)dwg.Ents[2]).Poly.Is ("M100,120H200");

      dwg.Ents.Clear ();
      dwg.Add (Poly.Line ((100, 100), (200, 100)));
      OnTypeIn (0, "0"); OnClick ((150, 100)); OnClick ((150, 20), iCtrl: true);
      dwg.Ents.Count.Is (3);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M100,100H200");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M100,20H200");
      ((E2Poly)dwg.Ents[2]).Poly.Is ("M100,180H200");

      dwg.Ents.Clear ();
      dwg.Add (Poly.Line ((100, 100), (200, 100)));
      OnTypeIn (0, "20"); OnClick ((150, 100)); OnClick ((150, 20));
      dwg.Ents.Count.Is (2);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M100,100H200");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M100,80H200");

      dwg.Ents.Clear ();
      dwg.Add (Poly.Line ((100, 100), (200, 100)));
      OnTypeIn (0, "20"); OnClick ((150, 100)); OnClick ((150, 20), iCtrl: true);
      dwg.Ents.Count.Is (3);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M100,100H200");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M100,80H200");
      ((E2Poly)dwg.Ents[2]).Poly.Is ("M100,120H200");
   }

   [Test (78, "Test MOVE widget")]
   void Move () {
      Dwg2 dwg = Begin (ECmd.Move);
      dwg.Add (new E2Point (dwg.Layers.Current, (200, 100)));
      dwg.Ents.First ().IsSelected = true;

      // Test mouse move by 100, 50
      OnClick ((0, 0)); OnClick ((100, 50));
      ((E2Point)dwg.Ents.First ()).Pt.Is ("(300,150)");

      // Test mouse move with copy by another 100, 50
      OnClick ((0, 0)); OnClick ((100, 50), iCtrl: true);
      dwg.Ents.Count.Is (2);
      ((E2Point)dwg.Ents[0]).Pt.Is ("(300,150)");
      ((E2Point)dwg.Ents[1]).Pt.Is ("(400,200)");

      // Reset the drawing
      dwg.Ents.Clear ();
      dwg.Add (new E2Point (dwg.Layers.Current, (200, 100)));
      dwg.Ents.First ().IsSelected = true;

      KeyboardMode = true;

      // Test keyboard move by 100, 50
      OnTypeIn (0, "100"); OnTypeIn (1, "50"); Enter ();
      ((E2Point)dwg.Ents.First ()).Pt.Is ("(300,150)");

      // Test keyboard move with copy by another 100, 50
      OnTypeIn (0, "100"); OnTypeIn (1, "50"); Enter (iCtrl: true);
      dwg.Ents.Count.Is (2);
      ((E2Point)dwg.Ents[0]).Pt.Is ("(300,150)");
      ((E2Point)dwg.Ents[1]).Pt.Is ("(400,200)");
   }

   [Test (76, "Test TRIM seg widget")]
   void TrimSeg () {
      Dwg2 dwg = Begin (ECmd.Trim);

      // Trim single Line poly
      dwg.Add (Poly.Line ((10, 10), (110, 10)));
      OnClick ((50, 10));
      dwg.Ents.Count.Is (0);

      // Trim single Arc poly
      dwg.Add (Poly.Arc ((100, 0), 90.D2R (), (-100, 0)));
      OnClick ((100, 0));
      dwg.Ents.Count.Is (0);

      // Trim lower edge of the rect
      dwg.Add (Poly.Rectangle (10, 10, 100, 50));
      OnClick ((50, 10));
      dwg.Ents.Count.Is (1);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M100,10V50H10V10");

      // Rect and circle. Trim rect upper edge against circle
      dwg.Ents.Clear ();
      dwg.Add (Poly.Rectangle (10, 10, 100, 50));
      dwg.Add (Poly.Circle ((50, 50), 20));
      OnClick ((40, 50));
      dwg.Ents.Count.Is (2);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("C50,50,20");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M30,50H10V10H100V50H70");

      // Rect and circle. Trim circle's lower section against the rect
      dwg.Ents.Clear ();
      dwg.Add (Poly.Rectangle (10, 10, 100, 50));
      dwg.Add (Poly.Circle ((50, 50), 20));
      OnClick ((50, 30));
      dwg.Ents.Count.Is (2);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M10,10H100V50H10Z");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M70,50Q30,50,2");
   }

   [Test (77, "Test EXTEND seg widget")]
   void ExtendSeg () {
      Dwg2 dwg = Begin (ECmd.Extend);
      OnTypeIn (0, "0");

      // Extend line towards another line
      dwg.Add (Poly.Line ((10, 10), (10, 30)));
      dwg.Add (Poly.Line ((20, 20), (50, 20)));
      OnClick ((25, 20)); // Extends
      dwg.Ents.Count.Is (2);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M10,10V30");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M10,20H50");
      OnClick ((45, 20)); // Nothing to extend against
      dwg.Ents.Count.Is (2);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M10,10V30");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M10,20H50");

      OnTypeIn (0, "15");
      OnClick ((45, 20)); // Nothing to extend against; so extend by specified length
      dwg.Ents.Count.Is (2);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M10,10V30");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M10,20H65");

      // Arc extend
      dwg.Ents.Clear ();
      OnTypeIn (0, "0");
      dwg.Add (Poly.Arc ((0, 100), Lib.PI, (0, -100)));
      dwg.Add (Poly.Line ((90, 0), (110, 0)));
      OnClick ((0, 100));
      dwg.Ents.Count.Is (2);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M90,0H110");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M100,0Q0,-100,3");

      OnTypeIn (0, "10");

      // Rect lower edge extended forward
      dwg.Ents.Clear ();
      dwg.Add (Poly.Rectangle (10, 10, 100, 50));
      OnClick ((90, 10));
      dwg.Ents.Count.Is (1);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M100,10V50H10V10H110");

      // Rect lower edge extended backwards
      dwg.Ents.Clear ();
      dwg.Add (Poly.Rectangle (10, 10, 100, 50));
      OnClick ((20, 10));
      dwg.Ents.Count.Is (1);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M0,10H100V50H10V10");

      // Rect upper edge extended forwards
      dwg.Ents.Clear ();
      dwg.Add (Poly.Rectangle (10, 10, 100, 50));
      OnClick ((20, 50));
      dwg.Ents.Count.Is (1);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M10,50V10H100V50H0");

      // Rect upper edge extended backwards
      dwg.Ents.Clear ();
      dwg.Add (Poly.Rectangle (10, 10, 100, 50));
      OnClick ((90, 50));
      dwg.Ents.Count.Is (1);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M110,50H10V10H100V50");
   }

   [Test (22, "Test LINEPERP widget")]
   void PerpendicularLine () {
      var dwg = Begin (ECmd.LinePerp);

      var line = Poly.Line ((10, 10), (50, 10));
      var arcCCW = Poly.Arc ((100, 100), 0, (200, 200));
      var arcCW = Poly.Arc ((-100, -100), 0, (-200, -200));
      var circle = Poly.Circle ((20, 20), 50);

      {
         OnTypeIn (0, "0");
         // Mouse | Free length | Line
         dwg.Ents.Clear (); dwg.Add (line);
         OnClick ((20, 10)); OnClick ((30, 20));
         ((E2Poly)dwg.Ents[1]).Poly.Is ("M30,10V20");
         // Other side
         dwg.Ents.Remove (dwg.Ents[^1]);
         OnClick ((20, 10)); OnClick ((30, -10));
         ((E2Poly)dwg.Ents[1]).Poly.Is ("M30,10V-10");

         // Mouse | Free length | Arc CCW
         dwg.Ents.Clear (); dwg.Add (arcCCW);
         OnClick ((100, 100)); OnClick ((150, 150));
         ((E2Poly)dwg.Ents[1]).Poly.Is ("M170.710678,129.289322L150,150");
         // Other side
         dwg.Ents.Remove (dwg.Ents[^1]);
         OnClick ((100, 100)); OnClick ((200, 100));
         ((E2Poly)dwg.Ents[1]).Poly.Is ("M170.710678,129.289322L200,100");

         // Mouse | Free length | Arc CW
         dwg.Ents.Clear (); dwg.Add (arcCW);
         OnClick ((-100, -100)); OnClick ((-150, -150));
         ((E2Poly)dwg.Ents[1]).Poly.Is ("M-170.710678,-129.289322L-150,-150");
         // Other side
         dwg.Ents.Remove (dwg.Ents[^1]);
         OnClick ((-100, -100)); OnClick ((-200, -100));
         ((E2Poly)dwg.Ents[1]).Poly.Is ("M-170.710678,-129.289322L-200,-100");

         // Mouse | Free length | Circle
         dwg.Ents.Clear (); dwg.Add (circle);
         OnClick ((70, 20)); OnClick ((20, 30));
         ((E2Poly)dwg.Ents[1]).Poly.Is ("M20,70V30");
         // Other side
         dwg.Ents.Remove (dwg.Ents[^1]);
         OnClick ((70, 20)); OnClick ((20, 80));
         ((E2Poly)dwg.Ents[1]).Poly.Is ("M20,70V80");
      }

      {
         OnTypeIn (0, "20");
         // Mouse | Free length | Line
         dwg.Ents.Clear (); dwg.Add (line);
         OnClick ((20, 10)); OnClick ((30, 20));
         ((E2Poly)dwg.Ents[1]).Poly.Is ("M30,10V30");
         // Other side
         dwg.Ents.Remove (dwg.Ents[^1]);
         OnClick ((20, 10)); OnClick ((30, -10));
         ((E2Poly)dwg.Ents[1]).Poly.Is ("M30,10V-10");

         // Mouse | Free length | Arc CCW
         dwg.Ents.Clear (); dwg.Add (arcCCW);
         OnClick ((100, 100)); OnClick ((150, 150));
         ((E2Poly)dwg.Ents[1]).Poly.Is ("M170.710678,129.289322L156.568542,143.431458");
         // Other side
         dwg.Ents.Remove (dwg.Ents[^1]);
         OnClick ((100, 100)); OnClick ((200, 100));
         ((E2Poly)dwg.Ents[1]).Poly.Is ("M170.710678,129.289322L184.852814,115.147186");

         // Mouse | Free length | Arc CW
         dwg.Ents.Clear (); dwg.Add (arcCW);
         OnClick ((-100, -100)); OnClick ((-150, -150));
         ((E2Poly)dwg.Ents[1]).Poly.Is ("M-170.710678,-129.289322L-156.568542,-143.431458");
         // Other side
         dwg.Ents.Remove (dwg.Ents[^1]);
         OnClick ((-100, -100)); OnClick ((-200, -100));
         ((E2Poly)dwg.Ents[1]).Poly.Is ("M-170.710678,-129.289322L-184.852814,-115.147186");

         // Mouse | Free length | Circle
         dwg.Ents.Clear (); dwg.Add (circle);
         OnClick ((70, 20)); OnClick ((20, 30));
         ((E2Poly)dwg.Ents[1]).Poly.Is ("M20,70V50");
         // Other side
         dwg.Ents.Remove (dwg.Ents[^1]);
         OnClick ((70, 20)); OnClick ((20, 80));
         ((E2Poly)dwg.Ents[1]).Poly.Is ("M20,70V90");
      }

      // nkTODO Need to simulate mouse move (which indicates the side perpendicular needs to appear)

      {
         OnTypeIn (0, "0");
         // Keyboard | Free length | Arc CCW
         // Keyboard | Free length | Arc CW
         // Keyboard | Free length | Line
         // Keyboard | Free length | Circle
      }

      {
         OnTypeIn (0, "10");
         // Keyboard | Fixed length | Arc CCW
         // Keyboard | Fixed length | Arc CW
         // Keyboard | Fixed length | Line
         // Keyboard | Fixed length | Circle
      }
   }

   [Test (23, "Test SCALE widget")]
   void Scale () {
      Dwg2 dwg = Begin (ECmd.Scale);

      // 1. Scale a line using mouse (pick points)
      dwg.Add (Poly.Line ((10, 0), (100, 0)));
      dwg.Ents[0].IsSelected = true;
      OnClick ((0, 0)); OnClick ((0, 10)); OnClick ((0, 20), iCtrl: true);
      ((E2Poly)dwg.Ents[^1]).Poly.Is ("M20,0H200");

      // 2. Scale a line using keyboard input (factor 1.5)
      dwg.Add (Poly.Line ((0, 0), (100, 0)));
      dwg.Ents[^1].IsSelected = true;
      KeyboardMode = true;
      OnTypeIn (0, "0,0"); OnTypeIn (1, "1.5"); Enter ();
      ((E2Poly)dwg.Ents[^1]).Poly.Is ("M0,0H150");

      // 3. Scale multiple entities (factor 2)
      dwg.Ents.Clear ();
      dwg.Add (Poly.Rectangle (0, 0, 200, 200));
      dwg.Add (Poly.Circle ((0, 0), 100));
      dwg.Ents.ForEach (e => e.IsSelected = true);
      OnTypeIn (0, "0,0"); OnTypeIn (1, "2"); Enter ();
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M0,0H400V400H0Z");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("C0,0,200");

      // 4. Copy while scaling (factor 2 with Ctrl)
      dwg.Ents.Clear ();
      dwg.Add (Poly.Rectangle (0, 0, 200, 200));
      dwg.Ents[0].IsSelected = true;
      OnClick ((0, 0)); OnClick ((200, 0)); Enter (iCtrl: true);
      dwg.Ents.Count.Is (2);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M0,0H200V200H0Z");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M0,0H400V400H0Z");
   }

   [Test (24, "Test ROTATE widget")]
   void Rotate () {
      Dwg2 dwg = Begin (ECmd.Rotate);
      dwg.Add (Poly.Line ((100, 0), (200, 0)));
      dwg.Ents.First ().IsSelected = true;

      // 1. Rotate a line using mouse
      OnClick ((50, 0)); OnClick ((100, 0)); OnClick ((50, 50));
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M50,50V150");

      // 2. Rotate a line using mouse (with Ctrl)
      OnClick ((50, 0)); OnClick ((50, 50)); OnClick ((0, 0), iCtrl: true);
      dwg.Ents.Count.Is (2);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M50,50V150");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M0,0H-100");

      // Reset the drawing
      dwg.Ents.Clear ();
      dwg.Add (Poly.Line ((100, 0), (200, 0)));
      dwg.Ents.First ().IsSelected = true;
      KeyboardMode = true;

      // 3. Rotate a line using keyboard
      OnTypeIn (0, "50,0"); OnTypeIn (1, "90"); Enter ();
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M50,50V150");

      // 4. Rotate a line using keyboard (using Ctrl)
      OnTypeIn (0, "50,0"); OnTypeIn (1, "90"); Enter (iCtrl: true);
      dwg.Ents.Count.Is (2);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M50,50V150");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M0,0H-100");
   }

   [Test (73, "Test KeySlot widget")]
   void KeySlot () {
      // Circle dxf
      Test ("KeySlot1.dxf", 0, 10);
      Test ("KeySlot2.dxf", 270, 10);
      // Counter clockwise arc dxf
      Test ("KeySlot3.dxf", 45, 10);
      Test ("KeySlot4.dxf", 90, 10);
      // Clockwise arc dxf
      Test ("KeySlot5.dxf", 100, 10);
      Test ("KeySlot6.dxf", 300, 10);

      static void Test (string file, double angle, double depth) {
         var dwg = Begin (ECmd.KeySlot, file);
         OnTypeIn (2, angle.S6 ()); OnTypeIn (1, depth.S6 ());
         Point2[] pts = [.. dwg.Ents.OfType<E2Point> ().Select (e => e.Pt)];
         pts.ForEach (pt => OnClick (pt));
         End (dwg, file);
      }
   }

   [Test (74, "Test MIRROR widget")]
   void Mirror () {
      var dwg = Begin (ECmd.Mirror);

      // Mirror | Using mouse
      ResetDwg (dwg);
      OnClick ((1, 1)); OnClick ((2, 2));
      dwg.Ents.Count.Is (1);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M0,10V20");

      // Mirror copy | Using mouse
      ResetDwg (dwg);
      OnClick ((1, 1)); OnClick ((2, 2), iCtrl: true);
      dwg.Ents.Count.Is (2);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M10,0H20");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M0,10V20");

      KeyboardMode = true;

      // Mirror | Using keyboard
      ResetDwg (dwg);
      OnTypeIn (0, "1,1"); OnTypeIn (1, "2,2"); Enter ();
      dwg.Ents.Count.Is (1);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M0,10V20");

      // Mirror copy | Using keyboard
      ResetDwg (dwg);
      OnTypeIn (0, "1,1"); OnTypeIn (1, "2,2"); Enter (iCtrl: true);
      dwg.Ents.Count.Is (2);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M10,0H20");
      ((E2Poly)dwg.Ents[1]).Poly.Is ("M0,10V20");

      static void ResetDwg (Dwg2 dwg) {
         dwg.Ents.Clear ();
         dwg.Add (Poly.Line ((10, 0), (20, 0))); // Line lying along X axis
         dwg.Ents[0].IsSelected = true;
      }
   }

   [Test (82, "Test MarkOpenPolys [immediate action] command")]
   void MarkOpenPolys () {
      // Note: This is an immediate action command. Hence there's no interaction widget for this command.
      var dwg = new Dwg2 ();

      {
         dwg.Add (Poly.Line ((10, 10), (20, 10)));

         DwgCmds.DoMarkOpenPolys (dwg);
         dwg.Ents.Count.Is (5);

         // Check the presence of markers
         List<Ent2> markerEnts = [.. dwg.Ents.Where (a => a.Layer.Name == "CleanupMarker")];
         markerEnts.Count.Is (4);

         var c1 = ((E2Poly)markerEnts[0]).Poly;
         c1.IsCircle.IsTrue (); c1[0].Center.Is ("(10,10)");
         ((E2Point)markerEnts[1]).Pt.Is ("(10,10)");

         var c2 = ((E2Poly)markerEnts[2]).Poly;
         c2.IsCircle.IsTrue (); c2[0].Center.Is ("(20,10)");
         ((E2Point)markerEnts[3]).Pt.Is ("(20,10)");
      }

      {
         // Replace the line with another line, and repeat
         // Repeat should also remove any existing markers, and replace them with new ones
         dwg.Remove (dwg.Ents[0]);
         dwg.Add (Poly.Line ((10, 100), (20, 100)));

         DwgCmds.DoMarkOpenPolys (dwg);
         dwg.Ents.Count.Is (5);

         // Check the presence of markers
         List<Ent2> markerEnts = [.. dwg.Ents.Where (a => a.Layer.Name == "CleanupMarker")];
         markerEnts.Count.Is (4);

         var c1 = ((E2Poly)markerEnts[0]).Poly;
         c1.IsCircle.IsTrue (); c1[0].Center.Is ("(10,100)");
         ((E2Point)markerEnts[1]).Pt.Is ("(10,100)");

         var c2 = ((E2Poly)markerEnts[2]).Poly;
         c2.IsCircle.IsTrue (); c2[0].Center.Is ("(20,100)");
         ((E2Point)markerEnts[3]).Pt.Is ("(20,100)");
      }
   }

   [Test (83, "Test MarkSmallSegs widget")]
   void MarkSmallSegs () {
      // Layer 0
      var dwg = Begin (ECmd.MarkSmallSegs);
      KeyboardMode = true;
      OnTypeIn (0, "1");

      {
         dwg.Add (Poly.Parse ("M11,10L10,10H20"));

         Enter ();
         dwg.Ents.Count.Is (3);

         // Check the presence of markers
         List<Ent2> markerEnts = [.. dwg.Ents.Where (a => a.Layer.Name == "CleanupMarker")];
         markerEnts.Count.Is (2);

         var circ = ((E2Poly)markerEnts[0]).Poly;
         circ.IsCircle.IsTrue (); circ[0].Center.Is ("(10.5,10)");
         ((E2Point)markerEnts[1]).Pt.Is ("(10.5,10)");
      }

      {
         // Replace the small line with another small line, and repeat
         // Repeat should also remove any existing markers, and replace them with new ones
         dwg.Remove (dwg.Ents[0]);
         dwg.Add (Poly.Line ((10, 100), (11, 100))); // Length: 1

         Enter ();
         dwg.Ents.Count.Is (3);

         // Check the presence of markers
         List<Ent2> markerEnts = [.. dwg.Ents.Where (a => a.Layer.Name == "CleanupMarker")];
         markerEnts.Count.Is (2);

         var circ = ((E2Poly)markerEnts[0]).Poly;
         circ.IsCircle.IsTrue (); circ[0].Center.Is ("(10.5,100)");
         ((E2Point)markerEnts[1]).Pt.Is ("(10.5,100)");
      }
   }

   [Test (85, "Test MarkNonTangent(Corners) widget")]
   void MarkNonTangentCorners () {
      var dwg = Begin (ECmd.MarkNonTangent);

      var tri = Poly.Parse ("M0,0H100L150,86.60254Z"); // Obtuse

      {
         dwg.Add (tri);
         KeyboardMode = true;
         OnTypeIn (0, "60"); OnTypeIn (1, "60"); Enter ();
         // Check the presence of markers
         List<Ent2> markerEnts = [.. dwg.Ents.Where (a => a.Layer.Name == "CleanupMarker")];
         markerEnts.Count.Is (2);
         var circ = ((E2Poly)markerEnts[0]).Poly;
         circ.IsCircle.IsTrue (); circ[0].Center.Is ("(100,0)");
         ((E2Point)markerEnts[1]).Pt.Is ("(100,0)");
      }
   }

   [Test (87, "Test MarkLargeRadius widget")]
   void MarkLargeRadius () {
      var dwg = Begin (ECmd.MarkLargeRadius);
      Poly[] arcs = [Poly.Parse("M8.6,139.8Q-15.190671,149.325801,2.131941") ,
         Poly.Parse("M-2.8,120.9Q45.7,116.9,2"),
         Poly.Parse("M63.4,136.3Q45.7,88.3,-2.081733"),
         Poly.Parse("M-23.2,76.1Q30.5,82.1,0.24926")];
      foreach (var arc in arcs)
         dwg.Add (arc);
      KeyboardMode = true;
      OnTypeIn (0, "15"); Enter ();
      // Check the presence of markers
      List<Ent2> markerEnts = [.. dwg.Ents.Where (a => a.Layer.Name == "CleanupMarker")];
      markerEnts.Count.Is (6);
      var segs = arcs.SelectMany (arc => arc.Segs).Where (a => a.IsArc && a.Radius > 15).ToList ();
      for (int i = 0; i < segs.Count; i++) {
         var circle = ((E2Poly)markerEnts[i * 2]).Poly;
         circle.IsCircle.IsTrue ();
         segs[i].Midpoint.Is (circle[0].Center);
      }
   }

   [Test (84, "Test (misc geometry cmds) auto dwg stitch")]
   void AutoStitch () {
      // Will simply test by drawing some lines in Line cmd mode
      var dwg = Begin (ECmd.Line);
      OnClick ((10, 10)); OnClick ((20, 10)); // Line 1
      OnClick ((15, 20)); OnClick ((10, 10)); // Line 2
      OnClick ((15, 20)); OnClick ((20, 10)); // Line 3
      dwg.Ents.Count.Is (1);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M15,20L10,10H20Z");
   }

   [Test (86, "Test Cleanup(Markers) [immediate action] command")]
   void CleanupMarkers () {
      // Note: This is an immediate action command. Hence there's no interaction widget for this command.
      // Note: It essentially purges the "CleanupMarker" layer clean!
      var dwg = new Dwg2 ();
      dwg.Add (Poly.Parse ("M100,100H200"));

      // Create some "markers"
      DwgCmds.DoMarkOpenPolys (dwg);
      dwg.Ents.Where (a => a.Layer.Name == "CleanupMarker").Count ().Is (4);
      dwg.Ents.Count.Is (5);

      // Test this command
      DwgCmds.DoPurgeMarkers (dwg);
      dwg.Ents.Where (a => a.Layer.Name == "CleanupMarker").Count ().Is (0);
      dwg.Ents.Count.Is (1);
      ((E2Poly)dwg.Ents[0]).Poly.Is ("M100,100H200");
   }

   [Test (94, "Test DimAngle widget")]
   void DimAngle () {
      var dwg = Begin (ECmd.DimAngle);
      OnTypeIn (0, ""); OnTypeIn (1, "");
      dwg.Add (Poly.Line ((10, 0), (100, 0)));
      dwg.Add (Poly.Line ((0, 10), (0, 100)));

      OnClick ((50, 0)); OnClick ((0, 50)); OnClick ((100, 100));

      dwg.Purge ();
      DXFWriter.Save (dwg, PT.TmpDXF);
      Assert.TextFilesEqual (PT.File ($"Widget/DimAngle.dxf"), PT.TmpDXF);
   }

   [Test (107, "Test DimAngle3P widget")]
   void DimAngle3P () {
      var dwg = Begin (ECmd.DimAngle3P);
      OnTypeIn (0, ""); OnTypeIn (1, "");

      // This will measure "inner" angle
      OnClick ((10, 10)); OnClick ((50, 10)); OnClick ((100, 100));
      OnClick ((70, 70));

      // Same 3 points - this time it measures "outer" angle
      OnClick ((10, 10)); OnClick ((50, 10)); OnClick ((100, 100));
      OnClick ((70, 0));

      dwg.Purge ();
      DXFWriter.Save (dwg, PT.TmpDXF);
      Assert.TextFilesEqual (PT.File ($"Widget/DimAngle3P.dxf"), PT.TmpDXF);
   }

   [Test (95, "Test DimSegment widget")]
   void DimSegment () {
      var dwg = Begin (ECmd.DimSegment);
      dwg.Add (Poly.Line ((10, 20), (100, 200)));

      OnClick ((10, 20)); // Pick the line segment
      OnClick ((10, 10)); // Place the dimension here

      DXFWriter.Save (dwg, PT.TmpDXF);
      Assert.TextFilesEqual (PT.File ($"Widget/DimSegment.dxf"), PT.TmpDXF);
   }

   [Test (93, "Test DimLinear widget - Aligned dimensioning")]
   void Dim2P () {
      var dwg = Begin (ECmd.Dim2P);
      OnTypeIn (0, ""); OnTypeIn (1, "");
      dwg.Add (Poly.Rectangle (0, 0, 200, 100));

      // Horizontal measurement
      OnTypeIn (2, "0");
      OnClick ((0, 0)); OnClick ((200, 0));
      OnClick ((50, -25));

      // Vertical measurement
      OnTypeIn (2, "90");
      OnClick ((0, 0)); OnClick ((0, 100));
      OnClick ((-25, 50));

      // Aligned measurement - CTRL for reference line
      // Aligned with the Top of the Rect
      OnClick ((50, 100), iCtrl: true);
      OnClick ((0, 100)); OnClick ((200, 100));
      OnClick ((50, 125));

      // Aligned measurement - CTRL for reference line
      // Aligned with the Right of the Rect
      OnClick ((200, 50), iCtrl: true);
      OnClick ((200, 0)); OnClick ((200, 100));
      OnClick ((225, 50));

      DXFWriter.Save (dwg, PT.TmpDXF);
      Assert.TextFilesEqual (PT.File ($"Widget/Dim2P.dxf"), PT.TmpDXF);
   }

   [Test (103, "Test DimLinear widget - Rotated (non-aligned) dimensioning")]
   void Dim2P2 () {
      var dwg = Begin (ECmd.Dim2P, "RotatedDim.dxf");
      OnTypeIn (0, ""); OnTypeIn (1, "");

      // Rotated dimension. Align perpendicular to picked line.
      OnClick ((80, 80), iShift: true); // Aligns measurement axis perpendicular to this line.
      OnClick ((100, 100)); OnClick ((100, 130));
      OnClick ((140, 150));

      // Rotated dimension. Align parallel to picked line. [Essentially, an aligned dim again!]
      OnClick ((70.55, 38.75), iCtrl: true); // Aligns measurement axis parallel to this line.
      OnClick ((100, 50)); OnClick ((100, 80));
      OnClick ((140, 100));

      DXFWriter.Save (dwg, PT.TmpDXF);
      Assert.TextFilesEqual (PT.File ($"Widget/RotatedDim.dxf"), PT.TmpDXF);
   }

   [Test (100, "Test DimRadius widget, on Arc")]
   void DimArcRadius () {
      var dwg = Begin (ECmd.DimRadius);
      OnTypeIn (0, ""); OnTypeIn (1, ""); // Reset the (Text, Tolerance) fields

      double r = 50;
      Point2 center = (10, 10);
      dwg.Add (Poly.Arc (center, r, -45.D2R (), 45.D2R (), ccw: true)); // Add an Arc

      OnClick (center.Polar (r, 10.D2R ())); // Pick the Arc
      OnClick (center.Polar (r + 10, -40.D2R ())); // Place the "radius" dimension (outside)

      OnClick (center.Polar (r, 10.D2R ())); // Pick the Arc
      OnClick (center.Polar (r - 10, -20.D2R ())); // Place the "radius" dimension (inside)

      OnClick (center.Polar (r, 10.D2R ())); // Pick the Arc, again
      OnClick (center.Polar (r + 10, 40.D2R ()), iShift: true); // Place the "radius" dimension, with "center" point included (outside)

      OnClick (center.Polar (r, 10.D2R ())); // Pick the Arc, again
      OnClick (center.Polar (r - 10, 20.D2R ()), iShift: true); // Place the "radius" dimension, with "center" point included (inside)

      DXFWriter.Save (dwg, PT.TmpDXF);
      Assert.TextFilesEqual (PT.File ("Widget/DimArcRadius.dxf"), PT.TmpDXF);

      // Tolerance text handling test (for DimRadius widget)
      dwg.Ents.Clear ();
      dwg.Add (Poly.Arc (center, r, -45.D2R (), 45.D2R (), ccw: true)); // Add an Arc

      OnTypeIn (0, "Text"); OnTypeIn (1, "Tolerance");
      OnClick (center.Polar (r, 10.D2R ())); // Pick the Arc
      OnClick (center.Polar (r + 10, -40.D2R ())); // Place the "radius" dimension (outside)

      DXFWriter.Save (dwg, PT.TmpDXF);
      Assert.TextFilesEqual (PT.File ("Widget/DimArcRadiusText.dxf"), PT.TmpDXF);
   }

   [Test (101, "Test DimRadius widget, on Circle")]
   void DimCircleRadius () {
      var dwg = Begin (ECmd.DimRadius);
      OnTypeIn (0, ""); OnTypeIn (1, ""); // Reset the (Text, Tolerance) fields

      double r = 50;
      Point2 center = (10, 10);
      dwg.Add (Poly.Circle (center, r)); // Add an Arc

      OnClick (center.Polar (r, 10.D2R ())); // Pick the Arc
      OnClick (center.Polar (r + 10, -40.D2R ())); // Place the "radius" dimension (outside)

      OnClick (center.Polar (r, 10.D2R ())); // Pick the Arc
      OnClick (center.Polar (r - 10, -20.D2R ())); // Place the "radius" dimension (inside)

      OnClick (center.Polar (r, 10.D2R ())); // Pick the Arc, again
      OnClick (center.Polar (r + 10, 40.D2R ()), iShift: true); // Place the "radius" dimension, with "center" point included (outside)

      OnClick (center.Polar (r, 10.D2R ())); // Pick the Arc, again
      OnClick (center.Polar (r - 10, 20.D2R ()), iShift: true); // Place the "radius" dimension, with "center" point included (inside)

      DXFWriter.Save (dwg, PT.TmpDXF);
      Assert.TextFilesEqual (PT.File ("Widget/DimCircleRadius.dxf"), PT.TmpDXF);
   }

   [Test (102, "Test DimDiameter widget")]
   void DimDiameter () {
      var dwg = Begin (ECmd.DimDiameter);
      OnTypeIn (0, ""); OnTypeIn (1, ""); // Reset the (Text, Tolerance) fields

      double r = 50;
      Point2 center = (10, 10);
      dwg.Add (Poly.Circle (center, r)); // Add a Circle

      OnClick (center.Polar (r, 10.D2R ())); // Pick the Arc
      OnClick (center.Polar (r + 10, -40.D2R ())); // Place the "radius" dimension (outside)

      OnClick (center.Polar (r, 10.D2R ())); // Pick the Arc
      OnClick (center.Polar (r - 10, -20.D2R ())); // Place the "radius" dimension (inside)

      OnClick (center.Polar (r, 10.D2R ())); // Pick the Arc, again
      OnClick (center.Polar (r + 10, 40.D2R ()), iShift: true); // Place the "radius" dimension, with "center" point included (outside)

      OnClick (center.Polar (r, 10.D2R ())); // Pick the Arc, again
      OnClick (center.Polar (r - 10, 20.D2R ()), iShift: true); // Place the "radius" dimension, with "center" point included (inside)

      DXFWriter.Save (dwg, PT.TmpDXF);
      Assert.TextFilesEqual (PT.File ("Widget/DimCircleDia.dxf"), PT.TmpDXF);

      // Tolerance text handling test (for DimRadius widget)
      dwg.Ents.Clear ();
      dwg.Add (Poly.Circle (center, r)); // Add a Circle

      OnTypeIn (0, "Text"); OnTypeIn (1, "Tolerance");
      OnClick (center.Polar (r, 10.D2R ())); // Pick the Arc
      OnClick (center.Polar (r + 10, -40.D2R ())); // Place the "radius" dimension (outside)

      DXFWriter.Save (dwg, PT.TmpDXF);
      Assert.TextFilesEqual (PT.File ("Widget/DimCicleDiaText.dxf"), PT.TmpDXF);
   }

   [Test (104, "test DimCallout widget")]
   void DimCallout () {
      var dwg = Begin (ECmd.DimCallout);
      OnTypeIn (0, "Text"); OnTypeIn (1, "50");

      dwg.Add (Poly.Rectangle (0, 0, 100, 100));
      OnClick ((0, 50)); OnClick ((-10, 60));
      OnClick ((100, 50)); OnClick ((110, 40));

      OnTypeIn (0, ""); OnTypeIn (1, "");
      OnClick ((50, 0)); OnClick ((40, -10));
      OnClick ((50, 100)); OnClick ((60, 110));

      DXFWriter.Save (dwg, PT.TmpDXF);
      Assert.TextFilesEqual (PT.File ("Widget/DimCallout.dxf"), PT.TmpDXF);
   }

   [Test (105, "Test DimBaseline widget")]
   void DimBaseline () {
      // Explicit angle (horizontal)
      var dwg = Begin (ECmd.DimBaseline);
      OnTypeIn (0, ""); OnTypeIn (1, "");
      dwg.Add (Poly.Rectangle (0, 0, 300, 100));

      OnTypeIn (2, "0");
      OnClick ((0, 0));      // anchor: baseline fixed at origin
      OnClick ((100, 0));    // end point 1
      OnClick ((150, -30));  // place dim 1
      OnClick ((200, 0));    // end point 2 (same baseline)
      OnClick ((150, -50));  // place dim 2
      OnClick ((300, 0));    // end point 3
      OnClick ((150, -70));  // place dim 3

      DXFWriter.Save (dwg, PT.TmpDXF);
      Assert.TextFilesEqual (PT.File ("Widget/DimBaseline.dxf"), PT.TmpDXF);

      // Angle from perpendicular reference line (Shift)
      // Shift-clicking the bottom horizontal edge sets angle = 90 deg (vertical measurement)
      dwg = Begin (ECmd.DimBaseline);
      OnTypeIn (0, ""); OnTypeIn (1, "");
      dwg.Add (Poly.Rectangle (0, 0, 100, 300));

      OnClick ((50, 0), iShift: true); // Shift-pick bottom edge -> angle perp to horizontal = 90 deg
      OnClick ((0, 0));      // anchor: baseline fixed at origin
      OnClick ((0, 100));    // end point 1
      OnClick ((-30, 150));  // place dim 1
      OnClick ((0, 200));    // end point 2 (same baseline)
      OnClick ((-50, 150));  // place dim 2
      OnClick ((0, 300));    // end point 3
      OnClick ((-70, 150));  // place dim 3

      DXFWriter.Save (dwg, PT.TmpDXF);
      Assert.TextFilesEqual (PT.File ("Widget/DimBaselinePerp.dxf"), PT.TmpDXF);
   }

   [Test (106, "Test DimContinue widget")]
   void DimContinue () {
      // Explicit angle (horizontal)
      var dwg = Begin (ECmd.DimContinue);
      OnTypeIn (0, ""); OnTypeIn (1, "");
      dwg.Add (Poly.Rectangle (0, 0, 300, 100));

      OnTypeIn (2, "0");
      OnClick ((0, 0));      // chain start A
      OnClick ((100, 0));    // chain end B -> dim A->B
      OnClick ((50, -30));   // place dim 1
      OnClick ((200, 0));    // chain end C -> dim B->C
      OnClick ((150, -30));  // place dim 2 at same offset
      OnClick ((300, 0));    // chain end D -> dim C->D
      OnClick ((250, -30));  // place dim 3

      DXFWriter.Save (dwg, PT.TmpDXF);
      Assert.TextFilesEqual (PT.File ("Widget/DimContinue.dxf"), PT.TmpDXF);

      // Angle from perpendicular reference line (Shift)
      // Shift-clicking the bottom horizontal edge sets angle = 90 deg (vertical measurement)
      dwg = Begin (ECmd.DimContinue);
      OnTypeIn (0, ""); OnTypeIn (1, "");
      dwg.Add (Poly.Rectangle (0, 0, 100, 300));

      OnClick ((50, 0), iShift: true); // Shift-pick bottom edge -> angle perp to horizontal = 90 deg
      OnClick ((0, 0));      // chain start A
      OnClick ((0, 100));    // chain end B -> dim A->B
      OnClick ((-30, 50));   // place dim 1
      OnClick ((0, 200));    // chain end C -> dim B->C (chain advances to (0,100))
      OnClick ((-30, 150));  // place dim 2 at same offset
      OnClick ((0, 300));    // chain end D -> dim C->D (chain advances to (0,200))
      OnClick ((-30, 250));  // place dim 3

      DXFWriter.Save (dwg, PT.TmpDXF);
      Assert.TextFilesEqual (PT.File ("Widget/DimContinuePerp.dxf"), PT.TmpDXF);
   }

   static Dwg2 Begin (ECmd cmd, string? file = null) {
      Dwg2 dwg;
      if (file == null) dwg = new ();
      else dwg = new DXFReader (PT.File ($"Widget/In/{file}")).Load ();
      Dwg = dwg;
      var type = WidgetMap[cmd];
      Widget = (Widget)Activator.CreateInstance (type)!;
      return dwg;
   }

   static void End (Dwg2 dwg, string file) {
      dwg.Purge ();
      DXFWriter.Save (dwg, PT.TmpDXF);
      Assert.TextFilesEqual (PT.File ($"Widget/{file}"), PT.TmpDXF);
   }
}
