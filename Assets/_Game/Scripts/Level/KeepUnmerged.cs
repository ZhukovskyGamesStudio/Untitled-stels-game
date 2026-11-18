using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// Marks a part that changes while the house is played (a cage's door, the hostage, a vent's grille, the lift's doors
    /// and cab): neither the furniture and shell merge (<see cref="PropBatches"/>) nor static batching
    /// (<see cref="LevelBuilder.KeepsMoving"/>) touch its renderers, so moving or hiding it really moves or hides it.
    /// </summary>
    public class KeepUnmerged : MonoBehaviour
    {
    }
}
