using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// Furniture for rooms of any size and shape (pure, part of <see cref="LevelGenerator"/>). Works on the plan's raster
    /// and the exact footprints of the models: groups stand along the walls (backs to the wall, clear of doorways,
    /// stairs and windows' sills), islands fill the middle of big rooms with aisles around them, small things take
    /// the free corners. Every room type has its own groups (after office planning guides: back-to-back desk
    /// clusters, storage walls, a kitchenette with tables, a lounge corner, cold aisles between racks).
    /// Loot spots ride on the furniture (desk tops, shelves, counters).
    /// </summary>
    public sealed partial class Furnisher
    {
        /// <summary>Footprints of the models in their own frame: x = local X, y = local Z (front = +Z), metres.</summary>
        static readonly Dictionary<PropKind, Rect> Foot = new()
        {
            [PropKind.Desk] = Rect.MinMaxRect(-0.8f, -0.4f, 0.8f, 0.4f),
            [PropKind.Terminal] = Rect.MinMaxRect(-0.8f, -0.4f, 0.8f, 0.4f),
            [PropKind.Chair] = Rect.MinMaxRect(-0.3f, -0.3f, 0.3f, 0.3f),
            [PropKind.ServerRack] = Rect.MinMaxRect(-0.35f, -0.5f, 0.35f, 0.52f),
            [PropKind.Cabinet] = Rect.MinMaxRect(-0.45f, -0.25f, 0.45f, 0.28f),
            [PropKind.MeetingTable] = Rect.MinMaxRect(-1.6f, -0.65f, 1.6f, 0.65f),
            [PropKind.Plant] = Rect.MinMaxRect(-0.35f, -0.35f, 0.35f, 0.35f),
            [PropKind.KitchenCounter] = Rect.MinMaxRect(-1.22f, -0.35f, 1.22f, 0.35f),
            [PropKind.Shelf] = Rect.MinMaxRect(-1.0f, -0.3f, 1.0f, 0.3f),
            [PropKind.StorageRack] = Rect.MinMaxRect(-1.0f, -0.3f, 1.0f, 0.3f),
            [PropKind.ChargingDock] = Rect.MinMaxRect(-0.5f, -0.32f, 0.5f, 0.32f),
            [PropKind.BatteryStack] = Rect.MinMaxRect(-0.45f, -0.3f, 0.45f, 0.3f),
            [PropKind.Generator] = Rect.MinMaxRect(-0.8f, -0.6f, 0.8f, 0.6f),
            [PropKind.ScrapShrine] = Rect.MinMaxRect(-0.7f, -0.25f, 0.7f, 0.25f),
            [PropKind.ConduitGrowth] = Rect.MinMaxRect(-0.6f, -0.12f, 0.6f, 0.12f),
            [PropKind.CableMass] = Rect.MinMaxRect(-1.0f, -0.6f, 1.0f, 0.6f),
            [PropKind.Boxes] = Rect.MinMaxRect(-0.78f, -0.33f, 0.3f, 0.3f), // Unity mirrors X on import: the stack leans to −X
            [PropKind.WaterCooler] = Rect.MinMaxRect(-0.17f, -0.17f, 0.17f, 0.17f),
            [PropKind.Whiteboard] = Rect.MinMaxRect(-0.95f, -0.08f, 0.95f, 0.08f),
            [PropKind.Sofa] = Rect.MinMaxRect(-1.1f, -0.43f, 1.1f, 0.43f),
            [PropKind.ToiletStall] = Rect.MinMaxRect(-0.53f, -0.78f, 0.53f, 0.75f),
            [PropKind.Partition] = Rect.MinMaxRect(-0.81f, -0.03f, 0.81f, 0.03f),
            [PropKind.Bookshelf] = Rect.MinMaxRect(-0.5f, -0.18f, 0.5f, 0.18f),
            [PropKind.Printer] = Rect.MinMaxRect(-0.3f, -0.3f, 0.3f, 0.5f),
            [PropKind.Fridge] = Rect.MinMaxRect(-0.35f, -0.35f, 0.35f, 0.4f),
            [PropKind.CoffeeMachine] = Rect.MinMaxRect(-0.17f, -0.2f, 0.17f, 0.24f),
            [PropKind.RoundTable] = Rect.MinMaxRect(-0.5f, -0.5f, 0.5f, 0.5f),
            [PropKind.Armchair] = Rect.MinMaxRect(-0.44f, -0.38f, 0.44f, 0.38f),
            [PropKind.CoffeeTable] = Rect.MinMaxRect(-0.5f, -0.28f, 0.5f, 0.28f),
            [PropKind.TvStand] = Rect.MinMaxRect(-0.7f, -0.2f, 0.7f, 0.2f),
            [PropKind.ReceptionDesk] = Rect.MinMaxRect(-1.25f, -0.4f, 1.25f, 0.48f),
            [PropKind.TrashBin] = Rect.MinMaxRect(-0.19f, -0.2f, 0.19f, 0.2f),
            [PropKind.CoatRack] = Rect.MinMaxRect(-0.42f, -0.42f, 0.42f, 0.42f), // with its coats on (PropVariants)
            [PropKind.Lockers] = Rect.MinMaxRect(-0.5f, -0.25f, 0.5f, 0.28f),
            [PropKind.Pallet] = Rect.MinMaxRect(-0.6f, -0.5f, 0.6f, 0.5f),
            [PropKind.WashBasin] = Rect.MinMaxRect(-0.8f, -0.26f, 0.8f, 0.25f),
            [PropKind.VendingMachine] = Rect.MinMaxRect(-0.45f, -0.4f, 0.45f, 0.42f),
            [PropKind.FloorLamp] = Rect.MinMaxRect(-0.2f, -0.2f, 0.2f, 0.2f),
            [PropKind.PingPongTable] = Rect.MinMaxRect(-1.37f, -0.87f, 1.37f, 0.87f),
            [PropKind.BeanBag] = Rect.MinMaxRect(-0.45f, -0.45f, 0.45f, 0.45f),
            [PropKind.ArcadeMachine] = Rect.MinMaxRect(-0.36f, -0.4f, 0.36f, 0.55f),
            [PropKind.Safe] = Rect.MinMaxRect(-0.4f, -0.35f, 0.4f, 0.41f),
            [PropKind.Hatch] = Rect.MinMaxRect(-0.9f, -0.9f, 0.9f, 0.9f),
            [PropKind.ShaftHatch] = Rect.MinMaxRect(-0.9f, -0.9f, 0.9f, 0.9f),
            [PropKind.Crate] = Rect.MinMaxRect(-0.4f, -0.3f, 0.4f, 0.3f),
            [PropKind.MedTable] = Rect.MinMaxRect(-0.97f, -0.53f, 0.97f, 0.53f),
            [PropKind.Workbench] = Rect.MinMaxRect(-1.56f, -0.56f, 1.56f, 0.56f), // the car upgrade bench (poi.py model_workbench: 3.1 x 1.1)
            [PropKind.Programmer] = Rect.MinMaxRect(-0.99f, -0.5f, 0.99f, 0.42f),
            [PropKind.Cage] = Rect.MinMaxRect(-1.0f, -1.0f, 1.0f, 1.0f),
            // the themed rooms (Tools/Blender/rooms_*.py)
            [PropKind.KitchenSink] = Rect.MinMaxRect(-0.6f, -0.31f, 0.6f, 0.31f),
            [PropKind.Stove] = Rect.MinMaxRect(-0.3f, -0.31f, 0.3f, 0.31f),
            [PropKind.Microwave] = Rect.MinMaxRect(-0.25f, -0.18f, 0.25f, 0.18f),
            [PropKind.WallCabinet] = Rect.MinMaxRect(-0.6f, -0.175f, 0.6f, 0.175f),
            [PropKind.Urinal] = Rect.MinMaxRect(-0.2f, -0.175f, 0.2f, 0.175f),
            [PropKind.MirrorSink] = Rect.MinMaxRect(-0.35f, -0.25f, 0.35f, 0.25f),
            [PropKind.HandDryer] = Rect.MinMaxRect(-0.15f, -0.1f, 0.15f, 0.1f),
            [PropKind.Bench] = Rect.MinMaxRect(-0.8f, -0.2f, 0.8f, 0.2f),
            [PropKind.ShowerStall] = Rect.MinMaxRect(-0.48f, -0.48f, 0.48f, 0.48f),
            [PropKind.Treadmill] = Rect.MinMaxRect(-0.42f, -0.95f, 0.42f, 0.95f),
            [PropKind.ExerciseBike] = Rect.MinMaxRect(-0.28f, -0.55f, 0.28f, 0.55f),
            [PropKind.WeightBench] = Rect.MinMaxRect(-0.8f, -0.65f, 0.8f, 0.65f),
            [PropKind.DumbbellRack] = Rect.MinMaxRect(-0.7f, -0.25f, 0.7f, 0.25f),
            [PropKind.PunchingBag] = Rect.MinMaxRect(-0.3f, -0.3f, 0.3f, 0.3f),
            [PropKind.HospitalBed] = Rect.MinMaxRect(-0.48f, -1.05f, 0.48f, 1.05f),
            [PropKind.MedicineCabinet] = Rect.MinMaxRect(-0.45f, -0.2f, 0.45f, 0.2f),
            [PropKind.PrivacyScreen] = Rect.MinMaxRect(-0.8f, -0.15f, 0.8f, 0.15f),
            [PropKind.SleepPod] = Rect.MinMaxRect(-0.55f, -1f, 0.55f, 1f),
            [PropKind.Hammock] = Rect.MinMaxRect(-1.3f, -0.45f, 1.3f, 0.45f),
            [PropKind.TallBookcase] = Rect.MinMaxRect(-0.5f, -0.175f, 0.5f, 0.175f),
            [PropKind.ReadingTable] = Rect.MinMaxRect(-1f, -0.45f, 1f, 0.45f),
            [PropKind.Globe] = Rect.MinMaxRect(-0.3f, -0.3f, 0.3f, 0.3f),
            [PropKind.Copier] = Rect.MinMaxRect(-0.48f, -0.33f, 0.48f, 0.33f),
            [PropKind.PaperStack] = Rect.MinMaxRect(-0.4f, -0.3f, 0.4f, 0.3f),
            [PropKind.MailShelf] = Rect.MinMaxRect(-0.6f, -0.2f, 0.6f, 0.2f),
            [PropKind.Shredder] = Rect.MinMaxRect(-0.23f, -0.18f, 0.23f, 0.18f),
            [PropKind.MonitorWall] = Rect.MinMaxRect(-1.2f, -0.15f, 1.2f, 0.15f),
            [PropKind.ConsoleDesk] = Rect.MinMaxRect(-1.2f, -0.45f, 1.2f, 0.45f),
            [PropKind.MixingDesk] = Rect.MinMaxRect(-0.8f, -0.4f, 0.8f, 0.4f),
            [PropKind.MicStand] = Rect.MinMaxRect(-0.25f, -0.25f, 0.25f, 0.25f),
            [PropKind.AcousticPanel] = Rect.MinMaxRect(-0.6f, -0.04f, 0.6f, 0.04f),
            [PropKind.DrumKit] = Rect.MinMaxRect(-0.8f, -0.65f, 0.8f, 0.65f),
            [PropKind.PoolTable] = Rect.MinMaxRect(-1.25f, -0.7f, 1.25f, 0.7f),
            [PropKind.Foosball] = Rect.MinMaxRect(-0.7f, -0.375f, 0.7f, 0.375f),
            [PropKind.DartBoard] = Rect.MinMaxRect(-0.3f, -0.04f, 0.3f, 0.04f),
            [PropKind.BarCounter] = Rect.MinMaxRect(-1f, -0.3f, 1f, 0.3f),
            [PropKind.BarStool] = Rect.MinMaxRect(-0.2f, -0.2f, 0.2f, 0.2f),
            [PropKind.LabBench] = Rect.MinMaxRect(-0.9f, -0.375f, 0.9f, 0.375f),
            [PropKind.PartsCabinet] = Rect.MinMaxRect(-0.45f, -0.225f, 0.45f, 0.225f),
            [PropKind.RobotArm] = Rect.MinMaxRect(-0.3f, -0.3f, 0.3f, 0.45f),
            // round 2: five more pieces for every room type, and the vault (Tools/Blender/rooms2_*.py)
            [PropKind.StandingDesk] = Rect.MinMaxRect(-0.7f, -0.35f, 0.7f, 0.37f),
            [PropKind.PlanterDivider] = Rect.MinMaxRect(-0.81f, -0.18f, 0.81f, 0.18f),
            [PropKind.MobileWhiteboard] = Rect.MinMaxRect(-0.71f, -0.28f, 0.71f, 0.28f),
            [PropKind.FilePedestal] = Rect.MinMaxRect(-0.2f, -0.28f, 0.2f, 0.29f),
            [PropKind.SofaPod] = Rect.MinMaxRect(-0.9f, -0.45f, 0.9f, 0.45f),
            [PropKind.ExecutiveDesk] = Rect.MinMaxRect(-0.9f, -0.45f, 0.9f, 0.45f),
            [PropKind.FilingCabinet] = Rect.MinMaxRect(-0.23f, -0.3f, 0.23f, 0.3f),
            [PropKind.Credenza] = Rect.MinMaxRect(-0.8f, -0.23f, 0.8f, 0.25f),
            [PropKind.GuestChair] = Rect.MinMaxRect(-0.3f, -0.29f, 0.3f, 0.26f),
            [PropKind.WallClock] = Rect.MinMaxRect(-0.175f, -0.03f, 0.175f, 0.027f),
            [PropKind.ConferenceTable] = Rect.MinMaxRect(-1.8f, -0.6f, 1.8f, 0.6f),
            [PropKind.ProjectorScreen] = Rect.MinMaxRect(-1f, -0.08f, 1f, 0.06f),
            [PropKind.Flipchart] = Rect.MinMaxRect(-0.35f, -0.3f, 0.35f, 0.28f),
            [PropKind.AvCredenza] = Rect.MinMaxRect(-0.8f, -0.25f, 0.8f, 0.27f),
            [PropKind.MeetingChair] = Rect.MinMaxRect(-0.28f, -0.29f, 0.28f, 0.27f),
            [PropKind.InfoKiosk] = Rect.MinMaxRect(-0.3f, -0.25f, 0.3f, 0.25f),
            [PropKind.SecurityTurnstile] = Rect.MinMaxRect(-0.6f, -0.3f, 0.6f, 0.3f),
            [PropKind.RoundBenchPlanter] = Rect.MinMaxRect(-0.9f, -0.9f, 0.9f, 0.9f),
            [PropKind.LogoTotem] = Rect.MinMaxRect(-0.4f, -0.2f, 0.4f, 0.2f),
            [PropKind.QueueBarrier] = Rect.MinMaxRect(-0.8f, -0.15f, 0.8f, 0.15f),
            [PropKind.DiningTable] = Rect.MinMaxRect(-0.8f, -0.45f, 0.8f, 0.45f),
            [PropKind.KitchenIsland] = Rect.MinMaxRect(-1f, -0.455f, 1f, 0.455f),
            [PropKind.Dishwasher] = Rect.MinMaxRect(-0.3f, -0.31f, 0.3f, 0.31f),
            [PropKind.RecyclingBins] = Rect.MinMaxRect(-0.603f, -0.2f, 0.603f, 0.2f),
            [PropKind.PantryCabinet] = Rect.MinMaxRect(-0.45f, -0.305f, 0.45f, 0.305f),
            [PropKind.DoubleSink] = Rect.MinMaxRect(-0.7f, -0.28f, 0.7f, 0.28f),
            [PropKind.JanitorCart] = Rect.MinMaxRect(-0.45f, -0.23f, 0.45f, 0.23f),
            [PropKind.ChangingTable] = Rect.MinMaxRect(-0.45f, -0.25f, 0.45f, 0.25f),
            [PropKind.TowelDispenser] = Rect.MinMaxRect(-0.15f, -0.06f, 0.15f, 0.06f),
            [PropKind.SanitaryBin] = Rect.MinMaxRect(-0.15f, -0.128f, 0.15f, 0.128f),
            [PropKind.SectionalSofa] = Rect.MinMaxRect(-1.2f, -0.8f, 1.2f, 0.8f),
            [PropKind.EggChair] = Rect.MinMaxRect(-0.4f, -0.39f, 0.4f, 0.39f),
            [PropKind.SideTable] = Rect.MinMaxRect(-0.25f, -0.25f, 0.25f, 0.25f),
            [PropKind.MagazineRack] = Rect.MinMaxRect(-0.3f, -0.17f, 0.3f, 0.17f),
            [PropKind.AreaRug] = Rect.MinMaxRect(-1f, -0.7f, 1f, 0.7f),
            [PropKind.CorridorBench] = Rect.MinMaxRect(-0.7f, -0.22f, 0.7f, 0.22f),
            [PropKind.FireCabinet] = Rect.MinMaxRect(-0.2f, -0.126f, 0.2f, 0.126f),
            [PropKind.NoticeBoard] = Rect.MinMaxRect(-0.6f, -0.024f, 0.6f, 0.024f),
            [PropKind.BinPair] = Rect.MinMaxRect(-0.35f, -0.179f, 0.35f, 0.179f),
            [PropKind.UmbrellaStand] = Rect.MinMaxRect(-0.155f, -0.155f, 0.155f, 0.155f),
            [PropKind.NetworkCabinet] = Rect.MinMaxRect(-0.3f, -0.3f, 0.3f, 0.31f),
            [PropKind.UpsUnit] = Rect.MinMaxRect(-0.25f, -0.4f, 0.25f, 0.4f),
            [PropKind.CrashCart] = Rect.MinMaxRect(-0.3f, -0.25f, 0.3f, 0.24f),
            [PropKind.CracUnit] = Rect.MinMaxRect(-0.5f, -0.4f, 0.5f, 0.4f),
            [PropKind.TapeLibrary] = Rect.MinMaxRect(-0.6f, -0.4f, 0.6f, 0.41f),
            [PropKind.MetalShelving] = Rect.MinMaxRect(-0.9f, -0.25f, 0.9f, 0.25f),
            [PropKind.DrumPair] = Rect.MinMaxRect(-0.45f, -0.3f, 0.45f, 0.3f),
            [PropKind.HandTruck] = Rect.MinMaxRect(-0.24f, -0.26f, 0.24f, 0.25f),
            [PropKind.LowShelf] = Rect.MinMaxRect(-0.45f, -0.23f, 0.45f, 0.23f),
            [PropKind.CrateStack] = Rect.MinMaxRect(-0.6f, -0.39f, 0.6f, 0.39f),
            [PropKind.RollingCage] = Rect.MinMaxRect(-0.44f, -0.34f, 0.44f, 0.34f),
            [PropKind.Plotter] = Rect.MinMaxRect(-0.75f, -0.3f, 0.75f, 0.3f),
            [PropKind.WorkTable] = Rect.MinMaxRect(-0.8f, -0.4f, 0.8f, 0.4f),
            [PropKind.SupplyShelf] = Rect.MinMaxRect(-0.5f, -0.2f, 0.5f, 0.2f),
            [PropKind.MailCart] = Rect.MinMaxRect(-0.43f, -0.24f, 0.47f, 0.24f),
            [PropKind.PaperBin] = Rect.MinMaxRect(-0.3f, -0.2f, 0.3f, 0.2f),
            [PropKind.MapBoard] = Rect.MinMaxRect(-1f, -0.05f, 1f, 0.04f),
            [PropKind.RadioRack] = Rect.MinMaxRect(-0.3f, -0.3f, 0.3f, 0.3f),
            [PropKind.OperatorChair] = Rect.MinMaxRect(-0.34f, -0.35f, 0.34f, 0.32f),
            [PropKind.SupervisorDesk] = Rect.MinMaxRect(-0.9f, -0.4f, 0.9f, 0.4f),
            [PropKind.AlarmPanel] = Rect.MinMaxRect(-0.4f, -0.06f, 0.4f, 0.08f),
            [PropKind.DisplayCase] = Rect.MinMaxRect(-0.5f, -0.25f, 0.5f, 0.25f),
            [PropKind.PlanterLarge] = Rect.MinMaxRect(-0.49f, -0.47f, 0.5f, 0.46f),
            [PropKind.HallBench] = Rect.MinMaxRect(-0.9f, -0.24f, 0.9f, 0.24f),
            [PropKind.Sculpture] = Rect.MinMaxRect(-0.3f, -0.3f, 0.3f, 0.3f),
            [PropKind.DirectoryBoard] = Rect.MinMaxRect(-0.4f, -0.15f, 0.4f, 0.15f),
            [PropKind.RowingMachine] = Rect.MinMaxRect(-0.3f, -1.03f, 0.3f, 1.03f),
            [PropKind.PowerCage] = Rect.MinMaxRect(-0.705f, -0.7f, 0.705f, 0.7f),
            [PropKind.KettlebellRack] = Rect.MinMaxRect(-0.505f, -0.2f, 0.505f, 0.2f),
            [PropKind.MatRack] = Rect.MinMaxRect(-0.4f, -0.25f, 0.4f, 0.25f),
            [PropKind.CableMachine] = Rect.MinMaxRect(-0.6f, -0.5f, 0.6f, 0.5f),
            [PropKind.TowelShelf] = Rect.MinMaxRect(-0.45f, -0.2f, 0.45f, 0.2f),
            [PropKind.LaundryCart] = Rect.MinMaxRect(-0.4f, -0.3f, 0.4f, 0.3f),
            [PropKind.BenchIsland] = Rect.MinMaxRect(-0.9f, -0.4f, 0.9f, 0.4f),
            [PropKind.WeighingScale] = Rect.MinMaxRect(-0.2f, -0.21f, 0.2f, 0.2f),
            [PropKind.ShoeRack] = Rect.MinMaxRect(-0.5f, -0.18f, 0.5f, 0.18f),
            [PropKind.ExamTable] = Rect.MinMaxRect(-0.35f, -0.9f, 0.35f, 0.9f),
            [PropKind.Wheelchair] = Rect.MinMaxRect(-0.325f, -0.5f, 0.325f, 0.5f),
            [PropKind.IvStand] = Rect.MinMaxRect(-0.235f, -0.25f, 0.235f, 0.25f),
            [PropKind.DoctorDesk] = Rect.MinMaxRect(-0.7f, -0.35f, 0.7f, 0.35f),
            [PropKind.DefibCabinet] = Rect.MinMaxRect(-0.2f, -0.08f, 0.2f, 0.096f),
            [PropKind.BunkBed] = Rect.MinMaxRect(-0.5f, -1f, 0.5f, 1f),
            [PropKind.Daybed] = Rect.MinMaxRect(-1f, -0.45f, 1f, 0.44f),
            [PropKind.CurtainDivider] = Rect.MinMaxRect(-0.795f, -0.1f, 0.795f, 0.1f),
            [PropKind.FloorCushions] = Rect.MinMaxRect(-0.575f, -0.58f, 0.575f, 0.6f),
            [PropKind.NightTable] = Rect.MinMaxRect(-0.25f, -0.22f, 0.25f, 0.22f),
            [PropKind.CardCatalog] = Rect.MinMaxRect(-0.4f, -0.23f, 0.4f, 0.23f),
            [PropKind.StudyCarrel] = Rect.MinMaxRect(-0.5f, -0.35f, 0.5f, 0.35f),
            [PropKind.Lectern] = Rect.MinMaxRect(-0.25f, -0.25f, 0.25f, 0.25f),
            [PropKind.WingbackChair] = Rect.MinMaxRect(-0.395f, -0.4f, 0.395f, 0.4f),
            [PropKind.BookCart] = Rect.MinMaxRect(-0.385f, -0.2f, 0.385f, 0.2f),
            [PropKind.FumeHood] = Rect.MinMaxRect(-0.75f, -0.4f, 0.75f, 0.41f),
            [PropKind.Printer3d] = Rect.MinMaxRect(-0.36f, -0.36f, 0.36f, 0.35f),
            [PropKind.ComponentShelf] = Rect.MinMaxRect(-0.6f, -0.22f, 0.6f, 0.22f),
            [PropKind.RobotTable] = Rect.MinMaxRect(-0.9f, -0.45f, 0.9f, 0.45f),
            [PropKind.ScopeCart] = Rect.MinMaxRect(-0.3f, -0.25f, 0.3f, 0.25f),
            [PropKind.GuitarStand] = Rect.MinMaxRect(-0.22f, -0.19f, 0.22f, 0.18f),
            [PropKind.SynthStand] = Rect.MinMaxRect(-0.65f, -0.25f, 0.65f, 0.25f),
            [PropKind.VocalBooth] = Rect.MinMaxRect(-0.6f, -0.6f, 0.6f, 0.63f),
            [PropKind.AmpCabinet] = Rect.MinMaxRect(-0.355f, -0.2f, 0.355f, 0.2f),
            [PropKind.StudioCouch] = Rect.MinMaxRect(-1f, -0.45f, 1f, 0.45f),
            [PropKind.AirHockey] = Rect.MinMaxRect(-1f, -0.55f, 1f, 0.55f),
            [PropKind.PokerTable] = Rect.MinMaxRect(-0.65f, -0.65f, 0.65f, 0.65f),
            [PropKind.Jukebox] = Rect.MinMaxRect(-0.4f, -0.3f, 0.4f, 0.3f),
            [PropKind.Pinball] = Rect.MinMaxRect(-0.375f, -0.7f, 0.375f, 0.73f),
            [PropKind.GamingCorner] = Rect.MinMaxRect(-0.8f, -0.3f, 0.8f, 0.3f),
            [PropKind.DepositWall] = Rect.MinMaxRect(-1.005f, -0.175f, 1.005f, 0.177f),
            [PropKind.GoldPallet] = Rect.MinMaxRect(-0.5f, -0.4f, 0.5f, 0.4f),
            [PropKind.MoneyCart] = Rect.MinMaxRect(-0.456f, -0.282f, 0.44f, 0.282f),
            [PropKind.CashShelf] = Rect.MinMaxRect(-0.8f, -0.25f, 0.8f, 0.25f),
            // the auto repair shop (Tools/Blender/garage.py)
            [PropKind.CarParked] = Rect.MinMaxRect(-0.95f, -2.27f, 0.95f, 2.27f),
            [PropKind.CarHoodOpen] = Rect.MinMaxRect(-0.95f, -2.27f, 0.95f, 2.27f),
            [PropKind.LiftCar] = Rect.MinMaxRect(-1.7f, -2.27f, 1.7f, 2.27f),
            [PropKind.LiftEmpty] = Rect.MinMaxRect(-1.7f, -1.25f, 1.7f, 1.25f),
            [PropKind.CarOnStands] = Rect.MinMaxRect(-1.25f, -2.27f, 1.6f, 2.27f), // (Blender x is mirrored: the loose wheels lie on +x)
            [PropKind.ToolChest] = Rect.MinMaxRect(-0.57f, -0.25f, 0.57f, 0.25f),
            [PropKind.TireChanger] = Rect.MinMaxRect(-0.7f, -0.52f, 0.42f, 0.61f),
            [PropKind.WheelBalancer] = Rect.MinMaxRect(-0.53f, -0.42f, 0.6f, 0.42f),
            [PropKind.TireRack] = Rect.MinMaxRect(-1.0f, -0.28f, 1.0f, 0.28f),
            [PropKind.TireStack] = Rect.MinMaxRect(-0.38f, -0.38f, 0.38f, 0.38f),
            [PropKind.EngineStand] = Rect.MinMaxRect(-0.34f, -0.46f, 0.44f, 0.46f),
            [PropKind.EngineHoist] = Rect.MinMaxRect(-0.45f, -0.66f, 0.45f, 1.1f),
            [PropKind.AirCompressor] = Rect.MinMaxRect(-0.61f, -0.26f, 0.61f, 0.26f),
            [PropKind.OilDrain] = Rect.MinMaxRect(-0.3f, -0.3f, 0.3f, 0.3f),
            [PropKind.HoseReel] = Rect.MinMaxRect(-0.19f, -0.1f, 0.19f, 0.27f),
            [PropKind.FloorJack] = Rect.MinMaxRect(-0.16f, -0.87f, 0.16f, 0.59f),
            [PropKind.OilDrums] = Rect.MinMaxRect(-0.6f, -0.55f, 0.6f, 0.55f),
            [PropKind.WeldingCart] = Rect.MinMaxRect(-0.28f, -0.61f, 0.28f, 0.35f),
            [PropKind.BayLines] = Rect.MinMaxRect(-1.7f, -2.9f, 1.7f, 2.9f), // (paint: a hand's breadth inside the bay's ends)
            [PropKind.GarageDoor] = Rect.MinMaxRect(-1.7f, 0f, 1.7f, 0.1f),
            [PropKind.PaintRack] = Rect.MinMaxRect(-0.8f, -0.25f, 0.66f, 0.25f),
            // the hospital (Tools/Blender/hospital.py; Blender x mirrored)
            [PropKind.BedHeadwall] = Rect.MinMaxRect(-0.73f, -0.21f, 0.73f, 0.21f),
            [PropKind.BedCurtain] = Rect.MinMaxRect(-0.65f, -1.18f, 0.42f, 1.2f),
            [PropKind.NurseStationDesk] = Rect.MinMaxRect(-1.53f, -0.54f, 1.53f, 0.54f),
            [PropKind.Gurney] = Rect.MinMaxRect(-0.43f, -1.03f, 0.43f, 1.03f),
            [PropKind.MedicationCart] = Rect.MinMaxRect(-0.48f, -0.29f, 0.48f, 0.29f),
            [PropKind.LinenCart] = Rect.MinMaxRect(-0.34f, -0.32f, 0.34f, 0.32f),
            [PropKind.SurgicalTable] = Rect.MinMaxRect(-0.89f, -1.0f, 0.89f, 1.0f),
            [PropKind.SurgicalLights] = Rect.MinMaxRect(-1.03f, -0.58f, 1.01f, 0.64f),
            [PropKind.AnesthesiaMachine] = Rect.MinMaxRect(-0.54f, -0.49f, 0.54f, 0.49f),
            [PropKind.InstrumentTable] = Rect.MinMaxRect(-0.94f, -0.32f, 0.94f, 0.32f),
            [PropKind.ScrubSink] = Rect.MinMaxRect(-0.76f, -0.28f, 0.76f, 0.28f),
            [PropKind.XrayMachine] = Rect.MinMaxRect(-0.71f, -1.52f, 0.71f, 1.52f),
            [PropKind.CtScanner] = Rect.MinMaxRect(-0.95f, -1.4f, 0.95f, 1.4f),
            [PropKind.WaitingSeats] = Rect.MinMaxRect(-1.23f, -0.31f, 1.23f, 0.31f),
            [PropKind.VitalsMonitor] = Rect.MinMaxRect(-0.3f, -0.3f, 0.3f, 0.3f),
            [PropKind.HandSanitizer] = Rect.MinMaxRect(-0.06f, -0.06f, 0.06f, 0.06f),
            // the apartment block (Tools/Blender/apartments.py)
            [PropKind.DoubleBed] = Rect.MinMaxRect(-0.8f, -1.025f, 0.8f, 1.025f),
            [PropKind.Wardrobe] = Rect.MinMaxRect(-0.6f, -0.3f, 0.6f, 0.31f),
            [PropKind.Dresser] = Rect.MinMaxRect(-0.5f, -0.25f, 0.5f, 0.27f),
            [PropKind.Bathtub] = Rect.MinMaxRect(-0.85f, -0.375f, 0.85f, 0.375f),
            [PropKind.HomeToilet] = Rect.MinMaxRect(-0.19f, -0.34f, 0.19f, 0.34f),
            [PropKind.BathVanity] = Rect.MinMaxRect(-0.3f, -0.237f, 0.3f, 0.23f),
            [PropKind.WashingMachine] = Rect.MinMaxRect(-0.3f, -0.3f, 0.3f, 0.31f),
            [PropKind.ShoeCabinet] = Rect.MinMaxRect(-0.451f, -0.162f, 0.451f, 0.183f),
            [PropKind.HallHooks] = Rect.MinMaxRect(-0.65f, -0.175f, 0.65f, 0.125f), // (the coats on its end hooks hang out past the rail)
            [PropKind.Mailboxes] = Rect.MinMaxRect(-0.6f, -0.125f, 0.6f, 0.129f),
            [PropKind.ElevatorDoors] = Rect.MinMaxRect(-0.65f, -0.06f, 0.65f, 0.077f),
        };

        /// <summary>Round-2 pieces hung on a wall (a clock, a board, a cabinet): no footprint on the floor to walk into.</summary>
        static readonly HashSet<PropKind> Hung = new() { PropKind.WallClock, PropKind.ProjectorScreen, PropKind.ChangingTable, PropKind.TowelDispenser, PropKind.FireCabinet, PropKind.NoticeBoard, PropKind.MapBoard, PropKind.AlarmPanel, PropKind.DefibCabinet,
            PropKind.HoseReel, PropKind.BedHeadwall, PropKind.BedCurtain, PropKind.SurgicalLights, PropKind.HandSanitizer,
            PropKind.HallHooks, PropKind.Mailboxes };

        /// <summary>
        /// Big pieces that stand higher than a window's sill by more than a monitor does (shelving, lockers, fridges,
        /// boards on legs, stalls): never in front of a window. Slim things (a floor lamp, a coat rack) and whatever stays
        /// under ~1.6 m (desks, sofas, counters, a water cooler) may stand there. Checked against the models by a test.
        /// </summary>
        public static readonly HashSet<PropKind> Tall = new()
        {
            PropKind.ServerRack, PropKind.Shelf, PropKind.Whiteboard, PropKind.ToiletStall, PropKind.Bookshelf, PropKind.Fridge, PropKind.Lockers,
            PropKind.VendingMachine, PropKind.ArcadeMachine, PropKind.Cage, PropKind.Urinal, PropKind.MirrorSink, PropKind.ShowerStall,
            PropKind.MedicineCabinet, PropKind.TallBookcase, PropKind.MailShelf, PropKind.MonitorWall, PropKind.MobileWhiteboard,
            PropKind.Flipchart, PropKind.LogoTotem, PropKind.RoundBenchPlanter, PropKind.PantryCabinet, PropKind.DirectoryBoard,
            PropKind.DisplayCase, PropKind.PlanterLarge, PropKind.CracUnit, PropKind.TapeLibrary, PropKind.MetalShelving, PropKind.RadioRack,
            PropKind.SupplyShelf, PropKind.PowerCage, PropKind.CableMachine, PropKind.FumeHood, PropKind.ComponentShelf, PropKind.VocalBooth,
            PropKind.DepositWall, PropKind.CashShelf, PropKind.Programmer, PropKind.WashBasin, PropKind.DoubleSink, PropKind.BunkBed,
            PropKind.Pinball, PropKind.MedTable, PropKind.Workbench, PropKind.StorageRack,
            PropKind.ChargingDock, PropKind.BatteryStack, PropKind.Generator, PropKind.ScrapShrine,
            PropKind.LiftCar, PropKind.LiftEmpty, PropKind.TireChanger, PropKind.ToolChest, PropKind.TireRack, PropKind.EngineHoist, PropKind.PaintRack, PropKind.GarageDoor,
            PropKind.BedCurtain, PropKind.Gurney, PropKind.AnesthesiaMachine, PropKind.XrayMachine, PropKind.CtScanner,
            PropKind.Wardrobe, PropKind.ElevatorDoors, PropKind.HallHooks, PropKind.BathVanity,
        };

        /// <summary>Pieces that hang on the wall itself: never on a window's glass or its frame.</summary>
        public static readonly HashSet<PropKind> OnWall = new()
        {
            PropKind.WallCabinet, PropKind.WallClock, PropKind.HandDryer, PropKind.TowelDispenser, PropKind.ChangingTable, PropKind.FireCabinet,
            PropKind.NoticeBoard, PropKind.MapBoard, PropKind.AlarmPanel, PropKind.DefibCabinet, PropKind.ProjectorScreen, PropKind.AcousticPanel,
            PropKind.DartBoard, PropKind.HoseReel, PropKind.BedHeadwall, PropKind.SurgicalLights, PropKind.HandSanitizer,
            PropKind.HallHooks, PropKind.Mailboxes,
            PropKind.DartBoard, PropKind.HoseReel, PropKind.ConduitGrowth,
        };

        /// <summary>
        /// How many of a kind one room takes (twice that in a hall of a room, over 70 m²): one water cooler, one clock, one
        /// safe… — three coolers along one wall read as a mistake, not as an office.
        /// </summary>
        public static int CapOf(PropKind k) => k switch
        {
            PropKind.DoubleBed or PropKind.Wardrobe or PropKind.Dresser or PropKind.Bathtub or PropKind.HomeToilet or PropKind.BathVanity or
                PropKind.WashingMachine or PropKind.ShoeCabinet or PropKind.HallHooks or PropKind.Mailboxes or PropKind.ElevatorDoors => 1,
            PropKind.WaterCooler or PropKind.WallClock or PropKind.Safe or PropKind.Printer or PropKind.JanitorCart or PropKind.Jukebox or
                PropKind.DefibCabinet or PropKind.CoatRack or PropKind.Globe or PropKind.Lectern or PropKind.WeighingScale or PropKind.ChangingTable or
                PropKind.Shredder or PropKind.Sculpture or PropKind.UmbrellaStand or PropKind.Wheelchair or PropKind.VendingMachine or
                PropKind.HandDryer or PropKind.TowelDispenser or PropKind.Stove or PropKind.Dishwasher or PropKind.MonitorWall or PropKind.ProjectorScreen or
                PropKind.TvStand or PropKind.Flipchart or PropKind.MapBoard or PropKind.AlarmPanel or PropKind.DirectoryBoard or PropKind.LogoTotem or
                PropKind.MagazineRack or PropKind.DartBoard or PropKind.Fridge or PropKind.CoffeeMachine or PropKind.Copier or PropKind.Plotter or
                PropKind.RecyclingBins or PropKind.PantryCabinet or PropKind.MedicineCabinet or PropKind.CardCatalog or PropKind.BookCart or
                PropKind.MailCart or PropKind.LaundryCart or PropKind.HandTruck or PropKind.CrashCart or PropKind.ScopeCart or PropKind.Printer3d or
                PropKind.FumeHood or PropKind.VocalBooth or PropKind.DrumKit or PropKind.PowerCage or PropKind.CableMachine or PropKind.MatRack or
                PropKind.GamingCorner or PropKind.BarCounter or PropKind.KitchenSink or PropKind.MobileWhiteboard or PropKind.SofaPod or
                PropKind.DisplayCase or PropKind.CracUnit or PropKind.TapeLibrary or PropKind.TireChanger or PropKind.WheelBalancer or
                PropKind.AirCompressor or PropKind.WeldingCart or PropKind.EngineHoist or PropKind.CtScanner or PropKind.XrayMachine or
                PropKind.SurgicalTable or PropKind.AnesthesiaMachine or PropKind.NurseStationDesk or PropKind.ScrubSink => 1,
            // (drawers: a wall of them in one room was a chore to open, and most were empty)
            PropKind.Cabinet or PropKind.FilingCabinet or PropKind.FilePedestal or
            PropKind.TrashBin or PropKind.SanitaryBin or PropKind.FireCabinet or PropKind.FloorLamp or PropKind.IvStand or PropKind.PunchingBag or
                PropKind.RobotArm or PropKind.MicStand or PropKind.InfoKiosk or PropKind.Whiteboard or PropKind.NoticeBoard or PropKind.BinPair or
                PropKind.PaperBin or PropKind.PaperStack or PropKind.GuitarStand or PropKind.AmpCabinet or PropKind.NightTable or PropKind.Plant or
                PropKind.PlanterLarge or PropKind.BeanBag or PropKind.StandingDesk or PropKind.PlanterDivider or PropKind.PaintRack or
                PropKind.HoseReel or PropKind.EngineStand or PropKind.OilDrums or PropKind.HandSanitizer or PropKind.Gurney or PropKind.LinenCart => 2,
            _ => int.MaxValue,
        };

        /// <summary>A model's footprint in its own frame (x = local X, y = local Z), metres.</summary>
        public static Rect FootprintOf(PropKind kind) => Foot.TryGetValue(kind, out var r) ? r : new Rect(-0.5f, -0.5f, 1f, 1f);

        const float DeskTop = LevelMetrics.DeskTop;
        const float CabinetTop = 1.2f; // the drawer cabinet's top (Tools/Blender/generate_models.py model_cabinet)
        const float WallGap = 0.06f;    // between a back and the wall's face
        const float HungGap = 0.005f;   // between the back of a thing hung on the wall (a board, a cabinet) and the wall's face
        const float Aisle = 1.25f;      // free walkway around an island and in front of a wall group (robots are 0.8 m wide)
        const float Facing = 0.3f;      // more than that between two pieces facing each other across a walkway
        static readonly float[] ShelfLevels = { 0.47f, 1.07f, 1.67f };
        /// <summary>The tall bookcase's shelves within reach (Tools/Blender/rooms_paper.py).</summary>
        static readonly float[] BookcaseLevels = { 0.56f, 0.97f, 1.38f };

        readonly LevelLayout m_L;
        readonly HousePlan m_P;
        readonly System.Random m_Rng;
        readonly RectGrid[] m_Taken;   // per floor: placed footprints
        readonly RectGrid[] m_Keep;    // per floor: must stay clear (doorways, stairs, the hatch)
        readonly RectGrid[] m_Solid;   // per floor: the footprints a walker has to go around (no rugs, nothing hung or standing on a top)
        RectGrid[] m_Windows;          // per floor: the strip of floor in front of every window (built with the first room)
        readonly Dictionary<PropKind, int> m_InRoom = new(); // pieces of the room being furnished, by kind (see CapOf)
        int m_Cap = 1;                 // CapOf times this in the room being furnished

        /// <summary>
        /// Rectangles of a floor in 2 m buckets: "does anything overlap this?" looks only at the buckets the question touches
        /// (a big house has hundreds of pieces per floor; scanning them all for every try was most of the furnishing time).
        /// The answers are exactly those of a full scan.
        /// </summary>
        sealed class RectGrid
        {
            const float Cell = 2f;
            readonly Dictionary<long, List<int>> m_Buckets = new();
            readonly List<Rect> m_Rects = new();
            int m_Stamp;
            readonly List<int> m_Mark = new();

            static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
            static int C(float v) => Mathf.FloorToInt(v / Cell);

            public void Add(Rect r)
            {
                int i = m_Rects.Count;
                m_Rects.Add(r);
                m_Mark.Add(0);
                for (int x = C(r.xMin); x <= C(r.xMax); x++)
                    for (int z = C(r.yMin); z <= C(r.yMax); z++)
                    {
                        if (!m_Buckets.TryGetValue(Key(x, z), out var list)) m_Buckets[Key(x, z)] = list = new List<int>();
                        list.Add(i);
                    }
            }

            public void Clear()
            {
                m_Buckets.Clear();
                m_Rects.Clear();
                m_Mark.Clear();
            }

            public bool Overlaps(Rect q)
            {
                m_Stamp++;
                for (int x = C(q.xMin); x <= C(q.xMax); x++)
                    for (int z = C(q.yMin); z <= C(q.yMax); z++)
                    {
                        if (!m_Buckets.TryGetValue(Key(x, z), out var list)) continue;
                        foreach (int i in list)
                        {
                            if (m_Mark[i] == m_Stamp) continue;
                            m_Mark[i] = m_Stamp;
                            if (m_Rects[i].Overlaps(q)) return true;
                        }
                    }
                return false;
            }

            /// <summary>Every rectangle that overlaps <paramref name="q"/>, appended to <paramref name="into"/>.</summary>
            public void Query(Rect q, List<Rect> into)
            {
                m_Stamp++;
                for (int x = C(q.xMin); x <= C(q.xMax); x++)
                    for (int z = C(q.yMin); z <= C(q.yMax); z++)
                    {
                        if (!m_Buckets.TryGetValue(Key(x, z), out var list)) continue;
                        foreach (int i in list)
                        {
                            if (m_Mark[i] == m_Stamp) continue;
                            m_Mark[i] = m_Stamp;
                            if (m_Rects[i].Overlaps(q)) into.Add(m_Rects[i]);
                        }
                    }
            }

            public bool Contains(Vector2 p)
            {
                if (!m_Buckets.TryGetValue(Key(C(p.x), C(p.y)), out var list)) return false;
                foreach (int i in list) if (m_Rects[i].Contains(p)) return true;
                return false;
            }
        }

        public Furnisher(LevelLayout l, System.Random rng)
        {
            m_L = l;
            m_P = l.Plan;
            m_Rng = rng;
            m_Taken = new RectGrid[m_P.Floors];
            m_Keep = new RectGrid[m_P.Floors];
            m_Solid = new RectGrid[m_P.Floors];
            for (int f = 0; f < m_P.Floors; f++) { m_Taken[f] = new RectGrid(); m_Keep[f] = new RectGrid(); m_Solid[f] = new RectGrid(); }
            InitOpening(m_P.Floors);
            foreach (var d in l.Doorways) KeepDoorway(d);
            foreach (var s in l.Stairs)
            {
                var a = PlanGenerator.StairArea(s, 0f);
                var grown = new Rect(a.x, a.y, a.width, a.height);
                // the approach to the first step and the landing above (towards +u, the room side of the frame)
                var bottom = LevelLayout.StairEnds(s).bottom;
                grown = Union(grown, new Rect(bottom.x - 1.1f, bottom.z - 1.1f, 2.2f, 2.2f));
                m_Keep[s.Floor].Add(Grow(grown, 0.25f)); // a clock on the far side of the hall's wall is still "at the stair"
                if (s.Floor + 1 < m_P.Floors)
                {
                    var top = LevelLayout.StairEnds(s).top;
                    m_Keep[s.Floor + 1].Add(Grow(Union(a, new Rect(top.x - 1.1f, top.z - 1.1f, 2.2f, 2.2f)), 0.25f));
                }
            }
        }

        static Rect Union(Rect a, Rect b) => Rect.MinMaxRect(Mathf.Min(a.xMin, b.xMin), Mathf.Min(a.yMin, b.yMin), Mathf.Max(a.xMax, b.xMax), Mathf.Max(a.yMax, b.yMax));
        static Rect Grow(Rect r, float k) => new(r.x - k, r.y - k, r.width + 2 * k, r.height + 2 * k);

        void Keep(int floor, Vector3 a, Vector3 b) =>
            m_Keep[floor].Add(Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z)));

        /// <summary>The doorway and a step either side of it stay free (an open passage: the whole wall).</summary>
        void KeepDoorway(Doorway d)
        {
            var along = d.Rotation * Vector3.right;
            var across = d.Rotation * Vector3.forward;
            float half = d.Width / 2f + 0.35f, depth = d.Kind == EdgeType.Open ? 1.0f : 1.35f;
            Keep(d.Floor, d.Position - along * half - across * depth, d.Position + along * half + across * depth);
        }

        /// <summary>
        /// A doorway was widened or slid along its wall after this furnisher was made (the vault's door): its new place stays
        /// free too, and the walk grid of the bare building (which knew the old one) is made again on the next check.
        /// </summary>
        public void DoorwayMoved(Doorway d)
        {
            KeepDoorway(d);
            m_WalkFree = null;
            m_WalkOwner = null;
        }

        /// <summary>Reserves a spot (the hatch, player spawns) before the furniture comes.</summary>
        public void Reserve(int floor, Vector3 centre, float half) => Keep(floor, centre - new Vector3(half, 0, half), centre + new Vector3(half, 0, half));
    }
}
