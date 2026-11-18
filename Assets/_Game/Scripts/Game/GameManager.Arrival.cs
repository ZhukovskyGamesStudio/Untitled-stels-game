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
    /// The arrival at the next house, with no black screen (<see cref="Arrival"/> has the timings). The car drives off
    /// (<see cref="DepartServer"/>): every peer shows the drive scene and, at once, starts generating the house the car
    /// goes to on a worker thread — every peer can tell which one (the route's lane the car took). Once the camera has
    /// settled in its side view the server puts the house up a piece a frame (<see cref="StartLevelSteps"/>), its content,
    /// and the team's car in the basement with everyone back in the seat they rode in; then it says when the camera is
    /// back in the eyes (<see cref="DriveUntil"/>, the cut) and when the car stops at the gate (<see cref="ArriveAt"/>).
    /// The other peers build the same house as soon as they hear of it. Each peer's drive scene glides back into its
    /// player's eyes, in step with the real car that rolls in down the basement's tunnel; there the scene ends.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>Server time the car stops at the gate in the new house's basement (the roll-in ends), replicated.</summary>
        public double ArriveAt { get; set; }

        // the next house generated in the background during the drive (every peer predicts it the same way)
        LevelInfo m_PreparedFor;
        Task<LevelLayout> m_Prepared;
        double m_DepartedAt;
        readonly ulong[] m_RideSeats = { Car.Nobody, Car.Nobody, Car.Nobody, Car.Nobody };
        /// <summary>Server: the house is being put up (over several frames).</summary>
        bool m_Arriving;
        /// <summary>A client: its house is being put up (over several frames), and which house.</summary>
        Coroutine m_LocalBuild;
        LevelInfo m_LocalFor;
        Coroutine m_ArriveRoutine;
        /// <summary>This peer: the drive scene of this drive is over (the house's own camera is on).</summary>
        bool m_DriveCut;

        /// <summary>The house the car drives to, as every peer can tell it: the next column, the lane the car took.</summary>
        LevelInfo PredictNext()
        {
            int index = Level.Index + 1;
            int lane = Mathf.Clamp(ChosenLane, 0, Route.LanesAt(index) - 1);
            return new LevelInfo
            {
                Seed = Route.Node(index, lane).Seed, Index = index, Players = TeamSize, Lane = lane, RunSeed = Level.RunSeed,
                ClassicEnemies = m_IsServer ? LevelGenerator.ClassicEnemies : Level.ClassicEnemies,
            };
        }

        /// <summary>Every peer, on the drive: the next house's layout starts generating on a worker thread (it is pure).</summary>
        void PrepareNextHouse()
        {
            if (DriveUntil <= 0 || Phase != RunPhase.Sewer || m_Prepared != null || Route == null || ChosenLane < 0) return;
            var info = PredictNext();
            if (info.Seed == 0) return;
            var level = Tuning.Level;
            var (building, security, features, classic) = (level.Building, level.Security, level.Features, LevelGenerator.Classic);
            m_PreparedFor = info;
            m_Prepared = Task.Run(() =>
            {
                try { return LevelGenerator.Generate(info.Seed, info.Index, building, security, Mathf.Max(1, info.Players), features, classic, info.ClassicEnemies); }
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

        /// <summary>Server, on the drive: the camera has settled and the next house is generated — put it up now.</summary>
        void UpdateDriveServer()
        {
            if (m_Arriving || Session.ServerTime < m_DepartedAt + Arrival.BuildAfter) return;
            if (m_Prepared != null && !m_Prepared.IsCompleted && Session.ServerTime < m_DepartedAt + Arrival.GiveUpWaiting) return;
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
        }

        IEnumerator ArriveRoutine()
        {
            int next = Level.Index + 1, lane = ChosenLane;
            var steps = StartLevelSteps(next, lane, 0, true);
            while (steps.MoveNext()) yield return null;
            m_Arriving = false;
            m_ArriveRoutine = null;
        }

        /// <summary>Server, the house stands: the car in its basement, rolling in with everyone back in the seat they rode in.</summary>
        void ArriveCarServer(LevelLayout layout)
        {
            var car = SpawnBasementCarServer(layout, true);
            if (car != null)
                for (int seat = 0; seat < Car.SeatCount; seat++)
                {
                    var p = PlayerController.Find(m_RideSeats[seat]);
                    if (p != null) car.SitServer(p, seat);
                }
            double cut = Arrival.CutAt(m_DepartedAt, Session.ServerTime, m_Run.Settings.DriveTime);
            DriveUntil = cut;
            ArriveAt = cut + Basement.RollTime;
        }

        /// <summary>Server: the team's car in the basement — at the start of its roll in (arriving) or standing at the gate.</summary>
        Car SpawnBasementCarServer(LevelLayout layout, bool rolling)
        {
            DespawnCarServer();
            if (CarPrefab == null || layout == null) return null;
            var pose = rolling ? Basement.RollPose(layout.Cellar, Basement.RollTime, out _) : Basement.CarPose(layout.Cellar);
            m_Car = Session.Spawn(CarPrefab, pose.position, pose.rotation);
            return m_Car != null ? m_Car.GetComponent<Car>() : null;
        }

        /// <summary>
        /// Every peer: the car rolling in down the basement's tunnel to the gate (the drive's last seconds): where it is now and
        /// how fast it goes. False when it is not rolling (the driver has it).
        /// </summary>
        public bool Rolling(out Pose pose, out float speed)
        {
            pose = default;
            speed = 0f;
            var l = ActiveLevel.Layout;
            if (DriveUntil <= 0 || ArriveAt <= 0 || Phase != RunPhase.Playing || l == null) return false;
            pose = Basement.RollPose(l.Cellar, (float)(ArriveAt - Session.ServerTime), out speed);
            return true;
        }

        /// <summary>A client: the house it heard of is put up a piece a frame while the drive scene goes on (else at once).</summary>
        void BuildLocal()
        {
            if (m_LocalBuild != null)
            {
                if (m_LocalFor.Equals(Level)) return;
                StopCoroutine(m_LocalBuild); // another house came meanwhile (the cheat)
                m_LocalBuild = null;
                m_BuiltSeed = 0;
            }
            bool spread = Application.isPlaying && (DriveUntil > 0 || DriveScene.Active != null);
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
            var steps = BuildLocalSteps(info);
            while (steps.MoveNext()) yield return null;
            m_LocalBuild = null;
        }

        /// <summary>
        /// Every peer, the drive scene: up from the departure; once this peer's house stands and the cut comes near, its
        /// camera glides back into the player's eyes in the real car (rolling in down the basement), and there it ends.
        /// </summary>
        void UpdateDriveScene()
        {
            if (DriveUntil <= 0)
            {
                m_DriveCut = false;
                // a house started some other way (the cheat): no scene left over
                if (m_DriveScene != null && Phase != RunPhase.Sewer) EndDriveScene();
                return;
            }
            if (m_DriveScene == null)
            {
                if (!m_DriveCut && Phase == RunPhase.Sewer && Art != null && Art.DriveScene != null)
                {
                    m_DriveScene = Instantiate(Art.DriveScene, new Vector3(0f, -400f, 0f), Quaternion.identity);
                    BeginDriveScene(m_DriveScene.GetComponent<DriveScene>());
                }
                return;
            }
            var scene = m_DriveScene.GetComponent<DriveScene>();
            bool built = Phase == RunPhase.Playing && ActiveLevel.Ready && ActiveLevel.Layout != null && ActiveLevel.Layout.Seed == Level.Seed && Car.Active != null;
            if (scene == null || !built) return;
            double left = DriveUntil - Session.ServerTime;
            if (!scene.Returning && left <= Arrival.GlideBack) scene.Return(Mathf.Max(Arrival.MinGlideBack, (float)left), Basement.RollSpeed);
            if (!scene.Returning) return;
            var me = PlayerController.Local;
            var car = Car.Active;
            Pose? eye = null;
            if (me != null && me.Camera != null && me.SeatIndex >= 0)
            {
                var cam = me.Camera.transform;
                eye = new Pose(car.transform.InverseTransformPoint(cam.position), Quaternion.Inverse(car.transform.rotation) * cam.rotation);
            }
            scene.ReturnTo(eye, me != null && me.Camera != null ? me.Camera.fieldOfView : 60f, me != null && me.Camera != null ? me.Camera.nearClipPlane : 0.05f);
            if (!scene.Returned) return;
            EndDriveScene();
            me?.Motor?.AddTrauma(0.12f); // the car bumps onto the basement's grate: the eyes are its own again
        }

        void EndDriveScene()
        {
            if (m_DriveScene != null) Destroy(m_DriveScene);
            m_DriveScene = null;
            m_DriveCut = true;
        }
    }

    /// <summary>The arrival's timings (pure): when the house is built, when the camera is back in the eyes, when the car stops.</summary>
    public static class Arrival
    {
        /// <summary>The server puts the house up this long after the departure: the camera has glided out to its side view (DriveScene.GlideTime).</summary>
        public const float BuildAfter = 3.4f;
        /// <summary>The longest the server waits for the background generation after the departure before it generates the house itself.</summary>
        public const float GiveUpWaiting = 15f;
        /// <summary>Seconds the other peers get to build the house after the server has.</summary>
        public const float ClientSlack = 1.5f;
        /// <summary>The camera's glide back into the eyes, and the shortest one (a peer that built its house late).</summary>
        public const float GlideBack = 2.6f, MinGlideBack = 0.9f;

        /// <summary>The cut: no sooner than the drive's own length, and late enough for every peer to build the house and glide back.</summary>
        public static double CutAt(double departed, double built, float driveTime) =>
            System.Math.Max(departed + driveTime, built + ClientSlack + GlideBack);
    }
}
