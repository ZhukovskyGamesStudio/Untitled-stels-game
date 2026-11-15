using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// The gate across the sewer between the car and the stretch the house's exit drops into (prefab Level/SewerGate):
    /// the team comes down on the far side, pulls the lever (<see cref="GateLever"/>) and the shutter rolls up so the car
    /// can drive through. Every peer shows it from the replicated state (<see cref="IsOpen"/>); the shutter blocks the car
    /// and the players until it is mostly up.
    /// </summary>
    public class SewerGate : MonoBehaviour
    {
        [Tooltip("The shutter: it slides up by its height.")]
        public Transform Door;
        public Collider DoorCollider;
        [Tooltip("The lever's handle: it flips when the gate opens.")]
        public Transform Handle;
        public Light[] HazardLights;
        [Tooltip("The spot over the lever: red while the gate is shut, green once it opens.")]
        public Light LeverLight;

        /// <summary>Is the gate of this sewer open (set by the game).</summary>
        public static System.Func<bool> IsOpen = () => false;

        public const float Lift = 3.4f;

        float m_Open;
        Vector3 m_Closed;
        Quaternion m_HandleRest;
        bool m_Sounded;

        void Awake()
        {
            if (Door != null) m_Closed = Door.localPosition;
            if (Handle != null) m_HandleRest = Handle.localRotation;
        }

        void Update()
        {
            bool open = IsOpen();
            if (open && !m_Sounded && m_Open < 0.01f)
            {
                m_Sounded = true;
                Audio.Sfx.At(Audio.Sfx.Bank?.GateOpen, transform.position + Vector3.up * 2f, 1f, Audio.Sfx.Group.Items);
            }
            m_Open = Mathf.MoveTowards(m_Open, open ? 1f : 0f, Time.deltaTime / 2f);
            if (Door != null) Door.localPosition = m_Closed + Vector3.up * Lift * Mathf.SmoothStep(0f, 1f, m_Open);
            if (DoorCollider != null) DoorCollider.enabled = m_Open < 0.6f;
            if (Handle != null) Handle.localRotation = m_HandleRest * Quaternion.Euler(open ? 70f : 0f, 0f, 0f);
            if (LeverLight != null)
            {
                // a slow pulse while shut draws the eye; steady green once open
                LeverLight.color = open ? new Color(0.35f, 1f, 0.45f) : new Color(1f, 0.25f, 0.15f);
                LeverLight.intensity = open ? 1.5f : 3f + 1.5f * Mathf.Sin(Time.time * 3f);
            }
            if (HazardLights != null)
                foreach (var l in HazardLights)
                    if (l != null) l.intensity = m_Open > 0f && m_Open < 1f ? 3f * (Mathf.Sin(Time.time * 10f) > 0f ? 1f : 0.1f) : 0f;
        }
    }
}
