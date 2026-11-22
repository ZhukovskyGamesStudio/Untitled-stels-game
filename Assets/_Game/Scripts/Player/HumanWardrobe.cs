using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Stealth.Player
{
    /// <summary>
    /// What a rigged human wears, slot by slot (<see cref="WardrobeRules.Slots"/>): the person's own outfit comes in their
    /// model (renderers named "&lt;part&gt;.&lt;slot&gt;"), any other item is a <see cref="WearPiece"/> made for their build,
    /// instantiated under the model and bound to its skeleton by the bones' names. Swap an item with <see cref="Wear"/>,
    /// dress a whole outfit with <see cref="Dress"/>; <see cref="Changed"/> tells the owner to paint the new parts.
    /// </summary>
    public class HumanWardrobe : MonoBehaviour
    {
        [Tooltip("The person whose build this is (backend, qa, sysadmin, designer): items made for them fit")]
        public string Who;
        [Tooltip("The model: its skeleton and the renderers of the person's own outfit")]
        public Transform Model;
        [Tooltip("The person's own items, slot by slot, and their renderers in the model")]
        public Worn[] Own = new Worn[0];

        /// <summary>An item of the person's own outfit: its slot, its name, its renderers.</summary>
        [System.Serializable]
        public class Worn
        {
            public string Slot, Item;
            public Renderer[] Renderers = new Renderer[0];
        }

        /// <summary>The wardrobe changed: new renderers to colour, shadow and dress.</summary>
        public event System.Action Changed;

        readonly Dictionary<string, WearPiece> m_Swapped = new();
        readonly HashSet<string> m_Bare = new();

        /// <summary>The item worn in a slot (null: none).</summary>
        public string ItemIn(string slot)
        {
            if (m_Bare.Contains(slot)) return null;
            if (m_Swapped.TryGetValue(slot, out var piece) && piece != null) return piece.Item;
            return Own.FirstOrDefault(w => w.Slot == slot)?.Item;
        }

        /// <summary>Wears an item made for this person in its slot instead of what was there.</summary>
        public void Wear(WearPiece prefab)
        {
            if (prefab == null) return;
            if (prefab.Who != Who) Debug.LogWarning($"[Wardrobe] {prefab.Item} is made for {prefab.Who}, worn by {Who}: it will not fit");
            Clear(prefab.Slot);
            ShowOwn(prefab.Slot, false);
            var piece = Instantiate(prefab, Model != null ? Model : transform);
            piece.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            piece.transform.localScale = Vector3.one;
            int missing = piece.Bind(Model != null ? Model : transform);
            if (missing > 0) Debug.LogWarning($"[Wardrobe] {prefab.Item}: {missing} bones not found on {Who}");
            m_Swapped[prefab.Slot] = piece;
            m_Bare.Remove(prefab.Slot);
            Changed?.Invoke();
        }

        /// <summary>Wears the person's own item of the slot again.</summary>
        public void WearOwn(string slot)
        {
            Clear(slot);
            ShowOwn(slot, true);
            m_Bare.Remove(slot);
            Changed?.Invoke();
        }

        /// <summary>Leaves the slot bare.</summary>
        public void TakeOff(string slot)
        {
            Clear(slot);
            ShowOwn(slot, false);
            m_Bare.Add(slot);
            Changed?.Invoke();
        }

        /// <summary>
        /// Dresses an outfit — item names, a slot each; a slot not named is left bare — out of <paramref name="library"/>
        /// (items made for every person; this one's are taken). An empty outfit keeps what is worn.
        /// </summary>
        public void Dress(IReadOnlyList<string> outfit, IEnumerable<WearPiece> library)
        {
            if (outfit == null || outfit.Count == 0) return;
            var mine = (library ?? Enumerable.Empty<WearPiece>()).Where(p => p != null && p.Who == Who).ToDictionary(p => p.Item, p => p);
            var own = Own.Where(w => w.Slot != null).ToDictionary(w => w.Slot, w => w.Item);
            string SlotOf(string item) => mine.TryGetValue(item, out var p) ? p.Slot : Own.FirstOrDefault(w => w.Item == item)?.Slot;
            foreach (var (slot, (action, item)) in WardrobeRules.Plan(own, outfit, SlotOf))
            {
                if (action == WearAction.Own) { if (ItemIn(slot) != item || m_Swapped.ContainsKey(slot)) WearOwn(slot); }
                else if (action == WearAction.Bare) { if (ItemIn(slot) != null) TakeOff(slot); }
                else if (ItemIn(slot) != item) Wear(mine[item]);
            }
        }

        void Clear(string slot)
        {
            if (!m_Swapped.TryGetValue(slot, out var piece)) return;
            m_Swapped.Remove(slot);
            if (piece == null) return;
            if (Application.isPlaying) Destroy(piece.gameObject);
            else DestroyImmediate(piece.gameObject);
        }

        void ShowOwn(string slot, bool shown)
        {
            foreach (var w in Own)
                if (w.Slot == slot)
                    foreach (var r in w.Renderers)
                        if (r != null) r.gameObject.SetActive(shown);
        }
    }
}
