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
        /// <summary>
        /// A floor hatch's model (Tools/Blender/hatches.py) under <paramref name="parent"/>, painted with the services'
        /// materials by part: the yellow frame and lifting bar, the dark bolts, ribs, knuckles and well, the steel lid,
        /// the lamps in the hatch's colour. No colliders of its own (the prefab sets them), no shadows from the thin frame.
        /// </summary>
        static GameObject HatchModel(string file, Transform parent, Material[] util, float yaw = 0f)
        {
            var model = Model(file);
            model.transform.SetParent(parent, false);
            model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = r.gameObject.name;
                r.sharedMaterial = n.StartsWith("Frame") || n.StartsWith("Handle") || n.StartsWith("Rungs") ? util[(int)UtilitySlot.Warning]
                    : n.StartsWith("Lid") || n.StartsWith("Well") ? util[(int)UtilitySlot.Panel]
                    : n.StartsWith("EyeLamp") ? M("HatchRing")
                    : util[(int)UtilitySlot.Dark];
                if (n.StartsWith("Frame") || n.StartsWith("Bolts") || n.StartsWith("Pit") || n.StartsWith("EyeLamp"))
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            return model;
        }

        static void BuildFittings(LevelArt art, Material[] util, Material led)
        {
            // a light switch beside a doorway (LightSwitch): a plate on the wall, a rocker that tilts, a small green lamp in it
            // that glows while the room is dark; +Z out of the wall, the pivot in the plate's middle on the wall's face
            var lightSwitch = Prefab($"{LevelPrefabs}/LightSwitch.prefab", () =>
            {
                var go = new GameObject("LightSwitch");
                ChamferBox(go.transform, "Plate", new Vector3(0f, 0f, 0.006f), new Vector3(0.085f, 0.125f, 0.012f), util[(int)UtilitySlot.Plate], 0.003f);
                var rocker = new GameObject("Rocker").transform;
                rocker.SetParent(go.transform, false);
                rocker.localPosition = new Vector3(0f, 0f, 0.012f);
                ChamferBox(rocker, "Key", new Vector3(0f, 0f, 0.005f), new Vector3(0.036f, 0.064f, 0.01f), util[(int)UtilitySlot.Plate], 0.0025f);
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
                // the frame in the ceiling board and the louvred grate on its hinge (Tools/Blender/vent.py vent_grate_*)
                var frameModel = Model("vent_grate_frame");
                frameModel.transform.SetParent(go.transform, false);
                foreach (var r in frameModel.GetComponentsInChildren<MeshRenderer>(true)) r.sharedMaterial = util[(int)UtilitySlot.Panel];
                var lid = new GameObject("Lid").transform;
                lid.SetParent(go.transform, false);
                lid.localPosition = new Vector3(0f, 0.01f, -size / 2f + 0.06f);
                var lidModel = Model("vent_grate_lid");
                lidModel.transform.SetParent(lid, false);
                foreach (var r in lidModel.GetComponentsInChildren<MeshRenderer>(true)) r.sharedMaterial = util[(int)UtilitySlot.Dark];
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
                // rests on its inner lip; the hinge along the frame's −Z bar (models: Tools/Blender/hatches.py)
                float half = Basement.ShaftHalf, lip = 0.03f;
                HatchModel("hatch_frame_shaft", go.transform, util);
                var lid = new GameObject("Lid").transform;
                lid.SetParent(go.transform, false);
                lid.localPosition = new Vector3(0f, 0.024f, -half - lip);
                float side = 2 * half + 2 * lip;
                HatchModel("hatch_lid_shaft", lid, util);
                var plateCollider = lid.gameObject.AddComponent<BoxCollider>();
                plateCollider.center = new Vector3(0f, 0.0175f, side / 2f);
                plateCollider.size = new Vector3(side, 0.035f, side);
                go.AddComponent<KeepUnmerged>(); // the lid moves: never baked into the furniture's merged mesh (it would stay shut there)
                var h = go.AddComponent<ShaftHatch>();
                h.Lid = lid;
                h.LidCollider = plateCollider;
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
            // the storage robot's way up the shaft: a navmesh link (the builder sets its ends; the lid opens and shuts it), in
            // the Ladder area no other robot walks (RobotController's area mask)
            var shaftLink = Prefab($"{LevelPrefabs}/ShaftLink.prefab", () =>
            {
                var go = new GameObject("ShaftLink");
                var link = go.AddComponent<Unity.AI.Navigation.NavMeshLink>();
                link.width = 0.8f;
                link.bidirectional = true;
                link.autoUpdate = false;
                link.costModifier = 4;
                link.area = Stealth.Level.LevelMetrics.LadderArea;
                return go;
            });
            Link(art, ref art.ShaftHatch, shaftHatch);
            Link(art, ref art.ShaftLadder, shaftLadder);
            Link(art, ref art.ShaftLink, shaftLink);
        }
    }
}
