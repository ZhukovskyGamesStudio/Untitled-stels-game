using Stealth.Level;
using Unity.AI.Navigation;
using UnityEngine;

namespace Stealth.EditorTools
{
    public static partial class AssetBuilder
    {
        const string LevelPrefabs = PrefabsDir + "/Level";
        const string PropPrefabs = PrefabsDir + "/Props";

        static LevelArt BuildLevelArt()
        {
            // Structure: unit cubes, scaled by the level builder.
            var root = Prefab($"{LevelPrefabs}/LevelRoot.prefab", BuildLevelRoot);
            var wall = Prefab($"{LevelPrefabs}/WallBlock.prefab", () => Block("WallBlock", M("Wall")));
            var trim = Prefab($"{LevelPrefabs}/TrimBlock.prefab", () => Block("TrimBlock", M("DoorFrame")));
            var slab = Prefab($"{LevelPrefabs}/FloorSlab.prefab", () => Block("FloorSlab", M("Floor")));
            var roof = Prefab($"{LevelPrefabs}/Roof.prefab", () => Block("Roof", M("Ceiling"), layer: LevelMetrics.CeilingLayer));
            var ground = Prefab($"{LevelPrefabs}/OutsideGround.prefab", () => Block("OutsideGround", M("OutsideGround"), layer: LevelMetrics.CeilingLayer));
            var window = Prefab($"{LevelPrefabs}/WindowPane.prefab", () => Block("WindowPane", M("Window"), collider: false));
            var fence = Prefab($"{LevelPrefabs}/Fence.prefab", BuildFence);

            // Pieces.
            var stairs = Prefab($"{LevelPrefabs}/Stairs.prefab", () => BuildStairs("Stairs", false));
            var stairsMirrored = Prefab($"{LevelPrefabs}/StairsMirrored.prefab", () => BuildStairs("StairsMirrored", true));
            var vent = Prefab($"{LevelPrefabs}/VentFrame.prefab", BuildVentFrame);
            var door = Prefab($"{LevelPrefabs}/AutoDoor.prefab", BuildAutoDoor);
            var light = Prefab($"{LevelPrefabs}/RoomLight.prefab", BuildRoomLight);
            var lamp = Prefab($"{LevelPrefabs}/StreetLamp.prefab", BuildStreetLamp);
            var hatch = Prefab($"{LevelPrefabs}/Hatch.prefab", BuildHatch);

            string p = PropPrefabs + "/";
            var desk = Prefab(p + "Desk.prefab", () => PropObject("Desk", "desk", "PropBody", "PropAccent", "PropScreen"));
            var chair = Prefab(p + "Chair.prefab", () => PropObject("Chair", "chair", "PropAccent", "PropBody", "PropScreen"));
            var rack = Prefab(p + "ServerRack.prefab", () => PropObject("ServerRack", "server_rack", "Rack", "Rack", "RackLights"));
            var cabinet = Prefab(p + "Cabinet.prefab", () => PropObject("Cabinet", "cabinet", "PropBody", "PropAccent", "PropScreen"));
            var meeting = Prefab(p + "MeetingTable.prefab", () => PropObject("MeetingTable", "meeting_table", "PropBody", "PropAccent", "PropScreen"));
            var plant = Prefab(p + "Plant.prefab", () => PropObject("Plant", "plant", "Plant", "PlantPot", "Plant"));
            var kitchen = Prefab(p + "KitchenCounter.prefab", () => PropObject("KitchenCounter", "kitchen_counter", "PropBody", "PropAccent", "PropAccent"));
            var shelf = Prefab(p + "Shelf.prefab", () => PropObject("Shelf", "shelf", "Rack", "Cardboard", "PropScreen"));
            var boxes = Prefab(p + "Boxes.prefab", () => PropObject("Boxes", "boxes", "Cardboard", "Cardboard", "Cardboard"));
            var cooler = Prefab(p + "WaterCooler.prefab", () => PropObject("WaterCooler", "water_cooler", "PropBody", "PropAccent", "Water"));
            var board = Prefab(p + "Whiteboard.prefab", () => PropObject("Whiteboard", "whiteboard", "Ceiling", "PropAccent", "PropScreen"));
            var sofa = Prefab(p + "Sofa.prefab", () => PropObject("Sofa", "sofa", "Fabric", "PropAccent", "PropAccent"));
            var stall = Prefab(p + "ToiletStall.prefab", () => PropObject("ToiletStall", "toilet_stall", "Stall", "Ceiling", "Ceiling"));

            return DataAsset<LevelArt>(DataDir + "/LevelArt.asset", art =>
            {
                art.LevelRoot = root;
                art.WallBlock = wall;
                art.TrimBlock = trim;
                art.FloorSlab = slab;
                art.Roof = roof;
                art.OutsideGround = ground;
                art.WindowPane = window;
                art.Fence = fence;
                art.Stairs = stairs;
                art.StairsMirrored = stairsMirrored;
                art.VentFrame = vent;
                art.AutoDoor = door;
                art.RoomLight = light;
                art.StreetLamp = lamp;
                art.Hatch = hatch;
                art.Desk = desk;
                art.Chair = chair;
                art.ServerRack = rack;
                art.Cabinet = cabinet;
                art.MeetingTable = meeting;
                art.Plant = plant;
                art.KitchenCounter = kitchen;
                art.Shelf = shelf;
                art.Boxes = boxes;
                art.WaterCooler = cooler;
                art.Whiteboard = board;
                art.Sofa = sofa;
                art.ToiletStall = stall;
            });
        }

