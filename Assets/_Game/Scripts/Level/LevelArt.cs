using UnityEngine;

namespace Stealth.Level
{
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
        public GameObject Roof;
        public GameObject OutsideGround;
        public GameObject Fence;

        [Header("Pieces")]
        public GameObject Stairs;
        public GameObject StairsMirrored;
        public GameObject VentFrame;
        [Tooltip("A vent under the ceiling with the crate stacks under it on both sides.")]
        public GameObject HighVentFrame;
        [Tooltip("Frame of a narrow slot in a wall, with the navmesh volume that keeps robots out.")]
        public GameObject SqueezeFrame;
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
            _ => null,
        };
    }
}
