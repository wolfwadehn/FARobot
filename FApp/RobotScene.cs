// ╔═╦╗
// ║╬╠╬╦╗ RobotScene.cs
// ║╔╣╠║╣ 3D robot scene with FK/IK solver and box collision detection
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Windows.Controls;
using System.Windows.Threading;
namespace FApp;

#region class RobotScene ---------------------------------------------------------------------------
class RobotScene : Scene3 {
   // Constructor --------------------------------------------------------------
   public RobotScene () {
      mMech   = Mechanism.Load ("N:/Wad/FanucX/mechanism.curl");
      mTip    = mMech.FindChild ("Tip")!;
      mJoints = [.. "SLURBT".Select (a => mMech.FindChild (a.ToString ())!)];

      for (int i = 0; i < 6; i++) {
         var m = mJoints[i];
         mMin[i] = m.JMin;
         mMax[i] = m.JMax;
      }
      mSolver = new (150, 770, 0, 0, 1016, 175, mMin, mMax);

      Lib.Tessellate = FastTess2D.Process;
      var boxPoly = Poly.Rectangle (-100, -100, 100, 100);
      mBoxMesh    = Mesh3.Extrude ([boxPoly], 200, Matrix3.Translation (0, 0, -100), ETess.Medium);
      mBoxOBB     = OBBTree.From (mBoxMesh);
      mBoxVN      = new Mesh3VN (mBoxMesh) { Mode = EShadeMode.Glass, Color = Color4.Blue };
      mBoxXfm     = new XfmVN (BoxWorldXfm, mBoxVN);

      foreach (var m in mMech.EnumTree ()) {
         var cm = m.CMesh;
         if (cm != null) mLinkOBBs[m] = OBBTree.From (cm);
         else if (m.Mesh != null) mLinkOBBs[m] = OBBTree.From (m.Mesh);
      }

      mGripper   = new XfmVN (Matrix3.Identity, new GroupVN ([]));
      mTriGroup  = new GroupVN ([]);
      (mX, mY, mZ) = (mHome.Org.X, mHome.Org.Y, mHome.Org.Z + LJointZ);
      mCS        = mHome; ComputeIK ();

      mPlayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds (500) };
      mPlayTimer.Tick += (_, _) => TickScript ();

