using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// A tunnel leading on from the sewer's hall (prefab Level/SewerBranch; +X leads into it): its bars (shown when it leads
    /// nowhere this time), the lights and the glowing zone inside (shown when open: drive the car into it to go this way),
    /// and an invisible wall at <see cref="SewerLocation.StopAt"/>, where the dark tunnel itself goes on.
    /// </summary>
    public class SewerBranch : MonoBehaviour
    {
        public GameObject Bars;
        public GameObject WhenOpen;
    }
}
