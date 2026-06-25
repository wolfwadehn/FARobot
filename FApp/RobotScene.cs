// ╔═╦╗
// ║╬╠╬╦╗ RobotScene.cs
// ║╔╣╠║╣ 3D robot scene with FK/IK solver and box collision detection
// ╚╝╚╩╩╝ ──────────────────────────────────────────────────────────────────────────────────────────
using System.Text.Json;
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
         try {
            // Build link collision OBBs from the render Mesh3 (reliable); fall back to the
            // collision TopoMesh only if there is no Mesh3.
            if (m.Mesh is { } msh) mLinkOBBs[m] = OBBTree.From (msh);
            else if (m.CMesh is { } cm) mLinkOBBs[m] = OBBTree.From (cm);
         } catch (Exception) { /* skip degenerate meshes OBBTree cannot process */ }
      }

      mGripper     = new XfmVN (Matrix3.Identity, new GroupVN ([]));
      mTriGroup    = new GroupVN ([]);
      mGeomGroup   = new GroupVN ([]);
      mPartGroup   = new GroupVN ([]);

      // ── ViewModel wiring ──────────────────────────────────────────────────
      // Populate joints and subscribe events, then trigger the initial IK solve.
      ViewModel.Joints                = [.. mMech.EnumTree ()
                                               .Where  (m => m.Joint != EJoint.None)
                                               .Select (m => new JointSliderModel (m, OnFK))];
      ViewModel.IKChanged           += ComputeIK;
      ViewModel.BoxChanged          += UpdateBox;
      ViewModel.HomeRequested       += GoHome;
      ViewModel.LoadScriptRequested += LoadScript;
      ViewModel.AddRequested        += AddWaypoint;
      ViewModel.PlayRequested       += TogglePlay;
      ViewModel.WaypointScrubbed    += ApplyWaypointPos;
      ViewModel.SelectedObjectChanged += OnSelectObject;
      ViewModel.ObjMoved              += OnObjMoved;
      ViewModel.FrameEdited          += OnFrameEdited;
      GoHome ();

      mPlayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds (40) };
      mPlayTimer.Tick += (_, _) => TickScript ();

      Lib.Tracer = TraceVN.Print;
      BgrdColor  = Color4.Gray (64);
      Bound      = new Bound3 (-1200, -1200, 0, 1200, 1200, 1500);
      Root       = new GroupVN ([
         new MechanismVN (mMech), mGripper, mBoxXfm, mTriGroup, mGeomGroup, mPartGroup,
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

   // The selected object (null if none), and the user frames to draw (one per object that
   // has a frame defined).
   SceneObject? Sel => ViewModel.SelectedObject >= 0 && ViewModel.SelectedObject < mObjects.Count
                       ? mObjects[ViewModel.SelectedObject] : null;
   public IEnumerable<CoordSystem> ObjectFrames => mObjects.Where (o => o.HasFrame).Select (o => o.Frame);
   // The selected object's 3 calibration points (null when none) — prefills the dialog.
   public (Point3 P1, Point3 P2, Point3 P3)? FramePoints
      => Sel is { HasFrame: true, CP1: { } p1, CP2: { } p2, CP3: { } p3 } ? (p1, p2, p3) : null;

   // The taught pickup pose in world coords (null when not set) — drawn by FrameVN.
   public CoordSystem? PickupCS => mHasPickup ? PickupPose () : null;

   // Implementation -----------------------------------------------------------
   public override void Detached () => mPlayTimer.IsEnabled = false;

   public override void Picked (object obj) {
      // Clicks on imported geometry drive the teach modes: the frame corner and the place
      // point are taught on an imported object; the pickup may be on the part or an object.
      var hit     = Lux.PickPos;
      var hitObj  = mObjects.FirstOrDefault (o => o.Mesh == obj);
      bool onObj  = hitObj != null;
      bool onPart = mPartMesh != null && obj == mPartMesh;
      if (mPickMode == EPickMode.Corner && onObj) {
         mPickMode = EPickMode.None; SetFrameFromCorner (hit, hitObj!); return;
      }
      if (mPickMode == EPickMode.Pickup && (onPart || onObj)) {
         mPickMode = EPickMode.None; SetPickup (hit, onPart, hitObj); return;
      }
      if (mPickMode == EPickMode.Place && onObj) {
         mPickMode = EPickMode.None; SetPlace (hit, hitObj); return;
      }
      if (onObj || onPart) return;   // geometry clicked outside a teach mode
      if (obj == mBoxMesh) { SnapToFace (hit); return; }
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
      SolveWorld (CurrentWorldPose ());
      mGripper.Xfm = mTip.Xfm;
      UpdateAttachedPart ();
      CheckCollisions ();
   }

   // Solves IK for a world TCP pose and applies the best solution to the joints.  Returns
   // false (joints unchanged) if the pose is unreachable.  Used by ComputeIK and the planner.
   //   - Subtract LJointZ (solver works in the L-joint frame, not world Z).
   //   - Back-solve wrist = TCP − R × TCP_offset and feed the analytic solver.
   //   - Pick the solution that avoids joint limits/singularity and is nearest current joints.
   bool SolveWorld (CoordSystem world) {
      var pose  = world + new Vector3 (0, 0, -LJointZ);
      var wrist = pose.Org - pose.VecX * mTcpOffset.X - pose.VecY * mTcpOffset.Y - pose.VecZ * mTcpOffset.Z;
      mCS = new CoordSystem (wrist, pose.VecX, pose.VecY);
      mSolver.ComputeStances (mCS.Org, mCS.VecZ, mCS.VecX);
      int best = -1; double bestCost = double.MaxValue;
      for (int j = 0; j < 8; j++) {
         if (!mSolver.Solutions[j].OK) continue;
         double cost = SolutionCost (j);
         if (cost < bestCost) { bestCost = cost; best = j; }
      }
      if (best < 0) return false;
      var sol = mSolver.Solutions[best];
      for (int i = 0; i < 6; i++) mJoints[i].JValue = sol.GetJointAngle (i);
      return true;
   }

   // Cost of an IK solution = limit/singularity penalty (dominant) + travel from the
   // current configuration.  Lower is better.
   double SolutionCost (int j) {
      var sol = mSolver.Solutions[j];
      double penalty = 0, travel = 0;
      for (int i = 0; i < 6; i++) {
         double a = sol.GetJointAngle (i);
         double d = a - mJoints[i].JValue; travel += d * d;
         // Quadratic ramp once a joint comes within Margin° of either limit.
         double room = Math.Min (a - mMin[i], mMax[i] - a);
         if (room < Margin) penalty += (Margin - room) * (Margin - room);
      }
      // Wrist singularity: the B axis (index 4) passing through 0° aligns R and T.
      double b = Math.Abs (sol.GetJointAngle (4));
      if (b < Margin) penalty += (Margin - b) * (Margin - b);
      return penalty * 1e4 + travel;
   }

   // Degrees of clearance from a joint limit (or from the wrist singularity) within
   // which the IK selector starts penalising a solution.
   const double Margin = 20;

   // The current TCP pose in WORLD coords from the ViewModel IK values (orientation first,
   // X-then-Y-then-Z Euler, then position).  IK is in world coordinates.
   CoordSystem CurrentWorldPose () {
      var pose = CoordSystem.World;
      pose    *= Matrix3.Rotation (EAxis.X, ViewModel.Rx.D2R ());
      pose    *= Matrix3.Rotation (EAxis.Y, ViewModel.Ry.D2R ());
      pose    *= Matrix3.Rotation (EAxis.Z, ViewModel.Rz.D2R ());
      pose    += new Vector3 (ViewModel.X, ViewModel.Y, ViewModel.Z);
      return pose;
   }

   void OnFK () {
      var tipCs = CoordSystem.World * mTip.Xfm;
      var off   = mTcpOffset;
      var tcp   = tipCs.Org + tipCs.VecX * off.X + tipCs.VecY * off.Y + tipCs.VecZ * off.Z;
      // Lift from the solver's L-joint frame back to world for the IK display.
      var world = new CoordSystem (tcp + new Vector3 (0, 0, LJointZ), tipCs.VecX, tipCs.VecY);
      var (rx, ry, rz) = MatrixToEuler (world);
      ViewModel.SetIKDisplay (world.Org.X, world.Org.Y, world.Org.Z, rx, ry, rz);
      foreach (var js in ViewModel.Joints) js.Refresh ();
      mGripper.Xfm = mTip.Xfm;
      UpdateAttachedPart ();
      CheckCollisions ();
   }

   // Extracts XYZ Euler angles (degrees) from a coordinate system — the exact inverse of
   // cs = Rot(X,Rx) * Rot(Y,Ry) * Rot(Z,Rz) (this lib's row-vector rotations), so it is
   // correct for COMBINED rotations, not just a single axis.  For that build:
   //   VecX = ( cosRy·cosRz,  cosRy·sinRz, −sinRy )
   //   VecY.Z = sinRx·cosRy,   VecZ.Z = cosRx·cosRy
   static (double Rx, double Ry, double Rz) MatrixToEuler (CoordSystem cs) {
      double ry = Math.Asin (Math.Clamp (-cs.VecX.Z, -1, 1));
      double rx, rz;
      if (Math.Abs (cs.VecX.Z) < 1 - 1e-6) {
         rz = Math.Atan2 (cs.VecX.Y, cs.VecX.X);
         rx = Math.Atan2 (cs.VecY.Z, cs.VecZ.Z);
      } else {                       // gimbal lock (Ry = ±90°): fix Rz = 0
         rz = 0;
         rx = Math.Atan2 (cs.VecY.X, cs.VecY.Y);
      }
      const double R2D = 180 / Math.PI;
      return (rx * R2D, ry * R2D, rz * R2D);
   }

   Matrix3 BoxWorldXfm => Matrix3.Translation (ViewModel.BX, ViewModel.BY, ViewModel.BZ);

   void UpdateBox () {
      mBoxXfm.Xfm = BoxWorldXfm;
      CheckCollisions ();
   }

   // Runs on every robot move and object move.  Robot collision is always active.  The part
   // participates too: while resting it is an obstacle for the robot; once picked up it is
   // carried geometry checked against the box and every imported object.
   void CheckCollisions () {
      var boxOBBW = mBoxOBB.With (BoxWorldXfm);
      using var bc = OBBCollider.Borrow ();

      bool boxHit = false, partHit = false;
      Dictionary<string, bool> groupHit = [];
      foreach (var tri in mTris) groupHit.TryAdd (tri.Group, false);
      bool[] objHit = new bool[mObjects.Count];

      // Robot links vs the obstacle box, collision triangles, and imported objects.
      // (The part is NOT an obstacle here — it only collides once attached, below.)
      foreach (var (m, linkOBB) in mLinkOBBs) {
         var wLink    = linkOBB.With (m.Xfm);
         bool linkHit = bc.Check (wLink, boxOBBW);
         if (linkHit) boxHit = true;
         foreach (var tri in mTris)
            if (bc.Check (wLink, tri.OBB.With (Matrix3.Identity))) { linkHit = true; groupHit[tri.Group] = true; }
         for (int k = 0; k < mObjects.Count; k++)
            if (mObjects[k].OBB is { } oobb && bc.Check (wLink, oobb.With (mObjects[k].Xfm))) {
               linkHit = true; objHit[k] = true;
            }
         m.IsColliding = linkHit;
      }

      // The carried part (only once picked up) vs the box and every imported object.  Its
      // collision OBB is eroded slightly (see BuildPartOBB) so resting/contact with a pallet
      // surface doesn't false-trigger.
      if (mHasPart && mPartAttached && mPartOBB != null && mPartXfmVN != null) {
         var partW = mPartOBB.With (mPartXfmVN.Xfm);
         if (bc.Check (partW, boxOBBW)) { partHit = boxHit = true; }
         for (int k = 0; k < mObjects.Count; k++)
            if (mObjects[k].OBB is { } oobb && bc.Check (partW, oobb.With (mObjects[k].Xfm))) {
               partHit = true; objHit[k] = true;
            }
      }

      mBoxVN.Color = boxHit ? Color4.Red : Color4.Blue;
      foreach (var tri in mTris) tri.IsColliding = groupHit[tri.Group];
      bool objAny = false;
      for (int k = 0; k < mObjects.Count; k++) {
         var idle = k == ViewModel.SelectedObject ? SelObjColor : mObjects[k].Idle;
         mObjects[k].VN.Color = objHit[k] ? Color4.Red : (mArmed ? Color4.Cyan : idle);
         objAny |= objHit[k];
      }
      if (mHasPart && mPartVN != null) mPartVN.Color = partHit ? Color4.Red : PartIdle;
      InCollision = boxHit || partHit || objAny || mLinkOBBs.Keys.Any (m => m.IsColliding);
   }

   // True while any collision (robot link, part, object, box) is active — shown on-screen.
   public bool InCollision { get; private set; }

   // Highlight colour for the currently selected (movable) object.
   static readonly Color4 SelObjColor = new (90, 170, 230);

   // Side-effect-free collision query used by the planner (no colour changes): robot links
   // vs box, triangles and imported objects; plus the carried part (positioned at the
   // planning grasp on the flange) vs box and objects.
   bool HasCollision (bool carrying) {
      using var bc = OBBCollider.Borrow ();
      var boxW = mBoxOBB.With (BoxWorldXfm);
      foreach (var (m, linkOBB) in mLinkOBBs) {
         var wLink = linkOBB.With (m.Xfm);
         if (bc.Check (wLink, boxW)) return true;
         foreach (var tri in mTris) if (bc.Check (wLink, tri.OBB.With (Matrix3.Identity))) return true;
         for (int k = 0; k < mObjects.Count; k++)
            if (mObjects[k].OBB is { } o && bc.Check (wLink, o.With (mObjects[k].Xfm))) return true;
      }
      if (carrying && mPartOBB != null) {
         var pw = mPartOBB.With (mPlanGraspRel * mTip.Xfm);
         if (bc.Check (pw, boxW)) return true;
         for (int k = 0; k < mObjects.Count; k++)
            if (mObjects[k].OBB is { } o && bc.Check (pw, o.With (mObjects[k].Xfm))) return true;
      }
      return false;
   }
   Matrix3 mPlanGraspRel = Matrix3.Identity;   // part pose relative to the flange while carried

   // Records the robot's current pose as a new Move waypoint (the action can be changed
   // afterward from the waypoint list).
   internal void AddWaypoint () {
      mScript.Add ((ViewModel.X, ViewModel.Y, ViewModel.Z, ViewModel.Rx, ViewModel.Ry, ViewModel.Rz, EAction.Move));
      SaveScript (); AfterScriptChanged ();
      Lib.Trace ($"Added waypoint {mScript.Count}");
   }

   // Cycles a waypoint's action Move → Pick → Place → Move (called from the list row).
   internal void CycleAction (int i) {
      if (i < 0 || i >= mScript.Count) return;
      var w = mScript[i];
      w.A = w.A switch { EAction.Move => EAction.Pick, EAction.Pick => EAction.Place, _ => EAction.Move };
      mScript[i] = w;
      SaveScript (); RefreshWaypointList ();
   }

   internal void RemoveWaypoint (int i) {
      if (i < 0 || i >= mScript.Count) return;
      mScript.RemoveAt (i);
      SaveScript (); AfterScriptChanged ();
   }

   // Scrubs the robot to a waypoint (slider + IK follow).
   internal void GoToWaypoint (int i) { if (i >= 0 && i < mScript.Count) ViewModel.WaypointPos = i; }

   void LoadScript (string path) {
      mScript.Clear ();
      try {
         var ic = System.Globalization.CultureInfo.InvariantCulture;
         foreach (var line in File.ReadLines (path)) {
            var t = line.Trim ();
            if (t.Length == 0 || t[0] == '#') continue;
            var p = t.Split ((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length < 6) continue;
            var act = p.Length >= 7 ? p[6].ToUpperInvariant () switch {
               "PICK" => EAction.Pick, "PLACE" => EAction.Place, _ => EAction.Move } : EAction.Move;
            mScript.Add ((double.Parse (p[0], ic), double.Parse (p[1], ic), double.Parse (p[2], ic),
                          double.Parse (p[3], ic), double.Parse (p[4], ic), double.Parse (p[5], ic), act));
         }
         Lib.Trace ($"Loaded {mScript.Count} waypoints");
      } catch (Exception ex) { Lib.Trace ($"Load failed: {ex.Message}"); }
      AfterScriptChanged ();
   }

   // Writes the current waypoint list (with PICK/PLACE tags) back to the script file.
   void SaveScript () {
      try {
         var ic = System.Globalization.CultureInfo.InvariantCulture;
         var sb = new StringBuilder ();
         sb.AppendLine ("# Waypoints (frame-relative). Actions: PICK / PLACE.");
         foreach (var w in mScript) {
            var tag = w.A switch { EAction.Pick => " PICK", EAction.Place => " PLACE", _ => "" };
            sb.AppendLine (string.Format (ic, "{0:F1} {1:F1} {2:F1} {3:F1} {4:F1} {5:F1}",
                                          w.X, w.Y, w.Z, w.Rx, w.Ry, w.Rz) + tag);
         }
         File.WriteAllText (ViewModel.ScriptPath, sb.ToString ());
      } catch (Exception ex) { Lib.Trace ($"Save failed: {ex.Message}"); }
   }

   // After any change to the waypoint set: refresh the slider range, the list, and reset.
   void AfterScriptChanged () {
      ViewModel.WaypointMax = Math.Max (0, mScript.Count - 1);
      ViewModel.WaypointPos = 0;
      mFiredUpto = -1;
      ResetPart ();
      RefreshWaypointList ();
      ApplyWaypointPos ();
   }

   // Rebuilds ViewModel.Waypoints so the sidebar list mirrors mScript.
   void RefreshWaypointList () {
      ViewModel.Waypoints.Clear ();
      for (int i = 0; i < mScript.Count; i++) {
         int idx = i;
         var (label, brush) = mScript[i].A switch {
            EAction.Pick  => ("Pick",  sPickBrush),
            EAction.Place => ("Place", sPlaceBrush),
            _             => ("Move",  sMoveBrush) };
         ViewModel.Waypoints.Add (new WaypointVM (i + 1, label, brush,
            () => GoToWaypoint (idx), () => CycleAction (idx), () => RemoveWaypoint (idx)));
      }
   }

   static readonly System.Windows.Media.Brush
      sMoveBrush  = new System.Windows.Media.SolidColorBrush (System.Windows.Media.Colors.Silver),
      sPickBrush  = new System.Windows.Media.SolidColorBrush (System.Windows.Media.Colors.LimeGreen),
      sPlaceBrush = new System.Windows.Media.SolidColorBrush (System.Windows.Media.Colors.Orange);

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
         ResetPart ();                       // drop the part so the cycle picks it fresh
         mFiredUpto            = 0;           // waypoint 0 is the start; don't re-fire it
         ViewModel.WaypointPos = 0;          // start of cycle
         mPlayTimer.IsEnabled  = true;
         ViewModel.PlayLabel   = "Stop";
      }
   }

   // One animation tick: advance the scrubber, firing each waypoint's Pick/Place action as
   // the robot arrives at it; stop after a single forward cycle.
   void TickScript () {
      double max  = Math.Max (0, mScript.Count - 1);
      double next = Math.Min (ViewModel.WaypointPos + PlayStep, max);
      ViewModel.WaypointPos = next;          // setter fires WaypointScrubbed → ApplyWaypointPos
      FireActionsUpto ((int)Math.Floor (next + 1e-6));
      if (next >= max) { mPlayTimer.IsEnabled = false; ViewModel.PlayLabel = "Play"; }
   }

   // Runs the Pick/Place action of every waypoint the robot has now reached but not yet
   // executed (actions fire on arrival = "completion" of that waypoint).
   void FireActionsUpto (int arrived) {
      for (int i = mFiredUpto + 1; i <= arrived && i < mScript.Count; i++) {
         switch (mScript[i].A) {
            case EAction.Pick:  AttachPart (); break;
            case EAction.Place: PlacePart ();  break;
         }
      }
      if (arrived > mFiredUpto) mFiredUpto = arrived;
   }

   // Scrub increment per timer tick.  At a 40 ms interval, 0.04 ≈ one segment per second.
   const double PlayStep = 0.04;

   internal void GoHome () { ResetPart (); SetIKDisplayFromWorld (mHome); }

   // Captures the robot's current TCP pose as the new home, then re-solves the pickup
   // path (if a pickup is set) so the generated waypoints start from the new home.
   internal void SetHome () {
      mHome = CurrentWorldPose ();
      Lib.Trace ($"Home set at ({mHome.Org.X:F0}, {mHome.Org.Y:F0}, {mHome.Org.Z:F0})");
      if (mHasPickup) GenerateWaypoints ();
   }

   // ── Per-object user frame ──────────────────────────────────────────────────
   // Selection changed: push the selected object's placement + frame into the panel.
   void OnSelectObject () {
      if (Sel is { } o) {
         ViewModel.SetObjMove  (o.X, o.Y, o.Z, o.Rx, o.Ry, o.Rz);
         ViewModel.SetObjFrame (o.FX, o.FY, o.FZ, o.FRx, o.FRy, o.FRz);
      }
      CheckCollisions ();   // recolours so the selected object is highlighted
      Lux.Redraw ();
   }

   // Panel move fields edited: apply to the selected object.  If the object is pinned to
   // its user frame, the frame rides along with it.
   void OnObjMoved () {
      if (Sel is not { } o) return;
      (o.X, o.Y, o.Z, o.Rx, o.Ry, o.Rz) =
         (ViewModel.ObjX, ViewModel.ObjY, ViewModel.ObjZ, ViewModel.ObjRx, ViewModel.ObjRy, ViewModel.ObjRz);
      o.ApplyPlacement ();
      if (o.FramePinned) {                                  // frame follows the object
         SetFrameFromMatrix (o, o.FrameRel * o.Xfm);
         ViewModel.SetObjFrame (o.FX, o.FY, o.FZ, o.FRx, o.FRy, o.FRz);
      }
      UpdatePartRest ();                                    // sheet rides the pallet
      CheckCollisions ();
      Lux.Redraw ();
   }

   // Panel frame fields edited: apply the 6 parameters to the selected object's frame.  If
   // the object is pinned to the frame, the object (pallet) moves rigidly with the frame —
   // translation AND rotation.  The first edit (before any calibration) just pins the
   // current relationship without moving the object.
   void OnFrameEdited () {
      if (Sel is not { } o) return;
      (o.FX, o.FY, o.FZ, o.FRx, o.FRy, o.FRz) =
         (ViewModel.FrX, ViewModel.FrY, ViewModel.FrZ, ViewModel.FrRx, ViewModel.FrRy, ViewModel.FrRz);
      o.HasFrame = true;
      if (o.FramePinned) {                                  // object follows the frame
         SetPlacementFromMatrix (o, o.FrameRel.GetInverse () * o.FrameXfm);
         ViewModel.SetObjMove (o.X, o.Y, o.Z, o.Rx, o.Ry, o.Rz);
         CheckCollisions ();
      } else o.PinFrame ();                                 // establish the rigid link
      UpdatePartRest ();                                    // sheet rides the pallet
      Lux.Redraw ();
   }

   // Applies a 3-point calibration to the selected object's frame, pins the object to it,
   // and reflects the computed 6 parameters back into the panel.
   internal void SetPalletFrame (Point3 origin, Point3 xptr, Point3 plane) {
      if (Sel is not { } o) { Lib.Trace ("Select an object first"); return; }
      var cs = BuildFrame (origin, xptr, plane);
      var (rx, ry, rz) = MatrixToEuler (cs);
      (o.FX, o.FY, o.FZ, o.FRx, o.FRy, o.FRz) = (cs.Org.X, cs.Org.Y, cs.Org.Z, rx, ry, rz);
      (o.CP1, o.CP2, o.CP3) = (origin, xptr, plane);
      o.HasFrame = true;
      o.PinFrame ();                                        // lock the frame to the pallet
      ViewModel.SetObjFrame (o.FX, o.FY, o.FZ, o.FRx, o.FRy, o.FRz);
      UpdatePartRest ();                                    // sheet rides the pallet
      Lux.Redraw ();
   }

   internal void ClearPalletFrame () {
      if (Sel is not { } o) return;
      o.HasFrame = o.FramePinned = false;
      (o.CP1, o.CP2, o.CP3) = (null, null, null);
      ViewModel.SetObjFrame (0, 0, 0, 0, 0, 0);
      Lux.Redraw ();
   }

   // Decompose a local→world matrix into an object's 6-DOF placement and apply it.
   static void SetPlacementFromMatrix (SceneObject o, Matrix3 m) {
      var cs = m.ToCS ();
      var (rx, ry, rz) = MatrixToEuler (cs);
      (o.X, o.Y, o.Z, o.Rx, o.Ry, o.Rz) = (cs.Org.X, cs.Org.Y, cs.Org.Z, rx, ry, rz);
      o.ApplyPlacement ();
   }

   // Decompose a frame-local→world matrix into an object's 6 frame parameters.
   static void SetFrameFromMatrix (SceneObject o, Matrix3 mf) {
      var cs = mf.ToCS ();
      var (rx, ry, rz) = MatrixToEuler (cs);
      (o.FX, o.FY, o.FZ, o.FRx, o.FRy, o.FRz) = (cs.Org.X, cs.Org.Y, cs.Org.Z, rx, ry, rz);
   }

   // P1=origin, P2 sets +X, P3 lies on the +XY side.  Z = X×(P3−P1) (right-hand rule),
   // Y = Z×X — so the basis is orthonormal regardless of P3's exact position.
   static CoordSystem BuildFrame (Point3 origin, Point3 xptr, Point3 plane) {
      var vx = xptr - origin;
      var vz = vx * (plane - origin);   // cross product
      var vy = vz * vx;                 // ⊥ vx, points toward the P3 side
      return new CoordSystem (origin, vx, vy);
   }

   // ── Pallet geometry + pickup teach ────────────────────────────────────────
   // Loads an STL or OBJ mesh and shows it in the scene.  The mesh is rendered in
   // its file coordinates (assumed robot-world mm) and becomes pickable.
   // Loads an STL or OBJ mesh from disk (chosen by extension).
   static Mesh3 LoadMesh (string path) {
      var ext = Path.GetExtension (path).ToLowerInvariant ();
      return ext == ".obj" ? Mesh3.LoadObj (path) : new STLReader (path).BuildMesh ();
   }

   // Imports an STL/OBJ geometry as a new scene object (appends — multiple are allowed).
   // Each object is a pickable surface (frame/pickup/place) and a collision obstacle.
   internal void ImportGeometry (string path) {
      try {
         var obj = new SceneObject (path, LoadMesh (path));
         if (obj.OBB == null) Lib.Trace ($"Warning: '{obj.Name}' has no collision mesh (won't collide)");
         mObjects.Add (obj);
         mGeomGroup.Add (obj.Node);
         ViewModel.Objects.Add (new ObjectItemVM (obj.Name));
         ViewModel.SelectedObject = mObjects.Count - 1;   // select the new object
         ViewModel.PalletStatus = $"{mObjects.Count} object(s) — last: {obj.Name}";
         Lux.UIScene?.ZoomExtents ();
         CheckCollisions ();
         Lib.Trace ($"Imported '{obj.Name}' ({obj.Mesh.Triangle.Length / 3} tris)");
      } catch (Exception ex) {
         ViewModel.PalletStatus = "(load failed)";
         Lib.Trace ($"Geometry import failed: {ex.Message}");
      }
   }

   // ── Sheet-metal part: place on the pallet, pick at a Pick waypoint, ride the TCP ──
   // Imports a part mesh and places it on the pallet at the calibrated frame (the part's
   // own origin lands at the frame origin, axes aligned to the frame).  mPartHomeXfm is the
   // rest pose it returns to on reset.
   internal void ImportPart (string path) {
      try {
         var mesh = LoadMesh (path);
         if (mPartXfmVN != null) mPartGroup.Remove (mPartXfmVN);
         mPartPath     = path;
         mPartMesh     = mesh;
         mPartOBB      = BuildPartOBB (mesh);
         mPartAttached = false;
         // Place the part at the selected object's user frame if it has one, else at origin.
         // Record the host so the part rides the pallet when its frame moves/tilts.
         bool onFrame  = Sel is { HasFrame: true };
         mPartHost     = onFrame ? Sel : null;
         mPartHomeXfm  = onFrame ? Matrix3.To (Sel!.Frame) : Matrix3.Identity;
         mPartFrameRel = onFrame ? mPartHomeXfm * Sel!.FrameXfm.GetInverse () : Matrix3.Identity;
         mPartVN       = new Mesh3VN (mesh) { Mode = EShadeMode.Phong, Color = PartIdle };
         mPartXfmVN    = new XfmVN (mPartHomeXfm, mPartVN);
         mPartGroup.Add (mPartXfmVN);
         mHasPart      = true;
         ViewModel.PartStatus = onFrame ? $"{Path.GetFileName (path)} — on {Sel!.Name} frame"
                                        : $"{Path.GetFileName (path)} — at origin";
         Lux.UIScene?.ZoomExtents ();
         CheckCollisions ();
         Lib.Trace ($"Loaded part '{Path.GetFileName (path)}'");
      } catch (Exception ex) {
         ViewModel.PartStatus = "(load failed)";
         Lib.Trace ($"Part import failed: {ex.Message}");
      }
   }

   // Builds the part's collision OBB from a slightly eroded copy of its mesh (~3 mm inset;
   // a thin axis collapses to a centre sheet).  This gives a small clearance so the part
   // resting on / touching a pallet surface doesn't false-trigger a collision.
   static OBBTree? BuildPartOBB (Mesh3 mesh) {
      try {
         var b = mesh.Bound; var c = b.Midpoint; const double clr = 3;
         double S (double half) => Math.Max (0.05, (half - clr) / Math.Max (half, 1e-6));
         var m = Matrix3.Translation (-c.X, -c.Y, -c.Z)
               * Matrix3.Scaling (S (b.Width / 2), S (b.Height / 2), S (b.Depth / 2))
               * Matrix3.Translation (c.X, c.Y, c.Z);
         return OBBTree.From (mesh * m);
      } catch { return null; }
   }

   // Keeps the resting part on its host pallet: when the pallet's frame moves or tilts, the
   // part (sheet) rides along, staying seated on the (possibly tilted) surface.
   void UpdatePartRest () {
      if (!mHasPart || mPartAttached || mPartHost == null || mPartXfmVN == null) return;
      mPartHomeXfm   = mPartFrameRel * mPartHost.FrameXfm;
      mPartXfmVN.Xfm = mPartHomeXfm;
   }

   // Pick action: rigidly attach the part to the flange from wherever it currently rests,
   // capturing the relative transform so it follows without jumping.
   void AttachPart () {
      if (!mHasPart || mPartXfmVN == null || mPartAttached) return;
      mPartRelXfm    = mPartXfmVN.Xfm * mTip.Xfm.GetInverse ();
      mPartAttached  = true;
      mPartXfmVN.Xfm = mPartRelXfm * mTip.Xfm;
      CheckCollisions ();
      Lib.Trace ("Part picked up");
   }

   // Place action: release the part, leaving it at its current (just-reached) pose.
   void PlacePart () {
      if (!mHasPart || !mPartAttached) return;
      mPartAttached = false;
      CheckCollisions ();
      Lib.Trace ("Part placed");
   }

   // Called after every robot pose change: while held, keep the part fixed to the flange.
   void UpdateAttachedPart () {
      if (mHasPart && mPartAttached && mPartXfmVN != null) mPartXfmVN.Xfm = mPartRelXfm * mTip.Xfm;
   }

   // Drops the part back to its rest pose on the pallet (on Home / play restart / load).
   void ResetPart () {
      if (!mHasPart || mPartXfmVN == null) return;
      mPartAttached  = false;
      mPartXfmVN.Xfm = mPartHomeXfm;
   }

   // Arm the next click to set the work frame at a corner of an imported object.
   internal void BeginPickCorner () {
      if (mObjects.Count == 0) { Lib.Trace ("Import a geometry first"); return; }
      mPickMode = EPickMode.Corner;
      ArmHighlight (true);
      Lib.Trace ("Click near a geometry corner to set the frame (50×50 mm inward)");
   }

   // Arm the next click to fix the pickup surface (on the part or any imported object).
   internal void BeginPickPickup () {
      if (mObjects.Count == 0) { Lib.Trace ("Import a geometry first"); return; }
      mPickMode = EPickMode.Pickup;
      ArmHighlight (true);
      Lib.Trace ("Click the part (or a geometry) surface to set the pickup position");
   }

   // Arm the next click to ADD a place destination (the chain picks from the previous drop
   // and carries to the next).  Each click appends a destination.
   internal void BeginPickPlace () {
      if (mObjects.Count == 0) { Lib.Trace ("Import a geometry first"); return; }
      mPickMode = EPickMode.Place;
      ArmHighlight (true);
      Lib.Trace ("Click a geometry surface to add a place destination");
   }

   // Records the place position (where the part is set down) and previews it.  Coordinates
   // are reported in the destination object's frame (or world if it has none).
   void SetPlace (Point3 hit, SceneObject? obj) {
      ArmHighlight (false);
      var rot = (obj?.Xfm ?? Matrix3.Identity).ExtractRotation ();    // destination surface tilt
      mPlaces.Add ((hit, rot));
      ViewModel.PlaceStatus = $"Place: {mPlaces.Count} dest — last " + InFrame (hit, obj);
      SetIKDisplayFromWorld (TiltedPose (hit, rot));
   }

   // Clears the taught place destinations (start a fresh transfer chain).
   internal void ClearPlaces () {
      mPlaces.Clear ();
      ViewModel.PlaceStatus = "Place: (none)";
      Lib.Trace ("Place destinations cleared");
   }

   // Formats a world point relative to an object's user frame (or world if it has none).
   static string InFrame (Point3 world, SceneObject? o) {
      if (o is { HasFrame: true }) {
         var p = world * Matrix3.From (o.Frame);
         return $"{o.Name} ({p.X:F0}, {p.Y:F0}, {p.Z:F0})";
      }
      return $"world ({world.X:F0}, {world.Y:F0}, {world.Z:F0})";
   }

   // Tints every imported object while a pick mode is armed, so they read as "click me".
   void ArmHighlight (bool on) {
      mArmed = on;
      foreach (var o in mObjects) o.VN.Color = on ? Color4.Cyan : o.Idle;
      Lux.Redraw ();
   }

   // Builds the work frame from the bounding-box corner of the clicked object nearest the
   // clicked point, offset 50 mm inward along X and Y.  Axes stay world-aligned (X+, Y+,
   // Z up); the origin Z is the clicked surface height.
   void SetFrameFromCorner (Point3 hit, SceneObject obj) {
      ArmHighlight (false);
      var b        = obj.Mesh.GetBound (obj.Xfm);   // world-space bounding box of the object
      bool nearMinX = Math.Abs (hit.X - b.X.Min) <= Math.Abs (hit.X - b.X.Max);
      bool nearMinY = Math.Abs (hit.Y - b.Y.Min) <= Math.Abs (hit.Y - b.Y.Max);
      double cx = nearMinX ? b.X.Min : b.X.Max, sx = nearMinX ? 1 : -1;
      double cy = nearMinY ? b.Y.Min : b.Y.Max, sy = nearMinY ? 1 : -1;
      var origin = new Point3 (cx + sx * 50, cy + sy * 50, hit.Z);
      ViewModel.SelectedObject = mObjects.IndexOf (obj);   // calibrate the clicked object
      // Reuse the 3-point builder: +X and +XY helper points along world axes.
      SetPalletFrame (origin, origin + new Vector3 (100, 0, 0), origin + new Vector3 (0, 100, 0));
   }

   // Fixes the pickup at the clicked surface point.  Only the POSITION comes from the
   // surface; the orientation is taken from the current home pose (see PickupPose), so the
   // robot translates to the pickup without reorienting the wrist.  Highlights the face
   // and previews the move.
   void SetPickup (Point3 hit, bool onPart, SceneObject? obj) {
      ArmHighlight (false);
      mPickupPt  = hit;
      mHasPickup = true;
      // Capture the clicked surface's tilt so the TCP approaches perpendicular to it
      // (a flat pallet → identity → home orientation, as before).
      var xfm = onPart ? mPartHomeXfm : obj?.Xfm ?? Matrix3.Identity;
      mPickupRot = xfm.ExtractRotation ();
      var msh = onPart ? mPartMesh : obj?.Mesh;       // highlight the clicked triangle
      if (msh != null && FindFace (msh, hit * xfm.GetInverse ()) is { } f)
         HighlightWorldFace (f.A * xfm, f.B * xfm, f.C * xfm);
      ViewModel.PickupStatus = "Pick: " + InFrame (hit, onPart ? null : obj);
      SetIKDisplayFromWorld (PickupPose ());          // move robot to the pickup as a preview
   }

   // The pickup pose in WORLD coords: the surface point with the home orientation TILTED by
   // the pallet's tilt, so the tool stays perpendicular to the (possibly tilted) surface.
   CoordSystem PickupPose () => TiltedPose (mPickupPt, mPickupRot);

   // Home orientation rotated by a surface tilt, located at a world point.
   CoordSystem TiltedPose (Point3 pt, Matrix3 tilt) => new (pt, mHome.VecX * tilt, mHome.VecY * tilt);
   // The surface "up" (world +Z) rotated by a tilt — the approach/retract direction.
   static Vector3 TiltedUp (Matrix3 tilt) => Vector3.ZAxis * tilt;

   // Draws the selected pickup triangle (given in world coords) as a bright overlay,
   // offset 1 mm along its normal to avoid z-fighting with the surface.
   void HighlightWorldFace (Point3 a, Point3 b, Point3 c) {
      if (mPickupHiliteVN != null) mGeomGroup.Remove (mPickupHiliteVN);
      var o    = ((b - a) * (c - a)).Normalized () * 1.0;
      var mesh = new Mesh3Builder ([a + o, b + o, c + o]).Build ();
      mPickupHiliteVN = new Mesh3VN (mesh) { Mode = EShadeMode.Glass, Color = new Color4 (255, 140, 0) };
      mGeomGroup.Add (mPickupHiliteVN);
      Lux.Redraw ();
   }

   // Idle colour of the carried part (turns red on collision).
   static readonly Color4 PartIdle = new (120, 180, 225);

   // Generates a full pick-and-place cycle in WORLD coords and writes it (with PICK/PLACE
   // action tags) to the script, then loads it.  With several place destinations the part is
   // picked, dropped at the first, re-picked and carried to the next, and so on:
   //   Home → pickup[PICK] → place₁[PLACE] (→[PICK]) → place₂[PLACE] (→[PICK]) → … → Home
   // Pickup/place poses are tilted with their pallet so the tool approaches perpendicular to a
   // tilted surface, with the approach/retract offset along that tilted normal.
   internal void GenerateWaypoints () {
      if (!mHasPickup) { Lib.Trace ("Set a pickup position first"); return; }
      const double clearance = 100;                  // mm off the surface, along its tilted normal
      var pickup   = PickupPose ();                  // surface point, tilted orientation
      var pickHi   = pickup + TiltedUp (mPickupRot) * clearance;

      var path = new List<(CoordSystem Pose, EAction A)> {
         (mHome, EAction.Move), (pickHi, EAction.Move), (pickup, EAction.Pick), (pickHi, EAction.Move),
      };
      for (int i = 0; i < mPlaces.Count; i++) {
         var (pt, rot) = mPlaces[i];
         var place   = TiltedPose (pt, rot);
         var placeHi = place + TiltedUp (rot) * clearance;
         path.Add ((placeHi, EAction.Move));
         path.Add ((place,   EAction.Place));        // set the part down here
         if (i < mPlaces.Count - 1) path.Add ((place, EAction.Pick));  // re-grab to carry onward
         path.Add ((placeHi, EAction.Move));
      }
      path.Add ((mHome, EAction.Move));

      try {
         var ic = System.Globalization.CultureInfo.InvariantCulture;
         var sb = new StringBuilder ();
         sb.AppendLine ("# Generated pick-and-place (world coords). Actions: PICK / PLACE.");
         foreach (var (pose, act) in path) {
            var (rx, ry, rz) = MatrixToEuler (pose);
            var tag          = act switch { EAction.Pick => " PICK", EAction.Place => " PLACE", _ => "" };
            sb.AppendLine (string.Format (ic, "{0:F1} {1:F1} {2:F1} {3:F1} {4:F1} {5:F1}",
                                          pose.Org.X, pose.Org.Y, pose.Org.Z, rx, ry, rz) + tag);
         }
         File.WriteAllText (ViewModel.ScriptPath, sb.ToString ());
         LoadScript (ViewModel.ScriptPath);
         Lib.Trace ($"Generated {path.Count} waypoints → {Path.GetFileName (ViewModel.ScriptPath)}");
      } catch (Exception ex) { Lib.Trace ($"Waypoint generation failed: {ex.Message}"); }
   }

   // Drives the robot to a world pose, expressed through the active frame so the IK
   // display fields stay consistent with what ComputeIK consumes.
   void SetIKDisplayFromWorld (CoordSystem world) {
      var (rx, ry, rz) = MatrixToEuler (world);
      ViewModel.SetIKPose (world.Org.X, world.Org.Y, world.Org.Z, rx, ry, rz);
   }

   // Builds a CoordSystem from a position + XYZ Euler angles (degrees).
   static CoordSystem PoseFrom (double x, double y, double z, double rx, double ry, double rz) {
      var cs = CoordSystem.World;
      cs *= Matrix3.Rotation (EAxis.X, rx.D2R ());
      cs *= Matrix3.Rotation (EAxis.Y, ry.D2R ());
      cs *= Matrix3.Rotation (EAxis.Z, rz.D2R ());
      cs += new Vector3 (x, y, z);
      return cs;
   }

   // A rotation-only matrix ↔ its XYZ Euler angles (degrees), for persisting surface tilts.
   static double[] RotEuler (Matrix3 rot) { var (rx, ry, rz) = MatrixToEuler (rot.ToCS ()); return [rx, ry, rz]; }
   static Matrix3 RotFromEuler (double rx, double ry, double rz) => Matrix3.To (PoseFrom (0, 0, 0, rx, ry, rz));

   // ── Cell save / load ───────────────────────────────────────────────────────
   // Serialises the whole cell (objects + placements + frames, part, pickup/place, home,
   // obstacle box, waypoints) to a JSON file that can be reopened later.
   internal void SaveCell (string file) {
      try {
         double[] PoseArr (CoordSystem cs) { var (rx, ry, rz) = MatrixToEuler (cs); return [cs.Org.X, cs.Org.Y, cs.Org.Z, rx, ry, rz]; }
         var dto = new CellDto { Home = PoseArr (mHome), Box = [ViewModel.BX, ViewModel.BY, ViewModel.BZ] };
         foreach (var o in mObjects)
            dto.Objects.Add (new ObjDto {
               Path = o.Path, Pose = [o.X, o.Y, o.Z, o.Rx, o.Ry, o.Rz],
               HasFrame = o.HasFrame, Frame = [o.FX, o.FY, o.FZ, o.FRx, o.FRy, o.FRz],
               Calib = o is { CP1: { } a, CP2: { } b, CP3: { } c }
                       ? [a.X, a.Y, a.Z, b.X, b.Y, b.Z, c.X, c.Y, c.Z] : null });
         if (mHasPart) dto.Part = new PartDto {
            Path = mPartPath, Place = PoseArr (mPartHomeXfm.ToCS ()),
            Host = mPartHost != null ? mObjects.IndexOf (mPartHost) : -1 };
         if (mHasPickup) dto.Pickup = [mPickupPt.X, mPickupPt.Y, mPickupPt.Z, .. RotEuler (mPickupRot)];
         foreach (var (pt, rot) in mPlaces)
            dto.Places.Add ([pt.X, pt.Y, pt.Z, .. RotEuler (rot)]);
         foreach (var w in mScript)
            dto.Waypoints.Add (new WpDto { P = [w.X, w.Y, w.Z, w.Rx, w.Ry, w.Rz], A = w.A.ToString () });
         File.WriteAllText (file, JsonSerializer.Serialize (dto, sJsonOpts));
         Lib.Trace ($"Saved cell → {Path.GetFileName (file)}");
      } catch (Exception ex) { Lib.Trace ($"Save cell failed: {ex.Message}"); }
   }

   internal void LoadCell (string file) {
      try {
         var dto = JsonSerializer.Deserialize<CellDto> (File.ReadAllText (file));
         if (dto == null) { Lib.Trace ("Empty cell file"); return; }

         // Clear current objects, part, pickup/place and waypoints.
         foreach (var o in mObjects) mGeomGroup.Remove (o.Node);
         mObjects.Clear (); ViewModel.Objects.Clear ();
         if (mPickupHiliteVN != null) { mGeomGroup.Remove (mPickupHiliteVN); mPickupHiliteVN = null; }
         if (mPartXfmVN != null) { mPartGroup.Remove (mPartXfmVN); mPartXfmVN = null; }
         mHasPart = mPartAttached = mHasPickup = false; mPartMesh = null; mPartOBB = null;
         mPartHost = null; mPlaces.Clear ();

         foreach (var od in dto.Objects) {
            try {
               var obj = new SceneObject (od.Path, LoadMesh (od.Path));
               if (od.Pose.Length == 6) (obj.X, obj.Y, obj.Z, obj.Rx, obj.Ry, obj.Rz) =
                  (od.Pose[0], od.Pose[1], od.Pose[2], od.Pose[3], od.Pose[4], od.Pose[5]);
               obj.ApplyPlacement ();
               if (od.HasFrame && od.Frame.Length == 6) {
                  (obj.FX, obj.FY, obj.FZ, obj.FRx, obj.FRy, obj.FRz) =
                     (od.Frame[0], od.Frame[1], od.Frame[2], od.Frame[3], od.Frame[4], od.Frame[5]);
                  obj.HasFrame = true;
                  if (od.Calib is { Length: 9 } q) {
                     obj.CP1 = new (q[0], q[1], q[2]); obj.CP2 = new (q[3], q[4], q[5]); obj.CP3 = new (q[6], q[7], q[8]);
                  }
                  obj.PinFrame ();   // re-establish the rigid frame↔pallet link
               }
               mObjects.Add (obj); mGeomGroup.Add (obj.Node);
               ViewModel.Objects.Add (new ObjectItemVM (obj.Name));
            } catch (Exception ex) { Lib.Trace ($"Object '{od.Path}' failed: {ex.Message}"); }
         }
         ViewModel.SelectedObject = mObjects.Count > 0 ? 0 : -1;

         if (dto.Home.Length == 6) mHome = PoseFrom (dto.Home[0], dto.Home[1], dto.Home[2], dto.Home[3], dto.Home[4], dto.Home[5]);
         if (dto.Box.Length == 3) { ViewModel.BX = dto.Box[0]; ViewModel.BY = dto.Box[1]; ViewModel.BZ = dto.Box[2]; }

         if (dto.Part is { } pd) {
            try {
               var mesh = LoadMesh (pd.Path);
               mPartPath = pd.Path; mPartMesh = mesh;
               mPartOBB = BuildPartOBB (mesh);
               mPartHomeXfm = pd.Place.Length == 6
                  ? Matrix3.To (PoseFrom (pd.Place[0], pd.Place[1], pd.Place[2], pd.Place[3], pd.Place[4], pd.Place[5]))
                  : Matrix3.Identity;
               mPartVN    = new Mesh3VN (mesh) { Mode = EShadeMode.Phong, Color = PartIdle };
               mPartXfmVN = new XfmVN (mPartHomeXfm, mPartVN);
               mPartGroup.Add (mPartXfmVN);
               mHasPart = true; ViewModel.PartStatus = $"{Path.GetFileName (pd.Path)} — loaded";
               if (pd.Host >= 0 && pd.Host < mObjects.Count) {        // re-link part to its host pallet
                  mPartHost = mObjects[pd.Host];
                  mPartFrameRel = mPartHomeXfm * mPartHost.FrameXfm.GetInverse ();
               }
            } catch (Exception ex) { Lib.Trace ($"Part failed: {ex.Message}"); }
         }
         if (dto.Pickup is { Length: >= 3 } pk) {
            mPickupPt = new (pk[0], pk[1], pk[2]);
            mPickupRot = pk.Length >= 6 ? RotFromEuler (pk[3], pk[4], pk[5]) : Matrix3.Identity;
            mHasPickup = true; ViewModel.PickupStatus = "Pick: loaded";
         }
         foreach (var pl in dto.Places)
            if (pl.Length >= 3)
               mPlaces.Add ((new (pl[0], pl[1], pl[2]), pl.Length >= 6 ? RotFromEuler (pl[3], pl[4], pl[5]) : Matrix3.Identity));
         if (mPlaces.Count > 0) ViewModel.PlaceStatus = $"Place: {mPlaces.Count} dest";

         mScript.Clear ();
         foreach (var w in dto.Waypoints) {
            if (w.P.Length < 6) continue;
            var act = w.A?.ToUpperInvariant () switch { "PICK" => EAction.Pick, "PLACE" => EAction.Place, _ => EAction.Move };
            mScript.Add ((w.P[0], w.P[1], w.P[2], w.P[3], w.P[4], w.P[5], act));
         }
         SaveScript (); AfterScriptChanged ();
         ViewModel.PalletStatus = $"{mObjects.Count} object(s)";
         GoHome (); Lux.UIScene?.ZoomExtents ();
         Lib.Trace ($"Loaded cell '{Path.GetFileName (file)}'");
      } catch (Exception ex) { Lib.Trace ($"Load cell failed: {ex.Message}"); }
   }

   static readonly JsonSerializerOptions sJsonOpts = new () { WriteIndented = true };

   // ── Collision-free path planning (RRT) ─────────────────────────────────────
   // Re-routes the current waypoint sequence so each transfer is collision-free, inserting
   // intermediate Move waypoints (via an RRT planner) where the straight segment is blocked.
   // The carried-part swept volume is included between Pick and Place waypoints.
   internal void PlanCollisionFree () {
      if (mScript.Count < 2) { Lib.Trace ("Generate or add waypoints first"); return; }
      ComputeGraspRel ();
      var save = SaveJoints ();
      var outWp = new List<(double X, double Y, double Z, double Rx, double Ry, double Rz, EAction A)> { mScript[0] };
      bool carrying = false, anyFail = false; int inserted = 0;
      for (int i = 1; i < mScript.Count; i++) {
         var a = mScript[i - 1]; var b = mScript[i];
         if (a.A == EAction.Pick) carrying = true; else if (a.A == EAction.Place) carrying = false;
         Point3 pa = new (a.X, a.Y, a.Z), pb = new (b.X, b.Y, b.Z);
         // Orientation field along this segment: interpolate the endpoints' Euler angles by
         // straight-line progress, so inserted via-points keep the segment's (tilted) tool
         // orientation instead of snapping to home.
         (double rx, double ry, double rz) Ori (Point3 p) {
            var ab = pb - pa; double l2 = ab.LengthSq;
            double f = l2 < 1e-9 ? 0 : Math.Clamp ((p - pa).Dot (ab) / l2, 0, 1);
            return (a.Rx + (b.Rx - a.Rx) * f, a.Ry + (b.Ry - a.Ry) * f, a.Rz + (b.Rz - a.Rz) * f);
         }
         var via = Rrt (pa, pb, Ori, carrying);
         if (via == null) anyFail = true;
         else for (int j = 1; j < via.Count - 1; j++) {
            var (rx, ry, rz) = Ori (via[j]);
            outWp.Add ((via[j].X, via[j].Y, via[j].Z, rx, ry, rz, EAction.Move)); inserted++;
         }
         outWp.Add (b);
      }
      RestoreJoints (save);
      mScript.Clear (); mScript.AddRange (outWp);
      SaveScript (); AfterScriptChanged ();
      Lib.Trace (anyFail ? $"Re-routed with {inserted} via-points (some segments unsolved)"
                         : $"Collision-free: inserted {inserted} via-points");
   }

   // Captures the part's pose relative to the flange when grabbed at the pickup, so the
   // planner can sweep the carried part correctly.
   void ComputeGraspRel () {
      mPlanGraspRel = Matrix3.Identity;
      if (!mHasPart || !mHasPickup) return;
      var save = SaveJoints ();
      if (SolveWorld (PickupPose ())) mPlanGraspRel = mPartHomeXfm * mTip.Xfm.GetInverse ();
      RestoreJoints (save);
   }

   double[] SaveJoints () { var s = new double[6]; for (int i = 0; i < 6; i++) s[i] = mJoints[i].JValue; return s; }
   void RestoreJoints (double[] s) { for (int i = 0; i < 6; i++) mJoints[i].JValue = s[i]; }

   // True if a world TCP point with the given orientation is reachable and collision-free.
   bool PoseFree (Point3 p, (double rx, double ry, double rz) o, bool carrying) {
      var save = SaveJoints ();
      bool free = SolveWorld (PoseFrom (p.X, p.Y, p.Z, o.rx, o.ry, o.rz)) && !HasCollision (carrying);
      RestoreJoints (save);
      return free;
   }

   // True if the straight segment a→b is collision-free (sampled every ~25 mm), using the
   // orientation field 'ori' at each sample.
   bool SegmentFree (Point3 a, Point3 b, Func<Point3, (double, double, double)> ori, bool carrying) {
      double len = (b - a).Length; int n = Math.Max (1, (int)(len / 25));
      for (int i = 1; i <= n; i++) { var p = a + (b - a) * ((double)i / n); if (!PoseFree (p, ori (p), carrying)) return false; }
      return true;
   }

   // Goal-biased RRT in world XYZ; orientation at each point comes from 'ori'.  Returns a
   // collision-free poly-line from start to goal (inclusive), or null within the iteration cap.
   List<Point3>? Rrt (Point3 start, Point3 goal, Func<Point3, (double, double, double)> ori, bool carrying) {
      if (SegmentFree (start, goal, ori, carrying)) return [start, goal];
      // Planning box: union of start/goal and all object bounds, expanded.
      Point3 lo = new (Math.Min (start.X, goal.X), Math.Min (start.Y, goal.Y), Math.Min (start.Z, goal.Z));
      Point3 hi = new (Math.Max (start.X, goal.X), Math.Max (start.Y, goal.Y), Math.Max (start.Z, goal.Z));
      foreach (var o in mObjects) {
         var bb = o.Mesh.GetBound (o.Xfm);
         lo = new (Math.Min (lo.X, bb.X.Min), Math.Min (lo.Y, bb.Y.Min), Math.Min (lo.Z, bb.Z.Min));
         hi = new (Math.Max (hi.X, bb.X.Max), Math.Max (hi.Y, bb.Y.Max), Math.Max (hi.Z, bb.Z.Max));
      }
      const double m = 300;
      lo = new (lo.X - m, lo.Y - m, lo.Z - m); hi = new (hi.X + m, hi.Y + m, hi.Z + m);
      const double step = 80;
      List<(Point3 P, int Par)> tree = [(start, -1)];
      for (int iter = 0; iter < 4000; iter++) {
         var target = mRng.NextDouble () < 0.15 ? goal : new Point3 (
            lo.X + mRng.NextDouble () * (hi.X - lo.X),
            lo.Y + mRng.NextDouble () * (hi.Y - lo.Y),
            lo.Z + mRng.NextDouble () * (hi.Z - lo.Z));
         int ni = 0; double nd = double.MaxValue;
         for (int k = 0; k < tree.Count; k++) { double d = (tree[k].P - target).LengthSq; if (d < nd) { nd = d; ni = k; } }
         var from = tree[ni].P; var dir = target - from; double dist = dir.Length;
         if (dist < 1e-3) continue;
         var np = dist <= step ? target : from + dir * (step / dist);
         if (!PoseFree (np, ori (np), carrying) || !SegmentFree (from, np, ori, carrying)) continue;
         tree.Add ((np, ni));
         if (SegmentFree (np, goal, ori, carrying)) {
            List<Point3> path = []; for (int idx = tree.Count - 1; idx >= 0; idx = tree[idx].Par) path.Add (tree[idx].P);
            path.Reverse (); path.Add (goal);
            return Shortcut (path, ori, carrying);
         }
      }
      return null;
   }

   // Greedy shortcut: skip intermediate points whenever a direct segment is collision-free.
   List<Point3> Shortcut (List<Point3> path, Func<Point3, (double, double, double)> ori, bool carrying) {
      List<Point3> outp = [path[0]]; int i = 0;
      while (i < path.Count - 1) {
         int j = path.Count - 1;
         while (j > i + 1 && !SegmentFree (path[i], path[j], ori, carrying)) j--;
         outp.Add (path[j]); i = j;
      }
      return outp;
   }

   readonly Random mRng = new (12345);


   // The mesh triangle the hit point lies on (closest by perpendicular distance among
   // triangles whose face contains the projected point), with its outward unit normal.
   static (Vector3 Normal, Point3 A, Point3 B, Point3 C)? FindFace (Mesh3 mesh, Point3 hit) {
      var tris = mesh.Triangle; var v = mesh.Vertex;
      double best = double.MaxValue;
      (Vector3, Point3, Point3, Point3)? found = null;
      for (int i = 0; i < tris.Length; i += 3) {
         Point3 a = (Point3)v[tris[i]].Pos, b = (Point3)v[tris[i + 1]].Pos, c = (Point3)v[tris[i + 2]].Pos;
         var cross = (b - a) * (c - a);
         double len = cross.Length; if (len < 1e-9) continue;
         var n    = cross / len;
         double d = (hit - a).Dot (n);
         if (Math.Abs (d) < best && InTriangle (hit - n * d, a, b, c)) { best = Math.Abs (d); found = (n, a, b, c); }
      }
      return found;
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
   // mHome: full home TCP pose in WORLD coords (position + orientation).  Defaults to the
   // arm's-length pose; the user can overwrite it from the current pose via SetHome.
   CoordSystem mHome = DefaultHome ();
   static CoordSystem DefaultHome () {
      var cs = CoordSystem.World;
      cs    *= Matrix3.Rotation (EAxis.X, (-90.0).D2R ());   // home orientation (TCP Z → +Y)
      cs    += new Vector3 (1166, 0, 1161);                  // X=reach, Y=centred, Z world
      return cs;
   }
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

   // Imported geometries (pallets / fixtures): pickable for frame/pickup/place and used as
   // collision obstacles.  All are placed in world coords (identity transform).
   readonly GroupVN           mGeomGroup;
   readonly List<SceneObject> mObjects = [];
   Mesh3VN?                   mPickupHiliteVN;
   bool                       mArmed;            // a teach mode is waiting for a click
   Point3                     mPickupPt;
   Matrix3                    mPickupRot = Matrix3.Identity;             // pickup surface tilt at teach
   bool                       mHasPickup;
   // Ordered place destinations (point + surface tilt).  The part is picked, dropped at the
   // first, re-picked and carried to the next, and so on — enabling multi-pallet transfers.
   readonly List<(Point3 Pt, Matrix3 Rot)> mPlaces = [];
   EPickMode                  mPickMode;
   enum EPickMode { None, Corner, Pickup, Place }

   // Imported part: placed on a pallet, grabbed at pickup, then fixed to the flange.
   readonly GroupVN mPartGroup;
   string           mPartPath = "";
   Mesh3?           mPartMesh;
   Mesh3VN?         mPartVN;
   OBBTree?         mPartOBB;
   XfmVN?           mPartXfmVN;
   SceneObject?     mPartHost;                          // pallet the part rests on (rides its frame)
   Matrix3          mPartHomeXfm  = Matrix3.Identity;   // rest pose on the pallet
   Matrix3          mPartFrameRel = Matrix3.Identity;   // part rest pose relative to host frame
   Matrix3          mPartRelXfm   = Matrix3.Identity;   // pose relative to flange while held
   bool             mHasPart, mPartAttached;

   // Waypoint = TCP pose (frame-relative when a frame is active) + an action that fires on
   // arrival.  Pick attaches the part to the flange; Place drops it where it currently is.
   enum EAction { Move, Pick, Place }
   readonly List<(double X, double Y, double Z, double Rx, double Ry, double Rz, EAction A)> mScript = [];
   int      mFiredUpto = -1;          // highest waypoint index whose action has run this play
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
// Draws each imported object's user-frame triad plus the pickup marker, so the operator can
// see where the taught frames and pickup sit in the scene.
class FrameVN : VNode {
   public FrameVN () { Streaming = true; }
   public required RobotScene Scene { private get; init; }

   public override void SetAttributes () { Lux.ZLevel = 70; }

   public override void Draw () {
      foreach (var cs in Scene.ObjectFrames) {
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
      int lh = mFace.LineHeight, py = (int)sc.Rect.Height - lh * 5 - 8, px = 10;
      void Line (string s) { Lux.Text (s, new Vec2S (px, py)); py += lh; }
      // Collision banner (red) above the readout when anything is colliding.
      if (Scene.InCollision) { Lux.Color = new Color4 (255, 64, 32); Line ("⚠ COLLISION"); Lux.Color = Color4.White; }
      else py += lh;
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

#region class SceneObject --------------------------------------------------------------------------
// An imported geometry: a pickable surface (frame / pickup / place) and a collision obstacle.
// Movable in 6 DOF (Xfm) and carries its own user frame (6 params).  OBB is null if the mesh
// was too degenerate to build a tree.
class SceneObject {
   public SceneObject (string path, Mesh3 mesh) {
      Path = path; Name = System.IO.Path.GetFileName (path); Mesh = mesh;
      try { OBB = OBBTree.From (mesh); } catch { OBB = null; }
      VN   = new Mesh3VN (mesh) { Mode = EShadeMode.Phong, Color = Idle };
      Node = new XfmVN (Matrix3.Identity, VN);
   }

   public readonly string   Path;
   public readonly string   Name;
   public readonly Mesh3    Mesh;
   public readonly OBBTree? OBB;
   public readonly Mesh3VN  VN;
   public readonly XfmVN    Node;      // placement transform wrapping the mesh
   public readonly Color4   Idle = Color4.Gray (160);

   // Placement in world (6 DOF) and the resulting local→world transform.
   public double X, Y, Z, Rx, Ry, Rz;
   public Matrix3 Xfm => Matrix3.To (Pose (X, Y, Z, Rx, Ry, Rz));
   public void ApplyPlacement () => Node.Xfm = Xfm;

   // User frame (6 parameters, world) — built on demand from the stored values.
   public bool    HasFrame;
   public double  FX, FY, FZ, FRx, FRy, FRz;
   public Point3? CP1, CP2, CP3;       // 3-point calibration values (optional)
   public CoordSystem Frame => Pose (FX, FY, FZ, FRx, FRy, FRz);

   // Rigid link between the frame and the object: once pinned, the object and its user
   // frame move together.  FrameRel = (frame→world) × (object→world)⁻¹ captured when the
   // frame is established, so it stays constant as either is edited.
   public bool    FramePinned;
   public Matrix3 FrameRel = Matrix3.Identity;
   public Matrix3 FrameXfm => Matrix3.To (Frame);                         // frame-local → world
   public void PinFrame () { FrameRel = FrameXfm * Xfm.GetInverse (); FramePinned = true; }

   // Builds a CoordSystem from a position + XYZ Euler angles (degrees).
   static CoordSystem Pose (double x, double y, double z, double rx, double ry, double rz) {
      var cs = CoordSystem.World;
      cs *= Matrix3.Rotation (EAxis.X, rx.D2R ());
      cs *= Matrix3.Rotation (EAxis.Y, ry.D2R ());
      cs *= Matrix3.Rotation (EAxis.Z, rz.D2R ());
      cs += new Vector3 (x, y, z);
      return cs;
   }
}
#endregion

#region Cell DTOs ----------------------------------------------------------------------------------
// Plain data carriers for JSON cell persistence (objects, part, pickup/place, home, box, waypoints).
class CellDto {
   public List<ObjDto> Objects { get; set; } = [];
   public PartDto? Part { get; set; }
   public double[]? Pickup { get; set; }                 // X,Y,Z,Rx,Ry,Rz (rot = surface tilt)
   public List<double[]> Places { get; set; } = [];      // each X,Y,Z,Rx,Ry,Rz (ordered chain)
   public double[] Home { get; set; } = [];
   public double[] Box { get; set; } = [];
   public List<WpDto> Waypoints { get; set; } = [];
}
class ObjDto {
   public string Path { get; set; } = "";
   public double[] Pose { get; set; } = [];     // X,Y,Z,Rx,Ry,Rz
   public bool HasFrame { get; set; }
   public double[] Frame { get; set; } = [];    // X,Y,Z,Rx,Ry,Rz
   public double[]? Calib { get; set; }         // 3 points × 3 (or null)
}
class PartDto {
   public string Path { get; set; } = "";
   public double[] Place { get; set; } = [];    // X,Y,Z,Rx,Ry,Rz rest pose
   public int Host { get; set; } = -1;          // index of the pallet the part rides (or -1)
}
class WpDto   { public double[] P { get; set; } = []; public string A { get; set; } = "Move"; }
#endregion
