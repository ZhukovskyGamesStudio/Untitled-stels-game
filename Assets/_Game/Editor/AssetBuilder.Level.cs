using System.Linq;
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
            var wall = Prefab($"{LevelPrefabs}/WallBlock.prefab", () => Muffles(Block("WallBlock", M("Wall")), SoundMaterial.Wall));
            var trim = Prefab($"{LevelPrefabs}/TrimBlock.prefab", () => Muffles(Block("TrimBlock", M("DoorFrame")), SoundMaterial.Wall));
            var slab = Prefab($"{LevelPrefabs}/FloorSlab.prefab", () => Muffles(Block("FloorSlab", M("Floor")), SoundMaterial.Slab));
            var roof = Prefab($"{LevelPrefabs}/Roof.prefab", () => Muffles(Block("Roof", M("Ceiling"), layer: LevelMetrics.CeilingLayer), SoundMaterial.Slab));
            var ground = Prefab($"{LevelPrefabs}/OutsideGround.prefab", () => Block("OutsideGround", M("OutsideGround"), layer: LevelMetrics.CeilingLayer));
            var fence = Prefab($"{LevelPrefabs}/Fence.prefab", BuildFence);

            // Pieces.
            var stairs = Prefab($"{LevelPrefabs}/Stairs.prefab", () => BuildStairs("Stairs", false));
            var stairsMirrored = Prefab($"{LevelPrefabs}/StairsMirrored.prefab", () => BuildStairs("StairsMirrored", true));
            var vent = Prefab($"{LevelPrefabs}/VentFrame.prefab", BuildVentFrame);
            var door = Prefab($"{LevelPrefabs}/AutoDoor.prefab", BuildAutoDoor);
            var light = Prefab($"{LevelPrefabs}/RoomLight.prefab", BuildRoomLight);
            var lamp = Prefab($"{LevelPrefabs}/StreetLamp.prefab", BuildStreetLamp);
            var hatch = Prefab($"{LevelPrefabs}/Hatch.prefab", BuildHatch);
            var window = Prefab($"{LevelPrefabs}/Window.prefab", BuildWindow);
            var exitSign = Prefab($"{LevelPrefabs}/ExitSign.prefab", BuildExitSign);

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
            var pPartition = Prefab(p + "Partition.prefab", () => PropObject("Partition", "partition", "Fabric", "PropAccent", "PropAccent"));
            var pBookshelf = Prefab(p + "Bookshelf.prefab", () => PropObject("Bookshelf", "bookshelf", "Wood", "Folders", "Folders"));
            var pPrinter = Prefab(p + "Printer.prefab", () => PropObject("Printer", "printer", "Ceiling", "PropAccent", "PropScreen"));
            var pFridge = Prefab(p + "Fridge.prefab", () => PropObject("Fridge", "fridge", "Ceiling", "Metal", "Metal"));
            var pCoffeeMachine = Prefab(p + "CoffeeMachine.prefab", () => PropObject("CoffeeMachine", "coffee_machine", "Rack", "Metal", "RackLights"));
            var pRoundTable = Prefab(p + "RoundTable.prefab", () => PropObject("RoundTable", "round_table", "Wood", "Metal", "Metal"));
            var pArmchair = Prefab(p + "Armchair.prefab", () => PropObject("Armchair", "armchair", "Fabric", "PropAccent", "PropAccent"));
            var pCoffeeTable = Prefab(p + "CoffeeTable.prefab", () => PropObject("CoffeeTable", "coffee_table", "Wood", "Metal", "Metal"));
            var pTvStand = Prefab(p + "TvStand.prefab", () => PropObject("TvStand", "tv_stand", "Wood", "Rack", "PropScreen"));
            var pReceptionDesk = Prefab(p + "ReceptionDesk.prefab", () => PropObject("ReceptionDesk", "reception_desk", "Wood", "Ceiling", "PropScreen"));
            var pTrashBin = Prefab(p + "TrashBin.prefab", () => PropObject("TrashBin", "trash_bin", "Metal", "PropAccent", "PropAccent"));
            var pCoatRack = Prefab(p + "CoatRack.prefab", () => PropObject("CoatRack", "coat_rack", "Fabric", "Metal", "Metal"));
            var pLockers = Prefab(p + "Lockers.prefab", () => PropObject("Lockers", "lockers", "Lockers", "PropAccent", "PropAccent"));
            var pPallet = Prefab(p + "Pallet.prefab", () => PropObject("Pallet", "pallet", "Cardboard", "Wood", "Wood"));
            var pWashBasin = Prefab(p + "WashBasin.prefab", () => PropObject("WashBasin", "wash_basin", "Ceiling", "Metal", "Mirror"));
            var pVendingMachine = Prefab(p + "VendingMachine.prefab", () =>
            {
                var go = PropObject("VendingMachine", "vending_machine", "Vending", "PropAccent", "VendingGlass");
                Lamp(go.transform, "Glow", new Vector3(0f, 1.2f, 0.75f), Quaternion.identity, LightType.Point, new Color(0.7f, 0.85f, 1f), 0.6f, 3f);
                return go;
            });
            var pDeskLamp = Prefab(p + "DeskLamp.prefab", () =>
            {
                var go = PropObject("DeskLamp", "desk_lamp", "Metal", "LampShade", "LampBulb", collider: false);
                var lamp = Lamp(go.transform, "Light", new Vector3(0f, 0.27f, 0.16f), Quaternion.Euler(65f, 0f, 0f), LightType.Spot, new Color(1f, 0.82f, 0.55f), 1.8f, 3.2f);
                lamp.spotAngle = 120f;
                lamp.innerSpotAngle = 50f;
                return go;
            });
            var pFloorLamp = Prefab(p + "FloorLamp.prefab", () =>
            {
                var go = PropObject("FloorLamp", "floor_lamp", "Metal", "LampShade", "LampBulb");
                Lamp(go.transform, "Light", new Vector3(0f, 1.42f, 0f), Quaternion.identity, LightType.Point, new Color(1f, 0.8f, 0.55f), 1.3f, 4.5f);
                return go;
            });

            return DataAsset<LevelArt>(DataDir + "/LevelArt.asset", art =>
            {
                art.LevelRoot = root;
                art.WallBlock = wall;
                art.TrimBlock = trim;
                art.FloorSlab = slab;
                art.Roof = roof;
                art.OutsideGround = ground;
                art.Fence = fence;
                art.Stairs = stairs;
                art.StairsMirrored = stairsMirrored;
                art.VentFrame = vent;
                art.AutoDoor = door;
                art.RoomLight = light;
                art.StreetLamp = lamp;
                art.Window = window;
                art.ExitSign = exitSign;
                art.DeskLamp = pDeskLamp;
                art.FloorLamp = pFloorLamp;
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
                art.Partition = pPartition;
                art.Bookshelf = pBookshelf;
                art.Printer = pPrinter;
                art.Fridge = pFridge;
                art.CoffeeMachine = pCoffeeMachine;
                art.RoundTable = pRoundTable;
                art.Armchair = pArmchair;
                art.CoffeeTable = pCoffeeTable;
                art.TvStand = pTvStand;
                art.ReceptionDesk = pReceptionDesk;
                art.TrashBin = pTrashBin;
                art.CoatRack = pCoatRack;
                art.Lockers = pLockers;
                art.Pallet = pPallet;
                art.WashBasin = pWashBasin;
                art.VendingMachine = pVendingMachine;
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

        /// <summary>Sounds passing through this piece are muffled as <paramref name="material"/> (Data/Config/Perception).</summary>
        static GameObject Muffles(GameObject go, SoundMaterial material)
        {
            go.AddComponent<SoundBlocker>().Material = material;
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
            Muffles(root, SoundMaterial.Door); // only while closed: the blocker collider is off when open
            return root;
        }

        /// <summary>
        /// A light that robots take into account (<see cref="LightSource"/>): robots see a player standing in it better.
        /// </summary>
        static Light Lamp(Transform parent, string name, Vector3 position, Quaternion rotation, LightType type, Color color, float intensity, float range,
            LightShadows shadows = LightShadows.None)
        {
            var go = Child(parent, name, position);
            go.transform.localRotation = rotation;
            var light = go.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = shadows;
            go.AddComponent<LightSource>();
            return light;
        }

        /// <summary>The model's renderers do not shadow the lamp inside them.</summary>
        static void NoShadows(GameObject model)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// Ceiling panel at the ceiling surface (the pivot) with a wide spot light shining down: soft shadows, pools of light
        /// under it, darker corners. <see cref="RoomLight"/> sets colour / brightness per room and switches it off in dark rooms.
        /// </summary>
        static GameObject BuildRoomLight()
        {
            var root = new GameObject("RoomLight");
            var model = Model("ceiling_lamp");
            model.transform.SetParent(root.transform, false);
            Paint(model, "LampHousing", "LampHousing", "LampPanel");
            NoShadows(model);
            var light = Lamp(root.transform, "Lamp", new Vector3(0f, -0.1f, 0f), Quaternion.Euler(90f, 0f, 0f), LightType.Spot,
                Color.white, LightMath.RoomIntensity, 9f, LightShadows.Soft);
            light.spotAngle = 135f;
            light.innerSpotAngle = 80f;
            light.shadowStrength = 0.55f; // many lamps, diffusers: soft, faint office shadows
            light.shadowNearPlane = 0.2f;
            var rl = root.AddComponent<RoomLight>();
            rl.Lamp = light;
            rl.Panel = Parts(model, "Eye").FirstOrDefault();
            return root;
        }

        /// <summary>Facade window; the glass lets the moonlight in (no shadow), the blinds stripe it.</summary>
        static GameObject BuildWindow()
        {
            var go = PropObject("Window", "window", "Blinds", "WindowFrame", "Window", collider: false);
            foreach (var r in Parts(go, "Eye")) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>Green exit sign over a door (sticks out of both faces of the wall) with a small green light.</summary>
        static GameObject BuildExitSign()
        {
            var root = new GameObject("ExitSign");
            var model = Model("exit_sign");
            model.transform.SetParent(root.transform, false);
            Paint(model, "PropAccent", "ExitSignMark", "ExitSign");
            NoShadows(model);
            Lamp(root.transform, "Glow", new Vector3(0f, -0.2f, 0f), Quaternion.identity, LightType.Point, new Color(0.35f, 1f, 0.5f), 0.7f, 3.5f);
            return root;
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
            lightGo.AddComponent<LightSource>();
            return root;
        }

        static GameObject BuildHatch()
        {
            var hatch = PropObject("Hatch", "hatch", "Hatch", "PropAccent", "HatchRing", collider: false);
            hatch.AddComponent<Hatch>();
            var b = LocalBounds(hatch);
            var box = hatch.AddComponent<BoxCollider>();
            box.size = new Vector3(b.size.x, 0.3f, b.size.z); // easy target for dropping items
            box.center = new Vector3(b.center.x, 0.1f, b.center.z);
            return hatch;
        }

        // ------------------------------------------------------------------ props

        /// <summary>
        /// Furniture: the model plus a mesh collider on every part, so the colliders follow the shape — an item on a desk
        /// next to the monitor or on a shelf is not buried inside one big bounding box and can be grabbed from any side.
        /// </summary>
        static GameObject PropObject(string name, string file, string body, string accent, string eye, bool collider = true)
        {
            var root = new GameObject(name);
            var model = Model(file);
            model.transform.SetParent(root.transform, false);
            Paint(model, body, accent, eye);
            if (collider)
                foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            return root;
        }
    }
}
