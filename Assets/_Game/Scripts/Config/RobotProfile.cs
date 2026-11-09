using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// How one kind of mobile robot senses and behaves (Data/Robots/&lt;Kind&gt;.asset), referenced by its prefab.
    /// Devices with their own scripts (turret, spider, vacuum, lidar, PIR, plate, camera) keep their tuning on the prefab.
    /// </summary>
    [CreateAssetMenu(menuName = "Stealth/Config/Robot Profile", fileName = "Robot")]
    public class RobotProfile : ScriptableObject
    {
        public RobotSenses Senses = new();
        public RobotTraits Traits = new();
    }
}
