// ╔═╦╗
// ║╬╠╬╦╗ RobotScene.cs
// ║╔╣╠║╣ 3D robot scene with FK/IK solver and box collision detection
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Windows.Threading;
namespace FApp;

#region class RobotScene ---------------------------------------------------------------------------
class RobotScene : Scene3 {
   // Constructor --------------------------------------------------------------
   public RobotScene () {
      var mechPath = Path.Combine (AppContext.BaseDirectory, "FanucX", "mechanism.curl");
      if (!File.Exists (mechPath)) mechPath = "N:/Wad/FanucX/mechanism.curl";
      mMech   = Mechanism.Load (mechPath);
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
      // ViewModel must exist before BoxWorldXfm is first read (mBoxXfm init below).
      ViewModel   = new RobotViewModel ();
      mBoxXfm     = new XfmVN (BoxWorldXfm, mBoxVN);

      foreach (var m in mMech.EnumTree ()) {
         var cm = m.CMesh;
         if (cm != null) mLinkOBBs[m] = OBBTree.From (cm);
         else if (m.Mesh != null) mLinkOBBs[m] = OBBTree.From (m.Mesh);
      }

      mGripper  = new XfmVN (Matrix3.Identity, new GroupVN ([]));
      mTriGroup = new GroupVN ([]);

      // ── ViewModel wiring ──────────────────────────────────────────────────
      // Populate joints and subscribe events, then trigger the initial IK solve.
      ViewModel.Joints                = [.. mMech.EnumTree ()
                                               .Where  (m => m.Joint != EJoint.None)
                                               .Select (m => new JointSliderModel (m, OnFK))];
      ViewModel.IKChanged           += ComputeIK;
      ViewModel.BoxChanged          += UpdateBox;
      ViewModel.HomeRequested       += GoHome;
      ViewModel.LoadScriptRequested += LoadScript;
      ViewModel.AddRequested        += AddCurrentPose;
      ViewModel.PlayRequested       += TogglePlay;
      ViewModel.SetIKPose (mHome.Org.X, mHome.Org.Y, mHome.Org.Z + LJointZ, -90, 0, 0);

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
   public RobotViewModel ViewModel { get; }
   public Mechanism      Tip       => mTip;
   public Vector3        TcpOffset { get => mTcpOffset; set { mTcpOffset = value; ComputeIK (); } }

   public (double X, double Y, double Z, double Rx, double Ry, double Rz,
           Mechanism[] Joints) InfoData
      => (ViewModel.X, ViewModel.Y, ViewModel.Z,
          ViewModel.Rx, ViewModel.Ry, ViewModel.Rz, mJoints);

   // Implementation -----------------------------------------------------------
   public override void Detached () => mPlayTimer.IsEnabled = false;

   public override void Picked (object obj) {
      if (obj == mBoxMesh) { SnapToFace (Lux.PickPos); return; }
      foreach (var tri in mTris)
         if (obj == tri.Mesh) { SnapToTriNode (tri.P1, tri.Normal); return; }
   }

   void SnapToTriNode (Point3 p1, Vector3 normal) {
      var (rx, ry, rz) = NormalToEuler (normal);
      ViewModel.SetIKPose (p1.X, p1.Y, p1.Z, rx, ry, rz);
   }

   // Maps a unit normal to (Rx, Ry, Rz=0) so VecZ after cs*=Rot(X,Rx)*Rot(Y,Ry) equals normal.
   // VecZ formula: (sin(Ry), -sin(Rx)*cos(Ry), cos(Rx)*cos(Ry))
   static (double Rx, double Ry, double Rz) NormalToEuler (Vector3 n) {
      double ry    = Math.Asin (Math.Clamp (n.X, -1, 1));
      double cosRy = Math.Cos (ry);
      double rx    = Math.Abs (cosRy) > 1e-6 ? Math.Atan2 (-n.Y, n.Z) : 0;
      return (rx * (180 / Math.PI), ry * (180 / Math.PI), 0);
   }

   void SnapToFace (Point3 hit) {
      double rx = hit.X - ViewModel.BX, ry = hit.Y - ViewModel.BY, rz = hit.Z - ViewModel.BZ;
      double ax = Math.Abs (rx), ay = Math.Abs (ry), az = Math.Abs (rz);
      // VecZ points outward from the clicked face, which is the approach direction for the TCP.
      // Rx=0 → VecZ=+Z, Rx=-90 → VecZ=+Y, Ry=+90 → VecZ=+X (and their negatives).
      double newRx, newRy, newRz;
      if (ax >= ay && ax >= az)
         (newRx, newRy, newRz) = rx > 0 ? (0.0, 90.0, 0.0) : (0.0, -90.0, 0.0);
      else if (ay >= az)
         (newRx, newRy, newRz) = ry > 0 ? (-90.0, 0.0, 0.0) : (90.0, 0.0, 0.0);
      else
         (newRx, newRy, newRz) = rz > 0 ? (0.0, 0.0, 0.0) : (180.0, 0.0, 0.0);
      ViewModel.SetIKPose (hit.X, hit.Y, hit.Z, newRx, newRy, newRz);
   }

   // Step 1: Build TCP orientation from Euler angles (X-then-Y convention).
   // Step 2: Subtract LJointZ — the solver works in the L-joint frame, not world Z.
   // Step 3: Back-solve wrist = TCP_pos − R × TCP_offset.
   // Step 4: Pass wrist pose to the analytic solver (up to 8 closed-form solutions).
   // Step 5: Apply the first valid solution to joint angles.
   void ComputeIK () {
      var cs    = CoordSystem.World;
      cs       *= Matrix3.Rotation (EAxis.X, ViewModel.Rx.D2R ());
      cs       *= Matrix3.Rotation (EAxis.Y, ViewModel.Ry.D2R ());
      cs       *= Matrix3.Rotation (EAxis.Z, ViewModel.Rz.D2R ());
      var tcp   = new Vector3 (ViewModel.X, ViewModel.Y, ViewModel.Z - LJointZ);
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
      var tipCs    = CoordSystem.World * mTip.Xfm;
      var off      = mTcpOffset;
      var tcp      = tipCs.Org + tipCs.VecX * off.X + tipCs.VecY * off.Y + tipCs.VecZ * off.Z;
      var (rx, ry, rz) = MatrixToEuler (tipCs);
      ViewModel.SetIKDisplay (tcp.X, tcp.Y, tcp.Z + LJointZ, rx, ry, rz);
      foreach (var js in ViewModel.Joints) js.Refresh ();
      mGripper.Xfm = mTip.Xfm;
      CheckCollisions ();
   }

   // Extracts XYZ Euler angles (degrees) from a coordinate system.
   // Inverse of: cs *= Rot(X,Rx) * Rot(Y,Ry) * Rot(Z,Rz) used in ComputeIK.
   static (double Rx, double Ry, double Rz) MatrixToEuler (CoordSystem cs) {
      double ry    = Math.Asin (Math.Clamp (cs.VecZ.X, -1, 1));
      double cosRy = Math.Cos (ry);
      double rx, rz;
      if (Math.Abs (cosRy) > 1e-6) {
         rx = Math.Atan2 (-cs.VecZ.Y, cs.VecZ.Z);
         rz = Math.Atan2 (-cs.VecY.X, cs.VecX.X);
      } else {
         rx = Math.Atan2 (cs.VecX.Y, cs.VecY.Y);
         rz = 0;
      }
      const double R2D = 180 / Math.PI;
      return (rx * R2D, ry * R2D, rz * R2D);
   }

   Matrix3 BoxWorldXfm => Matrix3.Translation (ViewModel.BX, ViewModel.BY, ViewModel.BZ);

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

   void AddCurrentPose () {
      var ic   = System.Globalization.CultureInfo.InvariantCulture;
      var line = string.Format (ic, "{0:F1} {1:F1} {2:F1} {3:F1} {4:F1} {5:F1}",
                                ViewModel.X, ViewModel.Y, ViewModel.Z,
                                ViewModel.Rx, ViewModel.Ry, ViewModel.Rz);
      File.AppendAllText (ViewModel.ScriptPath, line + Environment.NewLine);
   }

   internal void LoadScript (string path) {
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

   internal void LoadCollision (string path) {
      try {
         foreach (var line in File.ReadLines (path)) {
            var p = line.Split (',');
            if (p[0].Trim ().Equals ("Name", StringComparison.OrdinalIgnoreCase)) continue;
            string name, group; int offset;
            if (p.Length >= 11)      { name = p[0].Trim (); group = p[1].Trim (); offset = 2; }
            else if (p.Length >= 10) { name = p[0].Trim (); group = "Box";        offset = 1; }
            else continue;
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            var d = new double[9]; bool ok = true;
            for (int i = 0; i < 9 && ok; i++) ok = double.TryParse (p[offset + i].Trim (), System.Globalization.NumberStyles.Float, ic, out d[i]);
            if (!ok) continue;
            AddTri (name, group, new (d[0], d[1], d[2]), new (d[3], d[4], d[5]), new (d[6], d[7], d[8]));
         }
         Lib.Trace ($"Loaded {mTris.Count} collision triangles from {path}");
      } catch (Exception ex) { Lib.Trace ($"Collision load failed: {ex.Message}"); }
   }

   internal void StartPlay () {
      if (mScript.Count == 0) { Lib.Trace ("No script loaded"); return; }
      mScriptIdx           = 0;
      mPlayTimer.IsEnabled = true;
      ViewModel.PlayLabel  = "Stop";
   }

   void TogglePlay () {
      if (mPlayTimer.IsEnabled) {
         mPlayTimer.IsEnabled = false;
         ViewModel.PlayLabel  = "Play";
      } else {
         StartPlay ();
      }
   }

   void TickScript () {
      if (mScriptIdx >= mScript.Count) {
         mPlayTimer.IsEnabled = false;
         ViewModel.PlayLabel  = "Play";
         return;
      }
      var pt = mScript[mScriptIdx++];
      ViewModel.SetIKPose (pt.X, pt.Y, pt.Z, pt.Rx, pt.Ry, pt.Rz);
   }

   internal void GoHome () =>
      ViewModel.SetIKPose (mHome.Org.X, mHome.Org.Y, mHome.Org.Z + LJointZ, -90, 0, 0);

   internal void AddTri (string name, string group, Point3 p1, Point3 p2, Point3 p3) {
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

   // Rebuilds ViewModel.Triangles so the XAML ItemsControl reflects the current list.
   void RefreshTriList () {
      ViewModel.Triangles.Clear ();
      foreach (var tri in mTris) {
         var t        = tri;
         var wpfColor = mGroupWpfColors.GetValueOrDefault (t.Group, System.Windows.Media.Colors.Gray);
         var brush    = new System.Windows.Media.SolidColorBrush (wpfColor);
         ViewModel.Triangles.Add (new CollisionTriVM (t.Name, t.Group, brush, () => RemoveTri (t)));
      }
   }

   // Fields -------------------------------------------------------------------
   // LJointZ: world Z height of the L-joint rotation axis.
   //   = Base.Sockets.Z (266.5 mm) + S-link.Sockets.Z (298.5 mm) = 565 mm.
   //   The IK solver expects all Z values relative to this plane, not from the floor.
   const double LJointZ = 565;
   // mHome: arm's-length pose used to initialise the ViewModel.
   //   X=1166 mm (TCP reach), Y=0 (centred), Z=1161 mm world = (1161-LJointZ) above L-joint.
   readonly CoordSystem mHome = new (new (1166, 0, 1161 - LJointZ), Vector3.XAxis, Vector3.YAxis);
   CoordSystem          mCS;
   readonly double[]    mMin    = new double[6], mMax = new double[6];
   readonly RBRSolver   mSolver;
   readonly Mechanism   mMech, mTip;
   readonly Mechanism[] mJoints;
   readonly XfmVN       mGripper;
   readonly Mesh3       mBoxMesh;
   readonly OBBTree     mBoxOBB;
   readonly Mesh3VN     mBoxVN;
   readonly XfmVN       mBoxXfm;
   readonly Dictionary<Mechanism, OBBTree> mLinkOBBs = [];
   Vector3 mTcpOffset = new (0, 0, -50);

   readonly List<(double X, double Y, double Z, double Rx, double Ry, double Rz)> mScript = [];
   int      mScriptIdx;
   readonly DispatcherTimer mPlayTimer;

   readonly GroupVN            mTriGroup;
   readonly List<CollisionTri> mTris = [];

   // Group → 3D color (Box pre-seeded; other groups assigned from palette on first use)
   readonly Dictionary<string, Color4>                      mGroupColors    = new () { ["Box"] = Color4.Blue };
   readonly Dictionary<string, System.Windows.Media.Color>  mGroupWpfColors = new () { ["Box"] = System.Windows.Media.Colors.CornflowerBlue };
   static readonly Color4[]                                 sGroupPalette    = [Color4.Cyan, Color4.Green, Color4.Yellow];
   static readonly System.Windows.Media.Color[]             sWpfGroupPalette = [System.Windows.Media.Colors.Cyan, System.Windows.Media.Colors.LimeGreen, System.Windows.Media.Colors.Yellow];
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
   public InfoVN () {
      Streaming = true;
      var fontPath = Path.Combine (AppContext.BaseDirectory, "GL", "Fonts", "RobotoMono-Regular.ttf");
      mFace = new TypeFace (File.Exists (fontPath) ? File.ReadAllBytes (fontPath)
                                                    : Lib.ReadBytes ("nori:GL/Fonts/RobotoMono-Regular.ttf"), 14);
   }
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

   readonly TypeFace mFace;
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
