using System.Collections.Generic;
using Stealth.Configs;
using Stealth.Enemies;
using Stealth.Game;
using Stealth.Level;
using Stealth.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Stealth.Player
{
    /// <summary>The player: where the player stands: placed by the server, unstuck, into a duct, up the ladder, teleports (see <see cref="PlayerController"/>).</summary>
    public partial class PlayerController
    {
        SewerLocation m_PlacedSewer;

        Vector3? m_ServerPlace;
        float m_ServerPlaceYaw;

        /// <summary>Peers: the owner's identity arrived; the server gives a player who came back their things.</summary>
        public void ApplyIdentity(string identity)
        {
            if (string.IsNullOrEmpty(identity) || identity == Identity) return;
            Identity = identity;
            if (IsServer) GameManager.Instance?.PlayerJoinedServer(this);
        }

        /// <summary>Server: tell the owner where to stand (a reconnect).</summary>
        public void PlaceServer(Vector3 position, float yaw)
        {
            Link.PlaceOwner(position, yaw);
        }

        /// <summary>
        /// Owner: whether this player already stands where the current location put them (the sewer's spawn, the house's
        /// hatch, or the server's spot after a reconnect). Until then the screen stays black (<see cref="UI.LoadingRules"/>).
        /// </summary>
        public bool PlacedHere
        {
            get
            {
                if (m_ServerPlace != null) return false;
                var sewer = SewerLocation.Active;
                if (sewer != null) return m_PlacedSewer == sewer;
                var layout = ActiveLevel.Layout;
                return layout != null && (m_PlacedSeed == layout.Seed || layout.PlayerSpawns.Count == 0);
            }
        }

        /// <summary>Owner: the server's spot wins over the usual spawn, once the house or the sewer is built here.</summary>
        public void PlaceFromServer(Vector3 position, float yaw)
        {
            m_ServerPlace = position;
            m_ServerPlaceYaw = yaw;
        }

        void PlaceOnLevelIfNeeded()
        {
            var sewer = SewerLocation.Active;
            if (m_ServerPlace is Vector3 place && (sewer != null || ActiveLevel.Layout != null))
            {
                m_ServerPlace = null;
                m_PlacedSewer = sewer;
                if (ActiveLevel.Layout != null) m_PlacedSeed = ActiveLevel.Layout.Seed;
                m_PlacedInHatch = InHatch;
                TeleportOwner(place, Quaternion.Euler(0f, m_ServerPlaceYaw, 0f));
                Motor.SetPitch(0f);
                return;
            }
            if (sewer != null && sewer != m_PlacedSewer)
            {
                m_PlacedSewer = sewer;
                int slot = PlayerLooks.SlotOf(ClientId);
                TeleportOwner(sewer.SpawnFor(slot), sewer.SpawnRotation(slot));
                Motor.SetPitch(0f);
                LeaveAllModes();
                return;
            }
            var layout = ActiveLevel.Layout;
            if (layout == null || !ActiveLevel.Ready || layout.Seed == m_PlacedSeed || layout.PlayerSpawns.Count == 0) return;
            m_PlacedSeed = layout.Seed;
            if (SeatIndex >= 0)
            {
                // arriving in the car: the seat holds us, rolling in with it
                m_PlacedInHatch = false;
                Motor.Stamina.Refill();
                Gas.Reset();
                return;
            }
            var spawn = layout.PlayerSpawns[(int)(ClientId % (ulong)layout.PlayerSpawns.Count)];
            var toHatch = layout.HatchPosition - spawn;
            toHatch.y = 0;
            TeleportOwner(spawn + Vector3.up * 0.1f, Quaternion.LookRotation(toHatch.sqrMagnitude > 0.01f ? -toHatch : Vector3.forward));
            m_PlacedInHatch = false; // the team starts in the entry hatch: sit down into it again
            Motor.SetPitch(15f);
            Motor.Stamina.Refill();
            Gas.Reset();
        }

        float m_UnstuckAt = -100f;

        /// <summary>Owner: "I'm stuck" — move to the nearest spot where you stand free (a cooldown keeps it from being an escape).</summary>
        public void RequestUnstuck()
        {
            if (!IsOwner || !IsSpawned) return;
            var gm = GameManager.Instance;
            if (Downed || InHatch) return;
            float wait = m_UnstuckAt + m_Interaction.UnstuckCooldown - Time.time;
            if (wait > 0f)
            {
                gm?.ShowBanner(Loc.F("unstuck.wait", Mathf.CeilToInt(wait)));
                return;
            }
            var fallback = new List<Vector3>();
            var sewer = SewerLocation.Active;
            if (sewer != null)
                foreach (var s in sewer.Spawns) fallback.Add(s.position);
            else if (ActiveLevel.Layout is { } layout)
            {
                int floor = LevelLayout.FloorOf(transform.position.y);
                foreach (var p in layout.PatrolPoints)
                    if (LevelLayout.FloorOf(p.y) == floor) fallback.Add(p);
                fallback.AddRange(layout.PlayerSpawns);
            }
            if (!Unstuck.FindSpot(transform.position, m_Controller.radius, m_Controller.height, fallback, transform, out var spot))
            {
                gm?.ShowBanner(Loc.T("unstuck.none"));
                return;
            }
            m_UnstuckAt = Time.time;
            LeaveAllModes();
            TeleportOwner(spot, transform.rotation);
            Debug.Log($"[Player] Unstuck: moved to {spot}");
        }

        float m_LastFeetY;

        /// <summary>
        /// Owner: a crouched jump up through an open vent grate (from the crates under it): once the feet are nearly at the
        /// duct's floor the body is set on it, crouched (the motor keeps it so under the duct's low top).
        /// </summary>
        void IntoDuctIfJumping()
        {
            var feet = transform.position;
            bool rising = feet.y > m_LastFeetY + 0.0005f;
            m_LastFeetY = feet.y;
            if (Motor.Grounded || Climbing || Downed) return;
            var grate = VentGrate.Under(feet, Motor.IsCrouching, rising);
            if (grate == null) return;
            var at = grate.transform.position;
            TeleportOwner(new Vector3(feet.x, at.y + LevelMetrics.CeilingBoard + 0.03f, feet.z), transform.rotation);
            Motor.ForceCrouch();
        }

        /// <summary>On the ladder between the basement and the lobby right now (<see cref="ShaftLadder"/>).</summary>
        public bool Climbing { get; private set; }

        /// <summary>
        /// Owner, every frame before the motor: the ladder. On it when walking into it facing it (or falling down the open
        /// shaft past it); W up, S down, Jump lets go; off at the floor below, off onto the lobby's floor at the top when the
        /// lid is open (shut, the head stops against it). While on it the motor only turns the view (it is "seated").
        /// </summary>
        void UpdateLadder(in PlayerInputState input, float dt)
        {
            var ladder = ShaftLadder.Main;
            if (ladder == null || Downed || InHatch || SeatIndex >= 0) { Climbing = false; return; }
            var feet = transform.position;
            if (!Climbing)
            {
                var facing = transform.forward;
                facing.y = 0f;
                Climbing = LadderMath.GetsOn(ladder.InZone(feet), input.Move.y, facing.normalized, ladder.Facing, Motor.Grounded, feet.y, ladder.TopY);
                if (!Climbing) return;
            }
            if (input.Jump || !ladder.InZone(feet)) { Climbing = false; return; }
            // W climbs the way the player looks: facing away from the ladder (back to the rungs), W goes down and S up
            var look = transform.forward;
            look.y = 0f;
            float climb = input.Move.y * LadderMath.Direction(look.normalized, ladder.Facing);
            m_Controller.Move(LadderMath.Step(feet, ladder.Column, climb, dt));
            feet = transform.position;
            if (LadderMath.OffAtTop(feet.y, ladder.TopY, World.IsOpened(OpenKeys.Shaft)))
            {
                Climbing = false;
                TeleportOwner(ladder.TopExit, transform.rotation);
            }
            else if (LadderMath.OffAtBottom(climb, m_Controller.isGrounded, feet.y, ladder.Bottom)) Climbing = false;
        }

        public void TeleportOwner(Vector3 position, Quaternion rotation)
        {
            m_Controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            m_Controller.enabled = true;
            if (Motor != null) Motor.ResetMotion();
            Link.Teleport(position, rotation);
            // A teleport is not running: motion and seismic sensors must not feel it (host side; remote jumps are filtered by speed).
            m_ServerLastPos = position;
            ServerSpeed = 0f;
        }
    }
}
