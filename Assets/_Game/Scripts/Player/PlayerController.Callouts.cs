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
    /// <summary>The player: pings, marks and the hatch (see <see cref="PlayerController"/>).</summary>
    public partial class PlayerController
    {
        // ------------------------------------------------------------------ callouts: pings and marks

        TeamSettings Team => Tuning.Player.Team;

        /// <summary>
        /// Owner: the ping key calls "here!" where the crosshair points; the mark key marks the robot / device in the
        /// crosshair for the whole team (or pings the spot when there is none).
        /// </summary>
        void UpdateCallouts()
        {
            // knocked out: call the team to the body (seen through walls by everyone for a while)
            if (KnockedOut && !GameMenu.IsOpen && Controls.Pressed(GameAction.CallHelp) && GameManager.Instance != null)
            {
                Link.SendPing(transform.position + Vector3.up * 0.4f);
                return;
            }
            // Knocked out: still a teammate — pings and marks from the eyes of the one being watched.
            bool canCall = !Downed || Watching != null;
            if (GameMenu.IsOpen || !canCall || CursorMode || Camera == null || GameManager.Instance == null) return;
            bool ping = Controls.Pressed(GameAction.Ping), mark = Controls.Pressed(GameAction.Mark);
            if (ping || mark) Callout(mark);
        }

        int[] m_Layers;
        bool m_ShownCalling;

        /// <summary>
        /// Every peer: a player calling for help (<see cref="PingKind.Help"/>) is drawn through walls like a marked robot — their
        /// body on the Marked layer — while the call lasts.
        /// </summary>
        void ShowCalling()
        {
            var gm = GameManager.Instance;
            bool calling = gm != null && gm.Pings.Calling(ClientId);
            if (calling == m_ShownCalling || Visual == null) return;
            m_ShownCalling = calling;
            var rs = Visual.GetComponentsInChildren<Renderer>(true);
            if (calling) { m_Layers = new int[rs.Length]; for (int i = 0; i < rs.Length; i++) m_Layers[i] = rs[i].gameObject.layer; }
            for (int i = 0; i < rs.Length; i++)
                rs[i].gameObject.layer = calling ? Stealth.Enemies.Detector.XrayLayer : (m_Layers != null && i < m_Layers.Length ? m_Layers[i] : 0);
        }

        /// <summary>Owner: ping where the crosshair points, or mark the robot in it (falls back to a ping). Also a test hook.</summary>
        public void Callout(bool mark)
        {
            if (Camera == null) return;
            var eye = Camera.transform.position;
            var forward = Camera.transform.forward;
            if (UsingTerminal != null && TerminalViewPose(out var at, out var rotation))
            {
                eye = at;
                forward = rotation * Vector3.forward;
            }
            var view = (position: eye, forward: forward);
            if (mark)
            {
                var target = MarkTarget(view.position, view.forward, Team.MarkRange, UsingTerminal != null ? TerminalView : null);
                if (target != null)
                {
                    Link.SendMark(target);
                    return;
                }
                // not a robot: a thing (loot, a crate, a panel…) — marked for the team in yellow-green
                if (MarkedThings.Target(view.position, view.forward, Team.MarkRange, out var point) != null)
                {
                    Link.SendMarkThing(point);
                    return;
                }
            }
            if (!Physics.Raycast(view.position, view.forward, out var hit, Team.PingRange, Perception.SightMask, QueryTriggerInteraction.Ignore)) return;
            var spot = hit.point + hit.normal * 0.05f;
            Link.SendPing(spot);
        }

        /// <summary>
        /// The robot / device the player aims at: the crosshair ray hits its body, or it is within a few degrees of the
        /// view and in plain sight (small devices are hard to hit exactly).
        /// </summary>
        public static Detector MarkTarget(Vector3 eye, Vector3 forward, float range, Detector except = null)
        {
            if (Physics.Raycast(eye, forward, out var direct, range, Perception.SightMask, QueryTriggerInteraction.Ignore))
            {
                var hit = direct.collider.GetComponentInParent<Detector>();
                if (hit != null && !hit.IsHacked && hit != except) return hit;
            }
            Detector best = null;
            float bestAngle = 5f;
            foreach (var d in Detector.All)
            {
                var to = d.Position - eye;
                float dist = to.magnitude;
                if (dist > range || dist < 0.01f || d.IsHacked || d == except) continue;
                float angle = Vector3.Angle(forward, to);
                if (angle >= bestAngle) continue;
                if (Physics.Raycast(eye, to / dist, out var block, dist - 0.6f, Perception.SightMask, QueryTriggerInteraction.Ignore)
                    && block.collider.GetComponentInParent<Detector>() != d) continue;
                best = d;
                bestAngle = angle;
            }
            return best;
        }

        /// <summary>Server: the owner hides in the hatch or climbs out of it.</summary>
        public void OnHatchRequest(bool hide)
        {
            var gm = GameManager.Instance;
            if (!hide) { InHatch = false; return; }
            if (Downed || gm == null || gm.Phase != RunPhase.Playing) return;
            int hatch = Hatch.IndexNear(transform.position);
            if (hatch < 0) return;
            InHatch = true;
            HatchIndex = hatch;
        }

        /// <summary>Server: the team left the house (or a new one starts).</summary>
        public void LeaveHatchServer() => InHatch = false;

        /// <summary>Server: a house starts with the team sitting in its entry hatch.</summary>
        public void EnterHatchServer(int hatch)
        {
            InHatch = true;
            HatchIndex = hatch;
        }
    }
}
