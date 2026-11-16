using System.Collections.Generic;
using Stealth.Game;
using Stealth.Items;
using Stealth.Level;
using UnityEngine;
using UnityEngine.AI;

namespace Stealth.Enemies
{
    /// <summary>
    /// Workers move loot around the house (<see cref="RobotTraits.MovesLoot"/>), server side. Empty-handed on patrol a
    /// worker drives up to loot lying on the floor nearby and scoops it onto its tray. Loaded, it takes the loot to the
    /// house's storage room (<see cref="LevelLayout.StorageRoom"/>) — through its locked door, which opens for robots —
    /// and leaves it on a shelf or in the pile (<see cref="LevelLayout.StorageDrops"/>); in a house without one it sets
    /// it down at some patrol stop. Every peer plays the swing from a pulse (<see cref="RobotAnimator.HandleCargo"/>,
    /// <see cref="CargoMotion"/>). Anything else — a suspicion, a chase, a hack — cuts the errand short.
    /// </summary>
    public partial class RobotController
    {
        enum LootErrand : byte { None, Fetch, Haul, Swing }

        LootErrand m_Errand;
        SwingKind m_Swing;
        float m_ErrandStart, m_ErrandUntil, m_NextErrand, m_NextSeek;
        bool m_Touched;
        float m_Rolled;
        WorldItem m_Fetch;
        Vector3 m_ErrandSpot, m_ErrandFace;
        /// <summary>Hauling: where the loot goes (a storage drop); none: the floor in front.</summary>
        Vector3? m_DropAt;
        bool m_WasArrived;
        System.Random m_LootRng;

        static readonly List<WorldItem> s_Candidates = new();
        static readonly List<Vector3> s_CandidateSpots = new();
        static readonly List<int> s_Drops = new();

        /// <summary>Hauls give up after this long (a long way round, a blocked door), seconds.</summary>
        const float HaulTimeout = 60f;
        /// <summary>A storage drop is taken if loot lies this close to it, metres.</summary>
        const float DropTaken = 0.4f;

        /// <summary>Server: runs the errand; true while it drives the body (the brain's patrol waits).</summary>
        bool UpdateLootErrandServer(float now, float dt)
        {
            bool patrol = State == RobotState.Patrol;
            bool arrived = Arrived, newStop = arrived && !m_WasArrived;
            m_WasArrived = arrived;
            switch (m_Errand)
            {
                case LootErrand.Swing:
                    return Swing(now, patrol);
                case LootErrand.Fetch:
                    return Approach(now, dt, patrol, BeginLiftServer);
                case LootErrand.Haul:
                    return Approach(now, dt, patrol, BeginLowerServer);
            }
            if (!patrol || now < m_NextErrand) return false;
            if (Cargo < 0)
            {
                if (now < m_NextSeek) return false;
                m_NextSeek = now + 0.5f;
                return Seek(now);
            }
            if (!LootErrands.CanTake(DecodeCargo(Cargo).shape)) return false; // a key it guards stays with it
            var layout = ActiveLevel.Layout;
            if (layout != null && layout.StorageRoom >= 0)
            {
                if (now < m_NextSeek) return false;
                m_NextSeek = now + 1f;
                return Haul(now, layout);
            }
            // no storage room: it leaves its load at some patrol stop
            if (newStop && m_LootRng.NextDouble() < Traits.PutDownChance && CanSetDown())
            {
                m_DropAt = null;
                BeginLowerServer(now);
                return true;
            }
            return false;
        }

        void EndLootErrand()
        {
            m_Errand = LootErrand.None;
            m_Fetch = null;
            m_DropAt = null;
        }

        /// <summary>Driving to where the swing starts, then turning to face the spot; then <paramref name="begin"/>.</summary>
        bool Approach(float now, float dt, bool patrol, System.Action<float> begin)
        {
            bool gone = m_Errand == LootErrand.Fetch && (m_Fetch == null || !m_Fetch.isActiveAndEnabled || m_Fetch.Taking);
            if (!patrol || gone || now > m_ErrandUntil)
            {
                EndLootErrand();
                m_NextErrand = now + 2f; // gave up (or something else came up): look again a bit later
                return false;
            }
            var flat = m_ErrandSpot - transform.position;
            flat.y = 0f;
            if (flat.magnitude > 0.3f && !Arrived)
            {
                Move(Traits.PatrolSpeed); // the path was set when it chose the errand
                return true;
            }
            m_Agent.isStopped = true;
            Face(transform.position + m_ErrandFace, dt * 2f);
            if (Vector3.Angle(Forward, m_ErrandFace) >= 6f) return true;
            if (m_Errand == LootErrand.Haul && m_DropAt is Vector3 at && DropTakenAt(at))
            {
                // someone (another worker, a player) left something there first: another drop, soon
                EndLootErrand();
                m_NextSeek = now + 0.5f;
                return false;
            }
            begin(now);
            return true;
        }

