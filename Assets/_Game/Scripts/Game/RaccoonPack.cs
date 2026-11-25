using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using Stealth.Vehicles;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// Linus's backpack, on every peer: a pack of its own for each level of the backpack upgrade (Tools/Blender/raccoon.py:
    /// a scruffy sack, the plaid pack, a hiking pack with a bedroll, a frame pack), and everything in the team's stash
    /// strapped round it in its own size — the stash grid wrapped over the pack's sides, top and back
    /// (<see cref="PackWrap"/>): a thing moved on the grid moves on the pack too. Purely visual; rebuilt when the stash or
    /// the level changes.
    /// </summary>
    public class RaccoonPack : MonoBehaviour
    {
        [Tooltip("The pack of each level 0..3 (one shown).")]
        public GameObject[] Levels;
        [Tooltip("Each level's load: the middle of its pack's top, +X to the right seen from behind, +Y up, +Z towards the head.")]
        public Transform[] Loads;
        [Tooltip("Each level's box the stash wraps round, in its load's space: width (x), height (y), length (z), metres.")]
        public Vector3[] Boxes;
        [Tooltip("The load's pieces (each a MeshFilter with a MeshRenderer, hidden until a thing lies there): one per stash cell at most.")]
        public MeshFilter[] Pieces;
        public ItemVisuals Visuals;

        readonly GameObject[] m_Models = new GameObject[TeamStash.MaxWidth * TeamStash.MaxHeight];
        int m_Version = -1, m_Level = -1;

        /// <summary>The pack's level now (the backpack upgrade).</summary>
        static int LevelNow(GameManager gm) => Mathf.Clamp(gm.Upgrades.Level(CarUpgrade.Kennel), 0, CarUpgrades.MaxLevelled);

        void Update()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            var stash = gm.Stash;
            int level = LevelNow(gm);
            bool moved = level != m_Level;
            if (moved)
            {
                m_Level = level;
                if (Levels != null)
                    for (int i = 0; i < Levels.Length; i++)
                        if (Levels[i] != null) Levels[i].SetActive(i == level);
                var load = Load(level);
                if (load != null && Pieces != null)
                    foreach (var p in Pieces)
                        if (p != null) p.transform.SetParent(load, false);
            }
            if ((!moved && stash.Version == m_Version) || Pieces == null || Visuals == null) return;
            m_Version = stash.Version;
            var box = Boxes != null && level < Boxes.Length ? Boxes[level] : new Vector3(0.3f, 0.25f, 0.3f);
            var laid = PackWrap.Lay(stash.Items, stash.Columns, stash.Rows, new PackWrap.Box(box.x, box.z, box.y), Thickness);
            for (int i = 0; i < Pieces.Length; i++)
                Show(i, i < stash.Items.Count ? stash.Items[i] : (InventoryEntry?)null, i < laid.Count ? laid[i] : default);
        }

        Transform Load(int level) => Loads != null && Loads.Length > 0 ? Loads[Mathf.Clamp(level, 0, Loads.Length - 1)] : null;

        /// <summary>How thick a shape lies on the pack (its mesh's height), metres.</summary>
        float Thickness(int shape)
        {
            var meshes = Visuals != null ? Visuals.Meshes : null;
            if (meshes == null || meshes.Length == 0) return 0.08f;
            var mesh = meshes[Mathf.Clamp(shape, 0, meshes.Length - 1)];
            return mesh != null ? mesh.bounds.size.y : 0.08f;
        }

        void Show(int i, InventoryEntry? entry, PackWrap.Placed at)
        {
            var filter = Pieces[i];
            if (filter == null) return;
            var renderer = filter.GetComponent<MeshRenderer>();
            var model = entry is InventoryEntry m && Visuals.Models != null && m.Shape < Visuals.Models.Length ? Visuals.Models[m.Shape] : null;
            if (i < m_Models.Length && m_Models[i] != null && (model == null || m_Models[i].name != model.name))
            {
                Destroy(m_Models[i]);
                m_Models[i] = null;
            }
            if (model != null && i < m_Models.Length && m_Models[i] == null)
            {
                m_Models[i] = Instantiate(model, filter.transform);
                m_Models[i].name = model.name;
            }
            bool own = i < m_Models.Length && m_Models[i] != null;
            renderer.enabled = entry != null && !own;
            if (entry is not InventoryEntry e) return;
            filter.transform.SetLocalPositionAndRotation(at.Position, at.Rotation);
            filter.transform.localScale = at.Scale;
            filter.sharedMesh = Visuals.Meshes[Mathf.Clamp(e.Shape, 0, Visuals.Meshes.Length - 1)];
            renderer.sharedMaterial = Visuals.ColorMaterials[Mathf.Clamp(e.Color, 0, Visuals.ColorMaterials.Length - 1)];
            VisualStyles.Dress(filter.gameObject);
        }
    }
}
