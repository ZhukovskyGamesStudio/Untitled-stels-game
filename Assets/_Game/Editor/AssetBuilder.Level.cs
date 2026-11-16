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
            var vent = Prefab($"{LevelPrefabs}/VentFrame.prefab", () => BuildVentFrame(false));
            var highVent = Prefab($"{LevelPrefabs}/HighVentFrame.prefab", () => BuildVentFrame(true));
            var squeeze = Prefab($"{LevelPrefabs}/SqueezeFrame.prefab", BuildSqueezeFrame);
            var railingBay = Prefab($"{LevelPrefabs}/RailingBay.prefab", BuildRailingBay);
            var railingBlock = Prefab($"{LevelPrefabs}/RailingBlock.prefab", BuildRailingBlock);
            var door = Prefab($"{LevelPrefabs}/AutoDoor.prefab", () => BuildAutoDoor());
            // the auto repair shop: steel leaves; the paint booth's door as wide as a car needs
            var shopDoor = Prefab($"{LevelPrefabs}/ShopDoor.prefab", () => BuildAutoDoor("ShopDoor", LevelMetrics.DoorWidth, "ShopSteel"));
            var vehicleDoor = Prefab($"{LevelPrefabs}/VehicleDoor.prefab", () => BuildAutoDoor("VehicleDoor", LevelMetrics.VehicleDoorWidth, "ShopSteel"));
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
            var pLockers = Prefab(p + "Lockers.prefab", () =>
            {
                var go = PropObject("Lockers", "lockers", "Lockers", "PropAccent", "PropAccent");
                go.AddComponent<Stealth.Items.Storage>(); // the doors swing open (BodyDoor0..2)
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
                terminal.Screens = console.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name.StartsWith("Eye"));
                terminal.AccessScreens = access.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name.StartsWith("Eye"));
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
            if (levelArt.HighVentFrame != highVent || levelArt.RailingBay != railingBay || levelArt.RailingBlock != railingBlock || levelArt.OutsideTerrain != terrain)
            {
                // fields newer than the asset: filled without overwriting the rest
                levelArt.HighVentFrame = highVent;
                levelArt.RailingBay = railingBay;
                levelArt.RailingBlock = railingBlock;
                levelArt.OutsideTerrain = terrain;
                EditorUtility.SetDirty(levelArt);
            }
            if (levelArt.ShopDoor != shopDoor || levelArt.VehicleDoor != vehicleDoor)
            {
                levelArt.ShopDoor = shopDoor;
                levelArt.VehicleDoor = vehicleDoor;
                EditorUtility.SetDirty(levelArt);
            }
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
            AddStairLooks(root, mirrored); // what is seen: one of the built looks over these boxes
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
            // the removed grille: a frame with slats, leaning on the wall beside the hole on side A (beside the crates)
            var grille = new GameObject("Grille").transform;
            grille.SetParent(root.transform, false);
            float gw = w * 0.9f, gh = h * 0.9f;
            grille.localPosition = new Vector3(w / 2f + flange + gw / 2f + 0.06f, 0f, t / 2f + 0.14f); // the top rests on the wall face
            grille.localRotation = Quaternion.Euler(-10f, 0f, 0f); // its foot on the floor, its top against the wall
            Cube(grille, "GrilleTop", new Vector3(0, gh - 0.025f, 0.11f), new Vector3(gw, 0.05f, 0.025f), metal, false);
            Cube(grille, "GrilleBottom", new Vector3(0, 0.025f, 0.11f), new Vector3(gw, 0.05f, 0.025f), metal, false);
            foreach (float s in new[] { -1f, 1f })
                Cube(grille, s < 0 ? "GrilleLeft" : "GrilleRight", new Vector3(s * (gw / 2f - 0.025f), gh / 2f, 0.11f), new Vector3(0.05f, gh, 0.025f), metal, false);
            for (int i = 1; i < 8; i++)
            {
                var slat = Cube(grille, $"Slat{i}", new Vector3(0, gh * i / 8f, 0.11f), new Vector3(gw - 0.08f, 0.012f, 0.06f), metal, false);
                slat.transform.localRotation = Quaternion.Euler(35f, 0f, 0f); // louvres
            }
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

        /// <summary>A sliding door <paramref name="width"/> wide (nominal; the leaves stretched to it) in <paramref name="paint"/>.</summary>
        static GameObject BuildAutoDoor(string name, float width, string paint)
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
                Paint(leaf, paint, paint, paint);
                foreach (var r in leaf.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
                return leaf.transform;
            }
            door.PanelA = Leaf("PanelA", "door_leaf", -w / 4f, 0f);
            door.PanelB = Leaf("PanelB", "door_leaf_b", w / 4f, 180f); // its texture shifted: the halves do not mirror
            var blocker = root.AddComponent<BoxCollider>();
            blocker.center = new Vector3(0, h / 2f, 0);
            blocker.size = new Vector3(w, h, 0.1f);
            door.Blocker = blocker;
            // Lock lamps above the door on both sides: red while locked, green once hacked (hidden on doors without a lock).
            door.LockLamps = new Renderer[]
            {
                Cube(root.transform, "LockLampA", new Vector3(0, h + 0.1f, 0.08f), new Vector3(0.22f, 0.07f, 0.03f), M("LockLamp"), false).GetComponent<Renderer>(),
                Cube(root.transform, "LockLampB", new Vector3(0, h + 0.1f, -0.08f), new Vector3(0.22f, 0.07f, 0.03f), M("LockLamp"), false).GetComponent<Renderer>(),
            };
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
