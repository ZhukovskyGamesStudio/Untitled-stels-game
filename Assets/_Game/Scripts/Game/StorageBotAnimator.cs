using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// The storage robot's body language, on every peer, from how it moves (its place is replicated): it waddles from side
    /// to side and bobs on its way, its nose rises as it climbs up through a grate and dips as it climbs down, it presses
    /// itself flat crawling through a duct, it breathes and glances about while it waits, and its crate's lid opens while a
    /// player is next to it (its stash in reach).
    /// </summary>
    public class StorageBotAnimator : MonoBehaviour
    {
        [Tooltip("The model (sways, bobs and tilts).")]
        public Transform Model;
        [Tooltip("The crate's lid on its hinge (turns about its local X).")]
        public Transform LidPivot;
        public float LidOpenAngle = -70f;

        StorageBot m_Bot;
        Vector3 m_LastPos, m_Velocity, m_BasePos;
        Quaternion m_BaseRot;
        Vector3 m_BaseScale;
        float m_Phase, m_Lid, m_Pitch, m_Flat, m_Glance, m_NextGlance;

        void Awake()
        {
            m_Bot = GetComponent<StorageBot>();
            m_LastPos = transform.position;
            if (Model == null) return;
            m_BasePos = Model.localPosition;
            m_BaseRot = Model.localRotation;
            m_BaseScale = Model.localScale;
        }

        /// <summary>
        /// The pose (pure): tilt up/down from the vertical speed (degrees, nose up negative), sideways sway and bob from the
        /// walking speed at <paramref name="phase"/>.
        /// </summary>
        public static (float pitch, float roll, float bob) Pose(float speed, float climb, float phase)
        {
            float pitch = Mathf.Clamp(-climb * 40f, -60f, 60f);
            float walk = Mathf.Clamp01(speed / 2.5f);
            return (pitch + walk * 4f, Mathf.Sin(phase) * 6f * walk, Mathf.Abs(Mathf.Sin(phase)) * 0.035f * walk);
        }

        void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var v = (transform.position - m_LastPos) / dt;
            m_LastPos = transform.position;
            m_Velocity = Vector3.Lerp(m_Velocity, v, 1f - Mathf.Exp(-10f * dt));
            if (Model == null) return;
            float speed = new Vector2(m_Velocity.x, m_Velocity.z).magnitude;
            m_Phase += speed * 4.2f * dt;
            var (pitch, roll, bob) = Pose(speed, m_Velocity.y, m_Phase);
            m_Pitch = Mathf.Lerp(m_Pitch, pitch, 1f - Mathf.Exp(-6f * dt));
            // in a duct: pressed flat
            bool duct = m_Bot != null && m_Bot.Mode is BotMode.InDucts or BotMode.WaitingAbove || (m_Bot != null && m_Bot.InDuct);
            m_Flat = Mathf.MoveTowards(m_Flat, duct ? 1f : 0f, dt * 3f);
            // waiting: it breathes and now and then glances aside
            float idle = 1f - Mathf.Clamp01(speed / 0.3f);
            if (Time.time >= m_NextGlance)
            {
                m_NextGlance = Time.time + 2.5f + Mathf.Repeat(transform.position.x * 13.7f + Time.time, 3f);
                m_Glance = idle > 0.5f ? (Mathf.Repeat(Time.time * 7.3f, 2f) - 1f) * 25f : 0f;
            }
            float breathe = Mathf.Sin(Time.time * 1.7f) * 0.012f * idle;
            Model.localPosition = m_BasePos + Vector3.up * (bob + breathe - m_Flat * 0.05f);
            Model.localRotation = m_BaseRot * Quaternion.Euler(m_Pitch, Mathf.LerpAngle(0f, m_Glance, idle), roll);
            Model.localScale = Vector3.Scale(m_BaseScale, new Vector3(1f + m_Flat * 0.08f, 1f - m_Flat * 0.22f, 1f + m_Flat * 0.08f));
            // the lid opens while someone stands by with the stash in reach
            bool reach = false;
            if (m_Bot != null)
                foreach (var p in PlayerController.All)
                    if (StorageBotRules.InReach(m_Bot.Mode, transform.position, p.transform.position)) { reach = true; break; }
            m_Lid = Mathf.MoveTowards(m_Lid, reach ? 1f : 0f, dt * 2.5f);
            if (LidPivot != null) LidPivot.localRotation = Quaternion.Euler(Mathf.SmoothStep(0f, 1f, m_Lid) * LidOpenAngle, 0f, 0f);
        }
    }
}