      Lib.Tracer = TraceVN.Print;
      BgrdColor  = Color4.Gray (64);
      Bound      = new Bound3 (-1200, -1200, 0, 1200, 1200, 1500);
      Root       = new GroupVN ([
         new MechanismVN (mMech), mGripper, mBoxXfm, mTriGroup,
         new TcpVN  { Scene = this },
         new InfoVN { Scene = this },
         TraceVN.It
      ]);
   }

   // Properties ---------------------------------------------------------------
   public Mechanism Tip       => mTip;
   public Vector3   TcpOffset { get => mTcpOffset; set { mTcpOffset = value; ComputeIK (); } }
   public (double X, double Y, double Z, double Rx, double Ry, double Rz,
           Mechanism[] Joints) InfoData
      => (mX, mY, mZ, mRx, mRy, mRz, mJoints);

   // Methods ------------------------------------------------------------------
   public void CreateUI (UIElementCollection ui) {
      ui.Clear ();

      AddSection ("Forward Kinematics");
      foreach (var m in mMech.EnumTree ()) {
         if (m.Joint == EJoint.None) continue;
         AddSlider (m.Name, m.JMin, m.JMax, m.JValue, v => { m.JValue = v; OnFK (); });
      }

      AddSection ("Inverse Kinematics");
      AddSlider ("X",  -2000, 3000, mX,  v => { mX  = v; ComputeIK (); });
      AddSlider ("Y",  -2000, 2000, mY,  v => { mY  = v; ComputeIK (); });
      AddSlider ("Z",  -1000, 3200, mZ,  v => { mZ  = v; ComputeIK (); });
      AddSlider ("Rx", -180,  180,  mRx, v => { mRx = v; ComputeIK (); });
      AddSlider ("Ry", -180,  180,  mRy, v => { mRy = v; ComputeIK (); });
      AddSlider ("Rz", -180,  180,  mRz, v => { mRz = v; ComputeIK (); });

      AddSection ("Obstacle");
      AddSlider ("BX", -1200, 1200, mBX, v => { mBX = v; UpdateBox (); });
      AddSlider ("BY", -1200, 1200, mBY, v => { mBY = v; UpdateBox (); });
      AddSlider ("BZ",  0,    1500, mBZ, v => { mBZ = v; UpdateBox (); });

      AddSection ("Script");
      var pathBox = new TextBox {
         Margin     = new Thickness (6, 0, 6, 4),
         Text       = "N:/Demos/WPFDemo/robot_script.txt",
         Background = System.Windows.Media.Brushes.DimGray,
         Foreground = System.Windows.Media.Brushes.WhiteSmoke
      };
      ui.Add (pathBox);
      var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness (6, 0, 6, 4) };
      var loadBtn = new Button { Content = "Load" };
      loadBtn.Click += (_, _) => LoadScript (pathBox.Text);
      mPlayBtn = new Button { Content = "Play" };
      mPlayBtn.Click += (_, _) => TogglePlay ();
      row.Children.Add (loadBtn);
      row.Children.Add (mPlayBtn);
      ui.Add (row);

      AddSection ("Collision Triangles");
      var addBtn = new Button { Content = "Add Triangle…",
                                HorizontalAlignment = HorizontalAlignment.Left,
                                Margin = new Thickness (6, 0, 6, 6) };
      addBtn.Click += (_, _) => ShowAddTriDlg ();
      ui.Add (addBtn);
      mTriListPanel = new StackPanel ();
      ui.Add (mTriListPanel);
      RefreshTriList ();

      void AddSection (string text) {
         ui.Add (new TextBlock {
            Text       = text,
            FontSize   = 13,
            FontWeight = System.Windows.FontWeights.Bold,
            Foreground = System.Windows.Media.Brushes.LightSteelBlue,
            Margin     = new Thickness (8, 10, 0, 4)
         });
      }

      void AddSlider (string label, double min, double max, double value, Action<double> setter) {
         var sp  = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness (4, 0, 4, 2) };
         var lbl = new TextBlock {
            Text              = label, Width = 22,
            TextAlignment     = System.Windows.TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground        = System.Windows.Media.Brushes.Silver
         };
         var tb = new TextBox {
            Width             = 55, Text = value.ToString ("F1"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness (2, 1, 2, 4),
            Background        = System.Windows.Media.Brushes.DimGray,
            Foreground        = System.Windows.Media.Brushes.WhiteSmoke,
            BorderThickness   = new Thickness (1),
            TextAlignment     = System.Windows.TextAlignment.Right,
            Padding           = new Thickness (3, 1, 3, 1)
         };
         var sl = new Slider {
            Minimum = min, Maximum = max, Value = value,
            MinWidth = 130, IsSnapToTickEnabled = false,
            Margin   = new Thickness (4, 1, 4, 4)
         };
         sl.ValueChanged += (_, e) => {
            if (!mSyncingUI) setter (e.NewValue);
            tb.Text = e.NewValue.ToString ("F1");
         };
         void Commit () {
            if (double.TryParse (tb.Text, System.Globalization.NumberStyles.Float,
                                 System.Globalization.CultureInfo.InvariantCulture, out double v))
               sl.Value = Math.Clamp (v, min, max);
            else
               tb.Text = sl.Value.ToString ("F1");
         }
         tb.LostFocus += (_, _) => Commit ();
         tb.KeyDown   += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) Commit (); };
         sp.Children.Add (lbl);
         sp.Children.Add (sl);
         sp.Children.Add (tb);
         ui.Add (sp);
         mSliders[label] = sl;
      }
   }

   // Implementation -----------------------------------------------------------
   public override void Detached () => mPlayTimer.IsEnabled = false;

   public override void Picked (object obj) {
      if (obj == mBoxMesh) { SnapToFace (Lux.PickPos); return; }
      foreach (var tri in mTris)
         if (obj == tri.Mesh) { SnapToTriNode (tri.P1, tri.Normal); return; }
   }

   void SnapToTriNode (Point3 p1, Vector3 normal) {
      (mRx, mRy, mRz) = NormalToEuler (normal);
      mX = p1.X;
      mY = p1.Y;
      mZ = p1.Z;
      ComputeIK ();
      SyncIKSliders ();
   }

   // Maps a unit normal to (Rx, Ry, Rz=0) so that VecZ after cs*=Rot(X,Rx)*Rot(Y,Ry) equals normal.
   // VecZ formula: (sin(Ry), -sin(Rx)*cos(Ry), cos(Rx)*cos(Ry))
   static (double Rx, double Ry, double Rz) NormalToEuler (Vector3 n) {
      double ry    = Math.Asin (Math.Clamp (n.X, -1, 1));
      double cosRy = Math.Cos (ry);
      double rx    = Math.Abs (cosRy) > 1e-6 ? Math.Atan2 (-n.Y, n.Z) : 0;
      return (rx * (180 / Math.PI), ry * (180 / Math.PI), 0);
   }

   void SnapToFace (Point3 hit) {
      double rx = hit.X - mBX, ry = hit.Y - mBY, rz = hit.Z - mBZ;
      double ax = Math.Abs (rx), ay = Math.Abs (ry), az = Math.Abs (rz);
      // VecZ points from TCP toward wrist (outward from face), so VecZ = outward face normal.
      // Rx=0 → VecZ=+Z, Rx=-90 → VecZ=+Y, Ry=+90 → VecZ=+X (and their negatives).
      if (ax >= ay && ax >= az)
         (mRx, mRy, mRz) = rx > 0 ? (0.0, 90.0, 0.0) : (0.0, -90.0, 0.0);
      else if (ay >= az)
         (mRx, mRy, mRz) = ry > 0 ? (-90.0, 0.0, 0.0) : (90.0, 0.0, 0.0);
      else
         (mRx, mRy, mRz) = rz > 0 ? (0.0, 0.0, 0.0) : (180.0, 0.0, 0.0);
      mX = hit.X;
      mY = hit.Y;
      mZ = hit.Z;
      ComputeIK ();
      SyncIKSliders ();
   }

   void ComputeIK () {
      var cs  = CoordSystem.World;
      cs *= Matrix3.Rotation (EAxis.X, mRx.D2R ());
      cs *= Matrix3.Rotation (EAxis.Y, mRy.D2R ());
      cs *= Matrix3.Rotation (EAxis.Z, mRz.D2R ());
      var tcp   = new Vector3 (mX, mY, mZ - LJointZ);
      var wrist = tcp - cs.VecX * mTcpOffset.X - cs.VecY * mTcpOffset.Y - cs.VecZ * mTcpOffset.Z;
      mCS = cs * Matrix3.Translation (wrist);
      mSolver.ComputeStances (mCS.Org, mCS.VecZ, mCS.VecX);
      for (int j = 0; j < 8; j++) {
         var a = mSolver.Solutions[j];
         if (!a.OK) continue;
         for (int i = 0; i < 6; i++)
            mJoints[i].JValue = a.GetJointAngle (i);
         break;
      }
      mGripper.Xfm = mTip.Xfm;
      CheckCollisions ();
   }

   void OnFK () {
      mGripper.Xfm = mTip.Xfm;
      CheckCollisions ();
   }

   void SyncIKSliders () {
      mSyncingUI = true;
      foreach (var (key, val) in new[] { ("X", mX), ("Y", mY), ("Z", mZ), ("Rx", mRx), ("Ry", mRy), ("Rz", mRz) })
         if (mSliders.TryGetValue (key, out var s)) s.Value = Math.Clamp (val, s.Minimum, s.Maximum);
      mSyncingUI = false;
   }

   Matrix3 BoxWorldXfm => Matrix3.Translation (mBX, mBY, mBZ);

   void UpdateBox () {
      mBoxXfm.Xfm = BoxWorldXfm;
      CheckCollisions ();
   }

   void CheckCollisions () {
      var boxOBBW = mBoxOBB.With (BoxWorldXfm);
      using var bc = OBBCollider.Borrow ();

      bool boxHit = false;
      Dictionary<string, bool> groupHit = [];
      foreach (var tri in mTris) groupHit.TryAdd (tri.Group, false);

      foreach (var (m, linkOBB) in mLinkOBBs) {
         var wLink    = linkOBB.With (m.Xfm);
         bool linkHit = bc.Check (wLink, boxOBBW);
         if (linkHit) boxHit = true;
         foreach (var tri in mTris) {
            if (bc.Check (wLink, tri.OBB.With (Matrix3.Identity))) {
               linkHit = true;
               groupHit[tri.Group] = true;
            }
         }
         m.IsColliding = linkHit;
      }

      mBoxVN.Color = boxHit ? Color4.Red : Color4.Blue;
      foreach (var tri in mTris)
         tri.IsColliding = groupHit[tri.Group];
   }

   void LoadScript (string path) {
      mScript.Clear (); mScriptIdx = 0;
      try {
         var ic = System.Globalization.CultureInfo.InvariantCulture;
         foreach (var line in File.ReadLines (path)) {
            var t = line.Trim ();
            if (t.Length == 0 || t[0] == '#') continue;
            var p = t.Split ((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length < 6) continue;
            mScript.Add ((double.Parse (p[0], ic), double.Parse (p[1], ic), double.Parse (p[2], ic),
                          double.Parse (p[3], ic), double.Parse (p[4], ic), double.Parse (p[5], ic)));
         }
         Lib.Trace ($"Loaded {mScript.Count} waypoints");
      } catch (Exception ex) { Lib.Trace ($"Load failed: {ex.Message}"); }
   }

   void TogglePlay () {
      if (mPlayTimer.IsEnabled) {
         mPlayTimer.IsEnabled = false;
         mPlayBtn!.Content    = "Play";
      } else {
         if (mScript.Count == 0) { Lib.Trace ("No script loaded"); return; }
         mScriptIdx           = 0;
         mPlayTimer.IsEnabled = true;
         mPlayBtn!.Content    = "Stop";
      }
   }

   void TickScript () {
      if (mScriptIdx >= mScript.Count) {
         mPlayTimer.IsEnabled = false; mPlayBtn!.Content = "Play"; return;
      }
      var pt = mScript[mScriptIdx++];
      (mX, mY, mZ, mRx, mRy, mRz) = (pt.X, pt.Y, pt.Z, pt.Rx, pt.Ry, pt.Rz);
      ComputeIK ();
   }

   void ShowAddTriDlg () {
      var dlg = new TriangleDialog ();
      if (dlg.ShowDialog () is true)
         AddTri (dlg.TriName, dlg.Group, dlg.P1, dlg.P2, dlg.P3);
   }

   void AddTri (string name, string group, Point3 p1, Point3 p2, Point3 p3) {
      var tri = new CollisionTri (name, group, GroupColor (group), p1, p2, p3);
      mTris.Add (tri);
      mTriGroup.Add (tri.VN);
      CheckCollisions ();
      RefreshTriList ();
   }

   Color4 GroupColor (string group) {
      if (mGroupColors.TryGetValue (group, out var color)) return color;
      int idx = mGroupColors.Count - 1; // exclude the pre-seeded "Box" entry
      color = sGroupPalette[idx % sGroupPalette.Length];
      mGroupColors[group]    = color;
      mGroupWpfColors[group] = sWpfGroupPalette[idx % sWpfGroupPalette.Length];
      return color;
   }

   void RemoveTri (CollisionTri tri) {
      mTris.Remove (tri);
      mTriGroup.Remove (tri.VN);
      tri.IsColliding = false;
      CheckCollisions ();
      RefreshTriList ();
   }

   void RefreshTriList () {
      if (mTriListPanel is null) return;
      mTriListPanel.Children.Clear ();
      foreach (var tri in mTris) {
         var t   = tri;
         var row = new StackPanel { Orientation = Orientation.Horizontal,
                                    Margin = new Thickness (6, 2, 6, 2) };
         row.Children.Add (new TextBlock {
            Text = t.Name, Width = 110,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = System.Windows.Media.Brushes.Silver
         });
         var wpfColor = mGroupWpfColors.GetValueOrDefault (t.Group, System.Windows.Media.Colors.Gray);
         row.Children.Add (new TextBlock {
            Text = t.Group, Width = 62,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new System.Windows.Media.SolidColorBrush (wpfColor)
         });
         var del = new Button { Content = "×", Padding = new Thickness (5, 1, 5, 1),
                                               Margin  = new Thickness (2, 2, 2, 2) };
         del.Click += (_, _) => RemoveTri (t);
         row.Children.Add (del);
         mTriListPanel.Children.Add (row);
      }
   }

   // Fields -------------------------------------------------------------------
   // World Z of the L joint = Base.Sockets.Z(266.5) + S.Sockets.Z(298.5); solver expects Z relative to this plane
   const double LJointZ = 565;
   readonly CoordSystem mHome    = new (new (1166, 0, 1161 - LJointZ), Vector3.XAxis, Vector3.YAxis);
   CoordSystem          mCS;
   readonly double[]    mMin     = new double[6], mMax = new double[6];
   readonly RBRSolver   mSolver;
   readonly Mechanism   mMech, mTip;
   readonly Mechanism[] mJoints;
   readonly XfmVN       mGripper;
   readonly Mesh3       mBoxMesh;
   readonly OBBTree     mBoxOBB;
   readonly Mesh3VN     mBoxVN;
   readonly XfmVN       mBoxXfm;
   readonly Dictionary<Mechanism, OBBTree> mLinkOBBs = [];
   double mBX = 700, mBY = 0, mBZ = 700;
   double mX, mY, mZ, mRx = -90, mRy, mRz;
   Vector3 mTcpOffset = new (0, 0, -50);

   readonly List<(double X, double Y, double Z, double Rx, double Ry, double Rz)> mScript = [];
   int      mScriptIdx;
   readonly DispatcherTimer mPlayTimer;
   Button?  mPlayBtn;

   readonly Dictionary<string, Slider> mSliders = [];
   bool     mSyncingUI;

   readonly GroupVN            mTriGroup;
   readonly List<CollisionTri> mTris = [];
   StackPanel?                 mTriListPanel;

   // Group → 3D color (Box pre-seeded; other groups assigned from palette on first use)
   readonly Dictionary<string, Color4>                           mGroupColors    = new () { ["Box"] = Color4.Blue };
   readonly Dictionary<string, System.Windows.Media.Color>       mGroupWpfColors = new () { ["Box"] = System.Windows.Media.Colors.CornflowerBlue };
   static readonly Color4[]                                      sGroupPalette    = [Color4.Cyan, Color4.Green, Color4.Yellow];
   static readonly System.Windows.Media.Color[]                  sWpfGroupPalette = [System.Windows.Media.Colors.Cyan, System.Windows.Media.Colors.LimeGreen, System.Windows.Media.Colors.Yellow];
}
#endregion

