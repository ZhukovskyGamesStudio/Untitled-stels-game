using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
            // the land round the house out to the horizon (Tools/Blender/terrain.py): looks only — no collider, not in the
            // navmesh, no shadows; it wears the ground's material (the earth of the style)
            var terrain = Prefab($"{LevelPrefabs}/OutsideTerrain.prefab", () =>
            {
                var go = Model("outside_terrain");
                go.name = "OutsideTerrain";
                go.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
                foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
                foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    r.sharedMaterial = M("OutsideGround");
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    r.gameObject.layer = LevelMetrics.CeilingLayer;
                }
                go.layer = LevelMetrics.CeilingLayer;
                return go;
            });

            // Pieces.
            var stairs = Prefab($"{LevelPrefabs}/Stairs.prefab", () => BuildStairs("Stairs", false));
            var stairsMirrored = Prefab($"{LevelPrefabs}/StairsMirrored.prefab", () => BuildStairs("StairsMirrored", true));
            // the apartment block's stairwell: a flight pair stacked floor over floor (Stair.Stacked)
            var stairsStacked = Prefab($"{LevelPrefabs}/StairsStacked.prefab", () => BuildStairs("StairsStacked", false, true));
            var stairsStackedMirrored = Prefab($"{LevelPrefabs}/StairsStackedMirrored.prefab", () => BuildStairs("StairsStackedMirrored", true, true));
            var vent = Prefab($"{LevelPrefabs}/VentFrame.prefab", () => BuildVentFrame(false));
            var highVent = Prefab($"{LevelPrefabs}/HighVentFrame.prefab", () => BuildVentFrame(true));
            var squeeze = Prefab($"{LevelPrefabs}/SqueezeFrame.prefab", BuildSqueezeFrame);
            var breach = Prefab($"{LevelPrefabs}/BreachFrame.prefab", () => BuildRuin("BreachFrame", "breach", LevelMetrics.BreachWidth, LevelMetrics.DoorHeight, false));
            var hole = Prefab($"{LevelPrefabs}/HoleFrame.prefab", () => BuildRuin("HoleFrame", "hole", LevelMetrics.HoleWidth, LevelMetrics.HoleHeight, true));
            var innerWindow = Prefab($"{LevelPrefabs}/InnerWindowFrame.prefab", BuildInnerWindowFrame);
            var railingBay = Prefab($"{LevelPrefabs}/RailingBay.prefab", BuildRailingBay);
            var railingBlock = Prefab($"{LevelPrefabs}/RailingBlock.prefab", BuildRailingBlock);
            var door = Prefab($"{LevelPrefabs}/AutoDoor.prefab", () => BuildAutoDoor());
            // the auto repair shop: steel leaves; the paint booth's door as wide as a car needs
            // hinged doors (Tools/Blender/doors.py, DoorRules): steel for the shop and the plant room, the hospital's laminate
            // door-and-a-half and its swing doors, the thin wooden door between rooms, glazed double doors, the street door
            var shopDoor = Prefab($"{LevelPrefabs}/ShopDoor.prefab", () => BuildHingedDoor("ShopDoor", DoorKind.Steel, LevelMetrics.DoorWidth, "steel_leaf", "steel_leaf", "ShopSteel", "Metal", "ShopSteel"));
            var vehicleDoor = Prefab($"{LevelPrefabs}/VehicleDoor.prefab", () => BuildAutoDoor("VehicleDoor", LevelMetrics.VehicleDoorWidth, "ShopSteel"));
            var clinicDoor = Prefab($"{LevelPrefabs}/ClinicDoor.prefab", () => BuildHingedDoor("ClinicDoor", DoorKind.Clinic, LevelMetrics.DoorWidth, "clinic_leaf", "clinic_leaf", "ClinicDoor", "Metal", "ClinicFrame"));
            var clinicSwingDoor = Prefab($"{LevelPrefabs}/ClinicSwingDoor.prefab", () => BuildHingedDoor("ClinicSwingDoor", DoorKind.ClinicSwing, LevelMetrics.DoorWidth, "clinic_swing_leaf", "clinic_swing_leaf", "ClinicDoor", "Metal", "ClinicFrame"));
            var woodDoor = Prefab($"{LevelPrefabs}/WoodDoor.prefab", () => BuildHingedDoor("WoodDoor", DoorKind.Wood, LevelMetrics.DoorWidth, "wood_leaf", "wood_leaf", "WoodDoor", "Metal", "DoorFrame"));
            var woodDoubleDoor = Prefab($"{LevelPrefabs}/WoodDoubleDoor.prefab", () => BuildHingedDoor("WoodDoubleDoor", DoorKind.WoodDouble, LevelMetrics.DoorWidth, "wood_leaf_glass", "wood_leaf_glass", "WoodDoor", "Metal", "DoorFrame"));
            // the apartment block's lift: its doors on a landing (with the call button) and its cab (Tools/Blender/doors.py lift_*)
            var balcony = Prefab($"{LevelPrefabs}/Balcony.prefab", BuildBalcony);
            var liftDoor = Prefab($"{LevelPrefabs}/LiftDoor.prefab", BuildLiftDoor);
            var liftCab = Prefab($"{LevelPrefabs}/LiftCab.prefab", BuildLiftCab);
            var entranceDoor = Prefab($"{LevelPrefabs}/EntranceDoor.prefab", () => BuildHingedDoor("EntranceDoor", DoorKind.Entrance, LevelMetrics.DoorWidth, "entrance_leaf", "entrance_leaf", "EntranceSteel", "Metal", "EntranceSteel", true));
            // the storage room's door: steel leaves with a hazard band, a big lit sign over it and lit bands on the leaves
            var storageDoor = Prefab($"{LevelPrefabs}/StorageDoor.prefab", () => BuildAutoDoor("StorageDoor", LevelMetrics.DoorWidth, "ShopSteel", "storage_leaf", "ShopYellow", true));
            // a flat's entry door off the apartment block's landing (Tools/Blender/apartments.py flat_leaf)
            var flatDoor = Prefab($"{LevelPrefabs}/FlatDoor.prefab", () => BuildHingedDoor("FlatDoor", DoorKind.Entry, LevelMetrics.DoorWidth, "flat_leaf", "flat_side_leaf", "FlatDoor", "FlatDoorTrim", "Metal"));
            var narrowDoor = Prefab($"{LevelPrefabs}/NarrowDoor.prefab", () => BuildHingedDoor("NarrowDoor", DoorKind.Wood, LevelMetrics.NarrowDoorWidth, "wood_leaf", "wood_leaf", "WoodDoor", "Metal", "DoorFrame"));
            var light = Prefab($"{LevelPrefabs}/RoomLight.prefab", BuildRoomLight);
            var hatch = Prefab($"{LevelPrefabs}/Hatch.prefab", BuildHatch);
            var window = Prefab($"{LevelPrefabs}/Window.prefab", BuildWindow);
            var exitSign = Prefab($"{LevelPrefabs}/ExitSign.prefab", BuildExitSign);

            string p = PropPrefabs + "/";
            var desk = Prefab(p + "Desk.prefab", () => PropObject("Desk", "desk", "PropBody", "PropAccent", "PropScreen"));
            var chair = Prefab(p + "Chair.prefab", () => PropObject("Chair", "chair", "PropAccent", "PropBody", "PropScreen"));
            var rack = Prefab(p + "ServerRack.prefab", () => PropObject("ServerRack", "server_rack", "Rack", "Rack", "RackLights"));
            var cabinet = Prefab(p + "Cabinet.prefab", () =>
            {
                return Opening(PropObject("Cabinet", "cabinet", "PropBody", "PropAccent", "PropScreen")); // the drawers slide out (BodyDrawer0..2)
            });
            var meeting = Prefab(p + "MeetingTable.prefab", () => PropObject("MeetingTable", "meeting_table", "PropBody", "PropAccent", "PropScreen"));
            var plant = Prefab(p + "Plant.prefab", () => PropObject("Plant", "plant", "Plant", "PlantPot", "Plant"));
            var kitchen = Prefab(p + "KitchenCounter.prefab", () => Opening(PropObject("KitchenCounter", "kitchen_counter", "PropBody", "PropAccent", "PropAccent"))); // its doors open (BodyDoor0..3)
            var shelf = Prefab(p + "Shelf.prefab", () => PropObject("Shelf", "shelf", "Rack", "Cardboard", "PropScreen"));
            var boxes = Prefab(p + "Boxes.prefab", () => PropObject("Boxes", "boxes", "Cardboard", "Cardboard", "Cardboard"));
            var cooler = Prefab(p + "WaterCooler.prefab", () => PropObject("WaterCooler", "water_cooler", "PropBody", "PropAccent", "Water"));
            var board = Prefab(p + "Whiteboard.prefab", () => PropObject("Whiteboard", "whiteboard", "Ceiling", "PropAccent", "PropScreen"));
            var sofa = Prefab(p + "Sofa.prefab", () => PropObject("Sofa", "sofa", "Fabric", "PropAccent", "PropAccent"));
            var stall = Prefab(p + "ToiletStall.prefab", () => PropObject("ToiletStall", "toilet_stall", "Stall", "Ceiling", "Ceiling"));
            var pPartition = Prefab(p + "Partition.prefab", () => PropObject("Partition", "partition", "Fabric", "PropAccent", "PropAccent"));
            var pBookshelf = Prefab(p + "Bookshelf.prefab", () => { var go = PropObject("Bookshelf", "bookshelf", "Wood", "Folders", "Folders"); ShelfBooks(go); return go; }); // its books differ every time
            var pPrinter = Prefab(p + "Printer.prefab", () => PropObject("Printer", "printer", "Ceiling", "PropAccent", "PropScreen"));
            var pFridge = Prefab(p + "Fridge.prefab", () => Opening(PropObject("Fridge", "fridge", "Ceiling", "Metal", "Metal"))); // fridge and freezer doors open
            var pCoffeeMachine = Prefab(p + "CoffeeMachine.prefab", () => PropObject("CoffeeMachine", "coffee_machine", "Rack", "Metal", "RackLights"));
            var pRoundTable = Prefab(p + "RoundTable.prefab", () => PropObject("RoundTable", "round_table", "Wood", "Metal", "Metal"));
            var pArmchair = Prefab(p + "Armchair.prefab", () => PropObject("Armchair", "armchair", "Fabric", "PropAccent", "PropAccent"));
            var pCoffeeTable = Prefab(p + "CoffeeTable.prefab", () => PropObject("CoffeeTable", "coffee_table", "Wood", "Metal", "Metal"));
            var pTvStand = Prefab(p + "TvStand.prefab", () => Opening(PropObject("TvStand", "tv_stand", "Wood", "Rack", "PropScreen")));
            var pReceptionDesk = Prefab(p + "ReceptionDesk.prefab", () => PropObject("ReceptionDesk", "reception_desk", "Wood", "Ceiling", "PropScreen"));
            var pTrashBin = Prefab(p + "TrashBin.prefab", () => PropObject("TrashBin", "trash_bin", "Metal", "PropAccent", "PropAccent"));
            var pCoatRack = Prefab(p + "CoatRack.prefab", () => { var go = PropObject("CoatRack", HasModel("coat_stand_bare") ? "coat_stand_bare" : "coat_rack", "Wood", "Metal", "Metal"); HookGarments(go, "coat_stand_bare"); return go; }); // its coats differ every time
            var pLockers = Prefab(p + "Lockers.prefab", () =>
            {
                var go = PropObject("Lockers", "lockers", "Lockers", "PropAccent", "PropAccent");
                go.AddComponent<Stealth.Items.Storage>().Individual = true; // the doors swing open (BodyDoor0..1: two — from the middle)
                return go;
            });
            var pPallet = Prefab(p + "Pallet.prefab", () => PropObject("Pallet", "pallet", "Cardboard", "Wood", "Wood"));
            var pWashBasin = Prefab(p + "WashBasin.prefab", () => PropObject("WashBasin", "wash_basin", "Ceiling", "Metal", "Mirror"));
            var pVendingMachine = Prefab(p + "VendingMachine.prefab", () =>
            {
                var go = PropObject("VendingMachine", "vending_machine", "Vending", "PropAccent", "VendingGlass");
                VendingGoods(go, RealisticModel(go, "vending_window", "Vending", "PropAccent"));
                // Shines forward and a bit down, away from the wall it stands at: nothing reaches the next room or floor.
                var glow = Lamp(go.transform, "Glow", new Vector3(0f, 1.2f, 0.5f), Quaternion.Euler(25f, 0f, 0f), LightType.Spot, new Color(0.7f, 0.85f, 1f), 0.8f, 2.6f);
                glow.spotAngle = 120f;
                glow.innerSpotAngle = 60f;
                return go;
            });
            var pPingPong = Prefab(p + "PingPongTable.prefab", () => PropObject("PingPongTable", "ping_pong_table", "PingPong", "Ceiling", "Ceiling"));
            var pBeanBag = Prefab(p + "BeanBag.prefab", () => PropObject("BeanBag", "bean_bag", "BeanBag", "BeanBag", "BeanBag"));
            var pArcade = Prefab(p + "ArcadeMachine.prefab", () =>
            {
                var go = PropObject("ArcadeMachine", "arcade_machine", "Arcade", "PropAccent", "PropScreen");
                RealisticModel(go, "arcade_cabinet", "Arcade", "PropAccent");
                return go;
            });
            var pSafe = Prefab(p + "Safe.prefab", () =>
            {
                var go = PropObject("Safe", "safe", "SafeMetal", "Metal", "Keypad");
                // Crackable: the keypad turns from red to green (SafeBox keeps it out of the merged furniture).
                var box = go.AddComponent<Stealth.Items.SafeBox>();
                box.Keypad = go.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name.StartsWith("Eye"));
                return go;
            });
            var pTerminal = Prefab(p + "Terminal.prefab", () =>
            {
                // Two looks on one desk's place (Tools/Blender/terminals.py): the robots' control console (hacking, cameras)
                // and the storage door's access kiosk; SecurityTerminal shows the one for its kind. The screens glow in the
                // terminal's colour (SecurityTerminal keeps them out of the merged furniture).
                var go = PropObject("Terminal", "terminal_console", "PropBody", "PropAccent", "Keypad");
                var console = go.transform.GetChild(0).gameObject;
                console.name = "Console";
                var access = Model("terminal_access");
                access.name = "Access";
                access.transform.SetParent(go.transform, false);
                Paint(access, "Metal", "PropAccent", "Keypad");
                foreach (var mf in access.GetComponentsInChildren<MeshFilter>(true)) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                access.SetActive(false);
                var terminal = go.AddComponent<Stealth.Level.SecurityTerminal>();
                terminal.Console = console;
                terminal.Access = access;
                terminal.Screens = console.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name.StartsWith("Eye") && !r.name.StartsWith("EyeScreen"));
                terminal.AccessScreens = access.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name.StartsWith("Eye") && !r.name.StartsWith("EyeScreen"));
                // the monitors show the kind's picture (Tools/Art/generate_screens.py) through a glowing screen material
                var screenMat = TerminalScreenMaterial();
                terminal.Pictures = console.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name.StartsWith("EyeScreen"));
                terminal.AccessPictures = access.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name.StartsWith("EyeScreen"));
                foreach (var r in new[] { terminal.Pictures, terminal.AccessPictures }) if (r != null) r.sharedMaterial = screenMat;
                terminal.CamerasPicture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/Textures/Screens/screen_cameras.png");
                terminal.HackingPicture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/Textures/Screens/screen_hack.png");
                terminal.DoorsPicture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/Textures/Screens/screen_doors.png");
                terminal.DoorsOpenPicture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/Textures/Screens/screen_doors_open.png");
                terminal.MapPicture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/Textures/Screens/screen_map.png");
                return go;
            });
            var pDeskLamp = Prefab(p + "DeskLamp.prefab", () =>
            {
                var go = PropObject("DeskLamp", "desk_lamp", "Metal", "LampShade", "LampBulb", collider: false);
                // The light hangs well over the lamp, shining down on the desk: a pool that fades across the top. Under
                // the shade itself, a hand's breadth from the desk, the same light burnt a white disc into it (the
                // inverse square at 0.27 m) with a halo over half the screen. The lamp's own model casts no shadow
                // (the light is above it).
                NoShadows(go);
                var lamp = Lamp(go.transform, "Light", new Vector3(0f, 0.85f, 0.14f), Quaternion.Euler(84f, 0f, 0f), LightType.Spot, new Color(1f, 0.82f, 0.55f), 1.25f, 3.2f,
                    LightShadows.Soft, UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow); // small: low maps (LightBudget.Cost)
                lamp.spotAngle = 96f;
                lamp.innerSpotAngle = 24f;
                LitFromInside(go);
                return go;
            });
            var pFloorLamp = Prefab(p + "FloorLamp.prefab", () =>
            {
                var go = PropObject("FloorLamp", "floor_lamp", "Metal", "LampShade", "LampBulb");
                var lamp = Lamp(go.transform, "Light", new Vector3(0f, 1.4f, 0f), Quaternion.Euler(90f, 0f, 0f), LightType.Spot, new Color(1f, 0.8f, 0.55f), 1.4f, 4f,
                    LightShadows.Soft);
                lamp.spotAngle = 160f;
                lamp.innerSpotAngle = 90f;
                LitFromInside(go);
                return go;
            });

            var levelArt = DataAsset<LevelArt>(DataDir + "/LevelArt.asset", art =>
            {
                art.LevelRoot = root;
                art.WallBlock = wall;
                art.TrimBlock = trim;
                art.FloorSlab = slab;
                art.Roof = roof;
                art.OutsideGround = ground;
                art.OutsideTerrain = terrain;
                art.Fence = fence;
                art.Stairs = stairs;
                art.StairsMirrored = stairsMirrored;
                art.VentFrame = vent;
                art.HighVentFrame = highVent;
                art.SqueezeFrame = squeeze;
                art.BreachFrame = breach;
                art.HoleFrame = hole;
                art.InnerWindowFrame = innerWindow;
                art.AutoDoor = door;
                art.ShopDoor = shopDoor;
                art.VehicleDoor = vehicleDoor;
                art.RoomLight = light;
                art.Window = window;
                art.ExitSign = exitSign;
                art.DeskLamp = pDeskLamp;
                art.PingPongTable = pPingPong;
                art.BeanBag = pBeanBag;
                art.ArcadeMachine = pArcade;
                art.Safe = pSafe;
                art.Terminal = pTerminal;
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
            // fields newer than the asset: filled without overwriting the rest
            Link(levelArt, ref levelArt.RoomProbe, Prefab($"{LevelPrefabs}/RoomProbe.prefab", BuildRoomProbe));
            Link(levelArt, ref levelArt.BreachFrame, breach);
            Link(levelArt, ref levelArt.HoleFrame, hole);
            Link(levelArt, ref levelArt.InnerWindowFrame, innerWindow);
            Link(levelArt, ref levelArt.HighVentFrame, highVent);
            Link(levelArt, ref levelArt.RailingBay, railingBay);
            Link(levelArt, ref levelArt.RailingBlock, railingBlock);
            Link(levelArt, ref levelArt.OutsideTerrain, terrain);
            Link(levelArt, ref levelArt.Balcony, balcony);
            Link(levelArt, ref levelArt.LiftDoor, liftDoor);
            Link(levelArt, ref levelArt.LiftCab, liftCab);
            Link(levelArt, ref levelArt.WoodDoor, woodDoor);
            Link(levelArt, ref levelArt.WoodDoubleDoor, woodDoubleDoor);
            Link(levelArt, ref levelArt.EntranceDoor, entranceDoor);
            Link(levelArt, ref levelArt.ClinicSwingDoor, clinicSwingDoor);
            Link(levelArt, ref levelArt.NarrowDoor, narrowDoor);
            Link(levelArt, ref levelArt.StairsStacked, stairsStacked);
            Link(levelArt, ref levelArt.StairsStackedMirrored, stairsStackedMirrored);
            Link(levelArt, ref levelArt.FlatDoor, flatDoor);
            Link(levelArt, ref levelArt.ShopDoor, shopDoor);
            Link(levelArt, ref levelArt.VehicleDoor, vehicleDoor);
            Link(levelArt, ref levelArt.ClinicDoor, clinicDoor);
            Link(levelArt, ref levelArt.StorageDoor, storageDoor);
            Link(levelArt, ref levelArt.Displays, M("Displays"));
            Link(levelArt, ref levelArt.Mirror, M("Mirror"));
            LinkAll(levelArt, ref levelArt.Pictures, new[] { M("BoardSheet"), M("MirrorGlass"), M("ProjectorCloth") });
            LinkAll(levelArt, ref levelArt.Screens, new[] { M("PropScreen"), M("ScreenOff"), M("ScreenStatic"), M("ScreenCracked"), M("ScreenNoSignal"), M("ScreenBoot") });
            BuildFeatureArt(levelArt);
            BuildCityArt(levelArt);
            BuildRoomProps(levelArt);
            return levelArt;
        }

        // ------------------------------------------------------------------ structure

        static GameObject BuildLevelRoot()
        {
            var root = new GameObject("LevelRoot");
            root.AddComponent<LightBudget>(); // only the lamps that fit the shadow atlas are drawn
            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~(1 << LevelMetrics.CeilingLayer);
            return root;
        }

        /// <summary>A unit cube with a material: scaled to size by the level builder.</summary>
        /// <summary>
        /// Structure block (wall, slab, roof). Casts shadows from both faces: walls and slabs are what keeps the moon and
        /// the lamps of other rooms out — without them light shines straight through the building.
        /// </summary>
        static GameObject Block(string name, Material mat, bool collider = true, int layer = 0)
        {
            var go = Cube(null, name, Vector3.zero, Vector3.one, mat, collider);
            go.layer = layer;
            go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.TwoSided;
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
        static GameObject BuildStairs(string name, bool mirrored, bool stacked = false)
        {
            var root = new GameObject(name);
            var mod = root.AddComponent<NavMeshModifier>();
            mod.overrideArea = true;
            mod.area = LevelMetrics.StairsArea;
            foreach (var box in StairGeometry.Boxes(stacked))
            {
                var c = box.Center;
                if (mirrored) c.x = -c.x;
                Cube(root.transform, box.Name, c, box.Size, M("DoorFrame"));
            }
            AddStairLooks(root, mirrored, stacked); // what is seen: one of the built looks over these boxes
            return root;
        }

        /// <summary>
        /// A ventilation duct through the wall (modelled along local X; the sill is a wall piece): a sheet-metal lining inside
        /// the hole, a flange on both faces, and the grille someone unscrewed leaning against the wall. A high one sits under
        /// the ceiling with a stack of two crates under it on both sides (the only colliders here): jump onto the low crate,
        /// onto the high one, crouch and step into the duct.
        /// </summary>
        static GameObject BuildVentFrame(bool high)
        {
            var root = new GameObject(high ? "HighVentFrame" : "VentFrame");
            float w = LevelMetrics.ClearWidth(LevelMetrics.VentWidth), h = LevelMetrics.VentHeight, y0 = high ? LevelMetrics.HighVentSill : LevelMetrics.VentSill;
            float t = LevelMetrics.WallThickness, sheet = 0.02f, flange = 0.07f;
            // the lining and the flanges round the hole on both faces (Tools/Blender/vent.py vent_duct: folded seams, screws)
            var duct = Model("vent_duct");
            duct.transform.SetParent(root.transform, false);
            duct.transform.localPosition = new Vector3(0f, y0, 0f);
            Paint(duct, "Metal", "Metal", "Metal");
            // the grille: a frame with louvres (vent_grille, its foot at its origin, sized to its place) — shut in the hole on
            // side A (its collider keeps everyone out) until a player works it loose (VentGrille), then leaning on the wall
            // beside the hole (a high vent's: on the desk under it)
            float gw = w * 0.9f, gh = h * 0.9f;
            void Louvres(Transform grille, float lw, float lh)
            {
                var g = Model("vent_grille");
                g.transform.SetParent(grille, false);
                g.transform.localScale = new Vector3(lw / (w - 2f * sheet), lh / (h - 2f * sheet), 1f);
                Paint(g, "Metal", "Metal", "Metal");
            }
            var removed = new GameObject("GrilleRemoved").transform;
            removed.SetParent(root.transform, false);
            removed.localPosition = high ? new Vector3(w / 2f + flange + gw / 2f + 0.06f, LevelMetrics.DeskTop, t / 2f + 0.25f)
                : new Vector3(w / 2f + flange + gw / 2f + 0.06f, 0f, t / 2f + 0.25f); // the top rests on the wall face
            removed.localRotation = Quaternion.Euler(-10f, 0f, 0f); // its foot down, its top against the wall
            Louvres(removed, gw, gh * (high ? 0.8f : 1f));
            removed.gameObject.SetActive(false);
            removed.gameObject.AddComponent<KeepUnmerged>(); // shown and hidden in play: never merged
            var mounted = new GameObject("GrilleMounted").transform;
            mounted.SetParent(root.transform, false);
            mounted.localPosition = new Vector3(0f, y0 + sheet, t / 2f - 0.01f);
            Louvres(mounted, w - 2f * sheet, h - 2f * sheet);
            mounted.gameObject.AddComponent<KeepUnmerged>();
            var shut = mounted.gameObject.AddComponent<BoxCollider>();
            shut.center = new Vector3(0f, (h - 2f * sheet) / 2f, 0f);
            shut.size = new Vector3(w - 2f * sheet, h - 2f * sheet, 0.06f);
            var hole = Child(root.transform, "Hole", new Vector3(0f, y0 + h / 2f, 0f)).transform;
            var vent = root.AddComponent<VentGrille>();
            vent.Mounted = mounted.gameObject;
            vent.Removed = removed.gameObject;
            vent.Hole = hole;
            return root;
        }

        /// <summary>The wall the inner window's frame (Tools/Blender/ruins.py) was made for, metres thick: the prefab stretches it across today's.</summary>
        const float RuinWall = 0.2f;
        /// <summary>How many ways a hole in a wall broke (ruins.py VARIANTS).</summary>
        const int RuinVariants = 3;

        /// <summary>
        /// A breach or a hole knocked through a wall (Tools/Blender/ruins.py breach_k / hole_k): toothed brickwork in the
        /// wall's thickness, the mortar behind it, heaps of rubble on both faces — in <see cref="RuinVariants"/> variants
        /// (<see cref="RuinFrame"/>: the house shows one) — and on each face the torn plaster round the hole
        /// (<see cref="RuinSkin"/>), which the style dresses in the room's wall finish. Along local X, no colliders (the wall
        /// pieces keep the shape). A hole also keeps robots out with a navmesh volume: a player crawls through, a robot does not.
        /// </summary>
        static GameObject BuildRuin(string name, string model, float nominal, float height, bool robotsOut)
        {
            var root = new GameObject(name);
            float w = LevelMetrics.ClearWidth(nominal), wall = LevelMetrics.WallThickness;
            var brick = RuinMaterial("Ruin_Brick", "stone", "8A4A36");
            var mortar = RuinMaterial("Ruin_Mortar", "stone", "8C8880");
            var plaster = RuinMaterial("Ruin_Plaster", "paper", "CFCBC0");
            var frame = root.AddComponent<RuinFrame>();
            frame.Variants = new GameObject[RuinVariants];
            frame.Skins = new MeshRenderer[RuinVariants * 2];
            for (int k = 0; k < RuinVariants; k++)
            {
                var variant = Child(root.transform, $"V{k}", Vector3.zero);
                var m = Model($"{model}_{k}");
                m.name = "Rubble";
                m.transform.SetParent(variant.transform, false);
                foreach (var r in m.GetComponentsInChildren<MeshRenderer>(true))
                {
                    string n = r.gameObject.name;
                    r.sharedMaterial = n.StartsWith("Brick") ? brick : n.StartsWith("Mortar") ? mortar : n.StartsWith("Plaster") ? plaster : M("Metal");
                }
                // the plaster skin on both faces: the bare wall's material here, the room's wall finish once StyleDecor
                // dresses it (its +Z looks out of the wall)
                var skin = RuinSkin(w, height, 7919 * (k + 1) + (robotsOut ? 104729 : 0), $"{model}_skin_{k}");
                for (int f = 0; f < 2; f++)
                {
                    var part = Child(variant.transform, f == 0 ? "SkinFront" : "SkinBack", new Vector3(0f, 0f, f == 0 ? -wall / 2f : wall / 2f));
                    if (f == 0) part.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    part.AddComponent<MeshFilter>().sharedMesh = skin;
                    var r = part.AddComponent<MeshRenderer>();
                    r.sharedMaterial = M("Wall");
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    frame.Skins[2 * k + f] = r;
                }
                frame.Variants[k] = variant;
                variant.SetActive(k == 0);
            }
            if (robotsOut)
            {
                var block = root.AddComponent<NavMeshModifierVolume>();
                block.center = new Vector3(0f, 1.2f, 0f);
                block.size = new Vector3(w + 0.6f, 2.4f, 1.6f);
                block.area = 1; // Not Walkable
            }
            return root;
        }

        /// <summary>
        /// The plaster left round a hole in a wall, on one face: a strip from the opening's straight edge (up one jamb, along
        /// the head, down the other) into the gap to a torn line — a few centimetres here, nothing there — with the thickness
        /// of the coat along the tear. In the face's own frame: x along the wall, y up, the face at z 0 looking along +Z.
        /// </summary>
        static Mesh RuinSkin(float width, float height, int seed, string asset)
        {
            const float step = 0.045f, coat = 0.014f;
            var rng = new System.Random(seed);
            float hw = width / 2f;
            // the path round the opening and the way into the gap at every point of it
            var path = new List<(Vector2 at, Vector2 into)>();
            for (float y = 0f; y < height - 0.001f; y += step) path.Add((new Vector2(-hw, y), Vector2.right));
            path.Add((new Vector2(-hw, height), new Vector2(1f, -1f).normalized));
            for (float x = -hw + step; x < hw - 0.001f; x += step) path.Add((new Vector2(x, height), Vector2.down));
            path.Add((new Vector2(hw, height), new Vector2(-1f, -1f).normalized));
            for (float y = height - step; y > 0.001f; y -= step) path.Add((new Vector2(hw, y), Vector2.left));
            path.Add((new Vector2(hw, 0f), Vector2.left));

            var verts = new List<Vector3>();
            var tris = new List<int>();
            void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 facing)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0f) (b, c) = (c, b);
                int n = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                tris.Add(n); tris.Add(n + 1); tris.Add(n + 2);
            }
            float depth = 0.02f, target = 0.03f;
            Vector3 lastOuter = default, lastInner = default;
            for (int i = 0; i < path.Count; i++)
            {
                // the tear wanders: stretches where the coat held, bites where it came away to the edge
                if (rng.NextDouble() < 0.22) target = rng.NextDouble() < 0.3 ? 0.004f : 0.015f + (float)rng.NextDouble() * 0.06f;
                depth = Mathf.Clamp(Mathf.Lerp(depth, target, 0.55f) + ((float)rng.NextDouble() - 0.5f) * 0.016f, 0.003f, 0.08f);
                var (at, into) = path[i];
                Vector3 outer = at, inner = at + into * depth;
                if (i > 0)
                {
                    Tri(lastOuter, outer, inner, Vector3.forward);
                    Tri(lastOuter, inner, lastInner, Vector3.forward);
                    // the coat's thickness along the tear, looking into the gap
                    var back = Vector3.back * coat;
                    var gap = (Vector3)((path[i - 1].into + into) * 0.5f);
                    Tri(lastInner, inner, inner + back, gap);
                    Tri(lastInner, inner + back, lastInner + back, gap);
                }
                lastOuter = outer;
                lastInner = inner;
            }
            var mesh = new Mesh { name = asset };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            var uv = new List<Vector2>(verts.Count);
            foreach (var v in verts) uv.Add(new Vector2(v.x, v.y));
            mesh.SetUVs(0, uv);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EnsureFolder(Root + "/Art/Models/Level");
            return SaveMesh(mesh, Root + $"/Art/Models/Level/{asset}.asset");
        }

        /// <summary>
        /// What stops people in a shut doorway (<see cref="AutoDoor.Blocker"/>): a box standing on the floor, centred at
        /// <paramref name="x"/> along the wall and <paramref name="z"/> across it; the door turns it off while open.
        /// </summary>
        static BoxCollider DoorBlocker(GameObject root, float x, float z, float width, float height, float depth)
        {
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(x, height / 2f, z);
            box.size = new Vector3(width, height, depth);
            return box;
        }

        /// <summary>The frame of a window between rooms without its glass (Tools/Blender/ruins.py), along local X, no collider.</summary>
        static GameObject BuildInnerWindowFrame()
        {
            var root = new GameObject("InnerWindowFrame");
            var m = Model("inner_window_frame");
            m.name = "Frame";
            m.transform.SetParent(root.transform, false);
            m.transform.localScale = new Vector3(1f, 1f, LevelMetrics.WallThickness / RuinWall); // on both faces of today's wall
            Paint(m, "WindowFrame", "WindowFrame", "WindowFrame");
            return root;
        }

        /// <summary>
        /// Frame of a narrow slot (both faces of the wall, along local X, no collider) and a navmesh volume that keeps
        /// robots out of it: a player squeezes through, a robot does not.
        /// </summary>
        static GameObject BuildSqueezeFrame()
        {
            var root = new GameObject("SqueezeFrame");
            float w = LevelMetrics.ClearWidth(LevelMetrics.SqueezeWidth), h = LevelMetrics.DoorHeight;
            foreach (float z in new[] { LevelMetrics.WallThickness / 2f + 0.02f, -(LevelMetrics.WallThickness / 2f + 0.02f) })
            {
                ChamferBox(root.transform, "Top", new Vector3(0, h, z), new Vector3(w + 0.12f, 0.06f, 0.04f), M("DoorFrame"));
                ChamferBox(root.transform, "Left", new Vector3(-w / 2f - 0.03f, h / 2f, z), new Vector3(0.06f, h, 0.04f), M("DoorFrame"));
                ChamferBox(root.transform, "Right", new Vector3(w / 2f + 0.03f, h / 2f, z), new Vector3(0.06f, h, 0.04f), M("DoorFrame"));
            }
            var block = root.AddComponent<NavMeshModifierVolume>();
            block.center = new Vector3(0f, 1.2f, 0f);
            block.size = new Vector3(w + 0.6f, 2.4f, 1.6f);
            block.area = 1; // Not Walkable
            return root;
        }

        /// <summary>
        /// Sliding door: two panels and a blocker collider (enabled only while closed). Ignored by the navmesh:
        /// robots plan through doorways and the door opens when they arrive.
        /// </summary>
        static GameObject BuildAutoDoor() => BuildAutoDoor("AutoDoor", LevelMetrics.DoorWidth, "DoorFrameAlu", accent: "DoorSeal");

        /// <summary>
        /// A sliding door <paramref name="width"/> wide (nominal; the leaves stretched to it) in <paramref name="paint"/>.
        /// <paramref name="leafModel"/>: the leaf (and <c>_b</c>, its twin); <paramref name="accent"/>: its Accent part's paint.
        /// <paramref name="sign"/>: instead of the small lock lamps, a big lit sign over the door on both sides and a lit band
        /// across each leaf on both faces — red while locked, green while open, readable across the room (the storage room).
        /// </summary>
        static GameObject BuildAutoDoor(string name, float width, string paint, string leafModel = "door_leaf", string accent = null, bool sign = false)
        {
            bool glass = name == "AutoDoor"; // (the vehicle door keeps the leaf's panes in its steel)
            var root = new GameObject(name);
            root.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            float h = LevelMetrics.DoorHeight - 0.02f, w = width;
            float stretch = width / LevelMetrics.DoorWidth;
            var door = root.AddComponent<AutoDoor>();
            door.Width = w;
            // the two leaves: a model of their own (Tools/Blender/door_leaf.py), so a style can give them its own form
            // (the derelict riveted steel leaf); B is A turned round, its pull and seal on the meeting edge too
            Transform Leaf(string name, string file, float x, float turn)
            {
                var leaf = Model(file);
                leaf.name = name;
                leaf.transform.SetParent(root.transform, false);
                leaf.transform.localPosition = new Vector3(x, 0f, 0f);
                leaf.transform.localRotation = Quaternion.Euler(0f, turn, 0f);
                leaf.transform.localScale = new Vector3(stretch, 1f, 1f);
                Paint(leaf, paint, accent ?? paint, paint);
                foreach (var r in leaf.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    if (glass && r.gameObject.name.StartsWith("Glass")) r.sharedMaterial = M("DoorGlass"); // the sliding glass door's satin panes
                }
                return leaf.transform;
            }
            door.PanelA = Leaf("PanelA", leafModel, -w / 4f, 0f);
            door.PanelB = Leaf("PanelB", leafModel + "_b", w / 4f, 180f); // its texture shifted: the halves do not mirror
            door.Blocker = DoorBlocker(root, 0f, 0f, w, h, 0.1f);
            // Lock lamps above the door on both sides: red while locked, green once hacked (hidden on doors without a lock).
            if (!sign)
                door.LockLamps = OverDoorLamps(root, h);
            else
            {
                var lamps = new List<Renderer>();
                var pictures = new List<Renderer>();
                float face = LevelMetrics.WallThickness / 2f;
                var over = LevelBuilder.StorageSign; // between the door's trim and the ceiling
                foreach (float side in new[] { 1f, -1f })
                {
                    // the sign: a steel housing on the wall over the door, its whole face lit
                    ChamferBox(root.transform, side > 0 ? "SignHousingA" : "SignHousingB", new Vector3(0f, over.Center.y, side * (face + 0.035f)), new Vector3(2f * over.HalfWidth, 2f * over.HalfHeight, 0.07f), M("ShopSteel"));
                    foreach (float end in new[] { -1f, 1f })
                        lamps.Add(Cube(root.transform, $"SignLamp{(side > 0 ? "A" : "B")}{(end < 0f ? "L" : "R")}", new Vector3(end * 0.395f, over.Center.y, side * (face + 0.072f)), new Vector3(0.2f, 0.17f, 0.012f), M("LockLamp"), false).GetComponent<Renderer>());
                    // between the lamps: the picture its terminal shows (the shut doorway and padlock, or the open one)
                    var picture = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    picture.name = side > 0 ? "SignPictureA" : "SignPictureB";
                    Object.DestroyImmediate(picture.GetComponent<Collider>());
                    picture.transform.SetParent(root.transform, false);
                    picture.transform.localPosition = new Vector3(0f, over.Center.y, side * (face + 0.0745f));
                    picture.transform.localRotation = Quaternion.Euler(0f, side > 0 ? 180f : 0f, 0f);
                    picture.transform.localScale = new Vector3(0.368f, 0.23f, 1f);
                    var pr = picture.GetComponent<MeshRenderer>();
                    pr.sharedMaterial = TerminalScreenMaterial();
                    pr.shadowCastingMode = ShadowCastingMode.Off;
                    pictures.Add(pr);
                    // a band across each leaf, on the leaf (it slides with it), in front of its frame
                    foreach (var panel in new[] { door.PanelA, door.PanelB })
                        lamps.Add(Cube(panel, $"Band{(side > 0 ? "A" : "B")}", new Vector3(0f, 1.9f, side * 0.04f), new Vector3(0.62f, 0.07f, 0.01f), M("LockLamp"), false).GetComponent<Renderer>());
                }
                door.LockLamps = lamps.ToArray();
                door.Pictures = pictures.ToArray();
                door.LockedPicture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/Textures/Screens/screen_doors.png");
                door.OpenPicture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/Textures/Screens/screen_doors_open.png");
            }
            foreach (var lamp in door.LockLamps) lamp.shadowCastingMode = ShadowCastingMode.Off;
            // a locked (hackable) door: a hasp across the seam with a padlock hanging from it, on both faces
            door.Padlocks = new[] { 1f, -1f }.Select(side =>
            {
                var holder = Child(root.transform, side > 0 ? "PadlockA" : "PadlockB", new Vector3(0f, 1.2f, side * 0.032f)).transform;
                holder.localRotation = Quaternion.Euler(0f, side > 0 ? 0f : 180f, 0f);
                ChamferBox(holder, "Hasp", new Vector3(0f, 0f, 0.008f), new Vector3(0.2f, 0.05f, 0.016f), M("Metal"), 0.004f);
                var lockModel = Model("padlock");
                lockModel.transform.SetParent(holder, false);
                lockModel.transform.localPosition = new Vector3(0f, 0.01f, 0.035f);
                Paint(lockModel, "Gold", "Metal", "Metal");
                NoShadows(lockModel);
                holder.gameObject.SetActive(false);
                return holder;
            }).ToArray();
            // a key door: the lock panels beside the frame on both sides (shown only on doors of LevelLayout.KeyDoors)
            var keyLock = root.AddComponent<KeyLock>();
            keyLock.Door = door;
            // (on leaf A by the meeting edge, on both faces of the 6 cm leaf: it slides away with it)
            keyLock.Panels = KeyLockPanels(door.PanelA, root.transform.TransformPoint(new Vector3(-0.17f, KeyLockY, 0.035f)),
                root.transform.TransformPoint(new Vector3(-0.17f, KeyLockY, -0.035f)));
            if (sign) { door.LockUnits = new GameObject[0]; door.UnitLamps = new Renderer[0]; }
            else LockUnits(door, door.PanelA, root.transform.TransformPoint(new Vector3(-0.165f, KeyLockY, 0.03f)), root.transform.TransformPoint(new Vector3(-0.165f, KeyLockY, -0.03f)));
            root.AddComponent<DoorHandle>().Door = door;
            Muffles(root, SoundMaterial.Door); // only while closed: the blocker collider is off when open
            return root;
        }

        /// <summary>
        /// The robots' electronic lock (Tools/Blender/lock_unit.py) on both faces of a leaf: at the world points
        /// <paramref name="front"/> (+Z face) and <paramref name="back"/>, its bolt towards the leaf's free edge on +X. Shown on
        /// doors with such a lock (<see cref="AutoDoor.LockUnits"/>); it moves with the leaf.
        /// </summary>
        static void LockUnits(AutoDoor door, Transform leaf, Vector3 front, Vector3 back)
        {
            var lamps = new List<Renderer>();
            door.LockUnits = new[] { 1f, -1f }.Select(side =>
            {
                var unit = Model("lock_unit");
                unit.name = side > 0 ? "LockUnitA" : "LockUnitB";
                unit.transform.SetParent(leaf, false);
                unit.transform.position = side > 0 ? front : back;
                unit.transform.rotation = leaf.root.rotation * Quaternion.Euler(0f, side > 0 ? 0f : 180f, 0f);
                var ls = leaf.lossyScale;
                // the model's bolt is on its −X: on the +Z face mirrored, on the other turned round
                unit.transform.localScale = new Vector3((side > 0 ? -1f : 1f) / Mathf.Abs(ls.x), 1f / Mathf.Abs(ls.y), 1f / Mathf.Abs(ls.z));
                Paint(unit, "Metal", "PropAccent", "LockLamp");
                foreach (var r in unit.GetComponentsInChildren<Renderer>())
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                    if (r.gameObject.name.StartsWith("Eye")) lamps.Add(r);
                }
                unit.SetActive(false);
                return unit;
            }).ToArray();
            door.UnitLamps = lamps.ToArray();
        }

        /// <summary>The height of a key lock's panel on a leaf: over the handle, at the hand.</summary>
        const float KeyLockY = 1.32f;

        /// <summary>
        /// A key door's lock panels (shown only on doors of LevelLayout.KeyDoors): on the door's leaf itself, one on each
        /// face (world points <paramref name="front"/> on its +Z face, <paramref name="back"/> on its −Z face), so they move
        /// with the leaf. They used to stand on the wall beside the frame.
        /// </summary>
        static GameObject[] KeyLockPanels(Transform leaf, Vector3 front, Vector3 back)
        {
            return new[] { 1f, -1f }.Select(side =>
            {
                var panel = Model("key_lock");
                panel.name = side > 0 ? "KeyLockA" : "KeyLockB";
                panel.transform.SetParent(leaf, false);
                panel.transform.position = side > 0 ? front : back;
                // facing out of its face (the model faces its +Z; the hinge of a +X leaf is mirrored: keep it the world's way)
                panel.transform.rotation = leaf.root.rotation * Quaternion.Euler(0f, side > 0 ? 0f : 180f, 0f);
                var ls = leaf.lossyScale;
                panel.transform.localScale = new Vector3(0.8f / Mathf.Abs(ls.x), 0.8f / Mathf.Abs(ls.y), 0.8f / Mathf.Abs(ls.z));
                Paint(panel, "Metal", "Metal", "LockLamp");
                foreach (var r in panel.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                return panel;
            }).ToArray();
        }

        /// <summary>
        /// A hinged door of <paramref name="kind"/> (<see cref="DoorRules"/>) for a doorway <paramref name="nominal"/> wide. Its
        /// leaves open to its +Z (the level builder turns it to the side it opens to): the main leaf hinged on the −X jamb, the
        /// second on the +X jamb — a narrow one that opens only for robots (a door-and-a-half) or its twin (double doors;
        /// both ways: hinged in the wall's middle). The leaves (<paramref name="leaf"/>, <paramref name="second"/>: models of
        /// Tools/Blender/doors.py, 0.8 x 2.24 m, stretched to their span) hang under hinge pivots; the hinge pivot of a leaf
        /// on the +X jamb is mirrored (scale −1 on X), and <see cref="AutoDoor.HingeRight"/> mirrors both to hang them the
        /// other way. A frame lines the opening, flush with both faces (the style's trims cover them); a street door also has
        /// casings and a sill. Handles at the free edges (a lever on both faces; a pull on steel), barrel hinges, a closer and
        /// an intercom on the street door, push plates on swing doors; the lock lamps, padlocks and key panels of every door.
        /// </summary>
        static GameObject BuildHingedDoor(string name, DoorKind kind, float nominal, string leaf, string second, string paint, string accent, string frame,
            bool exterior = false)
        {
            var root = new GameObject(name);
            root.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            float clear = LevelMetrics.ClearWidth(nominal), height = LevelMetrics.DoorHeight, wall = LevelMetrics.WallThickness, lining = DoorRules.Lining;
            float t = DoorRules.Thickness(kind);
            bool both = DoorRules.BothWays(kind), twin = DoorRules.Double(kind);
            var (mainW, secondW) = DoorRules.Leaves(kind, clear);
            var door = root.AddComponent<AutoDoor>();
            door.Width = clear;
            door.SecondForRobots = !twin;
            door.BothWays = both;
            door.SwingAngle = -90f; // the −X leaf's free edge out to +Z
            door.SwingTime = kind is DoorKind.Entrance or DoorKind.Entry ? 0.7f : 0.5f;
            door.Sound = kind is DoorKind.Steel or DoorKind.Entrance or DoorKind.Entry ? DoorSound.Heavy : DoorSound.Swing;

            // the frame: jambs and head lining the opening, flush with the faces (+5 mm)
            var fm = M(frame);
            float depth = wall + 0.01f;
            foreach (float side in new[] { -1f, 1f })
                ChamferBox(root.transform, "Jamb", new Vector3(side * (clear / 2f - lining / 2f), height / 2f, 0f), new Vector3(lining, height, depth), fm);
            ChamferBox(root.transform, "Head", new Vector3(0f, height - lining / 2f, 0f), new Vector3(clear, lining, depth), fm);
            if (exterior)
            {
                const float casing = 0.12f, casingDepth = 0.03f;
                foreach (float side in new[] { -1f, 1f })
                    foreach (float face in new[] { -1f, 1f })
                        ChamferBox(root.transform, "Casing", new Vector3(side * (clear / 2f - lining + casing / 2f), (height + casing - lining) / 2f, face * (wall / 2f + casingDepth / 2f)),
                            new Vector3(casing, height + casing - lining, casingDepth), fm);
                foreach (float face in new[] { -1f, 1f })
                    ChamferBox(root.transform, "CasingHead", new Vector3(0f, height - lining + casing / 2f, face * (wall / 2f + casingDepth / 2f)),
                        new Vector3(clear + 2f * (casing - lining), casing, casingDepth), fm);
                ChamferBox(root.transform, "Sill", new Vector3(0f, 0.01f, 0f), new Vector3(clear, 0.02f, depth), M("Metal"));
            }
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;

            // the leaves under their hinge pivots: the pivot on the leaf's opening face (both ways: in the wall's middle),
            // the leaf from the jamb towards the middle; a pivot on the +X jamb is mirrored, so one layout serves both
            float edge = clear / 2f - lining, gap = 0.003f;
            float hingeZ = both ? 0f : wall / 2f - 0.012f, leafZ = both ? 0f : -t / 2f;
            float leafH = height - lining - 0.012f;
            const float ModelW = 0.8f, ModelH = 2.24f;
            var mHw = M(accent);
            Transform Leaf(string hingeName, float side, float w, string model, bool hardware)
            {
                var hinge = Child(root.transform, hingeName, new Vector3(side * (edge - (both ? t / 2f : 0f)), 0f, hingeZ)).transform;
                hinge.localScale = new Vector3(side < 0f ? 1f : -1f, 1f, 1f);
                var body = Model(model);
                body.name = "Leaf";
                body.transform.SetParent(hinge, false);
                body.transform.localPosition = new Vector3(gap + (w - 2f * gap) / 2f, 0.008f, leafZ);
                // flat_leaf (apartments.py) has its lock on Blender +X, Unity −X: turned round, the lock is at the free edge
                float flip = model == "flat_leaf" ? -1f : 1f;
                body.transform.localScale = new Vector3(flip * (w - 2f * gap) / ModelW, leafH / ModelH, 1f);
                Paint(body, paint, accent, "StairGlass");
                foreach (var r in body.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                float free = w - 0.075f; // the handle side, from the hinges
                float faceF = leafZ + t / 2f, faceB = leafZ - t / 2f;
                void Piece(string file, string partName, Vector3 at, bool back, Material mat)
                {
                    var go = Model(file);
                    go.name = partName;
                    go.transform.SetParent(hinge, false);
                    go.transform.localPosition = at;
                    go.transform.localScale = new Vector3(1f, 1f, back ? -1f : 1f); // the other face: mirrored through the leaf
                    foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        r.sharedMaterial = r.gameObject.name.StartsWith("Eye") ? M("PropScreen") : r.gameObject.name.StartsWith("Body") ? M("Metal") : mat;
                        r.shadowCastingMode = ShadowCastingMode.Off;
                    }
                }
                if (hardware)
                {
                    switch (kind)
                    {
                        case DoorKind.Wood: case DoorKind.WoodDouble: case DoorKind.Clinic:
                            Piece("door_lever", "LeverA", new Vector3(free, 1.0f, faceF), false, mHw);
                            Piece("door_lever", "LeverB", new Vector3(free, 1.0f, faceB), true, mHw);
                            break;
                        case DoorKind.Steel:
                            Piece("door_lever", "LeverA", new Vector3(free, 1.0f, faceF), false, M("Metal"));
                            Piece("door_lever", "LeverB", new Vector3(free, 1.0f, faceB), true, M("Metal"));
                            Piece("door_closer", "Closer", new Vector3(0.22f, leafH - 0.06f, faceF), false, M("Metal"));
                            break;
                        case DoorKind.Entrance:
                            Piece("door_pull", "PullA", new Vector3(free - 0.02f, 1.1f, faceF), false, M("Metal"));
                            Piece("door_pull", "PullB", new Vector3(free - 0.02f, 1.1f, faceB), true, M("Metal"));
                            Piece("door_closer", "Closer", new Vector3(0.22f, leafH - 0.06f, faceF), false, M("Metal"));
                            Piece("intercom", "Intercom", new Vector3(free - 0.2f, 1.42f, faceF), false, M("Metal"));
                            break;
                        case DoorKind.ClinicSwing:
                            foreach (float f in new[] { faceF + 0.003f, faceB - 0.003f })
                                ChamferBox(hinge, "PushPlate", new Vector3(w - 0.12f, 1.25f, f), new Vector3(0.1f, 0.3f, 0.006f), M("Metal"), 0.002f);
                            break;
                    }
                }
                // barrel hinges on the hinged edge (a swing door turns on floor and head pivots: none to see)
                if (!both)
                    foreach (float y in kind is DoorKind.Entrance or DoorKind.Entry ? new[] { 0.28f, leafH / 2f, leafH - 0.28f } : new[] { 0.25f, leafH - 0.25f })
                        Piece("door_hinge", "Hinge", new Vector3(0f, y, 0.0f), false, M("Metal"));
                return hinge;
            }
            door.Swing = Leaf("HingeMain", -1f, mainW, leaf, true);
            door.SwingB = Leaf("HingeSecond", 1f, secondW, second, twin);
            if (kind == DoorKind.Entry)
            {
                // the flat's number on the brass plate (flat_leaf's Trim at 1.72 m) of the face onto the landing (+Z): a small
                // world-space text drawn after the style filter, readable in every style; the builder writes the number
                const float pw = 90f, ph = 55f;
                var plate = new GameObject("Number", typeof(RectTransform));
                plate.transform.SetParent(door.Swing, false);
                var canvas = plate.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                plate.AddComponent<UnityEngine.UI.CanvasScaler>().dynamicPixelsPerUnit = 6f;
                var rt = (RectTransform)plate.transform;
                rt.sizeDelta = new Vector2(pw, ph);
                rt.localScale = Vector3.one * 0.001f; // 9 x 5.5 cm, the plate's size
                rt.localPosition = new Vector3(gap + (mainW - 2f * gap) / 2f, 0.008f + 1.72f * leafH / ModelH, leafZ + t / 2f + 0.0215f);
                rt.localRotation = Quaternion.Euler(0f, 180f, 0f); // a canvas faces its −Z: turned to face the landing
                door.Number = Txt(rt, "", 40, TextAnchor.MiddleCenter, new Color(0.2f, 0.15f, 0.08f), true, false);
                SetLayerRecursive(plate, LevelMetrics.WorldTextLayer);
            }

            // blockers: the whole opening for twin leaves; a door-and-a-half's main and narrow leaves apart
            float bz = both ? 0f : hingeZ - t / 2f;
            float seam = -edge + mainW;
            if (twin) door.Blocker = DoorBlocker(root, 0f, bz, clear, height, 0.1f);
            else
            {
                door.Blocker = DoorBlocker(root, (-clear / 2f + seam) / 2f, bz, seam + clear / 2f, height, 0.1f);
                door.BlockerB = DoorBlocker(root, (seam + clear / 2f) / 2f, bz, clear / 2f - seam, height, 0.1f);
            }
            // the key panel on the main leaf over its handle, on both faces (hinge-local: the free edge at mainW − 0.075)
            float keyX = mainW - 0.15f;
            DoorLocks(root, door, nominal, LevelMetrics.DoorHeight - 0.02f, twin ? 0f : seam, both ? t / 2f : hingeZ, both ? -t / 2f : hingeZ - t,
                door.Swing, door.Swing.TransformPoint(new Vector3(keyX, KeyLockY, leafZ + t / 2f + 0.002f)),
                door.Swing.TransformPoint(new Vector3(keyX, KeyLockY, leafZ - t / 2f - 0.002f)));
            float unitY = kind == DoorKind.ClinicSwing ? 0.86f : KeyLockY; // (under a swing door's push plate)
            LockUnits(door, door.Swing, door.Swing.TransformPoint(new Vector3(keyX, unitY, leafZ + t / 2f)), door.Swing.TransformPoint(new Vector3(keyX, unitY, leafZ - t / 2f)));
            root.AddComponent<DoorHandle>().Door = door;
            Muffles(root, SoundMaterial.Door); // only while closed: the blockers are off when open
            return root;
        }

        /// <summary>
        /// The lift's doors on a landing (+Z out onto it): a steel portal lining the doorway with casings on the landing's face,
        /// a lamp over it (lit while the cab is here) between two arrows, two sliding panels that part into the wall, the
        /// blocker, the call button on a plate beside the portal; a no-walk volume in the doorway keeps robots out of the shaft.
        /// </summary>
        static GameObject BuildLiftDoor()
        {
            var root = new GameObject("LiftDoor");
            root.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            root.AddComponent<KeepUnmerged>(); // its panels slide and its lamps light: never batched
            float clear = LevelMetrics.ClearWidth(LevelMetrics.LiftDoorWidth), h = LevelMetrics.DoorHeight, wall = LevelMetrics.WallThickness;
            const float lining = 0.04f, casing = 0.1f, casingDepth = 0.025f;
            var door = root.AddComponent<LiftDoor>();
            door.Width = clear;
            var steel = M("LiftSteel");
            float face = wall / 2f;
            foreach (float side in new[] { -1f, 1f })
            {
                ChamferBox(root.transform, "Jamb", new Vector3(side * (clear / 2f - lining / 2f), h / 2f, 0f), new Vector3(lining, h, wall + 0.01f), steel);
                ChamferBox(root.transform, "Casing", new Vector3(side * (clear / 2f - lining + casing / 2f), (h + casing - lining) / 2f, face + casingDepth / 2f),
                    new Vector3(casing, h + casing - lining, casingDepth), steel);
            }
            ChamferBox(root.transform, "Head", new Vector3(0f, h - lining / 2f, 0f), new Vector3(clear, lining, wall + 0.01f), steel);
            ChamferBox(root.transform, "CasingHead", new Vector3(0f, h - lining + casing / 2f, face + casingDepth / 2f), new Vector3(clear + 2f * (casing - lining), casing, casingDepth), steel);
            // the indicator over the portal: a housing, the lamp in the middle and an arrow lamp either side
            float iy = h + casing + 0.12f;
            // (Tools/Blender/lift_parts.py lift_indicator: a bevelled housing with a dark glass face, real arrows)
            var indicator = Model("lift_indicator");
            indicator.transform.SetParent(root.transform, false);
            indicator.transform.localPosition = new Vector3(0f, iy, face);
            Renderer Lamp(string part, string mat)
            {
                var r = Parts(indicator, part).First();
                r.sharedMaterial = M(mat);
                return r;
            }
            Parts(indicator, "Body").First().sharedMaterial = steel;
            Parts(indicator, "Trim").First().sharedMaterial = M("Rack");
            door.HereLamp = Lamp("EyeHere", "LampBulb");
            door.UpLamp = Lamp("EyeUp", "LockLamp");
            door.DownLamp = Lamp("EyeDown", "LockLamp");
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            // the panels in the wall's plane, parting into it (LiftDoor slides them)
            Transform Panel(string name, float x)
            {
                var panel = Model("lift_panel");
                panel.name = name;
                panel.transform.SetParent(root.transform, false);
                panel.transform.localPosition = new Vector3(x, 0.005f, 0.02f);
                panel.transform.localScale = new Vector3((clear / 2f + 0.015f) / 0.6f, (h - lining - 0.01f) / 2.28f, 1f);
                Paint(panel, "LiftSteel", "Metal", "Metal");
                NoShadows(panel);
                return panel.transform;
            }
            door.PanelA = Panel("PanelA", -clear / 4f);
            door.PanelB = Panel("PanelB", clear / 4f);
            door.Blocker = DoorBlocker(root, 0f, 0.02f, clear, h, 0.08f);
            // the call button on a plate on the landing's face, beside the portal
            float bx = clear / 2f + casing + 0.16f;
            var plate = Model("lift_callplate");
            plate.transform.SetParent(root.transform, false);
            plate.transform.localPosition = new Vector3(bx, 1.1f, face);
            Paint(plate, "LiftSteel", "Metal", "Metal");
            NoShadows(plate);
            var call = Model("lift_button");
            call.name = "CallButton";
            call.transform.SetParent(root.transform, false);
            call.transform.localPosition = new Vector3(bx, 1.1f, face + 0.012f);
            PaintLiftButton(call);
            var button = call.AddComponent<LiftButton>();
            button.InCab = false;
            button.Lamp = Parts(call, "Eye").FirstOrDefault();
            door.Call = button;
            var block = root.AddComponent<NavMeshModifierVolume>();
            block.center = new Vector3(0f, 1.2f, -0.55f);
            block.size = new Vector3(clear + 0.6f, 2.4f, 1.5f);
            block.area = 1; // Not Walkable: no robot into the shaft
            Muffles(root, SoundMaterial.Door);
            return root;
        }

        /// <summary>
        /// A balcony of the apartment block (+Z out of the house, origin on the wall's outer face at the floor): a concrete slab
        /// 3.2 x 1.1 m, a solid parapet 1 m high along its front and ends with a steel handrail on it. One can jump off it
        /// (down into the yard, a hard landing from high up): its solid part stops at <see cref="BalconyClimb"/>, under a jump.
        /// </summary>
        static GameObject BuildBalcony()
        {
            var root = new GameObject("Balcony");
            const float L = 3.2f, D = 1.1f, slab = 0.18f, wall = 0.08f;
            // the look: Tools/Blender/balcony.py (a chamfered slab with a drip groove, parapet panels with recessed fields and
            // a coping, a steel handrail on posts); the solid parts: the same boxes as ever
            var model = Model("balcony");
            model.transform.SetParent(root.transform, false);
            Paint(model, "Wall", "Metal", "Metal");
            void Solid(Vector3 c, Vector3 size)
            {
                var b = root.AddComponent<BoxCollider>();
                b.center = c;
                b.size = size;
            }
            Solid(new Vector3(0f, -slab / 2f, D / 2f), new Vector3(L, slab, D));
            // the parapet's solid part is lower than it looks (the handrail on top is for the eye): a jump (1.05 m) clears it
            const float solid = BalconyClimb;
            Solid(new Vector3(0f, solid / 2f, D - wall / 2f), new Vector3(L, solid, wall));
            foreach (float x in new[] { -1f, 1f })
                Solid(new Vector3(x * (L / 2f - wall / 2f), solid / 2f, (D - wall) / 2f), new Vector3(wall, solid, D - wall));
            return root;
        }

        /// <summary>The height of a balcony parapet's collider, metres: under a jump, so one can jump off the balcony.</summary>
        const float BalconyClimb = 0.75f;

        /// <summary>
        /// Furniture whose BodyDoor{n} / BodyDrawer{n} parts open for real (Items.Storage). A drawer whose front stands
        /// high gets a floor close under the front's top (<see cref="Stealth.Items.StorageMath.DrawerFloor"/>: a board in
        /// the drawer, with a collider — loot lies and falls on it): on the model's own bottom, a third of a metre under
        /// the front's top at chest height, the front hid the loot from a standing player.
        /// </summary>
        static GameObject Opening(GameObject go)
        {
            go.AddComponent<Stealth.Items.Storage>();
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("BodyDrawer")) continue;
                var mf = t.GetComponent<MeshFilter>();
                var mr = t.GetComponent<MeshRenderer>();
                if (mf == null || mf.sharedMesh == null || mr == null) continue;
                var points = mf.sharedMesh.vertices.Select(v => go.transform.InverseTransformPoint(t.TransformPoint(v))).ToArray();
                if (points.Length == 0) continue;
                var b = new Bounds(points[0], Vector3.zero);
                foreach (var q in points) b.Encapsulate(q);
                float floor = Stealth.Items.StorageMath.DrawerFloor(b, 0f);
                if (floor < b.min.y + 0.06f) continue; // the model's own bottom will do
                // the tray: what lies well behind the front (the front and its handle are wider and stand before it)
                var tray = points.Where(q => q.z < b.max.z - 0.08f && q.y < floor).ToArray();
                if (tray.Length == 0) continue;
                float x0 = tray.Min(q => q.x), x1 = tray.Max(q => q.x), z0 = tray.Min(q => q.z), z1 = b.max.z - 0.06f;
                if (x1 - x0 < 0.12f || z1 - z0 < 0.12f) continue;
                var size = new Vector3(x1 - x0 - 0.04f, 0.014f, z1 - z0 - 0.03f);
                var board = ChamferBox(go.transform, "DrawerFloor", new Vector3((x0 + x1) / 2f, floor - 0.007f, (z0 + 0.03f + z1) / 2f), size, mr.sharedMaterial, 0.003f);
                board.AddComponent<BoxCollider>().size = size;
                board.transform.SetParent(t, true); // it slides with the drawer
            }
            return go;
        }

        /// <summary>A lift button: steel bezel, a pale button, its lamp ring glowing (hidden while unlit).</summary>
        static void PaintLiftButton(GameObject button)
        {
            Paint(button, "PropBody", "Metal", "LampBulb");
            NoShadows(button);
            foreach (var r in Parts(button, "Eye")) r.enabled = false;
        }

        /// <summary>
        /// The lift's cab (root on the ground floor in the well, +Z to the landings; <see cref="LiftCab.Car"/> rides): its
        /// model (Tools/Blender/doors.py lift_cab), box colliders for the floor, walls, the front returns, the header and the
        /// ceiling, a small lamp under the ceiling, the floor buttons' panel on the right front return with eight buttons
        /// bottom up (those over the house's storeys are hidden). Left out of the navmesh: robots never ride.
        /// </summary>
        static GameObject BuildLiftCab()
        {
            var root = new GameObject("LiftCab");
            root.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
            root.AddComponent<KeepUnmerged>(); // it rides: never batched
            var cab = root.AddComponent<LiftCab>();
            const float W = 2.6f, D = 2.1f, H = 2.4f, T = 0.05f, Door = 1.1f;
            cab.HalfSize = new Vector2(W / 2f, D / 2f);
            var car = new GameObject("Car").transform;
            car.SetParent(root.transform, false);
            cab.Car = car;
            var model = Model("lift_cab");
            model.transform.SetParent(car, false);
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = r.gameObject.name;
                r.sharedMaterial = M(n.StartsWith("Eye") ? "LampBulb" : n.StartsWith("Accent") ? "Metal" : n.StartsWith("Floor") ? "LiftFloor" : "LiftSteel");
                r.shadowCastingMode = n.StartsWith("Eye") ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }
            void Box(string name, Vector3 c, Vector3 size)
            {
                var b = Child(car, name, Vector3.zero).AddComponent<BoxCollider>();
                b.center = c;
                b.size = size;
            }
            float ret = (W - Door) / 2f;
            Box("FloorCollider", new Vector3(0f, -0.03f, 0f), new Vector3(W, 0.06f, D));
            Box("BackCollider", new Vector3(0f, H / 2f, -D / 2f - T / 2f), new Vector3(W + 2f * T, H, T));
            foreach (float x in new[] { -1f, 1f })
            {
                Box("SideCollider", new Vector3(x * (W / 2f + T / 2f), H / 2f, 0f), new Vector3(T, H, D + 2f * T));
                Box("ReturnCollider", new Vector3(x * (W / 2f - ret / 2f), H / 2f, D / 2f + T / 2f), new Vector3(ret, H, T));
            }
            Box("HeaderCollider", new Vector3(0f, 2.3f + (H - 2.3f) / 2f, D / 2f + T / 2f), new Vector3(Door, H - 2.3f, T));
            Box("CeilingCollider", new Vector3(0f, H + T / 2f, 0f), new Vector3(W + 2f * T, T, D + 2f * T));
            Lamp(car, "Lamp", new Vector3(0f, H - 0.08f, 0f), Quaternion.Euler(90f, 0f, 0f), LightType.Spot, new Color(1f, 0.93f, 0.82f), 2.2f, 3.2f);
            // the floor buttons' panel on the inside of the right front return, facing into the cab
            var cop = Model("lift_cop");
            cop.name = "Panel";
            cop.transform.SetParent(car, false);
            float px = W / 2f - ret / 2f;
            cop.transform.localPosition = new Vector3(px, 1.2f, D / 2f);
            cop.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            Paint(cop, "Metal", "Metal", "PropScreen");
            NoShadows(cop);
            var buttons = new List<LiftButton>();
            for (int i = 0; i < 8; i++)
            {
                var b = Model("lift_button");
                b.name = $"Floor{i + 1}";
                b.transform.SetParent(car, false);
                b.transform.localPosition = new Vector3(px + (i % 2 == 0 ? -0.045f : 0.045f), 0.86f + (i / 2) * 0.1f, D / 2f - 0.012f);
                b.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                PaintLiftButton(b);
                var lb = b.AddComponent<LiftButton>();
                lb.InCab = true;
                lb.Cab = car;
                lb.Lamp = Parts(b, "Eye").FirstOrDefault();
                buttons.Add(lb);
            }
            cab.Buttons = buttons.ToArray();
            return root;
        }

        /// <summary>
        /// A lock lamp over a door on either face of its wall, on the head of the style's casing and proud of it (walls grew
        /// from 20 to 30 cm: at the old depth the lamps were inside the lintel).
        /// </summary>
        static Renderer[] OverDoorLamps(GameObject root, float h)
        {
            const float proud = 0.07f; // past the casing's keystone and cornice (4-5 cm)
            return new[] { 1f, -1f }.Select(side => ChamferBox(root.transform, side > 0f ? "LockLampA" : "LockLampB",
                new Vector3(0f, h + 0.1f, side * (LevelMetrics.WallThickness / 2f + proud / 2f)), new Vector3(0.22f, 0.07f, proud), M("LockLamp")).GetComponent<Renderer>()).ToArray();
        }

        /// <summary>
        /// The locks every door has: lock lamps over it on both sides (red while locked, green once hacked; hidden on doors
        /// without a lock), a hasp with a padlock across the leaves' seam at <paramref name="seam"/> on both faces
        /// (<paramref name="front"/>, <paramref name="back"/>: the faces' z), and the key panels on the main leaf
        /// (<paramref name="keyLeaf"/>, at the world points <paramref name="keyFront"/> and <paramref name="keyBack"/>).
        /// </summary>
        static void DoorLocks(GameObject root, AutoDoor door, float w, float h, float seam, float front, float back,
            Transform keyLeaf = null, Vector3 keyFront = default, Vector3 keyBack = default)
        {
            door.LockLamps = OverDoorLamps(root, h);
            foreach (var lamp in door.LockLamps) lamp.shadowCastingMode = ShadowCastingMode.Off;
            door.Padlocks = new[] { 1f, -1f }.Select(side =>
            {
                var holder = Child(root.transform, side > 0 ? "PadlockA" : "PadlockB", new Vector3(seam, 1.2f, side > 0 ? front : back)).transform;
                holder.localRotation = Quaternion.Euler(0f, side > 0 ? 0f : 180f, 0f);
                ChamferBox(holder, "Hasp", new Vector3(0f, 0f, 0.008f), new Vector3(0.2f, 0.05f, 0.016f), M("Metal"), 0.004f);
                var lockModel = Model("padlock");
                lockModel.transform.SetParent(holder, false);
                lockModel.transform.localPosition = new Vector3(0f, 0.01f, 0.035f);
                Paint(lockModel, "Gold", "Metal", "Metal");
                NoShadows(lockModel);
                holder.gameObject.SetActive(false);
                return holder;
            }).ToArray();
            var keyLock = root.AddComponent<KeyLock>();
            keyLock.Door = door;
            if (keyLeaf != null) keyLock.Panels = KeyLockPanels(keyLeaf, keyFront, keyBack);
        }

        /// <summary>
        /// A light that robots take into account (<see cref="LightSource"/>): robots see a player standing in it better.
        /// </summary>
        static Light Lamp(Transform parent, string name, Vector3 position, Quaternion rotation, LightType type, Color color, float intensity, float range,
            LightShadows shadows = LightShadows.None, int shadowTier = -1)
        {
            var go = Child(parent, name, position);
            go.transform.localRotation = rotation;
            var light = go.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            if (shadows != LightShadows.None)
                ShadowedLight(light, shadowTier >= 0 ? shadowTier : UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium);
            go.AddComponent<LightSource>();
            return light;
        }

        /// <summary>
        /// Full-strength soft shadows with a small bias: 20 cm walls and slabs must not let any light through or under them.
        /// </summary>
        static void ShadowedLight(Light light, int tier)
        {
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 1f;
            light.shadowBias = 0.03f;
            light.shadowNormalBias = 0.2f;
            light.shadowNearPlane = 0.1f;
            // The property setters are play-mode only; the serialized fields are what the asset keeps anyway.
            var data = new SerializedObject(light.GetUniversalAdditionalLightData());
            data.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = tier;
            data.FindProperty("m_UsePipelineSettings").boolValue = false;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>The model's renderers do not shadow the lamp inside them.</summary>
        static void NoShadows(GameObject model)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>
        /// Ceiling panel at the ceiling surface (the pivot) with a wide spot light shining down: soft shadows, pools of light
        /// under it, darker corners. <see cref="RoomLight"/> sets colour / brightness per room and switches it off in dark rooms.
        /// </summary>
        /// <summary>A monitor's screen showing a picture that glows through itself (the picture is set per terminal, by kind).</summary>
        static Material TerminalScreenMaterial()
        {
            var m = Mat("Props", "TerminalScreen", new Color(0.3f, 0.3f, 0.3f), Color.white * 1.2f, smoothness: 0.85f);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/Textures/Screens/screen_hack.png");
            if (tex != null && m.GetTexture("_EmissionMap") != tex)
            {
                m.SetTexture("_BaseMap", tex);
                m.SetTexture("_EmissionMap", tex);
                EditorUtility.SetDirty(m);
            }
            return m;
        }

        /// <summary>A lamp's shade (its Accent parts) glows by its bulb (the Eye parts): ShadeGlow.</summary>
        static void LitFromInside(GameObject lamp)
        {
            var glow = lamp.AddComponent<ShadeGlow>();
            var all = lamp.GetComponentsInChildren<Renderer>();
            glow.Shades = all.Where(r => r.name.StartsWith("Accent")).ToArray();
            glow.Bulbs = all.Where(r => r.name.StartsWith("Eye")).ToArray();
        }

        /// <summary>A room's reflection probe (RoomProbes): the engine takes its cube picture when asked, a face a frame.</summary>
        static GameObject BuildRoomProbe()
        {
            var root = new GameObject("RoomProbe");
            var probe = root.AddComponent<ReflectionProbe>();
            probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
            probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.boxProjection = true;
            probe.blendDistance = 0f;
            probe.resolution = 128;
            probe.hdr = true;
            probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = Color.black;
            probe.nearClipPlane = 0.05f;
            probe.farClipPlane = 30f;
            probe.shadowDistance = 30f;
            // not the player's own body, the X-ray silhouettes and world-space text
            probe.cullingMask = ~LayerMask.GetMask("OwnBody", "Marked", "MarkedThing", "WorldText", "UI", "TransparentFX");
            root.AddComponent<RoomProbe>().Probe = probe;
            return root;
        }

        static GameObject BuildRoomLight()
        {
            var root = new GameObject("RoomLight");
            var panels = new List<Renderer>();
            // A square fixture of two panels in the middle of the ceiling; the light comes out of it.
            foreach (float z in new[] { -0.32f, 0.32f })
            {
                var model = Model("ceiling_lamp");
                model.name = z < 0 ? "PanelA" : "PanelB";
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = new Vector3(0f, 0f, z);
                Paint(model, "LampHousing", "LampHousing", "LampPanel");
                NoShadows(model);
                panels.AddRange(Parts(model, "Eye"));
            }
            // A point light just under the fixture: precise shadows in every direction keep it inside the room.
            // Its cookie lets no light upwards — a recessed panel shines down, so the ceiling around it does not light up
            // as if by nothing. (No invisible geometry: a shadow-only shade still writes depth and hides the panels.)
            var light = Lamp(root.transform, "Lamp", new Vector3(0f, -0.3f, 0f), Quaternion.identity, LightType.Point,
                Color.white, LightMath.RoomIntensity, 9.5f, LightShadows.Soft);
            light.cookie = LampCookie(LampCookiePath, 0f);
            var rl = root.AddComponent<RoomLight>();
            rl.Lamp = light;
            rl.Cookie = light.cookie;
            rl.SoftCookie = LampCookie(SoftLampCookiePath, LightMath.SoftCeilingShare);
            rl.PoolCookie = LampCookie(PoolLampCookiePath, LightMath.SoftCeilingShare, true);
            rl.Panels = panels.ToArray();
            // The soft look (where the style asks: LightSource.CeilingGlow; hidden otherwise): a hot middle just under
            // each diffuser, fading to its edges, and the light the fixture throws on the ceiling round it — glow over
            // the picture, no light cast (the cookie keeps the real light off the ceiling: nothing leaks upstairs).
            Renderer Glow(string name, Vector3 at, Vector2 size)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(q.GetComponent<Collider>());
                q.name = name;
                q.transform.SetParent(root.transform, false);
                q.transform.localPosition = at;
                q.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // facing down
                q.transform.localScale = new Vector3(size.x, size.y, 1f);
                var r = q.GetComponent<MeshRenderer>();
                r.sharedMaterial = M("LampGlow");
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = false;
                return r;
            }
            rl.Cores = new[] { Glow("CoreA", new Vector3(0f, -0.082f, -0.32f), new Vector2(1.5f, 0.74f)), Glow("CoreB", new Vector3(0f, -0.082f, 0.32f), new Vector2(1.5f, 0.74f)) };
            rl.Halo = Glow("Halo", new Vector3(0f, -0.012f, 0f), new Vector2(4.4f, 4.4f));
            return root;
        }

        const string LampCookiePath = SpritesDir + "/LampCookie.cubemap", SoftLampCookiePath = SpritesDir + "/LampCookieSoft.cubemap",
            PoolLampCookiePath = SpritesDir + "/LampCookiePool.cubemap";

        /// <summary>
        /// Cube cookie of the ceiling lamps: full light downwards and sideways, fading out above the horizon (no light onto
        /// the ceiling it hangs from).
        /// </summary>
        static Cubemap LampCookie(string path, float up, bool pool = false)
        {
            var cookie = AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if (cookie != null && !s_Force) return cookie;
            const int size = 32;
            var cube = new Cubemap(size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var faces = new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ };
            foreach (var face in faces)
            {
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float dirY = CubeDirection(face, (x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f).normalized.y;
                        float v = pool ? LightMath.PoolCookie(dirY, up) : LightMath.Cookie(dirY, up);
                        px[y * size + x] = new Color(v, v, v, v);
                    }
                cube.SetPixels(px, face);
            }
            cube.Apply();
            if (cookie != null) AssetDatabase.DeleteAsset(path);
            EnsureFolder(SpritesDir);
            AssetDatabase.CreateAsset(cube, path);
            return cube;
        }

        /// <summary>Direction of a cubemap texel (face, s/t in -1..1, t growing down the face — Unity's layout).</summary>
        static Vector3 CubeDirection(CubemapFace face, float s, float t) => (face switch
        {
            CubemapFace.PositiveX => new Vector3(1f, -t, -s),
            CubemapFace.NegativeX => new Vector3(-1f, -t, s),
            CubemapFace.PositiveY => new Vector3(s, 1f, t),
            CubemapFace.NegativeY => new Vector3(s, -1f, -t),
            CubemapFace.PositiveZ => new Vector3(s, -t, 1f),
            _ => new Vector3(-s, -t, -1f),
        }).normalized;

        /// <summary>Facade window; the glass lets the moonlight in (no shadow), the blinds stripe it.</summary>
        static GameObject BuildWindow()
        {
            var go = PropObject("Window", "window", "Blinds", "WindowFrame", "Window", collider: false);
            foreach (var r in Parts(go, "Eye")) r.shadowCastingMode = ShadowCastingMode.Off; // the moon shines in through it
            // the glass fills the wall's hole: bodies and items stop at it, sight and light pass (it is glass);
            // sound comes through muffled like a door
            var glass = go.AddComponent<BoxCollider>();
            glass.size = new Vector3(LevelMetrics.WindowWidth, LevelMetrics.WindowHeight, LevelMetrics.WallThickness);
            go.AddComponent<SoundBlocker>().Material = SoundMaterial.Door;
            return go;
        }

        /// <summary>Green exit sign over a door (sticks out of both faces of the wall) with a small green light.</summary>
        static GameObject BuildExitSign()
        {
            var root = new GameObject("ExitSign");
            var model = Model("exit_sign");
            model.transform.SetParent(root.transform, false);
            Paint(model, "PropAccent", "ExitSignMark", "ExitSign");
            // made for a 20 cm wall (its faces 15 cm off the middle): out of today's, past the casing's cornice
            model.transform.localScale = new Vector3(1f, 1f, (LevelMetrics.WallThickness / 2f + 0.08f) / 0.15f);
            NoShadows(model);
            // Shines down into the doorway (both rooms): nothing goes up to the floor above.
            var glow = Lamp(root.transform, "Glow", new Vector3(0f, -0.12f, 0f), Quaternion.Euler(90f, 0f, 0f), LightType.Spot, new Color(0.35f, 1f, 0.5f), 0.35f, 2.6f);
            glow.spotAngle = 170f;
            glow.innerSpotAngle = 100f;
            return root;
        }

        /// <summary>
        /// The exit hatch, in the look of the entry's shaft lid (ShaftHatch; models Tools/Blender/hatches.py): a yellow steel
        /// angle frame in the floor with bolts, a dark pit inside it, a ribbed tread-plate lid on a hinge along the +X bar (it
        /// swings up while someone hides in the hatch, <see cref="Hatch"/>), a lifting bar at its free edge; four lamps in
        /// the frame's corners show its colour (the exit's green).
        /// </summary>
        static GameObject BuildHatch()
        {
            Material Util(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{CityMaterialsDir}/{name}.mat") ?? M("Metal");
            var util = new Material[System.Enum.GetValues(typeof(UtilitySlot)).Length];
            util[(int)UtilitySlot.Warning] = Util("UtilWarning");
            util[(int)UtilitySlot.Panel] = Util("UtilPanel");
            util[(int)UtilitySlot.Dark] = Util("UtilDark");
            // as the entry's lid (ShaftHatch): the frame right round the hole (3 x 3 plan cells), the lid on a hinge at −Z, the
            // ladder below it (the passage's shaft in the sewer, SewerLocation); lamps in the frame's corners in the exit's colour
            var go = new GameObject("Hatch");
            float half = Basement.ShaftHalf, lip = 0.03f;
            HatchModel("hatch_frame_exit", go.transform, util);
            var lid = new GameObject("Lid").transform;
            lid.SetParent(go.transform, false);
            lid.localPosition = new Vector3(0f, 0.024f, -half - lip);
            float side = 2 * half + 2 * lip;
            HatchModel("hatch_lid_exit", lid, util);
            var plateCollider = lid.gameObject.AddComponent<BoxCollider>();
            plateCollider.center = new Vector3(0f, 0.0175f, side / 2f);
            plateCollider.size = new Vector3(side, 0.035f, side);
            go.AddComponent<KeepUnmerged>();
            var h = go.AddComponent<ShaftHatch>();
            h.Lid = lid;
            h.LidCollider = plateCollider;
            h.Exit = true;
            h.Glow = go.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("Eye")).ToArray();
            return go;
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
            MachinePaint(model);
            if (collider)
                foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                    if (MachineMaterial(mf.name) is null or "MachinePrint") mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh; // (a keyboard, glass, a glow: nothing to bump into)
            // a desk's monitor and its stand: a player's body goes through them (they stood in the way into the duct over the desk)
            if (collider && ScreensOnDesks.Contains(name))
            {
                var screens = model.GetComponentsInChildren<MeshCollider>(true).Where(c => c.name.StartsWith("Eye")).Cast<Collider>().ToArray();
                if (screens.Length > 0) root.AddComponent<PassableForPlayers>().Parts = screens;
            }
            return root; // (no maps wired here: the styles' dressing tells each part the slice of the mesh it shows)
        }

        /// <summary>Desks that carry screens: the screens are <see cref="PassableForPlayers"/>.</summary>
        static readonly HashSet<string> ScreensOnDesks = new()
        {
            "Desk", "StandingDesk", "SupervisorDesk", "ExecutiveDesk", "DoctorDesk", "ReceptionDesk", "NurseStationDesk", "ConsoleDesk", "WorkTable", "StudyCarrel",
        };

        /// <summary>The material of a part printed or shining through a picture of its own (Tools/Blender/machines.py, keyboard.py), by how its name begins; null: none of these.</summary>
        static string MachineMaterial(string part) =>
            part.StartsWith("Keys") ? "KeyCaps" : part.StartsWith("Print") ? "MachinePrint" : part.StartsWith("Glow") ? "MachineGlow"
            : part.StartsWith("Lit") ? "MachineLit" : part.StartsWith("Pane") ? "MachinePane" : null;

        static void MachinePaint(GameObject model)
        {
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                var m = MachineMaterial(r.name);
                if (m == null) continue;
                r.sharedMaterial = M(m);
                if (m is "MachinePane" or "MachineGlow") r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>
        /// A second model for the realistic style under a prop: the prop's own model shows in the other styles and in a house
        /// furnished with models of its own (<see cref="Stealth.Level.StyleParts"/>). One box stops players and sight for both.
        /// </summary>
        static GameObject RealisticModel(GameObject go, string file, string body, string accent)
        {
            if (!HasModel(file)) return null;
            var old = go.transform.GetChild(0).gameObject;
            old.name = "Plain";
            foreach (var c in old.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            var model = Model(file);
            model.name = "Realistic";
            model.transform.SetParent(go.transform, false);
            Paint(model, body, accent, body);
            MachinePaint(model);
            bool first = true;
            var bounds = new Bounds();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                foreach (var v in mf.sharedMesh.vertices)
                {
                    var p = go.transform.InverseTransformPoint(mf.transform.TransformPoint(v));
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                    else bounds.Encapsulate(p);
                }
            var box = go.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size;
            var parts = go.AddComponent<Stealth.Level.StyleParts>();
            parts.Style = "S16_Derelict";
            parts.NotWhereFurnished = true;
            parts.Own = new[] { model };
            parts.Default = new[] { old };
            return model;
        }
    }
}