        // ------------------------------------------------------------------ structure

        static GameObject BuildLevelRoot()
        {
            var root = new GameObject("LevelRoot");
            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~(1 << LevelMetrics.CeilingLayer);
            return root;
        }

        /// <summary>A unit cube with a material: scaled to size by the level builder.</summary>
        static GameObject Block(string name, Material mat, bool collider = true, int layer = 0)
        {
            var go = Cube(null, name, Vector3.zero, Vector3.one, mat, collider);
            go.layer = layer;
            return go;
        }

        /// <summary>Invisible wall around the yard (scaled by the builder). Ceiling layer: not part of the navmesh.</summary>
        static GameObject BuildFence()
        {
            var go = new GameObject("Fence") { layer = LevelMetrics.CeilingLayer };
            go.AddComponent<BoxCollider>().size = Vector3.one;
            return go;
        }

        // ------------------------------------------------------------------ pieces

        /// <summary>
        /// U-shaped staircase from <see cref="StairGeometry"/>. The mirrored variant flips the corner frame (u → -x);
        /// the level builder picks the variant so that instances are only rotated.
        /// The whole staircase is NavMesh area "Stairs": robots without legs cannot use it.
        /// </summary>
        static GameObject BuildStairs(string name, bool mirrored)
        {
            var root = new GameObject(name);
            var mod = root.AddComponent<NavMeshModifier>();
            mod.overrideArea = true;
            mod.area = LevelMetrics.StairsArea;
            foreach (var box in StairGeometry.Boxes())
            {
                var c = box.Center;
                if (mirrored) c.x = -c.x;
                Cube(root.transform, box.Name, c, box.Size, M("DoorFrame"));
            }
            return root;
        }

        /// <summary>Dark frame around a crawl hole, both faces of the wall, modelled along local X (no collider).</summary>
        static GameObject BuildVentFrame()
        {
            var root = new GameObject("VentFrame");
            const float w = LevelMetrics.VentWidth, h = LevelMetrics.VentHeight;
            foreach (float z in new[] { LevelMetrics.WallThickness / 2f + 0.02f, -(LevelMetrics.WallThickness / 2f + 0.02f) })
            {
                Cube(root.transform, "Top", new Vector3(0, h, z), new Vector3(w + 0.16f, 0.08f, 0.04f), M("DoorFrame"), false);
                Cube(root.transform, "Left", new Vector3(-w / 2f, h / 2f, z), new Vector3(0.08f, h, 0.04f), M("DoorFrame"), false);
                Cube(root.transform, "Right", new Vector3(w / 2f, h / 2f, z), new Vector3(0.08f, h, 0.04f), M("DoorFrame"), false);
            }
            return root;
        }

        /// <summary>
        /// Sliding door: two panels and a blocker collider (enabled only while closed). Ignored by the navmesh:
        /// robots plan through doorways and the door opens when they arrive.
        /// </summary>
        static GameObject BuildAutoDoor()
        {
            var root = new GameObject("AutoDoor");
            root.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            float h = LevelMetrics.DoorHeight - 0.02f, w = LevelMetrics.DoorWidth;
            var door = root.AddComponent<AutoDoor>();
            door.Width = w;
            door.PanelA = Cube(root.transform, "PanelA", new Vector3(-w / 4f, h / 2f, 0), new Vector3(w / 2f, h, 0.06f), M("Door"), false).transform;
            door.PanelB = Cube(root.transform, "PanelB", new Vector3(w / 4f, h / 2f, 0), new Vector3(w / 2f, h, 0.06f), M("Door"), false).transform;
            var blocker = root.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0, h / 2f, 0);
            blocker.size = new Vector3(w, h, 0.1f);
            door.Blocker = blocker;
            return root;
        }

        static GameObject BuildRoomLight()
        {
            var go = new GameObject("RoomLight");
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 10f;
            light.shadows = LightShadows.None;
            go.AddComponent<RoomLight>();
            return go;
        }

        static GameObject BuildStreetLamp()
        {
            var root = new GameObject("StreetLamp");
            var pole = Cube(root.transform, "Pole", new Vector3(0, 2f, 0), new Vector3(0.15f, 4f, 0.15f), M("DoorFrame"));
            pole.layer = LevelMetrics.CeilingLayer;
            var lightGo = Child(root.transform, "Light", new Vector3(0, 4.1f, 0));
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 9f;
            light.intensity = 2.5f;
            light.color = new Color(1f, 0.85f, 0.55f);
            light.shadows = LightShadows.None;
            return root;
        }

        static GameObject BuildHatch()
        {
            var hatch = PropObject("Hatch", "hatch", "Hatch", "PropAccent", "HatchRing");
            hatch.AddComponent<Hatch>();
            var box = hatch.GetComponent<BoxCollider>();
            box.size = new Vector3(box.size.x, 0.3f, box.size.z); // easy target for dropping items
            box.center = new Vector3(box.center.x, 0.1f, box.center.z);
            return hatch;
        }

        // ------------------------------------------------------------------ props

        static GameObject PropObject(string name, string file, string body, string accent, string eye, bool collider = true)
        {
            var root = new GameObject(name);
            var model = Model(file);
            model.transform.SetParent(root.transform, false);
            Paint(model, body, accent, eye);
            if (collider)
            {
                var b = LocalBounds(root);
                var box = root.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = b.size;
            }
            return root;
        }
    }
}
