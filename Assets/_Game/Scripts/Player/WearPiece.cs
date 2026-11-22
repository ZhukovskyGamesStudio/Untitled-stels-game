using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Player
{
    /// <summary>
    /// An item a rigged human can wear (Tools/Blender/human.py <c>item=…</c> → Prefabs/Player/Wear/&lt;who&gt;/&lt;item&gt;):
    /// its slot, its name, the person whose build it is made for, and its skinned meshes with the names of the bones they
    /// follow. Worn, it is bound to that person's own skeleton by the bones' names (every person has the same skeleton,
    /// <see cref="HumanSkeleton"/>; the build is in the shape of the meshes, so an item fits the person it is made for).
    /// </summary>
    public class WearPiece : MonoBehaviour
    {
        [Tooltip("The wardrobe slot (HumanWardrobe.Slots): top, bottom, feet, hair, beard, head, eyes")]
        public string Slot;
        [Tooltip("The item's name in the wardrobe (human.py ITEMS)")]
        public string Item;
        [Tooltip("The person whose build it is made for (backend, qa, sysadmin, designer)")]
        public string Who;
        public Part[] Parts = new Part[0];

        /// <summary>A skinned mesh of the item and the bones it follows, by name.</summary>
        [System.Serializable]
        public class Part
        {
            public SkinnedMeshRenderer Renderer;
            public string[] Bones = new string[0];
            public string Root;
        }

        /// <summary>Binds every part to the skeleton under <paramref name="skeleton"/>; returns how many bones it lacked.</summary>
        public int Bind(Transform skeleton)
        {
            var byName = new Dictionary<string, Transform>();
            foreach (var t in skeleton.GetComponentsInChildren<Transform>(true))
                if (!byName.ContainsKey(t.name)) byName[t.name] = t;
            int missing = 0;
            foreach (var part in Parts)
            {
                if (part.Renderer == null) continue;
                var bones = new Transform[part.Bones.Length];
                for (int i = 0; i < bones.Length; i++)
                    if (!byName.TryGetValue(part.Bones[i], out bones[i])) missing++;
                part.Renderer.bones = bones;
                if (!string.IsNullOrEmpty(part.Root) && byName.TryGetValue(part.Root, out var root)) part.Renderer.rootBone = root;
            }
            return missing;
        }
    }
}
