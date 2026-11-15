using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// Marks a part of a prop that changes while the house is played (a cage's door, the hostage): the furniture merge
    /// (<see cref="PropBatches"/>) leaves its renderers alone, so hiding it really hides it.
    /// </summary>
    public class KeepUnmerged : MonoBehaviour
    {
    }
}
