using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// Tuning of one kind of mobile robot: how it senses, moves, reacts and is animated.
    /// A separate asset next to its prefab (Prefabs/Robots/Robot&lt;Kind&gt;Config.asset).
    /// </summary>
    [CreateAssetMenu(menuName = "Stealth/Config/Robot Profile", fileName = "RobotConfig")]
    public class RobotProfile : DetectorProfile
    {
        public RobotSenses Senses = new();
        public RobotTraits Traits = new();
    }
}
