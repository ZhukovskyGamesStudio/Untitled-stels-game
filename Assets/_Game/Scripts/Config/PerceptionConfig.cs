using Stealth.Enemies;
using UnityEngine;

namespace Stealth.Configs
{
    /// <summary>
    /// How security senses players, for every robot and device: how being partly hidden, far, at the edge of view,
    /// crouching or still slows noticing; how walls, doors and slabs muffle sounds; how speed shakes the floor.
    /// Edit Data/Config/Perception.asset. The speed of each sensor is in its own config next to the prefab.
    /// </summary>
    [CreateAssetMenu(menuName = "Stealth/Config/Perception", fileName = "Perception")]
    public class PerceptionConfig : ScriptableObject
    {
        public VisionSettings Vision = new();
        public HearingSettings Hearing = new();
        public VibrationSettings Vibration = new();
    }
}