#region class TcpVN --------------------------------------------------------------------------------
class TcpVN : VNode {
   public TcpVN () { Streaming = true; }
   public required RobotScene Scene { private get; init; }

   public override void SetAttributes () { Lux.ZLevel = 70; }

   public override void Draw () {
      var fcs    = CoordSystem.World * Scene.Tip.Xfm;
      var off    = Scene.TcpOffset;
      var tcp    = fcs.Org + fcs.VecX * off.X + fcs.VecY * off.Y + fcs.VecZ * off.Z;
      DrawArrow (tcp, fcs.VecX, Color4.Red);
      DrawArrow (tcp, fcs.VecY, Color4.Green);
      DrawArrow (tcp, fcs.VecZ, Color4.Blue);

      Lux.Color = Color4.White; Lux.LineWidth = 2.5f;
      const int N = 24; const double R = 20;
      var ring = new Vec3F[N * 2];
      for (int i = 0; i < N; i++) {
         double a1 = 2 * Math.PI * i / N, a2 = 2 * Math.PI * (i + 1) / N;
         ring[i * 2]     = (Vec3F)(tcp + fcs.VecX * (Math.Cos (a1) * R) + fcs.VecY * (Math.Sin (a1) * R));
         ring[i * 2 + 1] = (Vec3F)(tcp + fcs.VecX * (Math.Cos (a2) * R) + fcs.VecY * (Math.Sin (a2) * R));
      }
      Lux.Lines (ring.AsSpan ());
   }

