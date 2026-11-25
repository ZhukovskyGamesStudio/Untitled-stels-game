using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// A charm hanging off the raccoon's backpack (a carabiner with a mug, with keys): its transform is the clip point, its
    /// children the charm as it hangs at rest. After the animation it swings with the pack's moves and with gravity
    /// (<see cref="DangleMath"/>), on every peer; purely visual.
    /// </summary>
    public class Dangle : MonoBehaviour
    {
        [Tooltip("From the clip point to the charm's centre, metres (the builder measures it).")]
        public float Length = 0.1f;
        [Tooltip("Straight down in the parent's space in the model's rest pose (the builder sets it).")]
        public Vector3 RestDown = Vector3.down;

        Quaternion m_RestLocal;
        Vector3 m_RestDownLocal, m_Tip, m_Previous;
        bool m_Started;

        void Awake()
        {
            m_RestLocal = transform.localRotation;
            m_RestDownLocal = RestDown.sqrMagnitude > 1e-6f ? RestDown.normalized : Vector3.down;   // (straight down in the model's rest pose)
        }

        void OnEnable() => m_Started = false;

        void LateUpdate()
        {
            var parent = transform.parent;
            var rest = parent != null ? parent.TransformDirection(m_RestDownLocal).normalized : Vector3.down;
            var pivot = transform.position;
            if (!m_Started)
            {
                m_Tip = m_Previous = pivot + rest * Length;
                m_Started = true;
            }
            m_Tip = DangleMath.Step(pivot, rest, Length, m_Tip, ref m_Previous, Physics.gravity, Mathf.Min(Time.deltaTime, 0.05f));
            var restWorld = parent != null ? parent.rotation * m_RestLocal : m_RestLocal;
            transform.rotation = Quaternion.FromToRotation(rest, (m_Tip - pivot).normalized) * restWorld;
        }
    }
}
