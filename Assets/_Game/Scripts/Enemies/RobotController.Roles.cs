using System.Collections.Generic;
using Stealth.Game;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;
using UnityEngine.AI;

namespace Stealth.Enemies
{
    /// <summary>
    /// The roles and the attacks of the mobile robots (server; the rules are <see cref="RoleRules"/>): the warden's doors,
    /// the repairman's grilles, the smoke machine's and the cleaner's patches, the driller's doors, the mimic's sleep, the
    /// maniac's prey — and what each kind does to the player it has caught up with (a hit, a net, a shot, a sedative,
    /// loud talk, gas, a shriek). Effects every peer shows go out as pulses.
    /// </summary>
    public partial class RobotController
    {
        [Tooltip("The patch this robot leaves (goo, smoke or gas): shown on every peer where it is announced.")]
        public HazardView PatchPrefab;
        [Tooltip("The line of a shot (a robot with a gun): flashes from the muzzle to where it hit.")]
        public LineRenderer Tracer;
        [Tooltip("Where its shots come from.")]
        public Transform Muzzle;

        protected const byte PulsePatch = 7, PulseShriek = 8, PulseNet = 9, PulseGun = 10, PulseTalk = 11, PulseBreak = 12, PulseWork = 13;

        float m_RoleAt;
        int m_RoleUses;
        Vector3? m_RoleErrand;
        System.Action m_RoleErrandDone;
        float m_RoleErrandUntil;
        Vector3 m_Home;
        ulong m_Prey = Nobody;

        /// <summary>Server: the maniac's prey (a client id; <see cref="Detector.Nobody"/>: none).</summary>
        public ulong Prey => m_Prey;
        /// <summary>Server: where it was put into the house (a maniac's lair).</summary>
        public Vector3 Home => m_Home;

        /// <summary>Server, on spawn: the role's start (a mimic goes to sleep where it stands, a lair is remembered).</summary>
        void BeginRoleServer()
        {
            m_Home = transform.position;
            m_RoleAt = Time.time;
            if (Traits.Role == RobotRole.Mimic) m_Brain.SetDock(transform.position, transform.forward, Time.time);
        }

        /// <summary>The kinds of attack a robot makes standing (it does not run into the player's arms).</summary>
        public bool AttacksFromAfar => RoleRules.Ranged(Traits.Attack);

        // ------------------------------------------------------------------ attacks

        /// <summary>Server: what it does to the player it has caught up with (<see cref="RobotTraits.Attack"/>).</summary>
        void AttackServer(IIntruder target)
        {
            var kind = Traits.Attack;
            var from = Muzzle != null ? Muzzle.position : Feet + Vector3.up * 1.1f;
            if (RoleRules.NeedsLine(kind) && Perception.WallBetween(from, Perception.AimPoint(target, 0.6f))) return;
            var player = target as PlayerController;
            switch (kind)
            {
                case AttackKind.Net:
                    if (player == null || player.Netted) return;
                    player.NetServer();
                    Pulse(PulseNet);
                    return;
                case AttackKind.Pester:
                    NoiseSystem.EmitServer(transform.position, Traits.RoleRadius);
                    Pulse(PulseTalk);
                    return;
                case AttackKind.Gas:
                    Pulse(PulsePatch);
                    return;
                case AttackKind.Shriek:
                    foreach (var p in PlayerController.All)
                        if (RoleRules.Reaches(transform.position, p.transform.position, Traits.RoleRadius)) p.StunServer(Traits.RoleDuration);
                    Pulse(PulseShriek);
                    return;
                case AttackKind.Sedate:
                    if (player != null) player.KnockOutServer(Cause);
                    else target.HitServer(transform.position, Cause);
                    break;
                case AttackKind.Shoot:
                    target.HitServer(transform.position, Cause);
                    Pulse(PulseGun);
                    break;
                default:
                    target.HitServer(transform.position, Cause);
                    break;
            }
            // down at once: it takes the body away itself
            if (Traits.CarriesPlayers && player != null && player.CanBeCarried) FetchServer(player);
        }

        // ------------------------------------------------------------------ roles

