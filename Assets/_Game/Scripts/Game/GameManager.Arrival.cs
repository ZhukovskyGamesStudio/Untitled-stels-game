using System.Collections;
using System.Threading.Tasks;
using Stealth.Configs;
using Stealth.Level;
using Stealth.Player;
using Stealth.Vehicles;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// The arrival at the next house, in one unbroken drive of the real car with the team in its seats
    /// (<see cref="DrivePath"/>). The car drives off down the tunnel it was chosen in (<see cref="DepartServer"/>): every
    /// peer moves it by the server's clock and keeps the endless tunnel round it, and at once starts generating the house
    /// the car goes to on a worker thread — every peer can tell which one (the route's lane the car took). The sewer stays
    /// where it is (deep under the house, <see cref="SewerLocation.Origin"/>) while the server puts the house up a piece a
    /// frame (<see cref="StartLevelSteps"/>) and then says when the car changes into the basement's approach
    /// (<see cref="DriveUntil"/>, at the same place of the tunnel's pattern: nothing changes on screen) and when it stops
    /// beside the alcove (<see cref="ArriveAt"/>). The other peers build the same house as soon as they hear of it; each
    /// takes its sewer away once its car is in the basement.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>Server time the car stops beside the alcove in the new house's basement, replicated.</summary>
        public double ArriveAt { get; set; }
        /// <summary>Server time the car drove off, replicated.</summary>
        public double DepartAt { get; set; }
        /// <summary>Where the car drove off from in its tunnel (x, heading in radians, z: the tunnel's frame) and how fast (w), replicated.</summary>
        public Vector4 DriveFrom { get; set; }
        /// <summary>The tunnel the car drove off down (<see cref="SewerLocation.BranchFrame"/>), replicated; −1 none.</summary>
        public int DriveBranch { get; set; } = -1;

        // the next house generated in the background during the drive (every peer predicts it the same way)
        LevelInfo m_PreparedFor;
        Task<LevelLayout> m_Prepared;
        /// <summary>Server: the house is being put up (over several frames).</summary>
        bool m_Arriving;
        /// <summary>A client: its house is being put up (over several frames), and which house.</summary>
        Coroutine m_LocalBuild;
        LevelInfo m_LocalFor;
        Coroutine m_ArriveRoutine;
        DriveTunnel m_Approach;

        /// <summary>The house the car drives to, as every peer can tell it: the next column, the lane the car took.</summary>
        LevelInfo PredictNext()
        {
            int index = Level.Index + 1;
            int lane = Mathf.Clamp(ChosenLane, 0, Route.LanesAt(index) - 1);
            return new LevelInfo
            {
                Seed = Route.Node(index, lane).Seed, Index = index, Players = TeamSize, Lane = lane, RunSeed = Level.RunSeed,
            };
        }

        /// <summary>Every peer, on the drive: the next house's layout starts generating on a worker thread (it is pure).</summary>
        void PrepareNextHouse()
        {
            // as soon as the way is chosen (the car standing in its tunnel), not only on the drive: the team may still go
            // back up for more, the house it will drive to is known already
            if (Route == null || ChosenLane < 0 || Phase is not (RunPhase.Sewer or RunPhase.Playing)) return;
            var info = PredictNext();
            if (info.Seed == 0) return;
            if (m_Prepared != null)
            {
                if (m_PreparedFor.Equals(info) || DriveUntil > 0) return;
                m_Prepared = null; // another way chosen meanwhile (or the team changed): that one finishes unused
            }
            var level = Tuning.Level;
            var (building, security, features) = (level.Building, level.Security, level.Features);
            m_PreparedFor = info;
            m_Prepared = Task.Run(() =>
            {
                try { return LevelGenerator.Generate(info.Seed, info.Index, building, security, Mathf.Max(1, info.Players), features); }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[Game] The next house did not generate in the background ({e.Message}): it is generated when it is built");
                    return null;
                }
            });
        }

        /// <summary>The layout generated in the background for this house (waiting for it if it is still being made), or null.</summary>
        LevelLayout TakePrepared(LevelInfo info)
        {
            var task = m_Prepared;
            m_Prepared = null;
            if (task == null || !m_PreparedFor.Equals(info)) return null;
            try { return task.Result; }
            catch (System.Exception) { return null; }
        }

        /// <summary>Server, on the drive: the next house is generated — put it up now.</summary>
        void UpdateDriveServer()
        {
            if (m_Arriving || Session.ServerTime < DepartAt + Arrival.BuildAfter) return;
            if (m_Prepared != null && !m_Prepared.IsCompleted && Session.ServerTime < DepartAt + Arrival.GiveUpWaiting) return;
            m_Arriving = true;
            m_ArriveRoutine = StartCoroutine(ArriveRoutine());
        }

        /// <summary>Server: a house starts some other way (the cheat, a new run) — the arrival being built is dropped.</summary>
        void CancelArrival()
        {
            if (m_ArriveRoutine != null) StopCoroutine(m_ArriveRoutine);
            m_ArriveRoutine = null;
            m_Arriving = false;
            m_Prepared = null;
            ArriveAt = 0;
            DriveBranch = -1;
        }

        IEnumerator ArriveRoutine()
        {
            int next = Level.Index + 1, lane = ChosenLane;
            var steps = Timed(StartLevelSteps(next, lane, 0, true), "the next house (server)");
            while (steps.MoveNext()) yield return null;
            m_Arriving = false;
            m_ArriveRoutine = null;
        }

        /// <summary>Server: the car drives off down the tunnel it stands in, from where it stands.</summary>
        void DepartCarServer(Car car)
        {
            var sewer = SewerLocation.Active;
            int branch = sewer != null && car != null ? sewer.BranchAt(car.transform.position) : -1;
            var frame = sewer != null ? sewer.BranchFrame(branch) : null;
            DepartAt = Session.ServerTime;
            DriveBranch = frame != null ? branch : -1;
            if (frame == null) return;
            var local = frame.InverseTransformPoint(car.transform.position);
            var forward = frame.InverseTransformDirection(car.transform.forward);
            float heading = Mathf.Atan2(forward.z, forward.x);
            DriveFrom = new Vector4(local.x, heading, local.z, Mathf.Max(0f, car.Speed));
        }

        /// <summary>
        /// Server, the house stands: when the car changes into the basement's approach and when it stops (the same place of
        /// the pattern in both tunnels, late enough for every peer to have built the house).
        /// </summary>
        void ArriveCarServer(LevelLayout layout)
        {
            if (DriveBranch < 0 || Car.Active == null)
            {
                // nothing drove (the car was missing): it stands in the basement
                SpawnBasementCarServer(layout);
                DriveUntil = 0;
                return;
            }
            double swap = Arrival.SwapAt(DepartAt, Session.ServerTime, m_Run.Settings.DriveTime);
            ArriveAt = DrivePath.ArriveAtFor(DepartAt, From3, DriveFrom.w, swap + DrivePath.SwapLead);
            DriveUntil = ArriveAt - DrivePath.SwapLead;
        }

        Vector3 From3 => new(DriveFrom.x, DriveFrom.y, DriveFrom.z);

        /// <summary>Server: the team's car standing beside the alcove in the basement (a house started without a drive).</summary>
        Car SpawnBasementCarServer(LevelLayout layout)
        {
            DespawnCarServer();
            if (CarPrefab == null || layout == null) return null;
            var pose = Basement.CarPose(layout.Cellar);
            m_Car = Session.Spawn(CarPrefab, pose.position, pose.rotation);
            return m_Car != null ? m_Car.GetComponent<Car>() : null;
        }

        /// <summary>
        /// Every peer: the car on its drive — where it is now and how fast it goes, and whether it is in the basement's
        /// approach yet (else in the sewer's tunnel). False when it is not driving (a driver has it, or it stands).
        /// </summary>
        public bool Driving(out Pose pose, out float speed, out bool basement)
        {
            pose = default;
            speed = 0f;
            basement = false;
            if (DriveUntil <= 0 || DriveBranch < 0 || DepartAt <= 0) return false;
            double now = Session.ServerTime;
            var l = ActiveLevel.Layout;
            if (ArriveAt > 0 && now >= DriveUntil && l != null && ActiveLevel.Ready && l.Seed == Level.Seed)
            {
                var at = DrivePath.InBasement((float)(ArriveAt - now), out speed);
                pose = new Pose(l.Cellar.ToWorld(at.position), l.Cellar.Rotation * at.rotation);
                basement = true;
                return true;
            }
            var frame = m_Sewer != null ? m_Sewer.BranchFrame(DriveBranch) : null;
            if (frame == null) return false;
            var p = DrivePath.InBranch(From3, DriveFrom.w, (float)(now - DepartAt), out speed);
            pose = new Pose(frame.TransformPoint(p.position), frame.rotation * p.rotation);
            return true;
        }

        /// <summary>
        /// Every peer, on the drive: the endless tunnel round the car (in the sewer after the branch's reducer; in the
        /// basement its approach, put round where the car will come before it does), and the sewer taken away once the
        /// car is in the basement.
        /// </summary>
        void UpdateDrive()
        {
            if (DriveUntil <= 0 || DriveBranch < 0)
            {
                if (m_Approach != null) m_Approach.Clear();
                m_Approach = null;
                return;
            }
            ShiftSewerForDrive();
            if (!Driving(out var pose, out _, out bool basement)) return;
            var l = ActiveLevel.Layout;
            if (m_Approach == null && l != null && ActiveLevel.Ready && ActiveLevel.Root != null)
                m_Approach = ActiveLevel.Root.GetComponentInChildren<DriveTunnel>();
            if (basement)
            {
                m_Approach?.Follow(l.Cellar.ToLocal(pose.position).x);
                if (m_Sewer != null && Session.ServerTime > DriveUntil + 1.0) LeaveSewerLocal(); // out of sight behind the car
                return;
            }
            var frame = m_Sewer.BranchFrame(DriveBranch);
            m_Sewer.FollowDrive(DriveBranch, frame.InverseTransformPoint(pose.position).x);
            if (m_Approach != null && ArriveAt > 0 && l != null && l.Seed == Level.Seed)
                m_Approach.Follow(DrivePath.InBasement((float)(ArriveAt - Session.ServerTime), out _).position.x);
        }

        /// <summary>A client: the house it heard of is put up a piece a frame during the drive (else at once).</summary>
        void BuildLocal()
        {
            if (m_LocalBuild != null)
            {
                if (m_LocalFor.Equals(Level)) return;
                StopCoroutine(m_LocalBuild); // another house came meanwhile (the cheat)
                m_LocalBuild = null;
                m_BuiltSeed = 0;
            }
            bool spread = Application.isPlaying && DriveUntil > 0;
            if (!spread)
            {
                var all = BuildLocalSteps(Level);
                while (all.MoveNext()) { }
                return;
            }
            m_LocalFor = Level;
            m_LocalBuild = StartCoroutine(BuildSpread(Level));
        }

        IEnumerator BuildSpread(LevelInfo info)
        {
            var steps = Timed(BuildLocalSteps(info), "the next house");
            while (steps.MoveNext()) yield return null;
            m_LocalBuild = null;
        }

        /// <summary>A step of a house built on the drive that took longer than this is logged (development builds): the drive stalled there.</summary>
        const double SlowStepMs = 30.0;

        /// <summary>
        /// The steps of a piece-by-piece job, each timed: in a development build the slow ones are written to the log with
        /// their number and the total, so a stall on the drive can be found in a playtest's log.
        /// </summary>
        static IEnumerator Timed(IEnumerator steps, string what)
        {
            var watch = new System.Diagnostics.Stopwatch();
            double total = 0, worst = 0;
            int n = 0;
            while (true)
            {
                watch.Restart();
                bool more = steps.MoveNext();
                double ms = watch.Elapsed.TotalMilliseconds;
                total += ms;
                worst = System.Math.Max(worst, ms);
                if (ms > SlowStepMs && Debug.isDebugBuild) Debug.Log($"[Perf] {what}: step {n} took {ms:0} ms");
                n++;
                if (!more) break;
                yield return steps.Current;
            }
            if (Debug.isDebugBuild) Debug.Log($"[Perf] {what}: {n} steps, {total:0} ms of work, the longest {worst:0} ms");
        }
    }

    /// <summary>The arrival's timings (pure): when the house is built and the earliest the car may change into its basement.</summary>
    public static class Arrival
    {
        /// <summary>The server puts the house up this long after the departure (the car is under way).</summary>
        public const float BuildAfter = 1f;
        /// <summary>The longest the server waits for the background generation after the departure before it generates the house itself.</summary>
        public const float GiveUpWaiting = 15f;
        /// <summary>Seconds the other peers get to build the house after the server has.</summary>
        public const float ClientSlack = 2.5f;

        /// <summary>The earliest change into the basement: no sooner than the drive's own length, late enough for every peer to build the house.</summary>
        public static double SwapAt(double departed, double built, float driveTime) =>
            System.Math.Max(departed + driveTime, built + ClientSlack);
    }
}
