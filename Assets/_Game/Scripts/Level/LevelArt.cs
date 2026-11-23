using UnityEngine;

namespace Stealth.Level
{
    /// <summary>A furniture prefab of a kind (<see cref="LevelArt.RoomProps"/>).</summary>
    [System.Serializable]
    public struct PropPrefab
    {
        public PropKind Kind;
        public GameObject Prefab;
    }

    /// <summary>
    /// A city model (<see cref="CityPropKind"/>, Tools/Blender/street.py): its parts, the slot each is drawn with and where
    /// each sits in the model. The level builder merges every placed copy into the city's one mesh (no objects, no renderers).
    /// </summary>
    [System.Serializable]
    public class CityMesh
    {
        public CityPropKind Kind;
        public Mesh[] Meshes = new Mesh[0];
        public CitySlot[] Slots = new CitySlot[0];
        public Matrix4x4[] Locals = new Matrix4x4[0];
    }

    /// <summary>
    /// Every prefab the level builder instantiates. Created by the asset builder (Stealth / Build Prototype Assets),
    /// then freely editable: swap a prefab here to restyle the whole office.
    /// </summary>
    [CreateAssetMenu(menuName = "Stealth/Level Art")]
    public class LevelArt : ScriptableObject
    {
        [Header("Root (NavMeshSurface)")]
        public GameObject LevelRoot;

        [Header("Structure: unit cubes scaled by the builder")]
        public GameObject WallBlock;
        public GameObject TrimBlock;
        public GameObject FloorSlab;
        [Tooltip("A hole the heavy hammer knocked in a floor (a unit square scaled to it): the broken edge, rubble below, a navmesh obstacle.")]
        public GameObject FloorHole;
        public GameObject Roof;
        public GameObject OutsideGround;
        [Tooltip("The land round the house to the horizon (looks only): flat in the middle, mounds, rubble, far ridges")]
        public GameObject OutsideTerrain;
        public GameObject Fence;
        [Tooltip("The city round the yard (CityPlan): the object its merged mesh is drawn with, the materials by CitySlot, a street lamp's light.")]
        public GameObject CityPiece;
        public Material[] CityMaterials = new Material[0];
        [Tooltip("The city's models (CityPropKind, Tools/Blender/street.py): street furniture, cars, trees, the facades' kit; merged into the city's mesh.")]
        public CityMesh[] CityMeshes = new CityMesh[0];
        [Tooltip("The house's services (UtilityPlan): materials by UtilitySlot (drawn with CityPiece).")]
        public Material[] UtilityMaterials = new Material[0];
        [Tooltip("The furniture's screens: the glowing material the models carry, then what it becomes in a house — off, static, cracked (ScreenLook's order; ScreenLooks).")]
        public Material[] Screens = new Material[0];
        [Tooltip("A plain steel door-and-a-half on hinges (the shop, the plant room, storerooms): a louvre at the bottom, a wired-glass slot")] public GameObject ShopDoor;
        [Tooltip("The paint booth's sliding door, wide enough for a car (LevelMetrics.VehicleDoorWidth)")] public GameObject VehicleDoor;
        [Tooltip("The hospital's door-and-a-half on hinges: pale laminate, a vision slot, a steel kick plate")] public GameObject ClinicDoor;
        [Tooltip("The hospital's double doors with round windows that swing both ways (corridors, theatres)")] public GameObject ClinicSwingDoor;
        [Tooltip("A flat's entry door off the apartment block's landing: a padded steel door-and-a-half with a peephole and a number, opening out")] public GameObject FlatDoor;
        [Tooltip("A narrow wooden door-and-a-half (a flat's bathroom, LevelMetrics.NarrowDoorWidth)")] public GameObject NarrowDoor;
        [Tooltip("The thin wooden door-and-a-half between rooms: panelled leaves on hinges")] public GameObject WoodDoor;
        [Tooltip("Double wooden doors with glazed panels (big rooms, a flat's living room)")] public GameObject WoodDoubleDoor;
        [Tooltip("The apartment block's street door: thick steel, an intercom, a closer, opening out")] public GameObject EntranceDoor;
        [Tooltip("The storage room's door: steel leaves with a hazard band and a big lit sign (red: locked, green: open)")] public GameObject StorageDoor;
        [Tooltip("A light switch beside a doorway (UtilityPlan.SwitchSpots): switches its room's lamps.")]
        public GameObject LightSwitch;
        [Tooltip("A grate in a room's ceiling: the way into the crawl ducts (VentPlan).")]
        public GameObject VentGrate;
        [Tooltip("The lid over the shaft in the lobby (ShaftHatch), with the ladder's top under it (ShaftLadder).")]
        public GameObject ShaftHatch;
        [Tooltip("The ladder's foot in the basement (ShaftLadder).")]
        public GameObject ShaftLadder;
        [Tooltip("A navmesh link up the shaft (the storage robot climbs the ladder while the lid is open).")]
        public GameObject ShaftLink;
        public GameObject CityLamp;

