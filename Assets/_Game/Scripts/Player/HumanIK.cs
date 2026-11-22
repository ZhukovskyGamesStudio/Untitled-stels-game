using UnityEngine;

namespace Stealth.Player
{
    /// <summary>
    /// On the rigged human's Animator object: Unity sends the IK callback (layers with an IK pass) only to scripts beside
    /// the Animator, so this hands it to the <see cref="HumanRig"/> on the root.
    /// </summary>
    public class HumanIK : MonoBehaviour
    {
        public HumanRig Rig;

        void OnAnimatorIK(int layer)
        {
            if (Rig != null) Rig.OnIK(layer);
        }
    }
}
