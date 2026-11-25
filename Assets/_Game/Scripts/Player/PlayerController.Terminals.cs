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
    /// <summary>The player: security terminals (see <see cref="PlayerController"/>).</summary>
    public partial class PlayerController
    {
        // ------------------------------------------------------------------ security terminals

        /// <summary>Owner: the terminal being used (null = none) and the robot / camera on its screen.</summary>
        public SecurityTerminal UsingTerminal { get; private set; }

        /// <summary>Server: the terminal this player sits at (-1 none): cameras do not notice them, marks come from the screen.</summary>
        public int AtTerminal { get; private set; } = -1;
        SecurityCamera m_Steered;

        public void TerminalServer(int terminal, Detector steered, float yaw)
        {
            AtTerminal = terminal;
            var camera = terminal >= 0 ? steered as SecurityCamera : null;
            if (m_Steered != null && m_Steered != camera) m_Steered.ReleaseServer(this);
            m_Steered = camera;
            if (camera != null) camera.SteerServer(this, yaw);
        }

        SecurityCamera m_SteerCamera;
        float m_SteerYaw, m_TerminalSent;

        /// <summary>Owner at a camera terminal: the mouse and A/D turn the camera on the screen; its yaw goes to the server.</summary>
        void SteerTerminalCamera(Detector view)
        {
            var camera = view as SecurityCamera;
            if (camera != m_SteerCamera)
            {
                m_SteerCamera = camera;
                m_SteerYaw = camera != null ? camera.CurrentYaw : 0f;
            }
            if (camera != null)
            {
                float keys = (Controls.Held(GameAction.Right) ? 1f : 0f) - (Controls.Held(GameAction.Left) ? 1f : 0f);
                var mouse = Mouse.current;
                float mouseX = mouse != null ? mouse.delta.ReadValue().x * GameSettings.Sensitivity : 0f;
                m_SteerYaw = Mathf.Clamp(m_SteerYaw + keys * 70f * Time.deltaTime + mouseX, -SecurityCamera.SteerLimit, SecurityCamera.SteerLimit);
                camera.PreviewAim(m_SteerYaw);
            }
            SendTerminalState(UsingTerminal != null ? UsingTerminal.Index : -1, camera, m_SteerYaw, false);
        }

        void SendTerminalState(int terminal, Detector steered, float yaw, bool now)
        {
            if (!now && Time.time < m_TerminalSent) return;
            m_TerminalSent = Time.time + 0.1f;
            Link.SendTerminal(terminal, steered, yaw);
        }
        public Detector TerminalView { get; private set; }
        /// <summary>Owner at a terminal: what it shows (the cameras, or the hacking terminal's list) and which one is picked.</summary>
        public IReadOnlyList<Detector> TerminalTargets => m_TerminalTargets;
        public int TerminalIndex => m_TerminalIndex;
        public int TerminalViewNumber => m_TerminalIndex + 1;
        public int TerminalViewCount => m_TerminalTargets.Count;
        /// <summary>Owner: a terminal in reach (press E to use it), null if none.</summary>
        public SecurityTerminal TerminalTarget { get; private set; }
        /// <summary>Owner at a map terminal: the floor its map shows (A/D, the mouse buttons or the wheel flip it).</summary>
        public int TerminalFloor { get; private set; }
        /// <summary>0..1 of holding E at a hacking terminal.</summary>
        public float TerminalHackShare => m_TerminalHold / Mathf.Max(0.05f, m_Interaction.TerminalHackTime);

        readonly List<Detector> m_TerminalTargets = new();
        int m_TerminalIndex;
        float m_TerminalHold;
        /// <summary>How long E has been down at the terminal (−1: this press does not count — it sat us down, or it hacked).</summary>
        float m_TerminalPress = -1f;

        /// <summary>E let go after a short press: the way out of a terminal (pure but for its one field; <see cref="TerminalRules.Tap"/> is the length).</summary>
        bool TappedToLeave(bool held, float dt)
        {
            if (held)
            {
                if (m_TerminalPress >= 0f) m_TerminalPress += dt;
                return false;
            }
            bool tap = TerminalRules.IsTap(m_TerminalPress);
            m_TerminalPress = 0f;
            return tap;
        }

        /// <summary>Owner: Esc pressed while at a terminal, a safe's dial or a vault console — it leaves that (true), the pause menu stays shut.</summary>
        public bool LeaveModeOnEscape()
        {
            if (UsingTerminal == null && Cracking == null && AtConsole == null) return false;
            LeaveAllModes();
            return true;
        }
        bool m_TerminalWaitRelease;

        /// <summary>A key of the terminal pressed: sitting down at it, another camera, another floor.</summary>
        void TerminalKeySound() => Audio.Sfx.At(Audio.Sfx.Bank != null ? Audio.Sfx.Bank.TerminalKey : null, Hands, 0.7f, Audio.Sfx.Group.Items);

        /// <summary>
        /// Owner: E at a terminal sits down at it. A/D (or the mouse buttons) flip through what it shows, Space or S stands up.
        /// At a hacking terminal holding E hacks the robot on the screen (a charge of the terminal is spent on the server).
        /// </summary>
        void UpdateTerminal(bool holdE)
        {
            if (UsingTerminal == null)
            {
                TerminalTarget = ReviveTarget == null && HackTarget == null && DoorTarget == null && SafeTarget == null && Cracking == null && StorageTarget == null
                    ? SecurityTerminal.Near(transform.position, Hands, SecurityTerminal.Reach, View) : null;
                if (TerminalTarget != null && TerminalTarget.Kind == TerminalKind.Doors) TerminalTarget = null; // nothing to sit at: hold E to open the door
                if (TerminalTarget == null || CursorMode || !Controls.Pressed(GameAction.Interact)) return;
                StartTerminal(TerminalTarget);
                TerminalKeySound();
                return;
            }
            var t = UsingTerminal;
            var to = t != null ? transform.position - t.transform.position : Vector3.positiveInfinity;
            to.y = 0;
            // E again leaves — a tap of it: a hold is the hacking terminal's hack (Esc leaves too: LeaveModeOnEscape). The
            // jump and back keys no longer do: a finger on them kicked one out.
            if (t == null || to.magnitude > SecurityTerminal.Reach + 0.6f || TappedToLeave(Controls.Held(GameAction.Interact), Time.unscaledDeltaTime))
            {
                StopTerminal();
                return;
            }
            var mouse = Mouse.current;
            if (t.Kind == TerminalKind.Map)
            {
                // the map: no picture to look through, the floors to flip
                TerminalView = null;
                m_TerminalTargets.Clear();
                SendTerminalState(t.Index, null, 0f, false);
                float scroll = mouse != null ? mouse.scroll.ReadValue().y : 0f;
                int step = (Controls.Pressed(GameAction.Right) || (mouse != null && mouse.leftButton.wasPressedThisFrame) || scroll > 0f ? 1 : 0)
                         - (Controls.Pressed(GameAction.Left) || (mouse != null && mouse.rightButton.wasPressedThisFrame) || scroll < 0f ? 1 : 0);
                int floors = Mathf.Max(1, ActiveLevel.Layout != null ? ActiveLevel.Layout.Floors : 1);
                if (step != 0) TerminalKeySound();
                TerminalFloor = Mathf.Clamp(TerminalFloor + step, 0, floors - 1);
                return;
            }
            SecurityTerminal.Targets(t.Kind, t.transform.position, m_TerminalTargets);
            if (m_TerminalTargets.Count == 0) { TerminalView = null; SteerTerminalCamera(null); return; }
            // cameras: A/D and the mouse turn the camera, the mouse buttons and the wheel switch it; robots: A/D switch too
            bool steer = t.Kind == TerminalKind.Cameras;
            float wheel = mouse != null ? mouse.scroll.ReadValue().y : 0f;
            int shown = m_TerminalIndex;
            if ((!steer && Controls.Pressed(GameAction.Right)) || (mouse != null && mouse.leftButton.wasPressedThisFrame) || wheel < 0f) m_TerminalIndex++;
            if ((!steer && Controls.Pressed(GameAction.Left)) || (mouse != null && mouse.rightButton.wasPressedThisFrame) || wheel > 0f) m_TerminalIndex--;
            if (m_TerminalIndex != shown) TerminalKeySound();
            int n = m_TerminalTargets.Count;
            m_TerminalIndex = (m_TerminalIndex % n + n) % n;
            var view = m_TerminalTargets[m_TerminalIndex];
            if (view != TerminalView) m_TerminalHold = 0f;
            TerminalView = view;
            SteerTerminalCamera(steer ? view : null);
            if (!holdE) m_TerminalWaitRelease = false;
            bool hacking = t.Kind == TerminalKind.Hacking && t.Charges > 0 && holdE && !m_TerminalWaitRelease && !view.IsHacked;
            if (!hacking) { m_TerminalHold = 0f; return; }
            m_TerminalHold += Time.deltaTime;
            if (m_TerminalHold < m_Interaction.TerminalHackTime) return;
            m_TerminalHold = 0f;
            m_TerminalWaitRelease = true;
            m_TerminalPress = -1f; // this press was a hack, not a tap
            Link.SendRemoteHack(view, t.Index);
        }

        /// <summary>Owner: sit down at a terminal (also a test hook: <paramref name="waitRelease"/> false = E already counts).</summary>
        public void StartTerminal(SecurityTerminal terminal, bool waitRelease = true)
        {
            UsingTerminal = terminal;
            m_SteerCamera = null;
            SendTerminalState(terminal.Index, null, 0f, true);
            TerminalView = null;
            m_TerminalIndex = 0;
            m_TerminalHold = 0f;
            m_TerminalWaitRelease = waitRelease; // the E that sat us down is not a hack
            m_TerminalPress = waitRelease ? -1f : 0f; // …nor a tap to leave
            TerminalFloor = Mathf.Max(0, LevelLayout.FloorOf(transform.position.y)); // the map opens on this floor
        }

        /// <summary>Test hook: the next thing on the terminal's screen (as if D was pressed).</summary>
        public void NextTerminalViewForTest() => m_TerminalIndex++;

        /// <summary>Owner: stand up from the terminal; the camera is ours again.</summary>
        public void StopTerminal()
        {
            if (ReferenceEquals(UsingTerminal, null)) return;
            UsingTerminal = null;
            TerminalView = null;
            m_SteerCamera = null;
            SendTerminalState(-1, null, 0f, true);
            m_TerminalHold = 0f;
            if (Camera == null) return;
            Camera.transform.localPosition = Vector3.zero;
            Camera.transform.localRotation = Quaternion.identity;
        }

        /// <summary>Owner at a terminal: the camera looks through the security camera on the screen, or at the robot from a clear side.</summary>
        void ShowTerminalView()
        {
            if (TerminalViewPose(out var at, out var rotation)) Camera.transform.SetPositionAndRotation(at, rotation);
        }

        /// <summary>Where the terminal's picture is seen from (also where pings and marks come from while sitting at it).</summary>
        bool TerminalViewPose(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            var d = TerminalView;
            if (d == null) return false;
            // the hacking terminal is a list on its screen: nothing seen through the robots, the player's own eyes stay
            if (UsingTerminal != null && UsingTerminal.Kind == TerminalKind.Hacking) return false;
            if (d is SecurityCamera cam && cam.Pivot != null)
            {
                // just in front of the lens: the camera's own housing and glow stay behind the view
                position = d.Position + cam.Pivot.forward * 0.4f;
                rotation = cam.Pivot.rotation;
                return true;
            }
            // A robot: from behind (or the first side with no wall in between), a little above it and under the ceiling.
            var target = d.Position;
            float ceiling = LevelLayout.FloorY(LevelLayout.FloorOf(d.transform.position.y)) + LevelMetrics.WallHeight - 0.35f;
            var forward = Vector3.ProjectOnPlane(d.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            var right = Vector3.Cross(Vector3.up, forward);
            Vector3 At(Vector3 dir)
            {
                var at = target + dir.normalized * 2.2f + Vector3.up * 0.6f;
                at.y = Mathf.Min(at.y, ceiling);
                return at;
            }
            Vector3 best = At(-forward);
            foreach (var dir in new[] { -forward, -forward + right, -forward - right, right, -right, forward })
            {
                var at = At(dir);
                if (Physics.Linecast(target + Vector3.up * 0.1f, at, Perception.SightMask, QueryTriggerInteraction.Ignore)) continue;
                best = at;
                break;
            }
            position = best;
            rotation = Quaternion.LookRotation(target - best);
            return true;
        }

        /// <summary>
        /// Knocked out with a teammate still standing: the camera rides with them (their position, yaw and the synced head
        /// pitch); the mouse buttons switch between teammates. Back on your feet, the camera is yours again.
        /// </summary>
        void UpdateWatching()
        {
            s_Mates.Clear();
            var gm = GameManager.Instance;
            if (KnockedOut && gm != null && gm.Phase == RunPhase.Playing)
                foreach (var p in All)
                    if (p != this && p.IsSpawned && !p.KnockedOut) s_Mates.Add(p);
            if (s_Mates.Count == 0)
            {
                if (Watching == null) return;
                Watching = null;
                Camera.transform.localPosition = Vector3.zero;
                Camera.transform.localRotation = Quaternion.identity;
                return;
            }
            var mouse = Mouse.current;
            if (mouse != null && !GameMenu.IsOpen)
            {
                if (mouse.leftButton.wasPressedThisFrame) m_WatchIndex++;
                if (mouse.rightButton.wasPressedThisFrame) m_WatchIndex--;
            }
            var mate = s_Mates[(m_WatchIndex % s_Mates.Count + s_Mates.Count) % s_Mates.Count];
            float eye = mate.InHatch ? 0.25f : mate.Downed ? 0.35f : mate.Crouching ? 0.95f : 1.6f;
            var look = mate.transform.rotation * Quaternion.Euler(mate.LookPitch, mate.ShownLookYaw, 0f); // (their head turned off the held body too)
            m_WatchRotation = Watching == mate ? Quaternion.Slerp(m_WatchRotation, look, 1f - Mathf.Exp(-12f * Time.deltaTime)) : look;
            Watching = mate;
            // A little in front of their face: the camera does not look out from inside their head.
            var pos = mate.transform.position + Vector3.up * eye + m_WatchRotation * Vector3.forward * 0.18f;
            Camera.transform.SetPositionAndRotation(pos, m_WatchRotation);
        }

        /// <summary>
        /// Server: a callout is in range of this player — or, while knocked out, of a teammate still standing (it is
        /// made through their eyes).
        /// </summary>
        bool WithinCallRange(Vector3 position, float range)
        {
            if ((position - transform.position).magnitude <= range) return true;
            if (!KnockedOut) return false;
            foreach (var p in All)
                if (p != this && !p.KnockedOut && (position - p.transform.position).magnitude <= range) return true;
            return false;
        }

        public void OnPingRequest(Vector3 position)
        {
            var gm = GameManager.Instance;
            if (gm == null || (AtTerminal < 0 && !WithinCallRange(position, Team.PingRange + 5f))) return;
            // knocked out, calling at their own body: a call for help (the team sees them through walls)
            bool help = KnockedOut && (position - transform.position).magnitude < 1.5f;
            gm.CalloutServer(ClientId, help ? PingKind.Help : PingKind.Here, position, null);
        }

        /// <summary>Server: the owner marked a thing (every peer finds it at this point and lights it through walls).</summary>
        public void OnMarkThingRequest(Vector3 point)
        {
            var gm = GameManager.Instance;
            if (gm == null || (AtTerminal < 0 && !WithinCallRange(point, Team.MarkRange + 5f))) return;
            gm.CalloutServer(ClientId, PingKind.Thing, point, null);
        }

        /// <summary>Server: the owner marked a robot / device.</summary>
        public void OnMarkRequest(Detector target)
        {
            var gm = GameManager.Instance;
            if (gm == null || target == null || target.IsHacked) return;
            // at a terminal the player looks through the house's cameras: the mark comes from there, not from their eyes
            if (AtTerminal < 0 && !WithinCallRange(target.Position, Team.MarkRange + 5f)) return;
            gm.CalloutServer(ClientId, PingKind.Enemy, target.Position, target);
        }

        /// <summary>
        /// Where the player looks: E works on what is under the crosshair, not on whatever is nearest (null for the
        /// autopilot that holds E, <see cref="DevFlags.AutoInteract"/>: it uses whatever is in reach).
        /// </summary>
        public Aim? View => DevFlags.AutoInteract ? null
            : Camera != null ? new Aim(Camera.transform.position, Camera.transform.forward) : new Aim(Hands, transform.forward);

        /// <summary>Where the player reaches from (chest height): hacking and cracking need a clear line from here.</summary>
        public Vector3 Hands => transform.position + Vector3.up * (Height * 0.7f);

        /// <summary>
        /// Interactive objects are used by looking right at them (<see cref="LookTarget"/>); the autopilot (tests) and a
        /// player without a camera still use the old "anything within reach".
        /// </summary>
        bool UseLook => Camera != null && !DevFlags.AutoInteract;

        /// <summary>The <typeparamref name="T"/> under the crosshair within <see cref="InteractionSettings.LookReach"/>, or null.</summary>
        T Aimed<T>() where T : Component => LookTarget.At<T>(Camera.transform, transform, m_Interaction.LookReach, m_Interaction.LookRadius);

        /// <summary>The robot / device to hack: the one looked at (or, for the autopilot, the nearest in reach).</summary>
        Detector FindHackTarget()
        {
            if (UseLook)
            {
                var looked = Aimed<Detector>();
                if (looked != null) return looked.CanBeHackedFrom(transform.position, HackReach) && looked.ReachableFrom(Hands) ? looked : null;
                // devices without a body to hit (cameras, sensors, plates): the one closest to the crosshair, if close enough
                Detector bestLook = null;
                float bestAngle = m_Interaction.LookAngle;
                var eye = Camera.transform;
                foreach (var d in Detector.All)
                {
                    if (!d.CanBeHackedFrom(transform.position, HackReach) || !d.ReachableFrom(Hands)) continue;
                    float angle = Vector3.Angle(eye.forward, d.Position - eye.position);
                    if (angle < bestAngle) { bestLook = d; bestAngle = angle; }
                }
                return bestLook;
            }
            Detector best = null;
            float bestDist = float.MaxValue;
            foreach (var d in Detector.All)
            {
                if (!d.CanBeHackedFrom(transform.position, HackReach) || (View.HasValue && !View.Value.At(d.transform.position + Vector3.up * 0.1f, d.Position, 0.45f)) ||
                    !d.ReachableFrom(Hands)) continue;
                float dist = (d.transform.position - transform.position).sqrMagnitude;
                if (dist < bestDist) { best = d; bestDist = dist; }
            }
            return best;
        }
    }
}
