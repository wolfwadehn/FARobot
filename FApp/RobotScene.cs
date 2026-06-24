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
      // ViewModel must exist before BoxWorldXfm is first read (mBoxXfm init below).
      ViewModel   = new RobotViewModel ();
      mBoxXfm     = new XfmVN (BoxWorldXfm, mBoxVN);

      foreach (var m in mMech.EnumTree ()) {
         var cm = m.CMesh;
            try {
               if (cm != null) mLinkOBBs[m] = OBBTree.From (cm);
               else if (m.Mesh != null) mLinkOBBs[m] = OBBTree.From (m.Mesh);
            } catch (Exception) { /* Skip degenerate meshes that OBBTree cannot process */ }
      }

      mGripper     = new XfmVN (Matrix3.Identity, new GroupVN ([]));
      mTriGroup    = new GroupVN ([]);
      mPalletGroup = new GroupVN ([]);

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
      ViewModel.WaypointScrubbed    += ApplyWaypointPos;
      ViewModel.FrameToggled        += ComputeIK;
      LoadFrame ();
      ViewModel.SetIKPose (mHome.Org.X, mHome.Org.Y, mHome.Org.Z + LJointZ, -90, 0, 0);

      mPlayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds (40) };
      mPlayTimer.Tick += (_, _) => TickScript ();

      Lib.Tracer = TraceVN.Print;
      BgrdColor  = Color4.Gray (64);
      Bound      = new Bound3 (-1200, -1200, 0, 1200, 1200, 1500);
      Root       = new GroupVN ([
         new MechanismVN (mMech), mGripper, mBoxXfm, mTriGroup, mPalletGroup,
         new TcpVN   { Scene = this },
         new FrameVN { Scene = this },
         new InfoVN  { Scene = this },
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

   // Pallet frame is "active" only when one is calibrated AND the user has enabled it.
   public bool        FrameActive => mHasFrame && ViewModel.UseFrame;
   public CoordSystem FrameCS     => mFrame;
   // The 3 raw calibration points (null when uncalibrated) — used to prefill the dialog.
   public (Point3 P1, Point3 P2, Point3 P3)? FramePoints
      => mHasFrame ? (mFrameP1, mFrameP2, mFrameP3) : null;

   // The taught pickup pose in world coords (null when not set) — drawn by FrameVN.
   public CoordSystem? PickupCS => mHasPickup ? mPickupCS : null;

   // Implementation -----------------------------------------------------------
   public override void Detached () => mPlayTimer.IsEnabled = false;

   public override void Picked (object obj) {
      // A click on the imported pallet does different things depending on the active
      // teach mode: set the work frame at a corner, or fix the pickup surface.
      if (mPallet != null && obj == mPallet) {
         var hit = Lux.PickPos;
         switch (mPickMode) {
            case EPickMode.Corner: mPickMode = EPickMode.None; SetFrameFromCorner (hit); return;
            case EPickMode.Pickup: mPickMode = EPickMode.None; SetPickup (hit);          return;
            default: return;   // ignore stray clicks on the pallet outside a teach mode
         }
      }
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
   // Step 5: Pick the solution closest to the current joints (configuration continuity)
   //         so interpolated motion takes the short joint path instead of flipping the
   //         wrist to a limit.
   void ComputeIK () {
      // Build the target TCP pose in the active reference frame: orientation first
      // (X-then-Y-then-Z Euler), then position.  The values come straight from the
      // ViewModel, which are pallet-frame coordinates when a frame is active.
      var pose  = CoordSystem.World;
      pose     *= Matrix3.Rotation (EAxis.X, ViewModel.Rx.D2R ());
      pose     *= Matrix3.Rotation (EAxis.Y, ViewModel.Ry.D2R ());
      pose     *= Matrix3.Rotation (EAxis.Z, ViewModel.Rz.D2R ());
      pose     += new Vector3 (ViewModel.X, ViewModel.Y, ViewModel.Z);
      if (FrameActive) pose *= Matrix3.To (mFrame);   // pallet-local → world
      // LJointZ: the solver works relative to the L-joint plane, so drop world Z by it.
      pose     += new Vector3 (0, 0, -LJointZ);
      // Back-solve wrist = TCP − R × TCP_offset, then hand the wrist pose to the solver.
      var wrist = pose.Org - pose.VecX * mTcpOffset.X - pose.VecY * mTcpOffset.Y - pose.VecZ * mTcpOffset.Z;
      mCS = new CoordSystem (wrist, pose.VecX, pose.VecY);
      mSolver.ComputeStances (mCS.Org, mCS.VecZ, mCS.VecX);
      // Choose the valid solution nearest the current joint configuration (least total
      // angular travel) so the arm doesn't jump branches and flip the wrist mid-move.
      int best = -1; double bestCost = double.MaxValue;
      for (int j = 0; j < 8; j++) {
         var a = mSolver.Solutions[j];
         if (!a.OK) continue;
         double cost = 0;
         for (int i = 0; i < 6; i++) { double d = a.GetJointAngle (i) - mJoints[i].JValue; cost += d * d; }
         if (cost < bestCost) { bestCost = cost; best = j; }
      }
      if (best >= 0) {
         var sol = mSolver.Solutions[best];
         for (int i = 0; i < 6; i++) mJoints[i].JValue = sol.GetJointAngle (i);
      }
      mGripper.Xfm = mTip.Xfm;
      CheckCollisions ();
   }

   void OnFK () {
      var tipCs = CoordSystem.World * mTip.Xfm;
      var off   = mTcpOffset;
      var tcp   = tipCs.Org + tipCs.VecX * off.X + tipCs.VecY * off.Y + tipCs.VecZ * off.Z;
      // Lift from the solver's L-joint frame back to world, then express the pose in
      // the active frame so the IK display matches what ComputeIK consumes.
      var world = new CoordSystem (tcp + new Vector3 (0, 0, LJointZ), tipCs.VecX, tipCs.VecY);
      if (FrameActive) world *= Matrix3.From (mFrame);   // world → pallet-local
      var (rx, ry, rz) = MatrixToEuler (world);
      ViewModel.SetIKDisplay (world.Org.X, world.Org.Y, world.Org.Z, rx, ry, rz);
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

   void LoadScript (string path) {
      mScript.Clear ();
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
      // Reset the scrubber: max = last index; jump the robot to the first waypoint.
      ViewModel.WaypointMax = Math.Max (0, mScript.Count - 1);
      ViewModel.WaypointPos = 0;
      ApplyWaypointPos ();
   }

   // Drives the robot to the scrub position, interpolating between the two bracketing
   // waypoints (linear on position and Euler angles) for smooth motion.
   void ApplyWaypointPos () {
      if (mScript.Count == 0) return;
      double pos = Math.Clamp (ViewModel.WaypointPos, 0, Math.Max (0, mScript.Count - 1));
      int i = Math.Min ((int)Math.Floor (pos), mScript.Count - 1);
      var a = mScript[i];
      if (i >= mScript.Count - 1) { ViewModel.SetIKPose (a.X, a.Y, a.Z, a.Rx, a.Ry, a.Rz); return; }
      var b = mScript[i + 1];
      double f = pos - i;
      double L (double x, double y) => x + (y - x) * f;
      ViewModel.SetIKPose (L (a.X, b.X), L (a.Y, b.Y), L (a.Z, b.Z),
                           L (a.Rx, b.Rx), L (a.Ry, b.Ry), L (a.Rz, b.Rz));
   }

   void TogglePlay () {
      if (mPlayTimer.IsEnabled) {
         mPlayTimer.IsEnabled = false;
         ViewModel.PlayLabel  = "Play";
      } else {
         if (mScript.Count < 2) { Lib.Trace ("Need at least 2 waypoints to play"); return; }
         ViewModel.WaypointPos = 0;          // start of cycle
         mPlayTimer.IsEnabled  = true;
         ViewModel.PlayLabel   = "Stop";
      }
   }

   // One animation tick: advance the scrubber; stop after a single forward cycle.
   void TickScript () {
      double max  = Math.Max (0, mScript.Count - 1);
      double next = ViewModel.WaypointPos + PlayStep;
      if (next >= max) {
         ViewModel.WaypointPos = max;        // land exactly on the last waypoint
         mPlayTimer.IsEnabled  = false;
         ViewModel.PlayLabel   = "Play";
         return;
      }
      ViewModel.WaypointPos = next;          // setter fires WaypointScrubbed → ApplyWaypointPos
   }

   // Scrub increment per timer tick.  At a 40 ms interval, 0.04 ≈ one segment per second.
   const double PlayStep = 0.04;

   internal void GoHome () =>
      ViewModel.SetIKPose (mHome.Org.X, mHome.Org.Y, mHome.Org.Z + LJointZ, -90, 0, 0);

   // ── Pallet frame ──────────────────────────────────────────────────────────
   // Builds the pallet frame from the 3 calibration points, persists it, and turns
   // it on.  Enabling UseFrame fires FrameToggled → ComputeIK, so the robot re-solves
   // with the entered pose reinterpreted in pallet coordinates.
   internal void SetPalletFrame (Point3 origin, Point3 xptr, Point3 plane) {
      mFrame    = BuildFrame (origin, xptr, plane);
      (mFrameP1, mFrameP2, mFrameP3) = (origin, xptr, plane);
      mHasFrame = true;
      SaveFrame ();
      ViewModel.FrameStatus = $"origin ({origin.X:F0}, {origin.Y:F0}, {origin.Z:F0})";
      ViewModel.UseFrame    = true;   // fires FrameToggled → ComputeIK
   }

   internal void ClearPalletFrame () {
      mHasFrame             = false;
      ViewModel.FrameStatus = "(not calibrated)";
      try { if (File.Exists (mFramePath)) File.Delete (mFramePath); }
      catch (Exception ex) { Lib.Trace ($"Pallet frame delete failed: {ex.Message}"); }
      ViewModel.UseFrame    = false;  // fires FrameToggled → ComputeIK (back to world)
   }

   // P1=origin, P2 sets +X, P3 lies on the +XY side.  Z = X×(P3−P1) (right-hand rule),
   // Y = Z×X — so the basis is orthonormal regardless of P3's exact position.
   static CoordSystem BuildFrame (Point3 origin, Point3 xptr, Point3 plane) {
      var vx = xptr - origin;
      var vz = vx * (plane - origin);   // cross product
      var vy = vz * vx;                 // ⊥ vx, points toward the P3 side
      return new CoordSystem (origin, vx, vy);
   }

   void LoadFrame () {
      try {
         if (!File.Exists (mFramePath)) return;
         var ic   = System.Globalization.CultureInfo.InvariantCulture;
         var line = File.ReadLines (mFramePath)
                        .Select (l => l.Trim ())
                        .FirstOrDefault (l => l.Length > 0 && l[0] != '#');
         if (line == null) return;
         var p = line.Split (',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
         if (p.Length < 9) return;
         double D (int i) => double.Parse (p[i], ic);
         var p1 = new Point3 (D (0), D (1), D (2));
         var p2 = new Point3 (D (3), D (4), D (5));
         var p3 = new Point3 (D (6), D (7), D (8));
         mFrame    = BuildFrame (p1, p2, p3);
         (mFrameP1, mFrameP2, mFrameP3) = (p1, p2, p3);
         mHasFrame = true;
         // Loaded but left OFF: the initial home pose is in world coords, so the user
         // re-enables the frame with the sidebar checkbox when ready.
         ViewModel.FrameStatus = $"loaded — origin ({p1.X:F0}, {p1.Y:F0}, {p1.Z:F0})";
      } catch (Exception ex) { Lib.Trace ($"Pallet frame load failed: {ex.Message}"); }
   }

   void SaveFrame () {
      try {
         var ic = System.Globalization.CultureInfo.InvariantCulture;
         var s  = string.Format (ic, "{0:F3},{1:F3},{2:F3},{3:F3},{4:F3},{5:F3},{6:F3},{7:F3},{8:F3}",
                                 mFrameP1.X, mFrameP1.Y, mFrameP1.Z, mFrameP2.X, mFrameP2.Y, mFrameP2.Z,
                                 mFrameP3.X, mFrameP3.Y, mFrameP3.Z);
         File.WriteAllText (mFramePath,
            "# Pallet frame calibration: P1(origin),P2(+X),P3(+XY)" + Environment.NewLine + s + Environment.NewLine);
      } catch (Exception ex) { Lib.Trace ($"Pallet frame save failed: {ex.Message}"); }
   }

   // ── Pallet geometry + pickup teach ────────────────────────────────────────
   // Loads an STL or OBJ mesh and shows it in the scene.  The mesh is rendered in
   // its file coordinates (assumed robot-world mm) and becomes pickable.
   internal void ImportPallet (string path) {
      try {
         var ext  = Path.GetExtension (path).ToLowerInvariant ();
         var mesh = ext == ".obj" ? Mesh3.LoadObj (path) : new STLReader (path).BuildMesh ();
         if (mPalletVN != null) mPalletGroup.Remove (mPalletVN);
         mPallet   = mesh;
         mPalletVN = new Mesh3VN (mesh) { Mode = EShadeMode.Phong, Color = Color4.Gray (160) };
         mPalletGroup.Add (mPalletVN);
         ViewModel.PalletStatus = $"{Path.GetFileName (path)} — {mesh.Triangle.Length / 3} tris";
         Lux.UIScene?.ZoomExtents ();
         Lib.Trace ($"Loaded pallet '{Path.GetFileName (path)}'");
      } catch (Exception ex) {
         ViewModel.PalletStatus = "(load failed)";
         Lib.Trace ($"Pallet import failed: {ex.Message}");
      }
   }

   // Arm the next pallet click to set the work frame at a corner.
   internal void BeginPickCorner () {
      if (mPallet == null) { Lib.Trace ("Import a pallet first"); return; }
      mPickMode = EPickMode.Corner;
      Lib.Trace ("Click near a pallet corner to set the frame (50×50 mm inward)");
   }

   // Arm the next pallet click to fix the pickup surface.
   internal void BeginPickPickup () {
      if (mPallet == null) { Lib.Trace ("Import a pallet first"); return; }
      mPickMode = EPickMode.Pickup;
      Lib.Trace ("Click the pallet surface to set the pickup position");
   }

   // Builds the work frame from the bounding-box corner nearest the clicked point,
   // offset 50 mm inward along X and Y.  Axes stay world-aligned (X+, Y+, Z up); the
   // origin Z is the clicked surface height.
   void SetFrameFromCorner (Point3 hit) {
      var b        = mPallet!.Bound;
      bool nearMinX = Math.Abs (hit.X - b.X.Min) <= Math.Abs (hit.X - b.X.Max);
      bool nearMinY = Math.Abs (hit.Y - b.Y.Min) <= Math.Abs (hit.Y - b.Y.Max);
      double cx = nearMinX ? b.X.Min : b.X.Max, sx = nearMinX ? 1 : -1;
      double cy = nearMinY ? b.Y.Min : b.Y.Max, sy = nearMinY ? 1 : -1;
      var origin = new Point3 (cx + sx * 50, cy + sy * 50, hit.Z);
      // Reuse the 3-point builder/persistence: +X and +XY helper points along world axes.
      SetPalletFrame (origin, origin + new Vector3 (100, 0, 0), origin + new Vector3 (0, 100, 0));
   }

   // Fixes the pickup pose at the clicked surface: position = hit point, approach axis
   // = into the surface (anti-normal), and previews it by driving the robot there.
   void SetPickup (Point3 hit) {
      var normal  = NormalAt (mPallet!, hit);
      // Heading reference = the user frame's X axis, so the tool faces consistently with
      // the pallet frame instead of an arbitrary direction (which twists the wrist away).
      var heading = mHasFrame ? mFrame.VecX : Vector3.XAxis;
      mPickupCS   = ApproachFrame (hit, -normal, heading);   // TCP Z points into the surface
      mHasPickup  = true;
      var local  = mHasFrame ? mPickupCS * Matrix3.From (mFrame) : mPickupCS;
      ViewModel.PickupStatus = $"frame ({local.Org.X:F0}, {local.Org.Y:F0}, {local.Org.Z:F0})";
      SetIKDisplayFromWorld (mPickupCS);            // move robot to the pickup as a preview
   }

   // Generates Home → reorient → approach → pickup → retract and writes them to the
   // script file in frame-relative coordinates, then loads them and turns the frame on
   // for playback.
   internal void GenerateWaypoints () {
      if (!mHasPickup) { Lib.Trace ("Set a pickup position first"); return; }
      if (!mHasFrame)  { Lib.Trace ("Calibrate the pallet frame first"); return; }
      const double clearance = 100;                       // mm above the pickup along approach
      var approach = mPickupCS + mPickupCS.VecZ * -clearance;   // back off along +normal
      var home     = CoordSystem.World;
      home        *= Matrix3.Rotation (EAxis.X, (-90.0).D2R ());
      home        += new Vector3 (mHome.Org.X, mHome.Org.Y, mHome.Org.Z + LJointZ);
      // Reorient the TCP to the pickup approach orientation while still at the home
      // position: the wrist rotates in place first, instead of swinging through a joint
      // limit while also translating to the pickup.
      var reorient = new CoordSystem (home.Org, mPickupCS.VecX, mPickupCS.VecY);
      CoordSystem[] path = [home, reorient, approach, mPickupCS, approach];   // last = retract

      try {
         var ic = System.Globalization.CultureInfo.InvariantCulture;
         var sb = new StringBuilder ();
         sb.AppendLine ("# Generated waypoints (frame-relative): Home, reorient, approach, pickup, retract");
         foreach (var w in path) {
            var local        = w * Matrix3.From (mFrame);
            var (rx, ry, rz) = MatrixToEuler (local);
            sb.AppendLine (string.Format (ic, "{0:F1} {1:F1} {2:F1} {3:F1} {4:F1} {5:F1}",
                                          local.Org.X, local.Org.Y, local.Org.Z, rx, ry, rz));
         }
         File.WriteAllText (ViewModel.ScriptPath, sb.ToString ());
         LoadScript (ViewModel.ScriptPath);
         ViewModel.UseFrame = true;     // waypoints are frame-relative → play with frame on
         Lib.Trace ($"Generated {path.Length} waypoints → {Path.GetFileName (ViewModel.ScriptPath)}");
      } catch (Exception ex) { Lib.Trace ($"Waypoint generation failed: {ex.Message}"); }
   }

   // Drives the robot to a world pose, expressed through the active frame so the IK
   // display fields stay consistent with what ComputeIK consumes.
   void SetIKDisplayFromWorld (CoordSystem world) {
      var cs           = FrameActive ? world * Matrix3.From (mFrame) : world;
      var (rx, ry, rz) = MatrixToEuler (cs);
      ViewModel.SetIKPose (cs.Org.X, cs.Org.Y, cs.Org.Z, rx, ry, rz);
   }

   // A right-handed frame at 'org' whose Z axis is 'approachZ'.  The tool's X heading is
   // 'headingRef' projected onto the plane ⊥ Z, so the TCP faces consistently with the
   // user frame rather than an arbitrary direction (which can twist the wrist to a limit).
   static CoordSystem ApproachFrame (Point3 org, Vector3 approachZ, Vector3 headingRef) {
      var z = approachZ.Normalized ();
      var x = headingRef - z * headingRef.Dot (z);     // project heading onto the ⊥Z plane
      if (x.Length < 1e-6) {                            // heading parallel to Z → pick a fallback
         var alt = Math.Abs (z.X) > 0.9 ? Vector3.YAxis : Vector3.XAxis;
         x = alt - z * alt.Dot (z);
      }
      x = x.Normalized ();
      var y = (z * x).Normalized ();                    // z × x  ⟹  VecZ = x × y = z (approach)
      return new CoordSystem (org, x, y);
   }

   // Outward unit normal of the mesh triangle the hit point lies on (closest by
   // perpendicular distance among triangles whose face contains the projected point).
   static Vector3 NormalAt (Mesh3 mesh, Point3 hit) {
      var tris = mesh.Triangle; var v = mesh.Vertex;
      double best = double.MaxValue; Vector3 bestN = Vector3.ZAxis;
      for (int i = 0; i < tris.Length; i += 3) {
         Point3 a = (Point3)v[tris[i]].Pos, b = (Point3)v[tris[i + 1]].Pos, c = (Point3)v[tris[i + 2]].Pos;
         var cross = (b - a) * (c - a);
         double len = cross.Length; if (len < 1e-9) continue;
         var n    = cross / len;
         double d = (hit - a).Dot (n);
         if (Math.Abs (d) < best && InTriangle (hit - n * d, a, b, c)) { best = Math.Abs (d); bestN = n; }
      }
      return bestN;
   }

   // Barycentric point-in-triangle test (with a small tolerance for edge hits).
   static bool InTriangle (Point3 p, Point3 a, Point3 b, Point3 c) {
      Vector3 v0 = c - a, v1 = b - a, v2 = p - a;
      double d00 = v0.Dot (v0), d01 = v0.Dot (v1), d02 = v0.Dot (v2), d11 = v1.Dot (v1), d12 = v1.Dot (v2);
      double den = d00 * d11 - d01 * d01;
      if (Math.Abs (den) < 1e-12) return false;
      double u = (d11 * d02 - d01 * d12) / den, w = (d00 * d12 - d01 * d02) / den;
      return u >= -1e-3 && w >= -1e-3 && u + w <= 1 + 1e-3;
   }

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

   // Pallet frame: built from 3 calibration points, persisted to mFramePath.
   CoordSystem mFrame;
   bool        mHasFrame;
   Point3      mFrameP1, mFrameP2, mFrameP3;
   readonly string mFramePath = Path.Combine (AppContext.BaseDirectory, "pallet_frame.txt");

   // Imported pallet geometry + taught pickup pose.
   readonly GroupVN mPalletGroup;
   Mesh3?           mPallet;
   Mesh3VN?         mPalletVN;
   CoordSystem      mPickupCS;
   bool             mHasPickup;
   EPickMode        mPickMode;
   enum EPickMode { None, Corner, Pickup }

   readonly List<(double X, double Y, double Z, double Rx, double Ry, double Rz)> mScript = [];
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

#region class FrameVN ------------------------------------------------------------------------------
// Draws the calibrated pallet frame's X/Y/Z triad (when active) so the operator can
// see where the taught pallet origin and orientation sit in the scene.
class FrameVN : VNode {
   public FrameVN () { Streaming = true; }
   public required RobotScene Scene { private get; init; }

   public override void SetAttributes () { Lux.ZLevel = 70; }

   public override void Draw () {
      if (Scene.FrameActive) {
         var cs = Scene.FrameCS;
         DrawArrow (cs.Org, cs.VecX, Color4.Red);
         DrawArrow (cs.Org, cs.VecY, Color4.Green);
         DrawArrow (cs.Org, cs.VecZ, Color4.Blue);
      }
      // Pickup marker: a magenta arrow pointing along the TCP approach direction.
      if (Scene.PickupCS is { } pk) DrawArrow (pk.Org, pk.VecZ, Color4.Magenta);
   }

   void DrawArrow (Point3 from, Vector3 dir, Color4 color) {
      Lux.Color = color; Lux.LineWidth = 3f;
      const double L = 250, HL = 36, HW = 16;
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