        /// <summary>Server: the role's part of a frame; true while it drives the body (an errand to a door or a grille).</summary>
        bool UpdateRoleServer(float now, float dt)
        {
            var role = Traits.Role;
            if (role == RobotRole.None) return false;
            if (m_RoleErrand.HasValue) return RunRoleErrand(now, dt);
            bool calm = State == RobotState.Patrol && !Resting;
            switch (role)
            {
                case RobotRole.Smoker:
                    if (calm && RoleRules.Due(now, m_RoleAt, Traits.RolePeriod)) { m_RoleAt = now; Pulse(PulsePatch); }
                    break;
                case RobotRole.Mopper:
                    if (calm && m_Agent.velocity.sqrMagnitude > 0.3f && RoleRules.Due(now, m_RoleAt, Traits.RolePeriod)) { m_RoleAt = now; Pulse(PulsePatch); }
                    break;
                case RobotRole.Warden:
                    if (calm && RoleRules.Due(now, m_RoleAt, Traits.RolePeriod)) { m_RoleAt = now; return SeekDoor(); }
                    break;
                case RobotRole.Repairman:
                    if (calm && RoleRules.Due(now, m_RoleAt, Traits.RolePeriod)) { m_RoleAt = now; return SeekGrille(); }
                    break;
                case RobotRole.Driller:
                    if (State is RobotState.Chase or RobotState.Investigate && RoleRules.Due(now, m_RoleAt, 0.4f)) { m_RoleAt = now; BreakDoors(); }
                    break;
                case RobotRole.Maniac:
                    Stalk(now);
                    break;
            }
            return false;
        }

        /// <summary>Server: walk to a spot, stand a moment, do the thing; given up after a while (a blocked way).</summary>
        void StartRoleErrand(Vector3 at, System.Action done)
        {
            if (NavMesh.SamplePosition(at, out var hit, 1.5f, m_Agent.areaMask)) at = hit.position;
            m_RoleErrand = at;
            m_RoleErrandDone = done;
            m_RoleErrandUntil = Time.time + 25f;
            MoveTo(at, Traits.InvestigateSpeed);
        }

        bool RunRoleErrand(float now, float dt)
        {
            if (State != RobotState.Patrol || now > m_RoleErrandUntil) { EndRoleErrand(); return false; } // noticed someone: that first
            var flat = m_RoleErrand.Value - transform.position;
            flat.y = 0f;
            if (flat.magnitude > 1.3f && !Arrived) { Move(Traits.InvestigateSpeed); return true; }
            Stop();
            var done = m_RoleErrandDone;
            EndRoleErrand();
            done?.Invoke();
            Pulse(PulseWork);
            PatrolToNextPoint();
            return true;
        }

        void EndRoleErrand()
        {
            m_RoleErrand = null;
            m_RoleErrandDone = null;
        }

        static readonly List<Vector3> s_Spots = new();
        static readonly List<AutoDoor> s_Doors = new();
        static readonly List<VentGrille> s_Grilles = new();

        /// <summary>Server, the warden: the nearest door with something to put right — walk over, shut it, set its lock again.</summary>
        bool SeekDoor()
        {
            var gm = GameManager.Instance;
            if (gm == null) return false;
            s_Spots.Clear();
            s_Doors.Clear();
            int left = Traits.RoleLimit <= 0 ? int.MaxValue : Traits.RoleLimit - m_RoleUses;
            foreach (var d in AutoDoor.All)
            {
                if (d == null || d.Index < 0) continue;
                var job = RoleRules.Warden(d.State.Open, d.State.ByRobot, d.HasLock, d.HasLock && !d.Locked, d.Vault || d.Storage || d.NeedsKey, left);
                if (job == DoorJob.None) continue;
                s_Doors.Add(d);
                s_Spots.Add(d.transform.position);
            }
            int i = RoleRules.Nearest(transform.position, s_Spots, Traits.RoleRadius);
            if (i < 0) return false;
            var door = s_Doors[i];
            // (on its own side of the door: a step back from the leaf)
            var side = transform.position - door.transform.position;
            float sign = Vector3.Dot(side, door.transform.forward) >= 0f ? 1f : -1f;
            StartRoleErrand(door.transform.position + door.transform.forward * sign * 1.1f, () =>
            {
                if (door == null) return;
                if (gm.WardenDoorServer(door)) m_RoleUses++;
            });
            return true;
        }

        /// <summary>Server, the repairman: the nearest grille taken off its crawl way — walk over and screw it back on.</summary>
        bool SeekGrille()
        {
            var gm = GameManager.Instance;
            if (gm == null) return false;
            s_Spots.Clear();
            s_Grilles.Clear();
            foreach (var it in Interactable.All)
                if (it is VentGrille g && g.Open)
                {
                    s_Grilles.Add(g);
                    s_Spots.Add(g.transform.position);
                }
            int i = RoleRules.Nearest(transform.position, s_Spots, Traits.RoleRadius);
            if (i < 0) return false;
            var grille = s_Grilles[i];
            StartRoleErrand(grille.transform.position, () => { if (grille != null) gm.ShutGrilleServer(grille.Index); });
            return true;
        }