   void DrawArrow (Point3 from, Vector3 dir, Color4 color) {
      Lux.Color = color; Lux.LineWidth = 4f;
      const double L = 180, HL = 30, HW = 14;
      var tip    = from + dir * L;
      var notDir = Math.Abs (dir.X) > 0.85 ? new Vector3 (0, 0, 1) : Vector3.XAxis;
      var side   = (dir * notDir).Normalized ();
      var back   = tip - dir * HL;
      Vec3F[] pts = [(Vec3F)from, (Vec3F)tip,
                     (Vec3F)(back + side * HW), (Vec3F)tip,
                     (Vec3F)(back - side * HW), (Vec3F)tip];
      Lux.Lines (pts.AsSpan ());
   }
}
#endregion

#region class InfoVN --------------------------------------------------------------------------------
class InfoVN : VNode {
   public InfoVN () { Streaming = true; }
   public required RobotScene Scene { private get; init; }

   public override void SetAttributes () { Lux.Color = Color4.White; Lux.TypeFace = mFace; Lux.ZLevel = 90; }

   public override void Draw () {
      if (Lux.UIScene is not { } sc) return;
      var (x, y, z, rx, ry, rz, joints) = Scene.InfoData;
      int lh = mFace.LineHeight, py = (int)sc.Rect.Height - lh * 4 - 8, px = 10;
      void Line (string s) { Lux.Text (s, new Vec2S (px, py)); py += lh; }
      Line ($"X={x,8:F1}  Y={y,8:F1}  Z={z,8:F1}  mm");
      Line ($"Rx={rx,7:F1}°  Ry={ry,7:F1}°  Rz={rz,7:F1}°");
      Line ($"S={joints[0].JValue,7:F1}°  L={joints[1].JValue,7:F1}°  U={joints[2].JValue,7:F1}°");
      Line ($"R={joints[3].JValue,7:F1}°  B={joints[4].JValue,7:F1}°  T={joints[5].JValue,7:F1}°");
   }

