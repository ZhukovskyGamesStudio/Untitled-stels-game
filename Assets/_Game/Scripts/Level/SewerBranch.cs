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
        [Tooltip("Bricks in the mouth, shown with the bars when the tunnel leads nowhere this time.")]
        public GameObject Plug;
        [Tooltip("The lamps by the mouth on the hall's side (under WhenOpen): they keep their light, the ones inside are dimmed.")]
        public Light[] MouthLights;
    }
}
