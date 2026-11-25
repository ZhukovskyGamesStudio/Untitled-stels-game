using Stealth.Configs;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// The raccoon's body, on every peer, from what the server says it does (<see cref="StorageBot.Motion"/>) and how it
    /// moves (its place is replicated): the animator's takes (<see cref="StorageBotRules.TakeOf"/>) — idle, walk and bound
    /// blended by the floor speed, the ladder's climb at the climbing speed (backwards on the way down), the leap up into
    /// a grate and the drop out of one played to their end, the reaction to a pat, curled up asleep in the trunk, sat down
    /// where it was called (turning its head to the nearest player within the neck's reach, <see cref="StorageBotRules.HeadTurn"/>);
    /// on the floor its paws are set on the real floor under it (the navmesh lies a little above it, <see cref="StorageBotRules.Ground"/>);
    /// crawling in a duct it is pressed flat and the team sees it through the ceilings, outlined (its renderers on
    /// <see cref="XrayLayer"/>, drawn by the x-ray passes like a marked robot, in its own colour).
    /// </summary>
    public class StorageBotAnimator : MonoBehaviour
    {
        [Tooltip("The model (pressed flat in a duct, set down onto the floor).")]
        public Transform Model;
        [Tooltip("The raccoon's animator (Data/Raccoon/Raccoon.controller: states Move, Climb, VentUp, VentDown, Pet, Curl, Sit).")]
        public Animator Animator;
        [Tooltip("The bones \"neck\" and \"head\": turned to the nearest player on top of the Sit take.")]
        public Transform Neck, Head;

        /// <summary>Layer of Linus's renderers while he crawls in a duct: the renderer draws them through walls (feature "MuleXray").</summary>
        public const int XrayLayer = 12;

        static readonly int SpeedId = Animator.StringToHash("Speed"), StrideId = Animator.StringToHash("Stride"), ClimbId = Animator.StringToHash("ClimbSpeed");

        StorageBot m_Bot;
        Vector3 m_LastPos, m_Velocity, m_BaseScale = Vector3.one, m_BaseLocal;
        Vector2 m_Turn;
        float m_Flat, m_PetUntil, m_HoldUntil, m_Ground;
        int m_Pets;
        StorageBotRules.Take m_Take = StorageBotRules.Take.Move;
        Renderer[] m_Renderers;
        int[] m_Layers;
        bool m_Xray;

        void Awake()
        {
            m_Bot = GetComponent<StorageBot>();
            m_LastPos = transform.position;
            if (Model == null) return;
            m_BaseScale = Model.localScale;
            m_BaseLocal = Model.localPosition;
            m_Renderers = Model.GetComponentsInChildren<Renderer>(true);
            m_Layers = new int[m_Renderers.Length];
            for (int i = 0; i < m_Renderers.Length; i++) m_Layers[i] = m_Renderers[i].gameObject.layer;
        }

        /// <summary>Outlined through the ceilings while in a duct (or on its way up into one and down out of it).</summary>
        void Xray(bool on)
        {
            if (on == m_Xray || m_Renderers == null) return;
            m_Xray = on;
            for (int i = 0; i < m_Renderers.Length; i++)
                if (m_Renderers[i] != null) m_Renderers[i].gameObject.layer = on ? XrayLayer : m_Layers[i];
        }

        void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var v = (transform.position - m_LastPos) / dt;
            m_LastPos = transform.position;
            m_Velocity = Vector3.Lerp(m_Velocity, v, 1f - Mathf.Exp(-10f * dt));
            float speed = new Vector2(m_Velocity.x, m_Velocity.z).magnitude;
            if (m_Bot != null && m_Bot.Pets != m_Pets)
            {
                m_Pets = m_Bot.Pets;
                m_PetUntil = Time.time + StorageBotRules.PetTime;
            }
            var motion = m_Bot != null ? m_Bot.Motion : BotMotion.Floor;
            bool petted = Time.time < m_PetUntil;
            if (Animator != null)
            {
                Animator.SetFloat(SpeedId, speed, 0.12f, dt);
                Animator.SetFloat(StrideId, StorageBotRules.Stride(speed));
                Animator.SetFloat(ClimbId, StorageBotRules.ClimbRate(m_Velocity.y));
                var take = StorageBotRules.TakeOf(motion, petted);
                // the leap and the drop play out to their end (the body has landed or gone by then)
                if (take != m_Take && Time.time >= m_HoldUntil)
                {
                    m_Take = take;
                    float fade = take is StorageBotRules.Take.VentUp or StorageBotRules.Take.VentDown ? 0.06f : take == StorageBotRules.Take.Sit ? 0.4f : 0.2f;
                    Animator.CrossFadeInFixedTime(take.ToString(), fade);
                    m_HoldUntil = take switch
                    {
                        StorageBotRules.Take.VentUp => Time.time + StorageBotRules.LeapCrouch + StorageBotRules.LeapTime,
                        StorageBotRules.Take.VentDown => Time.time + StorageBotRules.DropHang + StorageBotRules.DropTime + 0.3f,
                        _ => 0f,
                    };
                }
            }
            Xray(motion == BotMotion.Duct || (m_Bot != null && m_Bot.InDuct));
            // in a duct: pressed flat
            m_Flat = Mathf.MoveTowards(m_Flat, motion == BotMotion.Duct ? 1f : 0f, dt * 3f);
            bool onFloor = motion is BotMotion.Floor or BotMotion.Sit;
            m_Ground = Mathf.Lerp(m_Ground, onFloor ? StorageBotRules.Ground(transform.position.y, FloorUnder()) : 0f, 1f - Mathf.Exp(-12f * dt));
            if (Model != null)
            {
                Model.localScale = Vector3.Scale(m_BaseScale, new Vector3(1f + m_Flat * 0.06f, 1f - m_Flat * 0.2f, 1f + m_Flat * 0.06f));
                Model.localPosition = m_BaseLocal + Vector3.up * m_Ground;
            }
            TurnHead(motion == BotMotion.Sit && !petted, dt);
        }

        /// <summary>The floor's height right under its middle (colliders, not the navmesh), or null: nothing within a step.</summary>
        float? FloorUnder()
        {
            var from = transform.position + Vector3.up * 0.4f;
            return Physics.Raycast(from, Vector3.down, out var hit, 0.4f + StorageBotRules.GroundReach + 0.05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.point.y : null;
        }

        /// <summary>
        /// Sat waiting: the neck and head turned to the nearest player (within the neck's reach; not at all to one behind),
        /// eased; otherwise eased back to the take's own. Applied over the animator's pose — only while it is drawn (culled,
        /// the animator leaves the bones as they were and a turn would add up).
        /// </summary>
        void TurnHead(bool sitting, float dt)
        {
            var s = Tuning.Run.Mule;
            var aim = Vector2.zero;
            if (sitting && Head != null && NearestPlayer() is Vector3 eyes) aim = StorageBotRules.HeadTurn(transform.forward, Head.position, eyes, s);
            float step = Mathf.Max(1f, s.NeckSpeed) * dt;
            m_Turn = new Vector2(Mathf.MoveTowards(m_Turn.x, aim.x, step), Mathf.MoveTowards(m_Turn.y, aim.y, step));
            if (m_Turn.sqrMagnitude < 1e-4f || Neck == null || Head == null || !Drawn()) return;
            // half the turn in the neck, half in the head
            var yaw = Quaternion.AngleAxis(m_Turn.x * 0.5f, Vector3.up);
            var right = Quaternion.AngleAxis(m_Turn.x, Vector3.up) * transform.right;
            var pitch = Quaternion.AngleAxis(-m_Turn.y * 0.5f, right);   // (a turn about the right axis tips the nose down: up is negative)
            Neck.rotation = pitch * yaw * Neck.rotation;
            Head.rotation = pitch * yaw * Head.rotation;
        }

        /// <summary>The eyes of the player nearest it, or null.</summary>
        Vector3? NearestPlayer()
        {
            Vector3? best = null;
            float d = float.MaxValue;
            foreach (var p in PlayerController.All)
            {
                if (p == null) continue;
                var eyes = p.Head != null ? p.Head.position : p.transform.position + Vector3.up * 1.6f;
                float dd = (eyes - transform.position).sqrMagnitude;
                if (dd < d) { d = dd; best = eyes; }
            }
            return best;
        }

        bool Drawn()
        {
            if (m_Renderers == null) return false;
            foreach (var r in m_Renderers)
                if (r != null && r.isVisible) return true;
            return false;
        }
    }
}
