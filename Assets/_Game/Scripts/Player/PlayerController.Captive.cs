using Stealth.Enemies;
using Stealth.Game;
using UnityEngine;

namespace Stealth.Player
{
    /// <summary>
    /// Knocked out and held: a player that lies does not get up alone — a servant carries them to the prison, a renegade
    /// to its own place (a spider's cocoon), a teammate brings them round with E (<see cref="CaptiveRules"/>). The server
    /// owns the state; the owner keeps the body where it is held (on the robot's back, in the place).
    /// </summary>
    public partial class PlayerController
    {
        /// <summary>Server-owned: where the knocked-out player is held (none: lying where they fell, or up and about).</summary>
        public CaptiveKind Captive { get; private set; }
        /// <summary>Server-owned: the robot carrying them (<see cref="Detector.NetId"/>; 0 none).</summary>
        public ulong CaptorId { get; private set; }
        /// <summary>Server-owned: where the body is kept while held in a place.</summary>
        public Vector3 CaptiveSpot { get; private set; }
        /// <summary>Server: who put them down last (<see cref="CaptiveRules.Carried"/>: the servants carry theirs away).</summary>
        public DownCause DownedBy { get; private set; }
        /// <summary>Server: when they fell or were locked up (Time.time): a lone player's way back counts from it.</summary>
        public float HeldSince { get; private set; }
        /// <summary>Server: a carrier is on its way to this body (one at a time).</summary>
        public RobotController Fetcher { get; set; }

        CaptiveSettings m_CaptiveSettings = new();

        [Header("Held")]
        [Tooltip("The spider's cocoon round the player (shown while held in one).")]
        public GameObject CocoonView;
        [Tooltip("The drone's net over the player (shown while netted).")]
        public GameObject NetView;

        /// <summary>Every peer: the cocoon and the net show while the player is in them.</summary>
        void ShowHeld()
        {
            bool cocoon = Captive == CaptiveKind.Cocoon, net = Netted && !Downed;
            if (CocoonView != null && CocoonView.activeSelf != cocoon) CocoonView.SetActive(cocoon);
            if (NetView != null && NetView.activeSelf != net) NetView.SetActive(net);
        }

        /// <summary>A teammate can bring this player round with E right now.</summary>
        public bool CanBeFreed => Downed && CaptiveRules.CanFree(Captive);

        /// <summary>Seconds of E it takes to bring <paramref name="other"/> round.</summary>
        public float FreeTimeOf(PlayerController other) =>
            CaptiveRules.FreeTime(other != null ? other.Captive : CaptiveKind.None, m_Interaction.ReviveTime, m_CaptiveSettings);

        /// <summary>Client: where the server holds this player.</summary>
        public void ApplyCaptivity(CaptiveKind kind, ulong captor, Vector3 spot)
        {
            Captive = kind;
            CaptorId = captor;
            CaptiveSpot = spot;
        }

        /// <summary>The robot carrying this player on this peer, or null.</summary>
        public Detector Captor
        {
            get
            {
                if (Captive != CaptiveKind.Carried) return null;
                foreach (var d in Detector.All) if (d != null && d.NetId == CaptorId) return d;
                return null;
            }
        }

        /// <summary>Where a robot holds the body it carries: over its back.</summary>
        public static Vector3 CarryPoint(Detector robot) => robot.transform.position + Vector3.up * 0.9f - robot.transform.forward * 0.25f;

        /// <summary>
        /// Owner: held, the body stays where it is held — on the robot's back while carried, at the spot in a place; the
        /// motor does not run. True while held.
        /// </summary>
        bool HoldCaptive()
        {
            if (!CaptiveRules.Held(Captive)) return false;
            var at = CaptiveSpot;
            if (Captive == CaptiveKind.Carried)
            {
                var robot = Captor;
                if (robot == null) return true; // (not spawned here yet: stay put)
                at = CarryPoint(robot);
            }
            m_Controller.enabled = false; // (no step-up, no pushing out of the robot's capsule)
            transform.position = at;
            m_Controller.enabled = true;
            return true;
        }

        // ------------------------------------------------------------------ server

        /// <summary>Server: a robot lifts the lying player on its back. False if they are not there to be lifted.</summary>
        public bool PickUpServer(Detector captor)
        {
            if (!IsServer || captor == null || !KnockedOut || Captive != CaptiveKind.None) return false;
            Captive = CaptiveKind.Carried;
            CaptorId = captor.NetId;
            Fetcher = null;
            return true;
        }

        /// <summary>Server: the robot carrying them was stopped (hacked, stunned) — they lie where it dropped them.</summary>
        public void DropServer()
        {
            if (!IsServer || Captive != CaptiveKind.Carried) return;
            Captive = CaptiveKind.None;
            CaptorId = 0;
            HeldSince = Time.time;
            DownedBy = DownCause.Self; // put down once: nobody comes for the body again, the team has its chance
        }

        /// <summary>Server: locked up in a place (the prison, a cocoon, the ward, a lair); the body is moved there.</summary>
        public void ImprisonServer(CaptiveKind kind, Vector3 at)
        {
            if (!IsServer || !KnockedOut || kind is CaptiveKind.None or CaptiveKind.Carried) return;
            Captive = kind;
            CaptorId = 0;
            CaptiveSpot = at;
            HeldSince = Time.time;
            Fetcher = null;
            GameManager.Instance?.RaiseDangerServer(DangerEvent.Capture);
        }

        /// <summary>Server: brought round (a teammate, or a lone player's own way back): free of whatever held them, on their feet.</summary>
        public void FreeServer()
        {
            if (!IsServer) return;
            Captive = CaptiveKind.None;
            CaptorId = 0;
            Fetcher = null;
            m_Health.Revive(Time.time);
            PublishHealth();
        }

        void ClearCaptivityServer()
        {
            Captive = CaptiveKind.None;
            CaptorId = 0;
            Fetcher = null;
            DownedBy = DownCause.Self;
        }
    }
}