        [Header("Pieces")]
        public GameObject Stairs;
        public GameObject StairsMirrored;
        [Tooltip("A stairwell's flight stacked floor over floor (the apartment block, Stair.Stacked): thin flights, the floor's landing open.")]
        public GameObject StairsStacked, StairsStackedMirrored;
        public GameObject VentFrame;
        [Tooltip("A vent under the ceiling with the crate stacks under it on both sides.")]
        public GameObject HighVentFrame;
        [Tooltip("Frame of a narrow slot in a wall, with the navmesh volume that keeps robots out.")]
        public GameObject SqueezeFrame;
        [Tooltip("The broken edges and rubble of a breach (a stretch of wall knocked out; no colliders).")]
        public GameObject BreachFrame;
        [Tooltip("The broken edges and rubble of a hole at the floor, with the navmesh volume that keeps robots out.")]
        public GameObject HoleFrame;
        [Tooltip("The frame of a window between rooms, its glass gone (no colliders: the wall pieces are the sill and the head).")]
        public GameObject InnerWindowFrame;
        [Tooltip("One bay of a gallery railing in every staircase look (StairLook picks one).")]
        public GameObject RailingBay;
        [Tooltip("An invisible box collider, stretched along a railing.")]
        public GameObject RailingBlock;
        [Tooltip("A balcony of the apartment block: the slab and the parapet outside the facade (+Z out of the house).")]
        public GameObject Balcony;
        [Tooltip("The apartment block's lift: its doors on a landing (with the call button) and its cab.")]
        public GameObject LiftDoor, LiftCab;
        [Tooltip("The office's glass sliding door (server rooms, labs, control rooms: DoorKind.Slide).")]
        public GameObject AutoDoor;
        public GameObject RoomLight;
        [Tooltip("Facade window: frame, glass, blinds on the interior side (local +Z).")]
        public GameObject Window;
        [Tooltip("Green exit sign over every door, with a small light: the only light in a dark room besides the lamps.")]
        public GameObject ExitSign;
        [Tooltip("The sewer between houses: the team sells, pays the quota and votes for the next house there.")]
        public GameObject Sewer;

        [Header("Props")]
        public GameObject Desk;
        public GameObject Chair;
        public GameObject ServerRack;
        public GameObject Cabinet;
        public GameObject MeetingTable;
        public GameObject Plant;
        public GameObject KitchenCounter;
        public GameObject Hatch;
        public GameObject Shelf;
        public GameObject Boxes;
        public GameObject WaterCooler;
        public GameObject Whiteboard;
        public GameObject Sofa;
        public GameObject ToiletStall;
        public GameObject Partition;
        public GameObject Bookshelf;
        public GameObject Printer;
        public GameObject Fridge;
        public GameObject CoffeeMachine;
        public GameObject RoundTable;
        public GameObject Armchair;
        public GameObject CoffeeTable;
        public GameObject TvStand;
        public GameObject ReceptionDesk;
        public GameObject TrashBin;
        public GameObject CoatRack;
        public GameObject Lockers;
        public GameObject Pallet;
        public GameObject WashBasin;
        public GameObject VendingMachine;
        public GameObject DeskLamp;
        public GameObject PingPongTable;
        public GameObject BeanBag;
        public GameObject ArcadeMachine;
        public GameObject Safe;
        public GameObject Terminal;
        public GameObject FloorLamp;
        [Tooltip("A crate to search (SearchCrate).")]
        public GameObject Crate;
        [Tooltip("Points of interest: the med table (hospital), the workbench (garage), the robot programming console (office).")]
        public GameObject MedTable, Workbench, Programmer;
        [Tooltip("The entrance of a hidden room: looks like a wall block, players walk through it, robots are kept out.")]
        public GameObject IllusionBlock;
        [Tooltip("The house's main job: a lock panel of the vault on a wall (VaultConsole), the hostage's cage (Cage), the vault door's lamps (VaultPanel).")]
        public GameObject VaultConsole, Cage, VaultPanel;
        [Tooltip("The vault's round bank door (an AutoDoor that swings), and the heavy doors in the facade.")]
        public GameObject VaultDoor, ExteriorDoor;

