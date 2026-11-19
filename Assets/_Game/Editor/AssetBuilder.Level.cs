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
            var breach = Prefab($"{LevelPrefabs}/BreachFrame.prefab", () => BuildRuin("BreachFrame", LevelMetrics.BreachWidth, LevelMetrics.DoorHeight, false));
            var hole = Prefab($"{LevelPrefabs}/HoleFrame.prefab", () => BuildRuin("HoleFrame", LevelMetrics.HoleWidth, LevelMetrics.HoleHeight, true));
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
                var go = PropObject("Cabinet", "cabinet", "PropBody", "PropAccent", "PropScreen");
                go.AddComponent<Stealth.Items.Storage>(); // the drawers slide out (BodyDrawer0..2)
                return go;
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
                go.AddComponent<Stealth.Items.Storage>().Individual = true; // the doors swing open (BodyDoor0..2)
                return go;
            });
            var pPallet = Prefab(p + "Pallet.prefab", () => PropObject("Pallet", "pallet", "Cardboard", "Wood", "Wood"));
            var pWashBasin = Prefab(p + "WashBasin.prefab", () => PropObject("WashBasin", "wash_basin", "Ceiling", "Metal", "Mirror"));
            var pVendingMachine = Prefab(p + "VendingMachine.prefab", () =>
            {
                var go = PropObject("VendingMachine", "vending_machine", "Vending", "PropAccent", "VendingGlass");
                // Shines forward and a bit down, away from the wall it stands at: nothing reaches the next room or floor.
                var glow = Lamp(go.transform, "Glow", new Vector3(0f, 1.2f, 0.5f), Quaternion.Euler(25f, 0f, 0f), LightType.Spot, new Color(0.7f, 0.85f, 1f), 0.8f, 2.6f);
                glow.spotAngle = 120f;
                glow.innerSpotAngle = 60f;
                return go;
            });
            var pPingPong = Prefab(p + "PingPongTable.prefab", () => PropObject("PingPongTable", "ping_pong_table", "PingPong", "Ceiling", "Ceiling"));
            var pBeanBag = Prefab(p + "BeanBag.prefab", () => PropObject("BeanBag", "bean_bag", "BeanBag", "BeanBag", "BeanBag"));
            var pArcade = Prefab(p + "ArcadeMachine.prefab", () => PropObject("ArcadeMachine", "arcade_machine", "Arcade", "PropAccent", "PropScreen"));
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
                return go;
            });
            var pDeskLamp = Prefab(p + "DeskLamp.prefab", () =>
            {
                var go = PropObject("DeskLamp", "desk_lamp", "Metal", "LampShade", "LampBulb", collider: false);
                var lamp = Lamp(go.transform, "Light", new Vector3(0f, 0.27f, 0.16f), Quaternion.Euler(65f, 0f, 0f), LightType.Spot, new Color(1f, 0.82f, 0.55f), 1.8f, 3.2f,
                    LightShadows.Soft, UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow); // small: low maps (LightBudget.Cost)
                lamp.spotAngle = 120f;
                lamp.innerSpotAngle = 50f;
                return go;
            });
            var pFloorLamp = Prefab(p + "FloorLamp.prefab", () =>
            {
                var go = PropObject("FloorLamp", "floor_lamp", "Metal", "LampShade", "LampBulb");
                var lamp = Lamp(go.transform, "Light", new Vector3(0f, 1.4f, 0f), Quaternion.Euler(90f, 0f, 0f), LightType.Spot, new Color(1f, 0.8f, 0.55f), 1.4f, 4f,
                    LightShadows.Soft);
                lamp.spotAngle = 160f;
                lamp.innerSpotAngle = 90f;
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
            var metal = M("Metal");
            // lining: floor, ceiling and sides of the duct, the length of the wall's thickness
            Cube(root.transform, "DuctFloor", new Vector3(0, y0 + sheet / 2f, 0), new Vector3(w, sheet, t + 0.02f), metal, false);
            Cube(root.transform, "DuctTop", new Vector3(0, y0 + h - sheet / 2f, 0), new Vector3(w, sheet, t + 0.02f), metal, false);
            foreach (float s in new[] { -1f, 1f })
                Cube(root.transform, s < 0 ? "DuctLeft" : "DuctRight", new Vector3(s * (w / 2f - sheet / 2f), y0 + h / 2f, 0), new Vector3(sheet, h, t + 0.02f), metal, false);
            foreach (float z in new[] { t / 2f + 0.015f, -(t / 2f + 0.015f) })
            {
                string side = z > 0 ? "A" : "B";
                Cube(root.transform, "FlangeTop" + side, new Vector3(0, y0 + h + flange / 2f, z), new Vector3(w + flange * 2f, flange, 0.03f), metal, false);
                Cube(root.transform, "FlangeBottom" + side, new Vector3(0, y0 - flange / 2f, z), new Vector3(w + flange * 2f, flange, 0.03f), metal, false);
                foreach (float s in new[] { -1f, 1f })
                    Cube(root.transform, (s < 0 ? "FlangeLeft" : "FlangeRight") + side, new Vector3(s * (w / 2f + flange / 2f), y0 + h / 2f, z),
                        new Vector3(flange, h, 0.03f), metal, false);
            }
            // the grille: a frame with louvres — shut in the hole on side A (its collider keeps everyone out) until a player
            // works it loose (VentGrille), then leaning on the wall beside the hole (a high vent's: on the desk under it)
            float gw = w * 0.9f, gh = h * 0.9f;
            void Louvres(Transform grille, float lw, float lh)
            {
                Cube(grille, "GrilleTop", new Vector3(0, lh - 0.025f, 0f), new Vector3(lw, 0.05f, 0.025f), metal, false);
                Cube(grille, "GrilleBottom", new Vector3(0, 0.025f, 0f), new Vector3(lw, 0.05f, 0.025f), metal, false);
                foreach (float s in new[] { -1f, 1f })
                    Cube(grille, s < 0 ? "GrilleLeft" : "GrilleRight", new Vector3(s * (lw / 2f - 0.025f), lh / 2f, 0f), new Vector3(0.05f, lh, 0.025f), metal, false);
                for (int i = 1; i < 8; i++)
                {
                    var slat = Cube(grille, $"Slat{i}", new Vector3(0, lh * i / 8f, 0f), new Vector3(lw - 0.08f, 0.012f, 0.06f), metal, false);
                    slat.transform.localRotation = Quaternion.Euler(35f, 0f, 0f); // louvres
                }
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

        /// <summary>
        /// The ruin round a breach or a hole knocked through a wall (Tools/Blender/ruins.py): broken edges up both sides and
        /// along the top, a pile of rubble at the foot on either face; along local X, no colliders (the wall pieces keep the
        /// shape). A hole also keeps robots out with a navmesh volume: a player crawls through, a robot does not.
        /// </summary>
        static GameObject BuildRuin(string name, float nominal, float height, bool robotsOut)
        {
            const float rimHeight = 2.3f; // breach_rim.fbx is modelled at the door height
            var root = new GameObject(name);
            float w = LevelMetrics.ClearWidth(nominal);
            void Piece(string file, string child, Vector3 pos, Quaternion rot, Vector3 scale)
            {
                var m = Model(file);
                m.name = child;
                m.transform.SetParent(root.transform, false);
                m.transform.localPosition = pos;
                m.transform.localRotation = rot;
                m.transform.localScale = scale;
                Paint(m, "Wall", "Wall", "Wall");
            }
            // the sides: the rim stands on the cut line and juts into the gap (+X); the right one turned round
            Piece("breach_rim", "EdgeLeft", new Vector3(-w / 2f, 0f, 0f), Quaternion.identity, new Vector3(1f, height / rimHeight, 1f));
            Piece("breach_rim", "EdgeRight", new Vector3(w / 2f, 0f, 0f), Quaternion.Euler(0f, 180f, 0f), new Vector3(1f, height / rimHeight, 1f));
            // the top: the same rim lying along the gap, jutting down
            Piece("breach_rim", "EdgeTop", new Vector3(-w / 2f, height, 0f), Quaternion.Euler(0f, 0f, -90f), new Vector3(1f, w / rimHeight, 1f));
            // rubble at the foot, one pile on each face, against the jambs
            float face = LevelMetrics.WallThickness / 2f + 0.22f;
            Piece("rubble", "RubbleA", new Vector3(-w / 2f + 0.05f, 0f, face), Quaternion.Euler(0f, 20f, 0f), Vector3.one);
            Piece("rubble", "RubbleB", new Vector3(w / 2f - 0.05f, 0f, -face), Quaternion.Euler(0f, 200f, 0f), Vector3.one * 0.85f);
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
                Cube(root.transform, "Top", new Vector3(0, h, z), new Vector3(w + 0.12f, 0.06f, 0.04f), M("DoorFrame"), false);
                Cube(root.transform, "Left", new Vector3(-w / 2f - 0.03f, h / 2f, z), new Vector3(0.06f, h, 0.04f), M("DoorFrame"), false);
                Cube(root.transform, "Right", new Vector3(w / 2f + 0.03f, h / 2f, z), new Vector3(0.06f, h, 0.04f), M("DoorFrame"), false);
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
        static GameObject BuildAutoDoor() => BuildAutoDoor("AutoDoor", LevelMetrics.DoorWidth, "Door");

        /// <summary>
        /// A sliding door <paramref name="width"/> wide (nominal; the leaves stretched to it) in <paramref name="paint"/>.
        /// <paramref name="leafModel"/>: the leaf (and <c>_b</c>, its twin); <paramref name="accent"/>: its Accent part's paint.
        /// <paramref name="sign"/>: instead of the small lock lamps, a big lit sign over the door on both sides and a lit band
        /// across each leaf on both faces — red while locked, green while open, readable across the room (the storage room).
        /// </summary>
        static GameObject BuildAutoDoor(string name, float width, string paint, string leafModel = "door_leaf", string accent = null, bool sign = false)
        {
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
                foreach (var r in leaf.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                return leaf.transform;
            }
            door.PanelA = Leaf("PanelA", leafModel, -w / 4f, 0f);
            door.PanelB = Leaf("PanelB", leafModel + "_b", w / 4f, 180f); // its texture shifted: the halves do not mirror
            door.Blocker = DoorBlocker(root, 0f, 0f, w, h, 0.1f);
            // Lock lamps above the door on both sides: red while locked, green once hacked (hidden on doors without a lock).
            if (!sign)
                door.LockLamps = new Renderer[]
                {
                    Cube(root.transform, "LockLampA", new Vector3(0, h + 0.1f, 0.08f), new Vector3(0.22f, 0.07f, 0.03f), M("LockLamp"), false).GetComponent<Renderer>(),
                    Cube(root.transform, "LockLampB", new Vector3(0, h + 0.1f, -0.08f), new Vector3(0.22f, 0.07f, 0.03f), M("LockLamp"), false).GetComponent<Renderer>(),
                };
            else
            {
                var lamps = new List<Renderer>();
                float face = LevelMetrics.WallThickness / 2f;
                foreach (float side in new[] { 1f, -1f })
                {
                    // the sign: a steel housing on the wall over the door, its whole face lit
                    Cube(root.transform, side > 0 ? "SignHousingA" : "SignHousingB", new Vector3(0f, h + 0.34f, side * (face + 0.035f)), new Vector3(1.1f, 0.32f, 0.07f), M("ShopSteel"), false);
                    lamps.Add(Cube(root.transform, side > 0 ? "SignLampA" : "SignLampB", new Vector3(0f, h + 0.34f, side * (face + 0.072f)), new Vector3(0.96f, 0.2f, 0.012f), M("LockLamp"), false).GetComponent<Renderer>());
                    // a band across each leaf, on the leaf (it slides with it), in front of its frame
                    foreach (var panel in new[] { door.PanelA, door.PanelB })
                        lamps.Add(Cube(panel, $"Band{(side > 0 ? "A" : "B")}", new Vector3(0f, 1.9f, side * 0.04f), new Vector3(0.62f, 0.07f, 0.01f), M("LockLamp"), false).GetComponent<Renderer>());
                }
                door.LockLamps = lamps.ToArray();
            }
            foreach (var lamp in door.LockLamps) lamp.shadowCastingMode = ShadowCastingMode.Off;
            // a locked (hackable) door: a hasp across the seam with a padlock hanging from it, on both faces
            door.Padlocks = new[] { 1f, -1f }.Select(side =>
            {
                var holder = Child(root.transform, side > 0 ? "PadlockA" : "PadlockB", new Vector3(0f, 1.2f, side * 0.032f)).transform;
                holder.localRotation = Quaternion.Euler(0f, side > 0 ? 0f : 180f, 0f);
                Cube(holder, "Hasp", new Vector3(0f, 0f, 0.008f), new Vector3(0.2f, 0.05f, 0.016f), M("Metal"), false);
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
            keyLock.Panels = new[] { 1f, -1f }.Select(side =>
            {
                var panel = Model("key_lock");
                panel.name = side > 0 ? "KeyLockA" : "KeyLockB";
                panel.transform.SetParent(root.transform, false);
                panel.transform.localPosition = new Vector3(w / 2f + 0.22f, 1.15f, side * (LevelMetrics.WallThickness / 2f + 0.005f));
                panel.transform.localRotation = Quaternion.Euler(0f, side > 0 ? 0f : 180f, 0f);
                Paint(panel, "Metal", "Metal", "LockLamp");
                return panel;
            }).ToArray();
            Muffles(root, SoundMaterial.Door); // only while closed: the blocker collider is off when open
            return root;
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
                Cube(root.transform, "Jamb", new Vector3(side * (clear / 2f - lining / 2f), height / 2f, 0f), new Vector3(lining, height, depth), fm, false);
            Cube(root.transform, "Head", new Vector3(0f, height - lining / 2f, 0f), new Vector3(clear, lining, depth), fm, false);
            if (exterior)
            {
                const float casing = 0.12f, casingDepth = 0.03f;
                foreach (float side in new[] { -1f, 1f })
                    foreach (float face in new[] { -1f, 1f })
                        Cube(root.transform, "Casing", new Vector3(side * (clear / 2f - lining + casing / 2f), (height + casing - lining) / 2f, face * (wall / 2f + casingDepth / 2f)),
                            new Vector3(casing, height + casing - lining, casingDepth), fm, false);
                foreach (float face in new[] { -1f, 1f })
                    Cube(root.transform, "CasingHead", new Vector3(0f, height - lining + casing / 2f, face * (wall / 2f + casingDepth / 2f)),
                        new Vector3(clear + 2f * (casing - lining), casing, casingDepth), fm, false);
                Cube(root.transform, "Sill", new Vector3(0f, 0.01f, 0f), new Vector3(clear, 0.02f, depth), M("Metal"), false);
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
                                Cube(hinge, "PushPlate", new Vector3(w - 0.12f, 1.25f, f), new Vector3(0.1f, 0.3f, 0.006f), M("Metal"), false);
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
            DoorLocks(root, door, nominal, LevelMetrics.DoorHeight - 0.02f, twin ? 0f : seam, both ? t / 2f : hingeZ, both ? -t / 2f : hingeZ - t);
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
                Cube(root.transform, "Jamb", new Vector3(side * (clear / 2f - lining / 2f), h / 2f, 0f), new Vector3(lining, h, wall + 0.01f), steel, false);
                Cube(root.transform, "Casing", new Vector3(side * (clear / 2f - lining + casing / 2f), (h + casing - lining) / 2f, face + casingDepth / 2f),
                    new Vector3(casing, h + casing - lining, casingDepth), steel, false);
            }
            Cube(root.transform, "Head", new Vector3(0f, h - lining / 2f, 0f), new Vector3(clear, lining, wall + 0.01f), steel, false);
            Cube(root.transform, "CasingHead", new Vector3(0f, h - lining + casing / 2f, face + casingDepth / 2f), new Vector3(clear + 2f * (casing - lining), casing, casingDepth), steel, false);
            // the indicator over the portal: a housing, the lamp in the middle and an arrow lamp either side
            float iy = h + casing + 0.12f;
            Cube(root.transform, "Indicator", new Vector3(0f, iy, face + 0.02f), new Vector3(0.46f, 0.12f, 0.04f), steel, false);
            door.HereLamp = Cube(root.transform, "HereLamp", new Vector3(0f, iy, face + 0.042f), new Vector3(0.16f, 0.06f, 0.006f), M("LampBulb"), false).GetComponent<Renderer>();
            door.UpLamp = Cube(root.transform, "UpLamp", new Vector3(-0.15f, iy, face + 0.042f), new Vector3(0.05f, 0.06f, 0.006f), M("LockLamp"), false).GetComponent<Renderer>();
            door.DownLamp = Cube(root.transform, "DownLamp", new Vector3(0.15f, iy, face + 0.042f), new Vector3(0.05f, 0.06f, 0.006f), M("LockLamp"), false).GetComponent<Renderer>();
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
            Cube(root.transform, "CallPlate", new Vector3(bx, 1.1f, face + 0.006f), new Vector3(0.09f, 0.16f, 0.012f), steel, false);
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
        /// 3.2 x 1.1 m, a solid parapet 1 m high along its front and ends with a steel handrail on it, and an invisible guard
        /// above the parapet — nobody jumps off (a jump clears 0.9 m).
        /// </summary>
        static GameObject BuildBalcony()
        {
            var root = new GameObject("Balcony");
            const float L = 3.2f, D = 1.1f, slab = 0.18f, wall = 0.08f, h = 1.0f;
            var concrete = M("Wall");
            var steel = M("Metal");
            Cube(root.transform, "Slab", new Vector3(0f, -slab / 2f, D / 2f), new Vector3(L, slab, D), concrete, true);
            Cube(root.transform, "ParapetFront", new Vector3(0f, h / 2f, D - wall / 2f), new Vector3(L, h, wall), concrete, true);
            foreach (float x in new[] { -1f, 1f })
                Cube(root.transform, "ParapetSide", new Vector3(x * (L / 2f - wall / 2f), h / 2f, (D - wall) / 2f), new Vector3(wall, h, D - wall), concrete, true);
            Cube(root.transform, "Rail", new Vector3(0f, h + 0.02f, D - wall / 2f), new Vector3(L + 0.02f, 0.04f, wall + 0.03f), steel, false);
            foreach (float x in new[] { -1f, 1f })
                Cube(root.transform, "RailSide", new Vector3(x * (L / 2f - wall / 2f), h + 0.02f, (D - wall) / 2f), new Vector3(wall + 0.03f, 0.04f, D - wall), steel, false);
            // the guard over the parapet: a box collider, nothing to see
            var guard = Child(root.transform, "Guard", Vector3.zero);
            guard.layer = LevelMetrics.CeilingLayer; // sight passes, bodies do not
            var front = guard.AddComponent<BoxCollider>();
            front.center = new Vector3(0f, h + 0.5f, D - wall / 2f);
            front.size = new Vector3(L, 1.0f, wall);
            foreach (float x in new[] { -1f, 1f })
            {
                var side = guard.AddComponent<BoxCollider>();
                side.center = new Vector3(x * (L / 2f - wall / 2f), h + 0.5f, (D - wall) / 2f);
                side.size = new Vector3(wall, 1.0f, D - wall);
            }
            return root;
        }

        /// <summary>Furniture whose BodyDoor{n} / BodyDrawer{n} parts open for real (Items.Storage).</summary>
        static GameObject Opening(GameObject go)
        {
            go.AddComponent<Stealth.Items.Storage>();
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
        /// The locks every door has: lock lamps over it on both sides (red while locked, green once hacked; hidden on doors
        /// without a lock), a hasp with a padlock across the leaves' seam at <paramref name="seam"/> on both faces
        /// (<paramref name="front"/>, <paramref name="back"/>: the faces' z), and the key panels beside the frame.
        /// </summary>
        static void DoorLocks(GameObject root, AutoDoor door, float w, float h, float seam, float front, float back)
        {
            door.LockLamps = new Renderer[]
            {
                Cube(root.transform, "LockLampA", new Vector3(0, h + 0.1f, 0.08f), new Vector3(0.22f, 0.07f, 0.03f), M("LockLamp"), false).GetComponent<Renderer>(),
                Cube(root.transform, "LockLampB", new Vector3(0, h + 0.1f, -0.08f), new Vector3(0.22f, 0.07f, 0.03f), M("LockLamp"), false).GetComponent<Renderer>(),
            };
            foreach (var lamp in door.LockLamps) lamp.shadowCastingMode = ShadowCastingMode.Off;
            door.Padlocks = new[] { 1f, -1f }.Select(side =>
            {
                var holder = Child(root.transform, side > 0 ? "PadlockA" : "PadlockB", new Vector3(seam, 1.2f, side > 0 ? front : back)).transform;
                holder.localRotation = Quaternion.Euler(0f, side > 0 ? 0f : 180f, 0f);
                Cube(holder, "Hasp", new Vector3(0f, 0f, 0.008f), new Vector3(0.2f, 0.05f, 0.016f), M("Metal"), false);
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
            keyLock.Panels = new[] { 1f, -1f }.Select(side =>
            {
                var panel = Model("key_lock");
                panel.name = side > 0 ? "KeyLockA" : "KeyLockB";
                panel.transform.SetParent(root.transform, false);
                panel.transform.localPosition = new Vector3(w / 2f + 0.22f, 1.15f, side * (LevelMetrics.WallThickness / 2f + 0.005f));
                panel.transform.localRotation = Quaternion.Euler(0f, side > 0 ? 0f : 180f, 0f);
                Paint(panel, "Metal", "Metal", "LockLamp");
                return panel;
            }).ToArray();
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
            light.cookie = LampCookie();
            var rl = root.AddComponent<RoomLight>();
            rl.Lamp = light;
            rl.Panels = panels.ToArray();
            return root;
        }

        const string LampCookiePath = SpritesDir + "/LampCookie.cubemap";

        /// <summary>
        /// Cube cookie of the ceiling lamps: full light downwards and sideways, fading out above the horizon (no light onto
        /// the ceiling it hangs from).
        /// </summary>
        static Cubemap LampCookie()
        {
            var cookie = AssetDatabase.LoadAssetAtPath<Cubemap>(LampCookiePath);
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
                        float v = LightMath.CookieUpFade(CubeDirection(face, (x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f).y);
                        px[y * size + x] = new Color(v, v, v, v);
                    }
                cube.SetPixels(px, face);
            }
            cube.Apply();
            if (cookie != null) AssetDatabase.DeleteAsset(LampCookiePath);
            EnsureFolder(SpritesDir);
            AssetDatabase.CreateAsset(cube, LampCookiePath);
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
            NoShadows(model);
            // Shines down into the doorway (both rooms): nothing goes up to the floor above.
            var glow = Lamp(root.transform, "Glow", new Vector3(0f, -0.12f, 0f), Quaternion.Euler(90f, 0f, 0f), LightType.Spot, new Color(0.35f, 1f, 0.5f), 0.35f, 2.6f);
            glow.spotAngle = 170f;
            glow.innerSpotAngle = 100f;
            return root;
        }

        static GameObject BuildHatch()
        {
            var hatch = PropObject("Hatch", "hatch", "Hatch", "PropAccent", "HatchRing", collider: false);
            // The lid (disc and bars) on a hinge at its edge: it swings up while someone hides in the hatch.
            var hinge = Child(hatch.transform, "LidHinge", new Vector3(0.75f, 0.03f, 0f));
            foreach (var part in hatch.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.transform).ToArray())
                if (part.name.StartsWith("Body") || part.name.StartsWith("Accent")) part.SetParent(hinge.transform, true);
            var h = hatch.AddComponent<Hatch>();
            h.Lid = hinge.transform;
            var b = LocalBounds(hatch);
            // a post at the rim with a lamp on top: the hatch's colour (entry blue — the tank's filler, exit green — the way down)
            Cube(hatch.transform, "MarkPost", new Vector3(0f, 0.65f, -0.85f), new Vector3(0.07f, 1.3f, 0.07f), M("Metal"), false);
            Cube(hatch.transform, "EyeMarkLamp", new Vector3(0f, 1.36f, -0.85f), new Vector3(0.2f, 0.14f, 0.2f), M("HatchRing"), false);
            h.Glow = hatch.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.StartsWith("Eye")).ToArray();
            hatch.AddComponent<KeepUnmerged>(); // the lid moves, the colours differ
            var box = hatch.AddComponent<BoxCollider>();
            // flush with the floor: walked over, never bumped into; the look ray finds it (the hatch key works only
            // while looking at the hatch, PlayerController.AtHatch) and dropped items land on it
            box.size = new Vector3(b.size.x, 0.08f, b.size.z);
            box.center = new Vector3(b.center.x, 0.04f, b.center.z);
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