        void StartErrand(LootErrand errand, Vector3 spot, Vector3 face, float until)
        {
            m_Errand = errand;
            m_ErrandSpot = spot;
            m_ErrandFace = face.sqrMagnitude > 1e-4f ? face.normalized : Forward;
            m_ErrandUntil = until;
            MoveTo(spot, Traits.PatrolSpeed);
        }

        // ------------------------------------------------------------------ pick up

        /// <summary>The nearest loot on the floor it can drive straight up to (not in the storage room); starts fetching it.</summary>
        bool Seek(float now)
        {
            var gm = GameManager.Instance;
            var layout = ActiveLevel.Layout;
            s_Candidates.Clear();
            s_CandidateSpots.Clear();
            foreach (var w in WorldItem.All)
            {
                if (w == null || w.Taking || !LootErrands.CanTake(w.Shape)) continue;
                var at = w.transform.position;
                if (gm != null && gm.CanTradeAt(at)) continue; // the team's stash at the hatches is not the house's loot
                if (layout != null && layout.StorageRoom >= 0 && layout.Plan.RoomAt(at + Vector3.up * 0.3f)?.Id == layout.StorageRoom) continue; // stored already
                s_Candidates.Add(w);
                s_CandidateSpots.Add(at);
            }
            var from = transform.position;
            for (int tries = 0; tries < 4 && s_Candidates.Count > 0; tries++)
            {
                int i = LootErrands.Nearest(from, s_CandidateSpots, Traits.LootSeekRadius);
                if (i < 0) break;
                var spot = LootErrands.Approach(from, s_CandidateSpots[i]);
                if (NavMesh.SamplePosition(spot, out var hit, 0.3f, m_Agent.areaMask) && !NavMesh.Raycast(from, hit.position, out _, m_Agent.areaMask))
                {
                    var face = s_CandidateSpots[i] - hit.position;
                    face.y = 0f;
                    m_Fetch = s_Candidates[i];
                    StartErrand(LootErrand.Fetch, hit.position, face, now + 4f + Traits.LootSeekRadius / Mathf.Max(Traits.PatrolSpeed, 0.3f));
                    return true;
                }
                s_Candidates.RemoveAt(i);
                s_CandidateSpots.RemoveAt(i);
            }
            return false;
        }

        void BeginLiftServer(float now) => BeginSwing(SwingKind.Pickup, PulseLift, now);

        /// <summary>The tray lies under the item: the world item goes, the cargo shows on the tray and rises with it.</summary>
        void TakeFetchedServer()
        {
            var item = m_Fetch;
            m_Fetch = null;
            if (item == null || !item.isActiveAndEnabled || item.Taking || Cargo >= 0) return; // a player was quicker
            SetCargo(EncodeCargo(item.Shape, item.Color));
            Session.Despawn(item.gameObject);
        }

        // ------------------------------------------------------------------ haul to the storage room