        /// <summary>Server, the driller: a shut door right in front of it is broken open for good.</summary>
        void BreakDoors()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            foreach (var d in AutoDoor.All)
            {
                if (d == null || d.Index < 0 || d.Vault || d.IsOpen && !d.HasLock) continue;
                var to = d.transform.position - Feet;
                if (Mathf.Abs(to.y) > 1.6f) continue;
                to.y = 0f;
                if (to.magnitude > Traits.RoleRadius || Vector3.Dot(to.normalized, transform.forward) < 0.3f) continue;
                if (gm.BreakDoorServer(d)) Pulse(PulseBreak);
            }
        }

        /// <summary>
        /// Server, the maniac: it has one prey at a time (the first from a seeded place who is up); every period it learns
        /// roughly where they are and goes to look. A prey that went down (it carries its own away) — the next one.
        /// </summary>
        void Stalk(float now)
        {
            var prey = PlayerController.Find(m_Prey);
            if (prey == null || prey.IsDown)
            {
                var all = PlayerController.All;
                if (all.Count == 0) { m_Prey = Nobody; return; }
                var up = new List<bool>(all.Count);
                foreach (var p in all) up.Add(!((IIntruder)p).IsDown);
                int next = RoleRules.NextPrey(up, m_LootRng.Next(all.Count));
                m_Prey = next >= 0 ? all[next].ClientId : Nobody;
                m_RoleAt = now;
                return;
            }
            if (State == RobotState.Patrol && !Hauling && RoleRules.Due(now, m_RoleAt, Traits.RolePeriod))
            {
                m_RoleAt = now;
                // not the spot itself: somewhere near (it searches, the prey hears it coming)
                var near = prey.transform.position + new Vector3(m_LootRng.Range(-1f, 1f), 0f, m_LootRng.Range(-1f, 1f)) * Traits.RoleRadius;
                m_Brain.Investigate(near, false, now);
            }
        }

        /// <summary>Server: a player tried to search the "crate": the mimic wakes, goes for them, and will freeze somewhere else.</summary>
        public void StirServer(PlayerController player)
        {
            if (!IsServer || IsHacked || m_Brain == null || !m_Brain.Resting || Traits.Role != RobotRole.Mimic) return;
            var layout = ActiveLevel.Layout;
            var next = transform.position;
            if (layout != null && layout.PatrolPoints.Count > 0)
            {
                var rng = LevelRandom.Shared(RandomChannel.Patrol);
                for (int k = 0; k < 8; k++)
                {
                    var p = layout.PatrolPoints[rng.Next(layout.PatrolPoints.Count)];
                    if (MayVisit(layout, p) && Vector3.Distance(p, transform.position) > 6f) { next = p; break; }
                }
            }
            m_Brain.Wake(player, next, Time.time);
        }

        // ------------------------------------------------------------------ what every peer shows

        /// <summary>Every peer: the pulses of the roles and the attacks. True if the effect was one of them.</summary>
        bool OnRolePulse(byte effect)
        {
            var bank = Audio.Sfx.Bank;
            switch (effect)
            {
                case PulsePatch:
                {
                    if (PatchPrefab == null) return true;
                    double until = Session.ServerTime + Traits.RoleDuration;
                    var at = Feet;
                    Hazards.Add(PatchPrefab.Kind, at, Traits.RoleRadius, until);
                    Instantiate(PatchPrefab, at, Quaternion.Euler(0f, NetId % 360, 0f)).Show(Traits.RoleRadius, until);
                    Audio.Sfx.At(PatchPrefab.Kind == HazardKind.Goo ? bank?.GooSplat : bank?.GasHiss, Position, 0.8f);
                    return true;
                }
                case PulseShriek:
                    if (RadioRipplePrefab != null)
                    {
                        var ring = Instantiate(RadioRipplePrefab, transform.position, Quaternion.identity);
                        ring.EndRadius = Traits.RoleRadius;
                    }
                    Audio.Sfx.At(bank?.Shriek, Position);
                    return true;
                case PulseNet:
                    Audio.Sfx.At(bank?.NetThrow, Position);
                    return true;
                case PulseGun:
                    Audio.Sfx.At(bank?.GunShot, Position);
                    if (Tracer != null)
                    {
                        Tracer.enabled = true;
                        CancelInvoke(nameof(HideTracer));
                        Invoke(nameof(HideTracer), 0.08f);
                    }
                    return true;
                case PulseTalk:
                    Audio.Sfx.At(bank?.SalesPitch, Position, 1f, Audio.Sfx.Group.Robots, 0.9f + (NetId % 5) * 0.05f);
                    return true;
                case PulseBreak:
                    Audio.Sfx.At(bank?.DoorBreak, Position);
                    return true;
                case PulseWork:
                    Audio.Sfx.At(bank?.LockClack, Position, 0.8f);
                    return true;
            }
            return false;
        }

        void HideTracer()
        {
            if (Tracer != null) Tracer.enabled = false;
        }
    }
}