        /// <summary>The prefab of a door kind (<see cref="DoorRules"/>); a narrow doorway's wooden door is the narrow one. Missing: the sliding door.</summary>
        public GameObject Door(DoorKind kind, bool narrow)
        {
            var go = kind switch
            {
                DoorKind.Wood => narrow ? NarrowDoor : WoodDoor,
                DoorKind.WoodDouble => narrow ? NarrowDoor : WoodDoubleDoor,
                DoorKind.Entry => FlatDoor,
                DoorKind.Entrance => EntranceDoor,
                DoorKind.Steel => ShopDoor,
                DoorKind.Clinic => ClinicDoor,
                DoorKind.ClinicSwing => ClinicSwingDoor,
                DoorKind.Storage => StorageDoor,
                DoorKind.Vehicle => VehicleDoor,
                DoorKind.Vault => VaultDoor,
                _ => AutoDoor,
            };
            return go != null ? go : AutoDoor;
        }
        [Tooltip("The furniture of the themed rooms (gym, library, kitchen run, restrooms…), by kind.")]
        public PropPrefab[] RoomProps = new PropPrefab[0];

        public GameObject Prop(PropKind kind) => kind switch
        {
            PropKind.Desk => Desk,
            PropKind.Chair => Chair,
            PropKind.ServerRack => ServerRack,
            PropKind.Cabinet => Cabinet,
            PropKind.MeetingTable => MeetingTable,
            PropKind.Plant => Plant,
            PropKind.KitchenCounter => KitchenCounter,
            PropKind.Hatch => Hatch,
            PropKind.ShaftHatch => ShaftHatch,
            PropKind.Shelf => Shelf,
            PropKind.Boxes => Boxes,
            PropKind.WaterCooler => WaterCooler,
            PropKind.Whiteboard => Whiteboard,
            PropKind.Sofa => Sofa,
            PropKind.ToiletStall => ToiletStall,
            PropKind.Partition => Partition,
            PropKind.Bookshelf => Bookshelf,
            PropKind.Printer => Printer,
            PropKind.Fridge => Fridge,
            PropKind.CoffeeMachine => CoffeeMachine,
            PropKind.RoundTable => RoundTable,
            PropKind.Armchair => Armchair,
            PropKind.CoffeeTable => CoffeeTable,
            PropKind.TvStand => TvStand,
            PropKind.ReceptionDesk => ReceptionDesk,
            PropKind.TrashBin => TrashBin,
            PropKind.CoatRack => CoatRack,
            PropKind.Lockers => Lockers,
            PropKind.Pallet => Pallet,
            PropKind.WashBasin => WashBasin,
            PropKind.VendingMachine => VendingMachine,
            PropKind.DeskLamp => DeskLamp,
            PropKind.FloorLamp => FloorLamp,
            PropKind.PingPongTable => PingPongTable,
            PropKind.BeanBag => BeanBag,
            PropKind.ArcadeMachine => ArcadeMachine,
            PropKind.Safe => Safe,
            PropKind.Terminal => Terminal,
            PropKind.Crate => Crate,
            PropKind.MedTable => MedTable,
            PropKind.Workbench => Workbench,
            PropKind.Programmer => Programmer,
            PropKind.Cage => Cage,
            _ => RoomProp(kind),
        };

        GameObject RoomProp(PropKind kind)
        {
            if (RoomProps != null) foreach (var p in RoomProps) if (p.Kind == kind) return p.Prefab;
            return null;
        }
    }
}