   readonly TypeFace mFace = new (Lib.ReadBytes ("nori:GL/Fonts/RobotoMono-Regular.ttf"), 14);
}
#endregion

#region class CollisionTri -------------------------------------------------------------------------
class CollisionTri {
   // Constructor --------------------------------------------------------------
   public CollisionTri (string name, string group, Color4 color, Point3 p1, Point3 p2, Point3 p3) {
      Name = name; Group = group; mIdleColor = color;
      P1     = p1;
      var v1 = new Vector3 (p2.X - p1.X, p2.Y - p1.Y, p2.Z - p1.Z);
      var v2 = new Vector3 (p3.X - p1.X, p3.Y - p1.Y, p3.Z - p1.Z);
      Normal = (v1 * v2).Normalized ();
      Mesh   = new Mesh3Builder ([p1, p2, p3]).Build ();
      OBB    = OBBTree.From (Mesh);
      VN     = new Mesh3VN (Mesh) { Mode = EShadeMode.Glass, Color = color };
   }

   // Properties ---------------------------------------------------------------
   public string  Name;
   public string  Group;
   public Point3  P1;
   public Vector3 Normal;
   public Mesh3   Mesh;
   public OBBTree OBB;
   public Mesh3VN VN;
   public bool IsColliding { set => VN.Color = value ? Color4.Red : mIdleColor; }

   // Fields -------------------------------------------------------------------
   readonly Color4 mIdleColor;
}
#endregion
