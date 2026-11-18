using Stealth.Level;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Stealth.EditorTools
{
    public static partial class AssetBuilder
    {
        /// <summary>
        /// The house's fittings with a part to play (built with the level art): the light switch by a doorway, the grate in
        /// a ceiling (the way into the crawl ducts), the lid over the shaft to the basement with its ladder and the storage
        /// robot's link up it. <paramref name="util"/>: the services' materials by <see cref="UtilitySlot"/>; <paramref name="led"/>: the switch's glow.
        /// </summary>
        static void BuildFittings(LevelArt art, Material[] util, Material led)
        {
            // a light switch beside a doorway (LightSwitch): a plate on the wall, a rocker that tilts, a small green lamp in it
            // that glows while the room is dark; +Z out of the wall, the pivot in the plate's middle on the wall's face
            var lightSwitch = Prefab($"{LevelPrefabs}/LightSwitch.prefab", () =>
            {
                var go = new GameObject("LightSwitch");
                Cube(go.transform, "Plate", new Vector3(0f, 0f, 0.006f), new Vector3(0.085f, 0.125f, 0.012f), util[(int)UtilitySlot.Plate], collider: false);
                var rocker = new GameObject("Rocker").transform;
                rocker.SetParent(go.transform, false);
                rocker.localPosition = new Vector3(0f, 0f, 0.012f);
                Cube(rocker, "Key", new Vector3(0f, 0f, 0.005f), new Vector3(0.036f, 0.064f, 0.01f), util[(int)UtilitySlot.Plate], collider: false);
                var glow = Cube(rocker, "Led", new Vector3(0f, -0.022f, 0.0105f), new Vector3(0.008f, 0.008f, 0.002f), led, collider: false);
                var s = go.AddComponent<LightSwitch>();
                s.Rocker = rocker;
                s.Led = glow.GetComponent<MeshRenderer>();
                return go;
            });
            Link(art, ref art.LightSwitch, lightSwitch);
            // a grate in a room's ceiling (VentGrate, the way into the crawl ducts): a frame in the ceiling board and a grille
            // on a hinge along one edge that swings down while a player is near; the pivot in the frame's middle on the ceiling
            var ventGrate = Prefab($"{LevelPrefabs}/VentGrate.prefab", () =>
            {
                var go = new GameObject("VentGrate");
                float size = VentPlan.Res, t = LevelMetrics.CeilingBoard;
                foreach (var (c, sz) in new[]
                         {
                             (new Vector3(0f, t / 2f, -size / 2f + 0.03f), new Vector3(size, t, 0.06f)), (new Vector3(0f, t / 2f, size / 2f - 0.03f), new Vector3(size, t, 0.06f)),
                             (new Vector3(-size / 2f + 0.03f, t / 2f, 0f), new Vector3(0.06f, t, size)), (new Vector3(size / 2f - 0.03f, t / 2f, 0f), new Vector3(0.06f, t, size)),
                         })
                    Cube(go.transform, "Frame", c, sz, util[(int)UtilitySlot.Panel], collider: false);
                var lid = new GameObject("Lid").transform;
                lid.SetParent(go.transform, false);
                lid.localPosition = new Vector3(0f, 0.01f, -size / 2f + 0.06f);
                for (int i = 0; i < 9; i++)
                    Cube(lid, "Slat", new Vector3(0f, 0f, 0.05f + i * 0.1f), new Vector3(size - 0.14f, 0.02f, 0.035f), util[(int)UtilitySlot.Dark], collider: false);
                Cube(lid, "Rail", new Vector3(-size / 2f + 0.1f, 0f, (size - 0.12f) / 2f), new Vector3(0.03f, 0.025f, size - 0.12f), util[(int)UtilitySlot.Panel], collider: false);
                Cube(lid, "Rail", new Vector3(size / 2f - 0.1f, 0f, (size - 0.12f) / 2f), new Vector3(0.03f, 0.025f, size - 0.12f), util[(int)UtilitySlot.Panel], collider: false);
                var floor = go.AddComponent<BoxCollider>(); // walked on in the duct; from below the grate is climbed with E
                floor.center = new Vector3(0f, t / 2f, 0f);
                floor.size = new Vector3(size - 0.04f, t, size - 0.04f);
                go.AddComponent<KeepUnmerged>(); // the grille swings open
                var g = go.AddComponent<VentGrate>();
                g.Lid = lid;
                return go;
            });
            Link(art, ref art.VentGrate, ventGrate);
            // the lid over the shaft to the basement (ShaftHatch): a steel frame in the floor, a ribbed lid on a hinge at −Z
            // with a yellow rim, and the ladder's top under it (ShaftLadder); the pivot on the floor in the shaft's middle
            var shaftHatch = Prefab($"{LevelPrefabs}/ShaftHatch.prefab", () =>
            {
                var go = new GameObject("ShaftHatch");
                // the frame lies on the floor right round the hole (its inner edge at the hole's: 3 × 3 plan cells), the lid
                // rests on its inner lip; the hinge along the frame's −Z bar
                float half = Basement.ShaftHalf, bar = 0.12f, lip = 0.03f;
                foreach (var (c, sz) in new[]
                         {
                             (new Vector3(0f, 0.012f, -half - bar / 2f), new Vector3(2 * half + 2 * bar, 0.024f, bar)), (new Vector3(0f, 0.012f, half + bar / 2f), new Vector3(2 * half + 2 * bar, 0.024f, bar)),
                             (new Vector3(-half - bar / 2f, 0.012f, 0f), new Vector3(bar, 0.024f, 2 * half)), (new Vector3(half + bar / 2f, 0.012f, 0f), new Vector3(bar, 0.024f, 2 * half)),
                         })
                    Cube(go.transform, "Frame", c, sz, util[(int)UtilitySlot.Warning], collider: false);
                var lid = new GameObject("Lid").transform;
                lid.SetParent(go.transform, false);
                lid.localPosition = new Vector3(0f, 0.024f, -half - lip);
                float side = 2 * half + 2 * lip;
                var plate = Cube(lid, "Plate", new Vector3(0f, 0.02f, side / 2f), new Vector3(side, 0.04f, side), util[(int)UtilitySlot.Panel], collider: true);
                for (int i = 0; i < 5; i++)
                    Cube(lid, "Rib", new Vector3(0f, 0.05f, 0.2f + i * (side - 0.4f) / 4f), new Vector3(side - 0.2f, 0.02f, 0.04f), util[(int)UtilitySlot.Dark], collider: false);
                Cube(lid, "Handle", new Vector3(0f, 0.075f, side - 0.12f), new Vector3(0.3f, 0.04f, 0.04f), util[(int)UtilitySlot.Warning], collider: false);
                go.AddComponent<KeepUnmerged>(); // the lid moves: never baked into the furniture's merged mesh (it would stay shut there)
                var h = go.AddComponent<ShaftHatch>();
                h.Lid = lid;
                h.LidCollider = plate.GetComponent<Collider>();
                var top = new GameObject("LadderTop");
                top.transform.SetParent(go.transform, false);
                top.AddComponent<ShaftLadder>().Top = true;
                return go;
            });
            var shaftLadder = Prefab($"{LevelPrefabs}/ShaftLadder.prefab", () =>
            {
                var go = new GameObject("ShaftLadder");
                go.AddComponent<ShaftLadder>();
                return go;
            });
            // the storage robot's way up the shaft: a navmesh link (the builder sets its ends; the lid opens and shuts it)
            var shaftLink = Prefab($"{LevelPrefabs}/ShaftLink.prefab", () =>
            {
                var go = new GameObject("ShaftLink");
                var link = go.AddComponent<Unity.AI.Navigation.NavMeshLink>();
                link.width = 0.8f;
                link.bidirectional = true;
                link.autoUpdate = false;
                link.costModifier = 4;
                return go;
            });
            Link(art, ref art.ShaftHatch, shaftHatch);
            Link(art, ref art.ShaftLadder, shaftLadder);
            Link(art, ref art.ShaftLink, shaftLink);
        }
    }
}