        /// <summary>A free drop in the storage room it can reach (wheels: its floor only); drives there — the locked door opens for it.</summary>
        bool Haul(float now, LevelLayout layout)
        {
            s_Drops.Clear();
            for (int i = 0; i < layout.StorageDrops.Count; i++) s_Drops.Add(i);
            for (int i = s_Drops.Count - 1; i > 0; i--) { int j = m_LootRng.Next(i + 1); (s_Drops[i], s_Drops[j]) = (s_Drops[j], s_Drops[i]); }
            var room = layout.Plan.Rooms[layout.StorageRoom];
            var path = new NavMeshPath();
            foreach (int i in s_Drops)
            {
                var d = layout.StorageDrops[i];
                if (DropTakenAt(d.Spot)) continue;
                var kind = d.Shelf ? SwingKind.Shelve : SwingKind.PutDown;
                // a shelf is faced square on; a pile spot from the middle of the room
                var face = d.Facing;
                if (face.sqrMagnitude < 1e-4f) face = d.Spot - room.Center;
                face.y = 0f;
                if (face.sqrMagnitude < 0.01f) face = Vector3.forward;
                face.Normalize();
                var stand = d.Spot - face * CargoMotion.StandOff(kind);
                stand.y = LevelLayout.FloorY(room.Floor);
                if (!NavMesh.SamplePosition(stand, out var hit, 0.3f, m_Agent.areaMask)) continue;
                // its way: open floor up to the pile spot and room to roll back from it, or room to roll in to the shelf
                var end = d.Shelf ? hit.position + face * CargoMotion.ShelfScoop : d.Spot;
                end.y = hit.position.y;
                if (NavMesh.Raycast(hit.position, end, out _, m_Agent.areaMask)) continue;
                if (!d.Shelf && NavMesh.Raycast(hit.position, hit.position - face * CargoMotion.Scoop, out _, m_Agent.areaMask)) continue;
                if (!m_Agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                m_DropAt = d.Spot;
                m_Swing = kind;
                StartErrand(LootErrand.Haul, hit.position, face, now + HaulTimeout);
                return true;
            }
            m_NextErrand = now + 5f; // the storage room is full or out of reach: carry it a while longer
            return false;
        }

        static bool DropTakenAt(Vector3 spot)
        {
            foreach (var w in WorldItem.All)
                if (w != null && (w.transform.position - spot).sqrMagnitude < DropTaken * DropTaken) return true;
            return false;
        }

        // ------------------------------------------------------------------ put down

        /// <summary>The floor in front of it is free: navmesh there, no wall between, nothing standing there, not at a hatch, room to roll back.</summary>
        bool CanSetDown()
        {
            var spot = transform.TransformPoint(CargoMotion.FloorSpot);
            if (!NavMesh.SamplePosition(spot, out var hit, 0.3f, NavMesh.AllAreas)) return false;
            if (NavMesh.Raycast(transform.position, hit.position, out _, NavMesh.AllAreas)) return false;
            if (NavMesh.Raycast(transform.position, transform.position - Forward * CargoMotion.Scoop, out _, m_Agent.areaMask)) return false;
            if (Physics.CheckSphere(spot + Vector3.up * 0.25f, 0.18f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
            var gm = GameManager.Instance;
            return gm == null || !gm.CanTradeAt(spot);
        }

        void BeginLowerServer(float now)
        {
            var kind = m_DropAt.HasValue ? m_Swing : SwingKind.PutDown;
            BeginSwing(kind, kind == SwingKind.Shelve ? PulseShelve : PulseLower, now);
        }

        /// <summary>The tray has left the cargo on the floor or the shelf: it becomes a world item there, the tray goes up empty.</summary>
        void FinishLowerServer()
        {
            if (Cargo < 0 || GameManager.Instance == null) return;
            var (shape, color) = DecodeCargo(Cargo);
            SetCargo(-1);
            Vector3 at;
            if (m_DropAt is Vector3 drop) at = drop;
            else
            {
                // it has rolled back part of the way: the loot lies where the tray put it down
                at = transform.TransformPoint(CargoMotion.FloorSpot - Vector3.forward * m_Rolled);
                if (NavMesh.SamplePosition(at, out var hit, 0.3f, NavMesh.AllAreas)) at.y = hit.position.y + CargoMotion.FloorHeight;
            }
            GameManager.Instance.SpawnItemServer(shape, color, at, transform.eulerAngles.y);
        }

        // ------------------------------------------------------------------ the swing

        void BeginSwing(SwingKind kind, byte pulse, float now)
        {
            m_Agent.isStopped = true;
            Pulse(pulse);
            m_Swing = kind;
            m_Errand = LootErrand.Swing;
            m_Touched = false;
            m_Rolled = 0f;
            m_ErrandStart = now;
            m_ErrandUntil = now + CargoMotion.TimeOf(kind, Traits.LootHandleTime);
            m_NextErrand = m_ErrandUntil + Traits.LootErrandCooldown;
        }

        bool Swing(float now, bool patrol)
        {
            // anything else (a suspicion, a chase) cuts the swing short; before the tray touched nothing changes hands —
            // the arms on every peer just finish their swing
            if (!patrol || now >= m_ErrandUntil) { EndLootErrand(); return false; }
            float t = (now - m_ErrandStart) / Mathf.Max(CargoMotion.TimeOf(m_Swing, Traits.LootHandleTime), 0.1f);
            // the tray on the surface: it rolls in or out while the tray slides
            float rolled = CargoMotion.Rolled(t, m_Swing);
            if (!Mathf.Approximately(rolled, m_Rolled))
            {
                m_Agent.Move(Forward * (rolled - m_Rolled));
                m_Rolled = rolled;
            }
            if (!m_Touched && t >= CargoMotion.TouchAt)
            {
                m_Touched = true;
                if (m_Swing == SwingKind.Pickup) TakeFetchedServer();
                else FinishLowerServer();
            }
            m_Agent.isStopped = true;
            return true;
        }

        // ------------------------------------------------------------------ every peer

        void PlayCargoMotion(SwingKind kind)
        {
            var anim = GetComponent<RobotAnimator>();
            if (anim != null) anim.HandleCargo(kind, CargoMotion.TimeOf(kind, Traits.LootHandleTime));
        }
    }
}
