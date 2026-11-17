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
    public sealed class Furnisher
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
            [PropKind.CoatRack] = Rect.MinMaxRect(-0.29f, -0.26f, 0.26f, 0.26f),
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
            [PropKind.Crate] = Rect.MinMaxRect(-0.4f, -0.3f, 0.4f, 0.3f),
            [PropKind.MedTable] = Rect.MinMaxRect(-0.97f, -0.53f, 0.97f, 0.53f),
            [PropKind.Workbench] = Rect.MinMaxRect(-1.0f, -0.43f, 1.0f, 0.55f),
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
            [PropKind.BayLines] = Rect.MinMaxRect(-1.7f, -3f, 1.7f, 3f),
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
            [PropKind.HallHooks] = Rect.MinMaxRect(-0.5f, -0.175f, 0.5f, 0.125f),
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
            foreach (var d in l.Doorways)
            {
                // the doorway and a step either side of it stay free (an open passage: the whole wall)
                var along = d.Rotation * Vector3.right;
                var across = d.Rotation * Vector3.forward;
                float half = d.Width / 2f + 0.35f, depth = d.Kind == EdgeType.Open ? 1.0f : 1.35f;
                Keep(d.Floor, d.Position - along * half - across * depth, d.Position + along * half + across * depth);
            }
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

        /// <summary>Reserves a spot (the hatch, player spawns) before the furniture comes.</summary>
        public void Reserve(int floor, Vector3 centre, float half) => Keep(floor, centre - new Vector3(half, 0, half), centre + new Vector3(half, 0, half));

        // ------------------------------------------------------------------ placing

        /// <summary>A piece of a group in world space, not yet committed.</summary>
        struct Piece { public PropKind Kind; public Vector3 Pos; public float Yaw; public bool Solid; }

        /// <summary>A group being laid out: pieces and loot spots, committed only if everything fits.</summary>
        sealed class Batch
        {
            public readonly List<Piece> Pieces = new();
            public readonly List<Vector3> Items = new();
            /// <summary>Where workers leave loot (the storage room's shelves).</summary>
            public readonly List<StorageDrop> Drops = new();
            public void Add(PropKind k, Vector3 p, float yaw, bool solid = true) => Pieces.Add(new Piece { Kind = k, Pos = p, Yaw = yaw, Solid = solid });
            public void Item(Vector3 p) => Items.Add(p);
        }

        /// <summary>Axes of a group: U along the wall (or across an island), N into the room; Yaw turns a model's front to N.</summary>
        readonly struct Frame
        {
            public readonly Vector3 O, U, N;
            public readonly float Yaw;
            public Frame(Vector3 o, Vector3 u, Vector3 n) { O = o; U = u; N = n; Yaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg; }
            public Vector3 P(float u, float n, float y = 0f) => O + U * u + N * n + Vector3.up * y;
            public float Face => Yaw;
            public float Back => Yaw + 180f;
            public float AlongU => Mathf.Atan2(U.x, U.z) * Mathf.Rad2Deg;
            public float AgainstU => AlongU + 180f;
        }

        /// <summary>The footprint of a model at a pose, as a world rect (yaws are multiples of 90°, near enough).</summary>
        static Rect FootAt(PropKind k, Vector3 p, float yaw)
        {
            var r = Foot.TryGetValue(k, out var f) ? f : Rect.MinMaxRect(-0.3f, -0.3f, 0.3f, 0.3f);
            var q = Quaternion.Euler(0, yaw, 0);
            Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
            foreach (var c in new[] { new Vector3(r.xMin, 0, r.yMin), new Vector3(r.xMax, 0, r.yMin), new Vector3(r.xMin, 0, r.yMax), new Vector3(r.xMax, 0, r.yMax) })
            {
                var w = p + q * c;
                min = Vector2.Min(min, new Vector2(w.x, w.z));
                max = Vector2.Max(max, new Vector2(w.x, w.z));
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>The rect lies on the floor of <paramref name="room"/> with a margin from its walls.</summary>
        bool OnFloor(int floor, int room, Rect r, float margin)
        {
            var g = Grow(r, margin);
            const float s = HousePlan.Res;
            for (float x = g.xMin + 0.01f; x < g.xMax; x += s * 0.5f)
                for (float z = g.yMin + 0.01f; z < g.yMax; z += s * 0.5f)
                    if (!InRoom(floor, room, x, z)) return false;
            return InRoom(floor, room, g.xMax - 0.01f, g.yMax - 0.01f) && InRoom(floor, room, g.xMax - 0.01f, g.yMin + 0.01f)
                && InRoom(floor, room, g.xMin + 0.01f, g.yMax - 0.01f);
        }

        bool InRoom(int floor, int room, float x, float z)
        {
            var c = HousePlan.CellOf(new Vector2(x, z));
            return m_P.KindAt(floor, c.x, c.y) == CellKind.Room && m_P.IdAt(floor, c.x, c.y) == room;
        }

        bool Clear(int floor, Rect r, float gap)
        {
            var g = Grow(r, gap);
            if (m_Taken[floor].Overlaps(g)) return false;
            if (m_Keep[floor].Overlaps(r)) return false;
            return true;
        }

        /// <summary>Commits a batch if every solid piece stands on the room's floor and clear of everything else.</summary>
        bool TryCommit(PlanRoom room, Batch b, float gap = 0.04f, float aisle = 0f, Vector3? front = null)
        {
            int f = room.Floor;
            if (m_Fridge && b.Pieces.Exists(p => p.Kind == PropKind.Fridge)) return false; // one fridge a kitchen
            var rects = new List<Rect>();
            // windows: a tall piece in front of one fails the group; a thing hung on the wall there is left out of it
            // (the kitchen run stays, its cupboards over the window do not) unless the group is only that
            if (m_Windows != null)
                for (int i = b.Pieces.Count - 1; i >= 0; i--)
                {
                    var p = b.Pieces[i];
                    bool tall = Tall.Contains(p.Kind), hangs = OnWall.Contains(p.Kind);
                    if ((!tall && !hangs) || !m_Windows[f].Overlaps(FootAt(p.Kind, p.Pos, p.Yaw))) continue;
                    if (tall || i == 0) return false;
                    b.Pieces.RemoveAt(i);
                }
            // one water cooler, one clock… per room
            foreach (var p in b.Pieces)
            {
                int cap = CapOf(p.Kind);
                if (cap == int.MaxValue) continue;
                int n = m_InRoom.TryGetValue(p.Kind, out int have) ? have : 0;
                foreach (var q in b.Pieces) if (q.Kind == p.Kind) n++;
                if (n > cap * m_Cap) return false;
            }
            foreach (var p in b.Pieces)
            {
                // a thing hung on the wall (cupboards, a dart board, foam): nothing to walk into, but never over a doorway
                if (!p.Solid)
                {
                    var hung = FootAt(p.Kind, p.Pos, p.Yaw);
                    if (m_Keep[f].Overlaps(Grow(hung, 0.06f)) || m_Taken[f].Overlaps(Grow(hung, -0.02f))) return false; // a thin thing: a margin round it
                    continue;
                }
                var r = FootAt(p.Kind, p.Pos, p.Yaw);
                if (!OnFloor(f, room.Id, r, 0.12f) || !Clear(f, r, gap)) return false;
                if (front is Vector3 n)
                {
                    // a walkway in front of a group along a wall: nothing placed there already, and still the room's floor
                    var ahead = Rect.MinMaxRect(Mathf.Min(r.xMin, r.xMin + n.x * Aisle), Mathf.Min(r.yMin, r.yMin + n.z * Aisle),
                                                Mathf.Max(r.xMax, r.xMax + n.x * Aisle), Mathf.Max(r.yMax, r.yMax + n.z * Aisle));
                    if (m_Taken[f].Overlaps(ahead)) return false;
                }
                rects.Add(r);
            }
            if (aisle > 0f && rects.Count > 0)
            {
                var all = rects[0];
                foreach (var r in rects) all = Union(all, r);
                // an aisle to the other furniture and most of one to the walls; the kept spots (a doorway's own depth,
                // the square a patrol stands on) only need a step: an aisle more around them kept every table out of
                // a room under 7 × 5 m
                if (m_Taken[f].Overlaps(Grow(all, aisle)) || m_Keep[f].Overlaps(Grow(all, 0.3f)) || !OnFloor(f, room.Id, all, aisle * 0.7f)) return false;
            }
            if (rects.Count > 0 && !StaysWalkable(room, rects)) return false;
            // pieces of one group may touch each other, but not other groups
            int group = m_Groups++;
            foreach (var p in b.Pieces)
            {
                m_L.Props.Add(new PropPlacement { Kind = p.Kind, Position = p.Pos, RotationY = p.Yaw });
                m_GroupOf.Add(group);
                m_InRoom[p.Kind] = (m_InRoom.TryGetValue(p.Kind, out int have) ? have : 0) + 1;
                m_Taken[f].Add(FootAt(p.Kind, p.Pos, p.Yaw)); // hung and standing-on things too: nothing else goes under or into them
                if (p.Solid && InTheWay(p.Kind)) m_Solid[f].Add(FootAt(p.Kind, p.Pos, p.Yaw));
            }
            foreach (var i in b.Items) { m_L.ItemSpots.Add(i); m_ItemGroup.Add(group); }
            if (room.Id == m_L.StorageRoom) m_L.StorageDrops.AddRange(b.Drops);
            foreach (var p in b.Pieces)
            {
                m_Fridge |= p.Kind == PropKind.Fridge;
                m_Table |= p.Kind is PropKind.RoundTable or PropKind.BarCounter;
            }
            return true;
        }

        int m_Groups;
        bool[] m_WalkFree;  // the walk grid of the bare building (see Walk), built on the first check
        int[] m_WalkOwner;
        readonly List<int> m_GroupOf = new();    // per prop of the layout: the group it came with (−1: not removable)
        readonly List<int> m_ItemGroup = new();  // per loot spot

        /// <summary>
        /// Under every high vent, on both sides: an ordinary desk against the wall (the players climb it to reach the duct —
        /// furniture that happens to stand there, not a staircase someone built). Never removed by the walkability check;
        /// the space in front of it stays free so nothing blocks the climb.
        /// </summary>
        public void PlaceVentDesks()
        {
            float face = LevelMetrics.WallThickness / 2f;
            float half = Foot[PropKind.Desk].height / 2f;
            foreach (var d in m_L.Doorways)
            {
                if (!d.High) continue;
                var across = d.Rotation * Vector3.forward;
                foreach (float side in new[] { -1f, 1f })
                {
                    var n = across * side;
                    var pos = d.Position + n * (face + WallGap + half);
                    var room = m_P.RoomAt(pos + Vector3.up * 0.1f);
                    if (room == null) continue;
                    float yaw = Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg; // its front (+Z) to the room, its back to the wall
                    Put(room, PropKind.Desk, pos, yaw);
                    Keep(d.Floor, pos + n * half - d.Rotation * Vector3.right * 0.9f, pos + n * (half + LevelMetrics.VentDeskAisle) + d.Rotation * Vector3.right * 0.9f);
                }
            }
        }

        public void Put(PlanRoom room, PropKind k, Vector3 p, float yaw)
        {
            m_L.Props.Add(new PropPlacement { Kind = k, Position = p, RotationY = yaw });
            m_GroupOf.Add(-1);
            m_Taken[room.Floor].Add(FootAt(k, p, yaw));
            if (InTheWay(k)) m_Solid[room.Floor].Add(FootAt(k, p, yaw));
        }

        // ------------------------------------------------------------------ walkability

        /// <summary>
        /// The middle of this room is for its furniture (a meeting room's table, a restroom's stalls): the patrol stands
        /// by the door instead (<see cref="LevelGenerator.PatrolSpot"/>) and "reached" means any free spot near the middle.
        /// </summary>
        public static bool MiddleIsFurniture(PlanRoom r) => r.Type is RoomType.Meeting or RoomType.Restroom or RoomType.PaintBooth
            or RoomType.NurseStation or RoomType.Operating or RoomType.Imaging // (the booth: its car; the hospital's: its counter, table, scanner)
            or RoomType.Bedroom or RoomType.Bathroom; // (a flat's: its bed, its bath)

        /// <summary>A walker has to go around this piece (it is not on a top, up on a wall or flat on the floor).</summary>
        static bool InTheWay(PropKind k) =>
            k is not (PropKind.Partition or PropKind.DeskLamp or PropKind.CoffeeMachine or PropKind.Hatch or PropKind.Microwave
                or PropKind.WallCabinet or PropKind.HandDryer or PropKind.AcousticPanel or PropKind.DartBoard or PropKind.AreaRug
                or PropKind.BayLines or PropKind.BedCurtain or PropKind.SurgicalLights) && !Hung.Contains(k);

        // The room being furnished, as the walkability check will see it (same grid, same margins): its free cells
        // without furniture, and the spots a walker must still get between: its middle, the inner side of each of its
        // doorways and open passages, the ends of its stairs.
        PlanRoom m_WalkRoom;
        int m_WX, m_WZ, m_WNX, m_WNZ;
        bool[] m_WBare, m_WFree, m_WSeen;
        readonly List<int> m_WTargets = new();
        readonly List<int> m_WStack = new();
        readonly List<Rect> m_WRects = new();
        const float WalkCell = 0.25f, WalkAgent = 0.4f, WalkClutter = 0.55f; // those of Walk

        void BeginRoomWalk(PlanRoom room)
        {
            m_WalkRoom = room;
            var b = room.Bounds;
            m_WX = Mathf.FloorToInt(b.xMin / WalkCell);
            m_WZ = Mathf.FloorToInt(b.yMin / WalkCell);
            m_WNX = Mathf.CeilToInt(b.xMax / WalkCell) - m_WX;
            m_WNZ = Mathf.CeilToInt(b.yMax / WalkCell) - m_WZ;
            int n = m_WNX * m_WNZ;
            if (m_WBare == null || m_WBare.Length < n) { m_WBare = new bool[n]; m_WFree = new bool[n]; m_WSeen = new bool[n]; }
            float reach = LevelMetrics.WallThickness / 2f + WalkAgent - 0.01f;
            for (int z = 0; z < m_WNZ; z++)
                for (int x = 0; x < m_WNX; x++)
                {
                    float cx = (m_WX + x + 0.5f) * WalkCell, cz = (m_WZ + z + 0.5f) * WalkCell;
                    // this room's floor, a walker's half width off everything that is not (walls; other rooms count too:
                    // the way through a doorway is checked by the whole-house pass, here only the way to it)
                    m_WBare[z * m_WNX + x] = InRoom(room.Floor, room.Id, cx, cz)
                        && InRoom(room.Floor, room.Id, cx - reach, cz) && InRoom(room.Floor, room.Id, cx + reach, cz)
                        && InRoom(room.Floor, room.Id, cx, cz - reach) && InRoom(room.Floor, room.Id, cx, cz + reach);
                }
            // a staircase in the room is solid, as in the whole-house pass (Walk): else a piece beside it could close the way
            // round it and only the final check would see it (taking half the house's furniture with it)
            foreach (var st in m_L.Stairs)
            {
                if (st.Floor != room.Floor && st.Floor + 1 != room.Floor) continue;
                var sa = PlanGenerator.StairArea(st, WalkAgent + 0.05f);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(sa.xMin / WalkCell) - m_WX), x1 = Mathf.Min(m_WNX - 1, Mathf.FloorToInt(sa.xMax / WalkCell) - m_WX);
                int z0 = Mathf.Max(0, Mathf.FloorToInt(sa.yMin / WalkCell) - m_WZ), z1 = Mathf.Min(m_WNZ - 1, Mathf.FloorToInt(sa.yMax / WalkCell) - m_WZ);
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                        m_WBare[z * m_WNX + x] = false;
            }
            m_WTargets.Clear();
            void Target(Vector3 p, int reach = 4)
            {
                // the free cell nearest to the spot, within 1 m (a stair's end: 1.5 m, as the whole-house pass links it)
                int best = -1;
                float bestD = reach * WalkCell;
                int px = Mathf.FloorToInt(p.x / WalkCell) - m_WX, pz = Mathf.FloorToInt(p.z / WalkCell) - m_WZ;
                for (int dz = -reach; dz <= reach; dz++)
                    for (int dx = -reach; dx <= reach; dx++)
                    {
                        int x = px + dx, z = pz + dz;
                        if (x < 0 || z < 0 || x >= m_WNX || z >= m_WNZ || !m_WBare[z * m_WNX + x]) continue;
                        float d = new Vector2((m_WX + x + 0.5f) * WalkCell - p.x, (m_WZ + z + 0.5f) * WalkCell - p.z).magnitude;
                        if (d < bestD) { bestD = d; best = z * m_WNX + x; }
                    }
                if (best >= 0 && !m_WTargets.Contains(best)) m_WTargets.Add(best);
            }
            if (!MiddleIsFurniture(room)) Target(room.Center);
            foreach (var d in m_L.Doorways)
            {
                if (d.Floor != room.Floor || d.Kind is not (EdgeType.Door or EdgeType.Open or EdgeType.Breach)) continue;
                var across = d.Rotation * Vector3.forward;
                foreach (float side in new[] { -0.75f, 0.75f })
                {
                    var p = d.Position + across * side;
                    if (InRoom(room.Floor, room.Id, p.x, p.z)) Target(p);
                }
            }
            foreach (var s in m_L.Stairs)
            {
                var (bottom, top) = LevelLayout.StairEnds(s);
                if (s.Floor == room.Floor && InRoom(room.Floor, room.Id, bottom.x, bottom.z)) Target(bottom, 6);
                if (s.Floor + 1 == room.Floor && InRoom(room.Floor, room.Id, top.x, top.z)) Target(top, 6);
            }
            // what the furniture already there (desks under vents, the hatch) leaves linked is the baseline
            Linked(null, m_WTargets);
        }

        /// <summary>
        /// Floods the room's free cells around its furniture (plus <paramref name="more"/>) from the first target; true
        /// when every target of <paramref name="keep"/> is reached. Without <paramref name="more"/> (the baseline) the
        /// targets it does not reach are dropped from the list.
        /// </summary>
        bool Linked(List<Rect> more, List<int> keep)
        {
            if (m_WTargets.Count < 2) return true;
            int n = m_WNX * m_WNZ;
            System.Array.Copy(m_WBare, m_WFree, n);
            System.Array.Clear(m_WSeen, 0, n);
            m_WRects.Clear();
            m_Solid[m_WalkRoom.Floor].Query(Grow(m_WalkRoom.Bounds, 1f), m_WRects);
            if (more != null) m_WRects.AddRange(more);
            foreach (var r in m_WRects)
            {
                var g = Grow(r, WalkClutter);
                int x0 = Mathf.Max(0, Mathf.FloorToInt(g.xMin / WalkCell) - m_WX), x1 = Mathf.Min(m_WNX - 1, Mathf.FloorToInt(g.xMax / WalkCell) - m_WX);
                int z0 = Mathf.Max(0, Mathf.FloorToInt(g.yMin / WalkCell) - m_WZ), z1 = Mathf.Min(m_WNZ - 1, Mathf.FloorToInt(g.yMax / WalkCell) - m_WZ);
                for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                        m_WFree[z * m_WNX + x] = false;
            }
            int start = m_WTargets[0];
            if (m_WFree[start])
            {
                m_WStack.Clear();
                m_WStack.Add(start);
                m_WSeen[start] = true;
                while (m_WStack.Count > 0)
                {
                    int i = m_WStack[m_WStack.Count - 1];
                    m_WStack.RemoveAt(m_WStack.Count - 1);
                    int x = i % m_WNX, z = i / m_WNX;
                    if (x > 0 && m_WFree[i - 1] && !m_WSeen[i - 1]) { m_WSeen[i - 1] = true; m_WStack.Add(i - 1); }
                    if (x < m_WNX - 1 && m_WFree[i + 1] && !m_WSeen[i + 1]) { m_WSeen[i + 1] = true; m_WStack.Add(i + 1); }
                    if (z > 0 && m_WFree[i - m_WNX] && !m_WSeen[i - m_WNX]) { m_WSeen[i - m_WNX] = true; m_WStack.Add(i - m_WNX); }
                    if (z < m_WNZ - 1 && m_WFree[i + m_WNX] && !m_WSeen[i + m_WNX]) { m_WSeen[i + m_WNX] = true; m_WStack.Add(i + m_WNX); }
                }
            }
            bool all = true;
            for (int k = keep.Count - 1; k >= 1; k--)
                if (!m_WSeen[keep[k]])
                {
                    all = false;
                    if (more == null) keep.RemoveAt(k);
                }
            return all && (more == null || m_WFree[start]);
        }

        /// <summary>
        /// With these footprints added, a walker still gets from the middle of the room to each of its doorways and
        /// stairs: checked when a group is placed, so a group that would wall something off is never put there (the
        /// whole-house check after the furnishing used to take it out again, with its neighbours: rooms went bare).
        /// </summary>
        bool StaysWalkable(PlanRoom room, List<Rect> rects)
        {
            if (m_WalkRoom != room) return true; // pieces put outside FurnishRoom (tests, the features)
            return Linked(rects, m_WTargets);
        }

        /// <summary>
        /// The safety net under all the rules above: a walker 0.8 m wide (a robot) must get from the hatch to the middle of
        /// every room. Checked on a 0.25 m grid of each floor: the rooms' floors minus a band along the solid walls and
        /// around the furniture, linked through doorways and open passages and between floors by the stairs. Where rooms
        /// stay out of reach, the furniture groups that wall them off go (with their loot), until everything is reachable.
        /// </summary>
        /// <summary>Furniture groups the last walkability check took out (for tests and logs).</summary>
        public static int LastRemoved { get; private set; }
        public static int LastDroppedItems { get; private set; }
        public static readonly List<string> LastNotes = new();
        /// <summary>The pieces the last walkability check took out (for the space audit and tests).</summary>
        public static readonly List<(PropKind kind, Vector3 pos)> LastCut = new();

        /// <summary>Rooms whose middle cannot be walked to from the hatch (walls, doorways, stairs, furniture placed so far).</summary>
        public List<PlanRoom> Unreached()
        {
            var grid = new Walk(m_L, this);
            var reached = grid.Flood();
            var list = new List<PlanRoom>();
            foreach (var r in m_L.Rooms) if (r.Id != m_L.HiddenRoom && !grid.Reached(reached, r.Center)) list.Add(r);
            return list;
        }

        /// <summary>Takes the walk grid of the bare building from another furnisher of the same layout (it is built only once).</summary>
        public void ShareWalk(Furnisher other)
        {
            if (other.m_L != m_L) return;
            m_WalkFree = other.m_WalkFree;
            m_WalkOwner = other.m_WalkOwner;
        }

        public void EnsureWalkable()
        {
            LastRemoved = 0;
            LastDroppedItems = 0;
            LastNotes.Clear();
            LastCut.Clear();
            for (int round = 0; round < 40; round++)
            {
                long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
                var grid = new Walk(m_L, this);
                long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
                var reached = grid.Flood();
                long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
                var lostRooms = new List<PlanRoom>();
                // the walker is a robot: it never gets into the hidden room (players do, through its illusion walls)
                foreach (var r in m_L.Rooms) if (r.Id != m_L.HiddenRoom && !grid.Reached(reached, r)) lostRooms.Add(r);
                long t3 = System.Diagnostics.Stopwatch.GetTimestamp();
                var cut = lostRooms.Count > 0 ? grid.Barrier(reached, lostRooms) : new HashSet<int>();
                long t4 = System.Diagnostics.Stopwatch.GetTimestamp();
                long tick = System.Diagnostics.Stopwatch.Frequency / 1000;
                LevelGenerator.Timing.Grid += (t1 - t0) / tick; LevelGenerator.Timing.Flood += (t2 - t1) / tick;
                LevelGenerator.Timing.Reach += (t3 - t2) / tick; LevelGenerator.Timing.Barrier += (t4 - t3) / tick;
                LevelGenerator.Timing.Rounds++;
                if (cut.Count == 0)
                {
                    foreach (var s in m_L.Stairs)
                        LastNotes.Add($"stair from {s.Floor} room {s.Room} reached {grid.Reached(reached, m_L.Rooms[s.Room].Center)} -> room {s.RoomAbove} reached {grid.Reached(reached, m_L.Rooms[s.RoomAbove].Center)}");
                    foreach (var r in m_L.Rooms)
                        if (!grid.Reached(reached, r)) LastNotes.Add($"lost #{r.Id} {r.Type} f{r.Floor} {r.Center}");
                    // loot nobody can reach (in a niche walled off by furniture) is not left there
                    for (int i = m_L.ItemSpots.Count - 1; i >= 0; i--)
                    {
                        var p = m_L.ItemSpots[i];
                        var floor = new Vector3(p.x, LevelLayout.FloorY(LevelLayout.FloorOf(p.y)), p.z);
                        bool hidden = m_L.HiddenRoom >= 0 && m_L.Plan.RoomAt(floor + Vector3.up * 0.3f)?.Id == m_L.HiddenRoom;
                        if (!hidden && !grid.Reached(reached, floor)) { m_L.ItemSpots.RemoveAt(i); m_ItemGroup.RemoveAt(i); LastDroppedItems++; }
                    }
                    return;
                }
                LastRemoved += cut.Count;
                RemoveGroups(cut);
            }
        }

        /// <summary>
        /// After <see cref="EnsureWalkable"/>: a kitchen whose run (with its fridge) had to go, because it stood in the way
        /// between doors, gets a fridge of its own back — against any wall, with a walkway in front of it. True if one was
        /// put back (then check the walking again).
        /// </summary>
        public bool RestoreFridges()
        {
            bool any = false;
            foreach (var room in m_L.Rooms)
            {
                if (room.Type != RoomType.Kitchen) continue;
                bool has = false;
                foreach (var p in m_L.Props) has |= p.Kind == PropKind.Fridge && m_P.RoomAt(p.Position + Vector3.up * 0.3f) == room;
                if (has) continue;
                m_Fridge = false;
                int before = m_L.Props.Count;
                AgainstAnyWall(room, PropKind.Fridge, walkway: true);
                any |= m_L.Props.Count > before;
            }
            return any;
        }

        void RemoveGroups(HashSet<int> groups)
        {
            for (int i = m_L.Props.Count - 1; i >= 0; i--)
                if (m_GroupOf[i] >= 0 && groups.Contains(m_GroupOf[i]))
                {
                    LastCut.Add((m_L.Props[i].Kind, m_L.Props[i].Position));
                    m_L.Props.RemoveAt(i);
                    m_GroupOf.RemoveAt(i);
                }
            for (int i = m_L.ItemSpots.Count - 1; i >= 0; i--)
                if (groups.Contains(m_ItemGroup[i])) { m_L.ItemSpots.RemoveAt(i); m_ItemGroup.RemoveAt(i); }
            for (int f = 0; f < m_Taken.Length; f++) { m_Taken[f].Clear(); m_Solid[f].Clear(); }
            foreach (var p in m_L.Props)
            {
                if (p.Kind is not (PropKind.Partition or PropKind.DeskLamp or PropKind.CoffeeMachine))
                    m_Taken[LevelLayout.FloorOf(p.Position.y)].Add(FootAt(p.Kind, p.Position, p.RotationY));
                if (InTheWay(p.Kind)) m_Solid[LevelLayout.FloorOf(p.Position.y)].Add(FootAt(p.Kind, p.Position, p.RotationY));
            }
        }

        /// <summary>The walkable grid of the house for one check.</summary>
        sealed class Walk
        {
            const float S = 0.25f, Agent = 0.4f, Clutter = 0.55f; // a robot's half width; around furniture a little more (the baked navmesh erodes more there)
            readonly LevelLayout m_L;
            readonly int m_NX, m_NZ, m_F;
            readonly bool[] m_Free;
            readonly int[] m_Owner; // the furniture group blocking a cell (−1 none, −2 wall)

            public Walk(LevelLayout l, Furnisher fur)
            {
                m_L = l;
                m_F = l.Floors;
                m_NX = Mathf.CeilToInt(l.SizeX / S);
                m_NZ = Mathf.CeilToInt(l.SizeZ / S);
                // the building itself (floors, walls, stairs) does not change while furniture is taken out: built once per
                // furnisher, copied for every check, and only the furniture is stamped on the copy
                if (fur.m_WalkFree != null)
                {
                    m_Free = (bool[])fur.m_WalkFree.Clone();
                    m_Owner = (int[])fur.m_WalkOwner.Clone();
                    Furniture(l, fur);
                    return;
                }
                m_Free = new bool[m_F * m_NX * m_NZ];
                m_Owner = new int[m_F * m_NX * m_NZ];
                for (int f = 0; f < m_F; f++)
                    for (int x = 0; x < m_NX; x++)
                        for (int z = 0; z < m_NZ; z++)
                        {
                            int i = Index(f, x, z);
                            m_Owner[i] = -1;
                            var c = Center(x, z);
                            m_Free[i] = l.Plan.IsFloor(f, c);
                        }
                // solid walls (openings cut out) with the walker's half width around them
                for (int w = 0; w < l.Plan.Walls.Count; w++)
                {
                    var wall = l.Plan.Walls[w];
                    var cuts = new List<(float a, float b)>();
                    bool open = false;
                    foreach (var o in l.Plan.OpeningsOf(w))
                    {
                        if (o.Kind == EdgeType.Open) open = true;
                        else if (o.Kind is EdgeType.Door or EdgeType.Breach) cuts.Add((o.At - LevelMetrics.ClearWidth(o.Width) / 2f, o.At + LevelMetrics.ClearWidth(o.Width) / 2f));
                    }
                    if (open) continue;
                    cuts.Sort();
                    float from = 0f;
                    foreach (var (a, b) in cuts) { Band(wall.Floor, wall.At(from), wall.At(a), LevelMetrics.WallThickness / 2f + Agent, -2); from = b; }
                    Band(wall.Floor, wall.At(from), wall.At(wall.Length), LevelMetrics.WallThickness / 2f + Agent, -2);
                }
                // the stairs' squares (their flights are walked through the links below)
                foreach (var s in l.Stairs)
                {
                    var a = PlanGenerator.StairArea(s, Agent + 0.05f); // the flights and rails, with a robot's half width
                    Box(s.Floor, a, -2);
                    if (s.Floor + 1 < m_F) Box(s.Floor + 1, a, -2);
                }
                fur.m_WalkFree = (bool[])m_Free.Clone();
                fur.m_WalkOwner = (int[])m_Owner.Clone();
                Furniture(l, fur);
            }

            void Furniture(LevelLayout l, Furnisher fur)
            {
                for (int i = 0; i < l.Props.Count; i++)
                {
                    var p = l.Props[i];
                    if (!InTheWay(p.Kind)) continue; // on a top or up on a wall, or flat on the floor
                    var r = FootAt(p.Kind, p.Position, p.RotationY);
                    Box(LevelLayout.FloorOf(p.Position.y), Grow(r, Clutter), fur.m_GroupOf[i] >= 0 ? fur.m_GroupOf[i] : -2);
                }
            }

            int Index(int f, int x, int z) => (f * m_NZ + z) * m_NX + x;
            static Vector2 Center(int x, int z) => new((x + 0.5f) * S, (z + 0.5f) * S);
            Vector2Int CellOf(Vector2 p) => new(Mathf.FloorToInt(p.x / S), Mathf.FloorToInt(p.y / S));
            bool In(int f, int x, int z) => f >= 0 && f < m_F && x >= 0 && z >= 0 && x < m_NX && z < m_NZ;

            void Block(int f, int x, int z, int owner)
            {
                if (!In(f, x, z)) return;
                int i = Index(f, x, z);
                if (!m_Free[i] && m_Owner[i] == -2) return;
                m_Free[i] = false;
                if (m_Owner[i] != -2) m_Owner[i] = owner == -2 ? -2 : (m_Owner[i] == -1 ? owner : m_Owner[i]);
            }

            void Box(int f, Rect r, int owner)
            {
                var a = CellOf(r.min);
                var b = CellOf(r.max);
                for (int x = a.x; x <= b.x; x++)
                    for (int z = a.y; z <= b.y; z++)
                        if (r.Overlaps(new Rect(x * S, z * S, S, S))) Block(f, x, z, owner);
            }

            void Band(int f, Vector2 a, Vector2 b, float half, int owner)
            {
                float len = (b - a).magnitude;
                var box = Rect.MinMaxRect(Mathf.Min(a.x, b.x) - half, Mathf.Min(a.y, b.y) - half, Mathf.Max(a.x, b.x) + half, Mathf.Max(a.y, b.y) + half);
                var lo = CellOf(box.min);
                var hi = CellOf(box.max);
                for (int x = lo.x; x <= hi.x; x++)
                    for (int z = lo.y; z <= hi.y; z++)
                    {
                        var c = Center(x, z);
                        float t = len > 1e-4f ? Mathf.Clamp(Vector2.Dot(c - a, (b - a) / len), 0f, len) : 0f;
                        var near = len > 1e-4f ? a + (b - a) / len * t : a;
                        if ((c - near).magnitude < half) Block(f, x, z, owner);
                    }
            }

            /// <summary>Cells reached from the hatch (stairs link their ends on two floors).</summary>
            public bool[] Flood()
            {
                var seen = new bool[m_Free.Length];
                var q = new Queue<int>();
                void Seed(Vector3 p)
                {
                    int f = LevelLayout.FloorOf(p.y);
                    var c = Nearest(f, new Vector2(p.x, p.z));
                    if (c < 0 || seen[c]) return;
                    seen[c] = true;
                    q.Enqueue(c);
                }
                Seed(m_L.HatchPosition + new Vector3(1.6f, 0f, 0f));
                Seed(m_L.HatchPosition - new Vector3(1.6f, 0f, 0f));
                Seed(m_L.HatchPosition + new Vector3(0f, 0f, 1.6f));
                Seed(m_L.HatchPosition - new Vector3(0f, 0f, 1.6f));
                void Visit(int j)
                {
                    if (seen[j] || !m_Free[j]) return;
                    seen[j] = true;
                    q.Enqueue(j);
                }
                bool more = true;
                var linked = new HashSet<int>();
                while (more)
                {
                    while (q.Count > 0)
                    {
                        int i = q.Dequeue();
                        int x = i % m_NX, z = i / m_NX % m_NZ;
                        if (x > 0) Visit(i - 1);
                        if (x < m_NX - 1) Visit(i + 1);
                        if (z > 0) Visit(i - m_NX);
                        if (z < m_NZ - 1) Visit(i + m_NX);
                    }
                    more = false;
                    for (int k = 0; k < m_L.Stairs.Count; k++)
                    {
                        if (linked.Contains(k)) continue;
                        var s = m_L.Stairs[k];
                        var (bottom, top) = LevelLayout.StairEnds(s);
                        int bi = Nearest(s.Floor, new Vector2(bottom.x, bottom.z), s.Room);
                        int ti = Nearest(s.Floor + 1, new Vector2(top.x, top.z), s.RoomAbove);
                        if (bi < 0 || ti < 0) { LastNotes.Add($"stair {k} floor {s.Floor}: bottom {bottom} cell {bi} room {s.Room} space {m_L.Plan.SpaceAt(s.Floor, new Vector2(bottom.x, bottom.z))}, top {top} cell {ti} room {s.RoomAbove} space {m_L.Plan.SpaceAt(s.Floor + 1, new Vector2(top.x, top.z))}"); continue; }
                        if (seen[bi] && !seen[ti]) { seen[ti] = true; q.Enqueue(ti); linked.Add(k); more = true; }
                        else if (seen[ti] && !seen[bi]) { seen[bi] = true; q.Enqueue(bi); linked.Add(k); more = true; }
                    }
                }
                return seen;
            }

            /// <summary>The free cell nearest to a point of a floor (within 1.5 m, or <paramref name="reach"/> cells; of that room when given), or −1.</summary>
            int Nearest(int f, Vector2 p, int room = -1, int reach = 6)
            {
                var c = CellOf(p);
                int best = -1;
                float bestD = reach * S;
                for (int dx = -reach; dx <= reach; dx++)
                    for (int dz = -reach; dz <= reach; dz++)
                    {
                        if (!In(f, c.x + dx, c.y + dz)) continue;
                        int i = Index(f, c.x + dx, c.y + dz);
                        if (!m_Free[i]) continue;
                        if (room >= 0 && m_L.Plan.SpaceAt(f, Center(c.x + dx, c.y + dz)) != room) continue;
                        float d = (Center(c.x + dx, c.y + dz) - p).magnitude;
                        if (d < bestD) { bestD = d; best = i; }
                    }
                return best;
            }

            public bool Reached(bool[] seen, Vector3 p, int reach = 6)
            {
                int f = LevelLayout.FloorOf(p.y);
                int i = Nearest(f, new Vector2(p.x, p.z), m_L.Plan.SpaceAt(f, new Vector2(p.x, p.z)), reach);
                return i >= 0 && seen[i];
            }

            /// <summary>
            /// A walker gets into the room: to its middle, or, where the middle is furniture (a meeting room's table, a
            /// restroom's stalls, the hostage's cage) and no free floor lies near it, to any of the room's free floor —
            /// as the check while furnishing has it (StaysWalkable: the doors, not the middle). Asking for a spot within
            /// 3.5 m of the middle stripped long meeting rooms with their door at the short end of the table and chairs.
            /// </summary>
            public bool Reached(bool[] seen, PlanRoom r)
            {
                if (!MiddleIsFurniture(r))
                {
                    int i = Nearest(r.Floor, new Vector2(r.Center.x, r.Center.z), r.Id);
                    if (i >= 0) return seen[i];
                }
                return AnyReached(seen, r);
            }

            /// <summary>Any free cell of the room is reached.</summary>
            bool AnyReached(bool[] seen, PlanRoom r)
            {
                var a = CellOf(r.Bounds.min);
                var b = CellOf(r.Bounds.max);
                for (int x = a.x; x <= b.x; x++)
                    for (int z = a.y; z <= b.y; z++)
                    {
                        if (!In(r.Floor, x, z)) continue;
                        int i = Index(r.Floor, x, z);
                        if (seen[i] && m_L.Plan.SpaceAt(r.Floor, Center(x, z)) == r.Id) return true;
                    }
                return false;
            }

            const int Reach = 6; // 1.5 m in cells: how close a blocking group has to be to both sides

            /// <summary>
            /// The furniture between the reached part of the house and the rooms out of reach: groups owning blocked cells
            /// within 1.5 m (Chebyshev, same floor) of both a reached cell and a free cell on the lost side (flooded from
            /// the lost rooms' middles). One flood of the lost side, two summed-area tables, then one look per blocked cell.
            /// </summary>
            public HashSet<int> Barrier(bool[] seen, List<PlanRoom> rooms)
            {
                var found = new HashSet<int>();
                int n = m_Free.Length, plane = m_NX * m_NZ;
                // the lost side: free unreached cells connected to a lost room's middle
                var lost = new bool[n];
                var stack = new Stack<int>();
                foreach (var room in rooms)
                {
                    int start = Nearest(room.Floor, new Vector2(room.Center.x, room.Center.z));
                    if (start < 0 || seen[start] || lost[start]) continue;
                    lost[start] = true;
                    stack.Push(start);
                }
                // and the unreached ends of stairs: furniture can wall off the approach to a flight inside a room that is
                // reached itself (a narrow strip beside the stair), and then the whole floor beyond is lost
                foreach (var s in m_L.Stairs)
                {
                    var (bottom, top) = LevelLayout.StairEnds(s);
                    foreach (int end in new[] { Nearest(s.Floor, new Vector2(bottom.x, bottom.z), s.Room), Nearest(s.Floor + 1, new Vector2(top.x, top.z), s.RoomAbove) })
                    {
                        if (end < 0 || seen[end] || lost[end]) continue;
                        lost[end] = true;
                        stack.Push(end);
                    }
                }
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    int x = i % m_NX, z = i / m_NX % m_NZ;
                    if (x > 0) Visit(i - 1);
                    if (x < m_NX - 1) Visit(i + 1);
                    if (z > 0) Visit(i - m_NX);
                    if (z < m_NZ - 1) Visit(i + m_NX);
                }
                void Visit(int j)
                {
                    if (!m_Free[j] || seen[j] || lost[j]) return;
                    lost[j] = true;
                    stack.Push(j);
                }
                var near = new List<int>();
                var nearSeen = Table(seen);
                var nearLost = Table(lost);
                int w = m_NX + 1;
                for (int f = 0; f < m_F; f++)
                {
                    int tb = f * w * (m_NZ + 1);
                    for (int z = 0; z < m_NZ; z++)
                    {
                        int z0 = Mathf.Max(0, z - Reach), z1 = Mathf.Min(m_NZ, z + Reach + 1);
                        for (int x = 0; x < m_NX; x++)
                        {
                            int i = f * plane + z * m_NX + x;
                            if (m_Free[i] || m_Owner[i] < 0 || found.Contains(m_Owner[i])) continue;
                            int x0 = Mathf.Max(0, x - Reach), x1 = Mathf.Min(m_NX, x + Reach + 1);
                            int a = tb + z0 * w + x0, b = tb + z0 * w + x1, c = tb + z1 * w + x0, d = tb + z1 * w + x1;
                            if (nearSeen[d] - nearSeen[b] - nearSeen[c] + nearSeen[a] > 0 &&
                                nearLost[d] - nearLost[b] - nearLost[c] + nearLost[a] > 0) { found.Add(m_Owner[i]); near.Add(i); }
                        }
                    }
                }
                // ...and of those, the groups that stand between the two sides in their own room: the reached and the lost
                // floor near them on their side of the walls. Counted through walls, a wing out of reach took with it every
                // piece standing against the far side of a wall (toilet stalls, a hospital bed, lab benches), not just the
                // one in the way.
                var mine = new HashSet<int>();
                foreach (int i in near)
                {
                    if (mine.Contains(m_Owner[i])) continue;
                    int f = i / plane, x = i % m_NX, z = i / m_NX % m_NZ;
                    int space = m_L.Plan.SpaceAt(f, Center(x, z));
                    bool r = false, l = false;
                    for (int dz = -Reach; dz <= Reach && !(r && l); dz++)
                        for (int dx = -Reach; dx <= Reach && !(r && l); dx++)
                        {
                            if (!In(f, x + dx, z + dz)) continue;
                            int j = Index(f, x + dx, z + dz);
                            if (!m_Free[j] || !(seen[j] || lost[j])) continue;
                            if (m_L.Plan.SpaceAt(f, Center(x + dx, z + dz)) != space) continue;
                            if (seen[j]) r = true; else l = true;
                        }
                    if (r && l) mine.Add(m_Owner[i]);
                }
                return mine.Count > 0 ? mine : found;
            }

            /// <summary>Summed-area table of the marked free cells, per floor ((NX + 1) × (NZ + 1) each, zero first row and column).</summary>
            int[] Table(bool[] marked)
            {
                int w = m_NX + 1, h = m_NZ + 1, plane = m_NX * m_NZ;
                var sum = new int[m_F * w * h];
                for (int f = 0; f < m_F; f++)
                {
                    int tb = f * w * h;
                    for (int z = 0; z < m_NZ; z++)
                    {
                        int row = 0;
                        for (int x = 0; x < m_NX; x++)
                        {
                            int i = f * plane + z * m_NX + x;
                            if (marked[i] && m_Free[i]) row++;
                            sum[tb + (z + 1) * w + x + 1] = sum[tb + z * w + x + 1] + row;
                        }
                    }
                }
                return sum;
            }
        }

        /// <summary>
        /// How far from the wall's face a piece's origin goes so its back touches the wall: standing furniture keeps
        /// <see cref="WallGap"/> (skirting, panels), what hangs on the wall itself sits flat against it (<see cref="HungGap"/>).
        /// </summary>
        float BackOf(PropKind k) => -(Foot.TryGetValue(k, out var f) ? f.yMin : -0.3f) + (OnWall.Contains(k) ? HungGap : WallGap);
        float R(float a, float b) => a + (float)m_Rng.NextDouble() * (b - a);
        bool Chance(double p) => m_Rng.NextDouble() < p;

        // ------------------------------------------------------------------ rooms

        /// <summary>How far in front of a window (from the wall's axis) a tall piece still covers it.</summary>
        const float WindowDepth = 1.0f;

        /// <summary>The strip of floor in front of every window of the house, per floor (<see cref="LevelBuilder.WindowCuts"/>).</summary>
        RectGrid[] WindowStrips()
        {
            var grids = new RectGrid[m_P.Floors];
            for (int f = 0; f < grids.Length; f++) grids[f] = new RectGrid();
            foreach (var (pos, yaw, _, _) in LevelBuilder.WindowCuts(m_L))
            {
                var q = Quaternion.Euler(0f, yaw, 0f);
                Vector3 inward = q * Vector3.forward, along = q * Vector3.right * (LevelMetrics.WindowWidth / 2f + 0.1f);
                Vector3 a = pos - along, c = pos + along + inward * WindowDepth;
                int floor = LevelLayout.FloorOf(pos.y - LevelMetrics.WindowCenterY + 0.3f);
                if (floor < 0 || floor >= grids.Length) continue;
                grids[floor].Add(Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.z, c.z), Mathf.Max(a.x, c.x), Mathf.Max(a.z, c.z)));
            }
            return grids;
        }

        /// <summary>Furnishes one room: islands first in big rooms, then the walls, then the corners.</summary>
        /// <summary>A printer stands in this room already: at most one per room (they looked like a row of kiosks).</summary>
        bool m_Printer;
        /// <summary>A fridge stands in this room already: every kitchen gets one, first thing along its walls.</summary>
        bool m_Fridge;
        /// <summary>A table stands in this room already (a kitchen gets one: a small one if the big one did not fit).</summary>
        bool m_Table;

        public void FurnishRoom(PlanRoom room)
        {
            if (room.Area < 4f) return;
            m_Printer = false;
            m_Fridge = false;
            m_Table = false;
            m_InRoom.Clear();
            m_Cap = room.Area > 70f ? 2 : 1;
            m_Windows ??= WindowStrips();
            BeginRoomWalk(room);
            if (room.Id == m_L.VaultRoom) { VaultRoom(room); return; }
            // islands in the middle: about one and a half times what an office planner would call "enough" — a house to search
            int islands = room.Type switch
            {
                RoomType.OpenSpace => Mathf.RoundToInt(room.Area / 15f),
                RoomType.Server => Mathf.RoundToInt(room.Area / 11f),
                RoomType.Storage => room.Area > 24f ? 1 : 0, // a pallet or so: the middle is the pile's floor (the workers stack loot there)
                RoomType.Meeting => room.Area > 60f ? 3 : room.Area > 30f ? 2 : 1,
                RoomType.Kitchen => Mathf.Max(1, Mathf.RoundToInt(room.Area / 17f)),
                RoomType.Lounge => Mathf.Max(1, Mathf.RoundToInt(room.Area / 19f)),
                RoomType.Lobby when m_L.Kind == HouseKind.Apartments => 0,
                RoomType.Lobby => 3,
                RoomType.Living => room.Area > 14f ? 1 : 0,
                RoomType.Office => room.Area > 22f ? Mathf.Max(1, Mathf.RoundToInt(room.Area / 28f)) : 0,
                RoomType.Hall => room.Area > 30f ? 1 : 0,
                RoomType.Gym => Mathf.Max(1, Mathf.RoundToInt(room.Area / 16f)),
                RoomType.Library => Mathf.Max(1, Mathf.RoundToInt(room.Area / 14f)),
                RoomType.CopyRoom => room.Area > 14f ? 1 : 0,
                RoomType.ControlRoom => room.Area > 20f ? 1 : 0,
                RoomType.Lab => Mathf.Max(1, Mathf.RoundToInt(room.Area / 16f)),
                RoomType.NapRoom => room.Area > 16f ? 1 : 0,
                RoomType.Studio => room.Area > 14f ? 1 : 0,
                RoomType.LockerRoom => Mathf.Max(1, Mathf.RoundToInt(room.Area / 18f)),
                RoomType.Infirmary => room.Area > 16f ? 1 : 0,
                RoomType.GameRoom => Mathf.Max(1, Mathf.RoundToInt(room.Area / 15f)),
                RoomType.Workshop => Mathf.RoundToInt(room.Area / 70f), // tyres, an engine, the drain cart: the walkway behind the bays stays open
                _ => 0,
            };
            Anchor(room);
            Islands(room, islands);
            if (room.Type == RoomType.Meeting) MeetingTable(room);
            if (room.Type == RoomType.Kitchen && !m_Table) SmallTable(room);
            Signature(room);
            Walls(room);
            if (room.Type != RoomType.Corridor) Walls(room); // a second pass fills the gaps the first left
            Corners(room);
            Sparse(room);
        }

        /// <summary>
        /// The vault behind the bank door: deposit boxes and cash shelves along every wall, gold on pallets and carts of
        /// money around the treasure in the middle (kept free by <see cref="HouseFeatures.PlanObjective"/>); the quarter
        /// circle the round door swings through stays clear.
        /// </summary>
        void VaultRoom(PlanRoom room)
        {
            if (m_L.VaultDoorway >= 0 && m_L.VaultDoorway < m_L.Doorways.Count)
            {
                var d = m_L.Doorways[m_L.VaultDoorway];
                var along = d.Rotation * Vector3.right;
                var across = d.Rotation * Vector3.forward;
                if (Vector3.Dot(room.Center - d.Position, across) < 0f) across = -across;
                Keep(room.Floor, d.Position - along * 1.4f, d.Position + along * 1.4f + across * 2.6f);
            }
            var faces = FacesOf(room);
            float y = LevelLayout.FloorY(room.Floor);
            foreach (var (w, n) in faces)
            {
                var u = new Vector3(w.Dir.x, 0, w.Dir.y);
                for (float t = 0.1f; t < w.Length - 0.3f;)
                {
                    var face = new Vector3(w.A.x, y, w.A.y) + u * t + n * (LevelMetrics.WallThickness / 2f);
                    var f = new Frame(face, u, n);
                    var batch = new Batch();
                    bool boxes = Chance(0.7);
                    var kind = boxes ? PropKind.DepositWall : PropKind.CashShelf;
                    float width = boxes ? 2.05f : 1.65f;
                    batch.Add(kind, f.P(width / 2f, BackOf(kind)), f.Face);
                    if (!boxes) batch.Item(f.P(width / 2f + R(-0.5f, 0.5f), BackOf(kind), 1.06f));
                    if (t + width <= w.Length - 0.05f && Deep(room, face, n, batch, 0.9f) && TryCommit(room, batch, 0.02f)) t += width + 0.02f;
                    else t += 0.25f;
                }
            }
            // gold and money between the walls and the treasure
            var b = room.Bounds;
            var spots = new List<Vector3>();
            for (float x = b.xMin + 1.0f; x < b.xMax - 0.9f; x += 0.5f)
                for (float z = b.yMin + 1.0f; z < b.yMax - 0.9f; z += 0.5f)
                    if (InRoom(room.Floor, room.Id, x, z)) spots.Add(new Vector3(x, y, z));
            Shuffle(spots);
            int placed = 0, max = Mathf.Clamp(Mathf.RoundToInt(room.Area / 6f), 2, 8);
            foreach (var s in spots)
            {
                if (placed >= max) break;
                var batch = new Batch();
                var kind = Chance(0.6) ? PropKind.GoldPallet : PropKind.MoneyCart;
                batch.Add(kind, s, m_Rng.Next(4) * 90f);
                if (kind == PropKind.GoldPallet && Chance(0.5)) batch.Item(s + Vector3.up * 0.6f);
                if (TryCommit(room, batch, 0.05f, 0.75f)) placed++;
            }
        }

        /// <summary>
        /// A meeting room is its table. Where the islands found no place for one (doors on every wall leave little of a
        /// small room), a smaller set gets every spot of the room and a narrower way round: the long table with six chairs,
        /// the round one with four, with two, alone.
        /// </summary>
        void MeetingTable(PlanRoom room)
        {
            if (m_InRoom.ContainsKey(PropKind.ConferenceTable) || m_InRoom.ContainsKey(PropKind.MeetingTable) || m_InRoom.ContainsKey(PropKind.RoundTable)) return;
            var b = room.Bounds;
            var spots = new List<Vector3>();
            float y = LevelLayout.FloorY(room.Floor);
            for (float x = b.xMin + 1.0f; x < b.xMax - 0.9f; x += 0.5f)
                for (float z = b.yMin + 1.0f; z < b.yMax - 0.9f; z += 0.5f)
                    if (InRoom(room.Floor, room.Id, x, z)) spots.Add(new Vector3(x, y, z));
            var c = room.Center;
            spots.Sort((p, q) => (p - c).sqrMagnitude.CompareTo((q - c).sqrMagnitude));
            bool wide = b.width >= b.height;
            for (int option = room.Area > 18f ? 0 : 1; option < 4; option++)
                foreach (var s in spots)
                    for (int turn = 0; turn < (option == 3 ? 1 : 2); turn++)
                    {
                        // the table along the room first
                        bool alongX = wide == (turn == 0);
                        var f = new Frame(s, alongX ? Vector3.right : Vector3.forward, alongX ? Vector3.forward : Vector3.left);
                        var batch = new Batch();
                        if (option == 0)
                        {
                            batch.Add(PropKind.MeetingTable, f.P(0, 0), f.Face);
                            for (int i = -1; i <= 1; i++)
                            {
                                batch.Add(PropKind.MeetingChair, f.P(i * 1.1f, 1.0f), f.Back);
                                batch.Add(PropKind.MeetingChair, f.P(i * 1.1f, -1.0f), f.Face);
                            }
                            batch.Item(f.P(R(-1.2f, 1.2f), R(-0.3f, 0.3f), 0.78f));
                        }
                        else
                        {
                            batch.Add(PropKind.RoundTable, f.P(0, 0), 0);
                            if (option == 1) Chairs(f, batch, 4);
                            if (option == 2)
                            {
                                batch.Add(PropKind.Chair, f.P(0, 1.0f), f.Back);
                                batch.Add(PropKind.Chair, f.P(0, -1.0f), f.Face);
                            }
                            batch.Item(f.P(0, 0, 0.76f));
                        }
                        if (TryCommit(room, batch, 0.05f, 0.6f)) return;
                    }
        }

        /// <summary>
        /// The pieces that make a room what it is: an office without a desk, a server room without a rack, a meeting room
        /// without a table reads as a mistake (measured over 72 houses: 61 % of offices had no desk — the wall groups gave
        /// them guest chairs, a safe and shelves —, 20 % of server rooms no rack, 13 % of meeting rooms no table).
        /// </summary>
        public static readonly Dictionary<RoomType, PropKind[]> Signatures = new()
        {
            [RoomType.Office] = new[] { PropKind.Desk, PropKind.ExecutiveDesk, PropKind.StandingDesk, PropKind.Terminal, PropKind.ConsoleDesk, PropKind.SupervisorDesk },
            [RoomType.OpenSpace] = new[] { PropKind.Desk, PropKind.StandingDesk, PropKind.Terminal },
            [RoomType.Server] = new[] { PropKind.ServerRack, PropKind.NetworkCabinet, PropKind.TapeLibrary },
            [RoomType.Meeting] = new[] { PropKind.MeetingTable, PropKind.RoundTable, PropKind.ConferenceTable },
            [RoomType.Lab] = new[] { PropKind.LabBench, PropKind.RobotTable, PropKind.WorkTable },
            [RoomType.Infirmary] = new[] { PropKind.HospitalBed, PropKind.ExamTable },
        };

        /// <summary>
        /// After the room's own groups and islands: a room still without its signature piece (<see cref="Signatures"/>)
        /// gets a compact one against a wall — a desk and its chair, a row of racks, a small table with two chairs, a work
        /// table, an examination couch — before the walls fill up with the rest.
        /// </summary>
        void Signature(PlanRoom room)
        {
            if (!Signatures.TryGetValue(room.Type, out var kinds)) return;
            foreach (var k in kinds) if (m_InRoom.ContainsKey(k)) return;
            Anchor(room, SignatureOption, SignatureOption + 3, false, 0.7f); // a small room with doors on every wall: a narrower way past it
        }

        /// <summary>The first option of <see cref="AnchorGroup"/> that <see cref="Signature"/> tries (the themed rooms' own options come before).</summary>
        const int SignatureOption = 10;

        void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = m_Rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        /// <summary>
        /// A themed room's own piece first, compact enough for a small room (a bed lengthwise along the wall, one pod, the
        /// video wall without its console…), so the room always reads as what it is; the rest comes after it. Tried along
        /// every wall, longest first, with the usual walkway in front, then with a narrower one.
        /// </summary>
        void Anchor(PlanRoom room)
        {
            if (room.Type == RoomType.Lobby && m_L.Kind == HouseKind.Hospital) { Anchor(room, 0, 2, false); return; } // the waiting area's rows of seats
            if (room.Type < RoomType.Gym && room.Type is not (RoomType.Kitchen or RoomType.Restroom)) return;
            if (room.Type == RoomType.Restroom)
            {
                // a restroom is its stalls and its basins: both, each as big as the room takes; where no stall fits
                // (they are 1.5 m deep: a narrow room with doors at both ends has no place for one) urinals on a wall
                if (!Anchor(room, 0, 3, false)) Anchor(room, 6, 9, false);
                Anchor(room, 3, 6, false);
                return;
            }
            if (room.Type == RoomType.Workshop) { WorkshopBays(room); return; }
            if (room.Type == RoomType.Ward) { if (!Anchor(room, 0, 1, false)) Anchor(room, 1, 2, false); return; } // two beds, or one
            // the flats: the bed with its night tables (or the bed alone), the sofa (with its coffee table), the bath, then the
            // toilet and the basin, the hallway's shoe cabinet under the coat hooks
            if (room.Type is RoomType.Bedroom or RoomType.Living) { if (!Anchor(room, 0, 1, false, 0.8f)) Anchor(room, 1, 2, false, 0.7f); return; }
            if (room.Type == RoomType.Bathroom) { Anchor(room, 0, 1, false, 0.6f); Anchor(room, 1, 2, false, 0.6f); return; }
            if (room.Type == RoomType.FlatHall) { Anchor(room, 0, 2, false, 0.7f); return; }
            if (room.Type == RoomType.NurseStation && StationCounter(room)) return;
            if (room.Type is RoomType.NurseStation or RoomType.Operating or RoomType.Imaging) // in the middle
            {
                Anchor(room, 0, 0, true);
                if (room.Type == RoomType.Operating)
                    for (int i = m_L.Props.Count - 1; i >= 0; i--)
                        if (m_L.Props[i].Kind == PropKind.SurgicalTable && m_P.RoomAt(m_L.Props[i].Position + Vector3.up * 0.3f) == room)
                        {
                            // the lamps hang from the ceiling over the table (nothing to walk into: no check against what stands under them)
                            Put(room, PropKind.SurgicalLights, m_L.Props[i].Position, m_L.Props[i].RotationY);
                            break;
                        }
                return;
            }
            if (room.Type == RoomType.PaintBooth) { Anchor(room, 0, 0, true); return; } // the car in the middle of the booth
            if (room.Type == RoomType.ControlRoom)
            {
                // the house's two robot terminals: side by side, or each against its own wall (the video wall if neither fits)
                // (a narrower way past them where the walls are short: the second terminal belongs here too)
                if (Anchor(room, 0, 1, false, 0.7f)) return;
                if (Anchor(room, 1, 2, false, 0.7f)) { Anchor(room, 1, 2, false, 0.7f); return; }
                Anchor(room, 2, 3, true);
                return;
            }
            Anchor(room, 0, 3, true);
        }

        /// <summary>
        /// The workshop hall's service bays along the street wall, one per 4 m: a closed sectional door against the wall, the
        /// painted lines of the bay and in it a car up on a lift, a car with its hood up, one parked, one on jack stands or
        /// a lift waiting empty — nose in, as they drive in. Where a doorway keeps the floor clear, the bay is left out.
        /// </summary>
        void WorkshopBays(PlanRoom room)
        {
            (PlanWall w, Vector3 n) street = default;
            bool found = false;
            foreach (var face in FacesOf(room))
            {
                if (face.n.z < 0.9f) continue;                     // the street is at z = 0: its wall's inner side looks +z
                if (!found || face.w.A.y < street.w.A.y - 0.01f || (Mathf.Abs(face.w.A.y - street.w.A.y) < 0.01f && face.w.Length > street.w.Length))
                {
                    street = face;
                    found = true;
                }
            }
            if (!found) return;
            var (wall, n) = street;
            var u = new Vector3(wall.Dir.x, 0, wall.Dir.y);
            float y = LevelLayout.FloorY(room.Floor);
            var origin = new Vector3(wall.A.x, y, wall.A.y) + n * (LevelMetrics.WallThickness / 2f);
            float bay = PlanGenerator.BayWidth;
            int count = Mathf.FloorToInt((wall.Length - 0.6f) / bay);
            float t0 = (wall.Length - count * bay) / 2f;
            var cars = new[] { PropKind.LiftCar, PropKind.CarHoodOpen, PropKind.CarParked, PropKind.CarOnStands, PropKind.LiftEmpty };
            var odds = new[] { 0.35, 0.2, 0.15, 0.15, 0.15 };
            for (int k = 0; k < count; k++)
            {
                var f = new Frame(origin + u * (t0 + k * bay + bay / 2f), u, n);
                double roll = m_Rng.NextDouble();
                int pick = 0;
                for (double acc = odds[0]; pick < odds.Length - 1 && roll > acc; acc += odds[++pick]) { }
                var car = cars[pick];
                // the whole bay first; where a doorway or a patrol keeps some of it free: without the door, without the lines,
                // a narrower car
                foreach (var (door, lines, kind) in new[] { (true, true, car), (false, true, car), (false, false, car), (false, false, PropKind.CarParked) })
                {
                    var b = new Batch();
                    if (door) b.Add(PropKind.GarageDoor, f.P(0, 0.01f), f.Face, solid: false);
                    if (lines) b.Add(PropKind.BayLines, f.P(0, 3.3f, 0.002f), f.Face, solid: false);
                    b.Add(kind, f.P(0, 3.4f), f.Face);
                    // a part left on the trunk lid of a car waiting its turn
                    if (kind is PropKind.CarParked or PropKind.CarHoodOpen && Chance(0.5)) b.Item(f.P(R(-0.3f, 0.3f), 1.7f, 0.9f));
                    if (TryCommit(room, b, 0.05f)) break;
                }
            }
        }

        /// <summary>
        /// The nurse station's counter: parallel to its open side, facing the corridor a little way in (the nurses see
        /// the corridor and whoever comes up to the ledge), their chairs behind it.
        /// </summary>
        bool StationCounter(PlanRoom room)
        {
            foreach (var d in m_L.Doorways)
            {
                if (d.Kind != EdgeType.Open || (d.RoomA != room.Id && d.RoomB != room.Id)) continue;
                var across = d.Rotation * Vector3.forward;
                across.y = 0f;
                if (Vector3.Dot(room.Center - d.Position, across) < 0f) across = -across;
                across.Normalize();
                var along = Vector3.Cross(Vector3.up, across);
                float yaw = Mathf.Atan2(-across.x, -across.z) * Mathf.Rad2Deg; // its front to the corridor
                foreach (float depth in new[] { 4.6f, 4.0f, 3.2f, 2.6f }) // past the room's middle: the floor before it stays open to the corridor
                {
                    var p = d.Position + across * depth;
                    p.y = LevelLayout.FloorY(room.Floor);
                    var b = new Batch();
                    b.Add(PropKind.NurseStationDesk, p, yaw);
                    b.Add(PropKind.Chair, p + across * 0.95f - along * 0.7f, yaw);
                    b.Add(PropKind.Chair, p + across * 0.95f + along * 0.7f, yaw);
                    b.Item(p + across * 0.1f + along * R(-1f, 1f) + Vector3.up * 0.78f);
                    if (TryCommit(room, b, 0.05f)) return true;
                }
            }
            return false;
        }

        /// <summary>The first of the options [from, to) of <see cref="AnchorGroup"/> that fits along a wall (or, when <paramref name="free"/>, in the middle).</summary>
        bool Anchor(PlanRoom room, int from, int to, bool free, float narrowest = 1.0f)
        {
            var faces = FacesOf(room);
            // stalls are deep (1.5 m): at the end wall of a narrow restroom they leave the whole length to walk, along
            // its side they would leave a slot
            if (room.Type == RoomType.Restroom && from == 0) faces.Reverse();
            float y = LevelLayout.FloorY(room.Floor);
            // the best option first (the biggest), smaller ones where doors leave too little wall
            for (int option = from; option < to; option++)
                foreach (float walk in narrowest < 1.0f ? new[] { 1.7f, 1.0f, narrowest } : new[] { 1.7f, 1.0f })
                    foreach (var (w, n) in faces)
                    {
                        var u = new Vector3(w.Dir.x, 0, w.Dir.y);
                        for (float t = 0.2f; t < w.Length - 0.3f; t += 0.25f)
                        {
                            var face = new Vector3(w.A.x, y, w.A.y) + u * t + n * (LevelMetrics.WallThickness / 2f);
                            var batch = new Batch();
                            float width = AnchorGroup(room, batch, new Frame(face, u, n), option);
                            if (width <= 0f || t + width > w.Length - 0.1f) break;
                            var shift = u * (width / 2f);
                            for (int i = 0; i < batch.Pieces.Count; i++) { var q = batch.Pieces[i]; q.Pos += shift; batch.Pieces[i] = q; }
                            for (int i = 0; i < batch.Items.Count; i++) batch.Items[i] += shift;
                            if (Deep(room, face + shift, n, batch, walk) && TryCommit(room, batch, front: n)) return true;
                        }
                    }
            if (!free) return false;
            // doors everywhere along the walls: the piece stands free in the room, an aisle round it
            var b = room.Bounds;
            var spots = new List<Vector3>();
            for (float x = b.xMin + 1.2f; x < b.xMax - 1.1f; x += 0.5f)
                for (float z = b.yMin + 1.2f; z < b.yMax - 1.1f; z += 0.5f)
                    if (InRoom(room.Floor, room.Id, x, z)) spots.Add(new Vector3(x, y, z));
            var c = room.Center;
            spots.Sort((p, q) => (p - c).sqrMagnitude.CompareTo((q - c).sqrMagnitude));
            bool wide = b.width >= b.height;
            foreach (var s in spots)
            {
                // the room's own free-standing group (a table, a bed, a pool table — never a thing that hangs on a wall)
                var f = new Frame(s, wide ? Vector3.right : Vector3.forward, wide ? Vector3.forward : Vector3.left);
                var batch = new Batch();
                Island(room, f, batch);
                if (batch.Pieces.Count > 0 && batch.Pieces.TrueForAll(p => p.Solid) && TryCommit(room, batch, 0.05f, 0.9f)) return true;
            }
            return false;
        }

        /// <summary>
        /// A themed room's signature group against a wall (centred on the frame, u along the wall), option 0 the biggest,
        /// 1 and 2 ever smaller; its width, 0 = no such option.
        /// </summary>
        float AnchorGroup(PlanRoom room, Batch b, Frame f, int option)
        {
            float Single(PropKind k, float w) { b.Add(k, f.P(0, BackOf(k)), f.Face); return w; }
            float Out(PropKind k, float v, float w) { b.Add(k, f.P(0, v), f.Face); return w; }
            // lying along the wall (its side to it): beds, pods
            float Along(PropKind k)
            {
                var r = FootprintOf(k);
                b.Add(k, f.P(0, r.width / 2f + WallGap), f.Face + 90f);
                return r.height + 0.05f;
            }
            switch (room.Type, option)
            {
                case (RoomType.Kitchen, 0):
                case (RoomType.Kitchen, 1):
                {
                    // the kitchen run (or its shorter half): sink, cooker, worktop with its microwave and the coffee machine, cupboards
                    // over it, and the fridge at its end (every kitchen has one); the whole run shifted to centre it
                    bool full = option == 0;
                    float shift = full ? -0.36f : -0.355f;
                    float sink = (full ? -1.52f : -1.22f) + shift, counter = (full ? 0.92f : 0.62f) + shift;
                    b.Add(PropKind.KitchenSink, f.P(sink, BackOf(PropKind.KitchenSink)), f.Face);
                    if (full) b.Add(PropKind.Stove, f.P(-0.62f + shift, BackOf(PropKind.Stove)), f.Face);
                    b.Add(PropKind.Fridge, f.P(counter + 1.22f + 0.37f, BackOf(PropKind.Fridge)), f.Face);
                    b.Add(PropKind.KitchenCounter, f.P(counter, BackOf(PropKind.KitchenCounter)), f.Face);
                    var cx = Quaternion.Euler(0, f.Face, 0) * Vector3.right;
                    b.Add(PropKind.CoffeeMachine, f.P(counter, BackOf(PropKind.KitchenCounter) + 0.05f, 0.93f) + cx * 0.75f, f.Face, solid: false);
                    b.Add(PropKind.WallCabinet, f.P(sink, BackOf(PropKind.WallCabinet)), f.Face, solid: false);
                    if (full) b.Add(PropKind.WallCabinet, f.P(counter, BackOf(PropKind.WallCabinet)), f.Face, solid: false);
                    b.Item(f.P(counter, BackOf(PropKind.KitchenCounter), 0.93f));
                    return full ? 5.02f : 4.41f;
                }
                case (RoomType.Kitchen, 2):
                    // the smallest: the sink and the fridge beside it
                    b.Add(PropKind.KitchenSink, f.P(-0.36f, BackOf(PropKind.KitchenSink)), f.Face);
                    b.Add(PropKind.WallCabinet, f.P(-0.36f, BackOf(PropKind.WallCabinet)), f.Face, solid: false);
                    b.Add(PropKind.Fridge, f.P(0.61f, BackOf(PropKind.Fridge)), f.Face);
                    return 1.96f;
                // the stalls: three, two, one
                case (RoomType.Restroom, 0): return Row(f, b, PropKind.ToiletStall, room.Area > 14f ? 3 : 2, 1.06f);
                case (RoomType.Restroom, 1): return Row(f, b, PropKind.ToiletStall, room.Area > 14f ? 2 : 1, 1.06f);
                case (RoomType.Restroom, 2): return Row(f, b, PropKind.ToiletStall, 1, 1.06f);
                // the basins under their mirrors, the hand dryer beside them
                case (RoomType.Restroom, 3):
                    b.Add(PropKind.MirrorSink, f.P(-0.62f, BackOf(PropKind.MirrorSink)), f.Face);
                    b.Add(PropKind.MirrorSink, f.P(0.18f, BackOf(PropKind.MirrorSink)), f.Face);
                    b.Add(PropKind.HandDryer, f.P(0.82f, BackOf(PropKind.HandDryer)), f.Face, solid: false);
                    return 2.0f;
                case (RoomType.Restroom, 4):
                    b.Add(PropKind.MirrorSink, f.P(-0.22f, BackOf(PropKind.MirrorSink)), f.Face);
                    b.Add(PropKind.HandDryer, f.P(0.42f, BackOf(PropKind.HandDryer)), f.Face, solid: false);
                    return 1.2f;
                case (RoomType.Restroom, 5): return Single(PropKind.MirrorSink, 0.75f);
                case (RoomType.Restroom, 6): return Row(f, b, PropKind.Urinal, 3, 0.7f);
                case (RoomType.Restroom, 7): return Row(f, b, PropKind.Urinal, 2, 0.7f);
                case (RoomType.Restroom, 8): return Single(PropKind.Urinal, 0.6f);
                case (RoomType.Gym, 0): return Single(PropKind.DumbbellRack, 1.5f);
                case (RoomType.Gym, 1): return Single(PropKind.ExerciseBike, 0.65f);
                case (RoomType.Gym, 2): return Out(PropKind.PunchingBag, 0.5f, 0.7f);
                case (RoomType.Library, 0): return Row(f, b, PropKind.TallBookcase, 2, 1.02f);
                case (RoomType.Library, 1): return Single(PropKind.TallBookcase, 1.05f);
                case (RoomType.Library, 2): return Out(PropKind.Globe, 0.4f, 0.7f);
                case (RoomType.CopyRoom, 0): { Single(PropKind.Copier, 1.0f); b.Item(f.P(0.2f, BackOf(PropKind.Copier), 1.05f)); return 1.0f; }
                case (RoomType.CopyRoom, 1): return Single(PropKind.PaperStack, 0.85f);
                case (RoomType.CopyRoom, 2): return Single(PropKind.Shredder, 0.5f);
                // the robots' control consoles (the house's terminals, LevelGenerator.PlaceTerminals), an operator's chair at each
                case (RoomType.ControlRoom, 0):
                    foreach (float u in new[] { -0.85f, 0.85f })
                    {
                        b.Add(PropKind.Terminal, f.P(u, BackOf(PropKind.Terminal)), f.Face);
                        b.Add(PropKind.OperatorChair, f.P(u, BackOf(PropKind.Terminal) + 0.85f), f.Back);
                    }
                    return 3.4f;
                case (RoomType.ControlRoom, 1):
                    b.Add(PropKind.Terminal, f.P(0, BackOf(PropKind.Terminal)), f.Face);
                    b.Add(PropKind.OperatorChair, f.P(0, BackOf(PropKind.Terminal) + 0.85f), f.Back);
                    return 1.7f;
                case (RoomType.ControlRoom, 2): return Single(PropKind.MonitorWall, 2.45f);
                case (RoomType.Lab, 0):
                    b.Add(PropKind.LabBench, f.P(0, BackOf(PropKind.LabBench)), f.Face);
                    b.Item(f.P(R(-0.6f, 0.6f), BackOf(PropKind.LabBench) + 0.1f, 0.91f));
                    return 1.85f;
                case (RoomType.Lab, 1): return Single(PropKind.PartsCabinet, 0.95f);
                case (RoomType.Lab, 2): return Single(PropKind.RobotArm, 0.7f);
                case (RoomType.NapRoom, 0): return Along(PropKind.SleepPod);
                case (RoomType.NapRoom, 1): return Out(PropKind.BeanBag, 0.6f, 0.95f);
                case (RoomType.Studio, 0):
                    b.Add(PropKind.AcousticPanel, f.P(-0.62f, BackOf(PropKind.AcousticPanel)), f.Face, solid: false);
                    b.Add(PropKind.AcousticPanel, f.P(0.62f, BackOf(PropKind.AcousticPanel)), f.Face, solid: false);
                    b.Add(PropKind.MicStand, f.P(0, 0.9f), f.Face);
                    return 2.45f;
                case (RoomType.Studio, 1): return Single(PropKind.MixingDesk, 1.65f);
                case (RoomType.Studio, 2): { b.Add(PropKind.AcousticPanel, f.P(0, BackOf(PropKind.AcousticPanel)), f.Face, solid: false); return 1.25f; }
                case (RoomType.LockerRoom, 0): return Row(f, b, PropKind.Lockers, 2, 1.05f);
                case (RoomType.LockerRoom, 1): return Single(PropKind.Lockers, 1.05f);
                case (RoomType.LockerRoom, 2): return Out(PropKind.Bench, 0.3f, 1.65f);
                case (RoomType.Lobby, 0): return Row(f, b, PropKind.WaitingSeats, 2, 2.5f);
                case (RoomType.Lobby, 1): return Single(PropKind.WaitingSeats, 2.5f);
                case (RoomType.Ward, 0):
                case (RoomType.Ward, 1):
                {
                    // patient beds head to the wall: the headwall unit over each, its curtain round it, a monitor and a night table beside
                    int beds = option == 0 ? 2 : 1;
                    float step = 1.95f, w = beds * step;
                    for (int i = 0; i < beds; i++)
                    {
                        float u = (i - (beds - 1) / 2f) * step;
                        float n = BackOf(PropKind.HospitalBed) + 0.45f; // pulled out from the wall: the nurses reach the head, the headwall's call cord hangs free
                        b.Add(PropKind.HospitalBed, f.P(u, n), f.Face);
                        b.Add(PropKind.BedHeadwall, f.P(u, BackOf(PropKind.BedHeadwall)), f.Face, solid: false);
                        b.Add(PropKind.BedCurtain, f.P(u - 0.05f, n + 0.05f), f.Face, solid: false);
                        if (Chance(0.6)) b.Item(f.P(u + 0.2f, n + 0.4f, 0.66f));
                    }
                    b.Add(PropKind.NightTable, f.P(beds == 2 ? 0f : 0.82f, BackOf(PropKind.NightTable)), f.Face);
                    return w + (beds == 1 ? 0.5f : 0f);
                }
                case (RoomType.Bedroom, 0):
                case (RoomType.Bedroom, 1):
                {
                    // the double bed head to the wall, a night table each side (a small room: the bed alone)
                    b.Add(PropKind.DoubleBed, f.P(0, BackOf(PropKind.DoubleBed)), f.Face);
                    if (option == 1) return 1.7f;
                    foreach (float s in new[] { -1.1f, 1.1f }) b.Add(PropKind.NightTable, f.P(s, BackOf(PropKind.NightTable)), f.Face);
                    if (Chance(0.5)) b.Item(f.P(Chance(0.5) ? -1.1f : 1.1f, BackOf(PropKind.NightTable), 0.56f));
                    return 2.75f;
                }
                case (RoomType.Living, 0):
                case (RoomType.Living, 1):
                    // the sofa against the wall (and its coffee table in front of it)
                    b.Add(PropKind.Sofa, f.P(0, BackOf(PropKind.Sofa)), f.Face);
                    if (option == 0) b.Add(PropKind.CoffeeTable, f.P(0, BackOf(PropKind.Sofa) * 2f + 0.62f), f.Face);
                    return 2.3f;
                case (RoomType.Bathroom, 0): return Single(PropKind.Bathtub, 1.75f);
                case (RoomType.Bathroom, 1):
                    b.Add(PropKind.HomeToilet, f.P(-0.42f, BackOf(PropKind.HomeToilet)), f.Face);
                    b.Add(PropKind.BathVanity, f.P(0.36f, BackOf(PropKind.BathVanity)), f.Face);
                    return 1.35f;
                case (RoomType.FlatHall, 0):
                    // the shoe cabinet under the coat hooks, a mirror's worth of wall beside
                    b.Add(PropKind.ShoeCabinet, f.P(-0.55f, BackOf(PropKind.ShoeCabinet)), f.Face);
                    b.Add(PropKind.HallHooks, f.P(0.55f, BackOf(PropKind.HallHooks)), f.Face, solid: false);
                    return 2.1f;
                case (RoomType.FlatHall, 1): return Single(PropKind.ShoeCabinet, 0.95f); // a hallway of doors: the shoe cabinet alone
                case (RoomType.Infirmary, 0): { float w = Along(PropKind.HospitalBed); b.Item(f.P(0, 0.5f, 0.66f)); return w; }
                case (RoomType.Infirmary, 1): { Single(PropKind.MedicineCabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.MedicineCabinet), 1.65f)); return 0.95f; }
                case (RoomType.Infirmary, 2): return Out(PropKind.PrivacyScreen, 0.3f, 1.65f);
                // the signature pieces (Signature): compact, against a wall
                case (RoomType.Office, SignatureOption):
                case (RoomType.Office, SignatureOption + 1):
                case (RoomType.OpenSpace, SignatureOption + 1):
                    b.Add(PropKind.Desk, f.P(0, 0.4f + WallGap), f.Face);
                    b.Add(PropKind.Chair, f.P(0, 1.2f), f.Back);
                    b.Item(f.P(R(-0.5f, 0.5f), 0.5f, DeskTop + 0.005f));
                    if (option != SignatureOption) return 1.65f;
                    b.Add(PropKind.FilePedestal, f.P(1.05f, BackOf(PropKind.FilePedestal)), f.Face);
                    return 2.55f;
                case (RoomType.OpenSpace, SignatureOption):
                    foreach (float u in new[] { -0.82f, 0.82f })
                    {
                        b.Add(PropKind.Desk, f.P(u, 0.4f + WallGap), f.Face);
                        b.Add(PropKind.Chair, f.P(u, 1.2f), f.Back);
                    }
                    b.Item(f.P(R(-1.2f, 1.2f), 0.5f, DeskTop + 0.005f));
                    return 3.3f;
                case (RoomType.OpenSpace, SignatureOption + 2):
                    Single(PropKind.StandingDesk, 1.45f);
                    b.Item(f.P(R(-0.4f, 0.4f), BackOf(PropKind.StandingDesk), 1.105f));
                    return 1.45f;
                case (RoomType.Server, SignatureOption):
                    for (int i = -1; i <= 1; i++) b.Add(PropKind.ServerRack, f.P(i * 0.72f, BackOf(PropKind.ServerRack)), f.Face);
                    return 2.2f;
                case (RoomType.Server, SignatureOption + 1): return Row(f, b, PropKind.ServerRack, 2, 0.72f);
                case (RoomType.Server, SignatureOption + 2): return Row(f, b, PropKind.NetworkCabinet, 2, 0.62f);
                case (RoomType.Meeting, SignatureOption):
                case (RoomType.Meeting, SignatureOption + 1):
                    // a small table against the wall, two chairs on the room side (or one)
                    b.Add(PropKind.RoundTable, f.P(0, 0.5f + WallGap), 0);
                    if (option == SignatureOption) { b.Add(PropKind.Chair, f.P(-0.35f, 1.3f), f.Back); b.Add(PropKind.Chair, f.P(0.35f, 1.3f), f.Back); }
                    else b.Add(PropKind.Chair, f.P(0, 1.3f), f.Back);
                    b.Item(f.P(0, 0.5f + WallGap, 0.76f));
                    return 1.25f;
                case (RoomType.Lab, SignatureOption):
                    b.Add(PropKind.WorkTable, f.P(0, BackOf(PropKind.WorkTable)), f.Face);
                    b.Add(PropKind.Chair, f.P(0, 1.2f), f.Back);
                    b.Item(f.P(R(-0.5f, 0.5f), BackOf(PropKind.WorkTable), DeskTop + 0.005f));
                    return 1.65f;
                case (RoomType.Lab, SignatureOption + 1): return Single(PropKind.WorkTable, 1.65f);
                case (RoomType.Office, SignatureOption + 2): b.Item(f.P(R(-0.5f, 0.5f), 0.5f, DeskTop + 0.005f)); return Single(PropKind.Desk, 1.65f);
                case (RoomType.Infirmary, SignatureOption): return Along(PropKind.ExamTable);
                case (RoomType.GameRoom, 0): return Row(f, b, PropKind.ArcadeMachine, 2, 0.8f);
                case (RoomType.GameRoom, 1): return Single(PropKind.ArcadeMachine, 0.8f);
                case (RoomType.GameRoom, 2): { b.Add(PropKind.DartBoard, f.P(0, BackOf(PropKind.DartBoard)), f.Face, solid: false); return 0.65f; }
            }
            return 0f;
        }

        /// <summary>The walls of a room with the normal into it, longest first (rails, curves and stubs left out).</summary>
        List<(PlanWall w, Vector3 n)> FacesOf(PlanRoom room)
        {
            var list = new List<(PlanWall w, Vector3 n)>();
            foreach (var w in m_P.Walls)
            {
                if (w.Floor != room.Floor || w.Railing || w.Shaped || w.Length < 0.9f) continue;
                if (w.RoomA != room.Id && w.RoomB != room.Id) continue;
                var d = w.Dir;
                var n = new Vector3(-d.y, 0, d.x);
                var mid = w.At(w.Length / 2f);
                if (!InRoom(room.Floor, room.Id, mid.x + n.x * 0.3f, mid.y + n.z * 0.3f)) n = -n;
                if (!InRoom(room.Floor, room.Id, mid.x + n.x * 0.3f, mid.y + n.z * 0.3f)) continue;
                list.Add((w, n));
            }
            list.Sort((p, q) => q.w.Length.CompareTo(p.w.Length));
            return list;
        }

        void Islands(PlanRoom room, int count)
        {
            if (count <= 0) return;
            var spots = new List<Vector3>();
            var b = room.Bounds;
            for (float x = b.xMin + 1.5f; x < b.xMax - 1.4f; x += 0.5f)
                for (float z = b.yMin + 1.5f; z < b.yMax - 1.4f; z += 0.5f)
                    if (InRoom(room.Floor, room.Id, x, z)) spots.Add(new Vector3(x, LevelLayout.FloorY(room.Floor), z));
            // closest to the centre first, a little shuffled
            var c = room.Center;
            var keys = new Dictionary<Vector3, float>();
            foreach (var s in spots) keys[s] = (s - c).sqrMagnitude + (float)m_Rng.NextDouble() * 6f;
            spots.Sort((p, q) => keys[p].CompareTo(keys[q]));
            int placed = 0;
            foreach (var s in spots)
            {
                if (placed >= count) break;
                bool turn = Chance(0.5);
                var f = new Frame(s, turn ? Vector3.forward : Vector3.right, turn ? Vector3.left : Vector3.forward);
                var batch = new Batch();
                Island(room, f, batch);
                // a small room keeps a narrower way round its table (the walk check at placement has the last word)
                if (batch.Pieces.Count > 0 && TryCommit(room, batch, 0.05f, room.Area < 24f ? 0.8f : Aisle)) placed++;
            }
        }

        /// <summary>One island group of the room's type around frame origin.</summary>
        void Island(PlanRoom room, Frame f, Batch b)
        {
            double roll = m_Rng.NextDouble();
            switch (room.Type)
            {
                case RoomType.Living:
                    // the rug and the coffee table on it, a magazine or a cup
                    b.Add(PropKind.AreaRug, f.P(0, 0), f.Face, solid: false);
                    b.Add(PropKind.CoffeeTable, f.P(0, 0), f.Face);
                    if (roll < 0.6) b.Item(f.P(R(-0.3f, 0.3f), 0, 0.45f));
                    break;
                case RoomType.OpenSpace:
                    if (roll < 0.62) DeskCluster(f, b);
                    else if (roll < 0.78)
                    {   // standing desks back to back, a planter at the end
                        b.Add(PropKind.StandingDesk, f.P(0, 0.38f), f.Face);
                        b.Add(PropKind.StandingDesk, f.P(0, -0.38f), f.Back);
                        b.Add(PropKind.PlanterDivider, f.P(1.12f, 0), f.Face + 90f);
                        b.Item(f.P(R(-0.4f, 0.4f), Chance(0.5) ? 0.4f : -0.4f, 1.105f));
                    }
                    else if (roll < 0.88)
                    {   // two quiet pods facing each other over a pedestal
                        b.Add(PropKind.SofaPod, f.P(0, 0.75f), f.Back);
                        b.Add(PropKind.SofaPod, f.P(0, -0.75f), f.Face);
                    }
                    else { b.Add(PropKind.RoundTable, f.P(0, 0), 0); Chairs(f, b, 3); }
                    break;
                case RoomType.Meeting:
                    if (room.Area > 26f && roll < 0.55)
                    {   // the board table: chairs down both sides and one at each end
                        b.Add(PropKind.ConferenceTable, f.P(0, 0), f.Face);
                        for (int i = -1; i <= 1; i++)
                        {
                            b.Add(PropKind.MeetingChair, f.P(i * 1.1f, 0.95f), f.Back);
                            b.Add(PropKind.MeetingChair, f.P(i * 1.1f, -0.95f), f.Face);
                        }
                        b.Add(PropKind.MeetingChair, f.P(-2.15f, 0), f.Face + 90f);
                        b.Add(PropKind.MeetingChair, f.P(2.15f, 0), f.Face - 90f);
                        b.Item(f.P(R(-1.4f, 1.4f), R(-0.3f, 0.3f), 0.755f));
                    }
                    else if (room.Area > 20f && roll < 0.85)
                    {
                        b.Add(PropKind.MeetingTable, f.P(0, 0), f.Face);
                        for (int i = -1; i <= 1; i++)
                        {
                            b.Add(PropKind.MeetingChair, f.P(i * 1.1f, 1.0f), f.Back);
                            b.Add(PropKind.MeetingChair, f.P(i * 1.1f, -1.0f), f.Face);
                        }
                        b.Item(f.P(R(-1.2f, 1.2f), R(-0.3f, 0.3f), 0.78f));
                    }
                    else { b.Add(PropKind.RoundTable, f.P(0, 0), 0); Chairs(f, b, 4); b.Item(f.P(0, 0, 0.76f)); }
                    break;
                case RoomType.Kitchen:
                    if (roll < 0.35)
                    {   // the island: worktop, cupboards to the back, three stools at its overhang
                        b.Add(PropKind.KitchenIsland, f.P(0, 0), f.Face);
                        for (int i = -1; i <= 1; i++) b.Add(PropKind.BarStool, f.P(i * 0.62f, 0.78f), f.Back);
                        b.Item(f.P(R(-0.7f, 0.7f), 0, 0.925f));
                    }
                    else if (roll < 0.55)
                    {   // a breakfast bar: the counter and its stools on the far side
                        b.Add(PropKind.BarCounter, f.P(0, 0), f.Face);
                        for (int i = -1; i <= 1; i++) b.Add(PropKind.BarStool, f.P(i * 0.62f, 0.62f), f.Back);
                        b.Item(f.P(R(-0.6f, 0.6f), 0, 1.06f));
                    }
                    else if (roll < 0.85)
                    {   // the dining table, chairs round it
                        b.Add(PropKind.DiningTable, f.P(0, 0), f.Face);
                        foreach (float u in new[] { -0.42f, 0.42f })
                        {
                            b.Add(PropKind.Chair, f.P(u, 0.78f), f.Back);
                            b.Add(PropKind.Chair, f.P(u, -0.78f), f.Face);
                        }
                        if (Chance(0.5)) b.Add(PropKind.Chair, f.P(1.12f, 0), f.Face - 90f);
                        b.Item(f.P(R(-0.5f, 0.5f), R(-0.2f, 0.2f), 0.755f));
                    }
                    else
                    {
                        b.Add(PropKind.RoundTable, f.P(0, 0), 0);
                        Chairs(f, b, 3 + m_Rng.Next(2)); // the kitchen table, chairs all round
                        b.Item(f.P(R(-0.2f, 0.2f), 0, 0.76f));
                    }
                    break;
                case RoomType.Gym:
                    if (roll < 0.3) { b.Add(PropKind.WeightBench, f.P(0, 0), f.Face); b.Item(f.P(0.6f, 0.4f)); }
                    else if (roll < 0.5) { b.Add(PropKind.Treadmill, f.P(-0.5f, 0), f.Face); b.Add(PropKind.Treadmill, f.P(0.5f, 0), f.Face); }
                    else if (roll < 0.72) { b.Add(PropKind.PowerCage, f.P(0, 0), f.Face); b.Item(f.P(0, 0)); }
                    else { b.Add(PropKind.RowingMachine, f.P(-0.42f, 0), f.Face); b.Add(PropKind.RowingMachine, f.P(0.42f, 0), f.Face); }
                    break;
                case RoomType.Library:
                    if (roll < 0.38)
                    {   // a reading table, chairs along both sides
                        b.Add(PropKind.ReadingTable, f.P(0, 0), f.Face);
                        foreach (float u in new[] { -0.5f, 0.5f })
                        {
                            b.Add(PropKind.Chair, f.P(u, 0.75f), f.Back);
                            b.Add(PropKind.Chair, f.P(u, -0.75f), f.Face);
                        }
                        b.Item(f.P(R(-0.7f, 0.7f), R(-0.2f, 0.2f), 0.77f));
                    }
                    else if (roll < 0.72)
                    {   // a double-sided stack of bookcases
                        foreach (float u in new[] { -0.5f, 0.5f })
                        {
                            b.Add(PropKind.TallBookcase, f.P(u, 0.18f), f.Face);
                            b.Add(PropKind.TallBookcase, f.P(u, -0.18f), f.Back);
                        }
                        b.Item(f.P(R(-0.8f, 0.8f), Chance(0.5) ? 0.2f : -0.2f, BookcaseLevels[m_Rng.Next(3)]));
                    }
                    else
                    {   // study carrels back to back, a chair at each
                        b.Add(PropKind.StudyCarrel, f.P(0, 0.36f), f.Face);
                        b.Add(PropKind.StudyCarrel, f.P(0, -0.36f), f.Back);
                        b.Add(PropKind.Chair, f.P(0, 1.05f), f.Back);
                        b.Add(PropKind.Chair, f.P(0, -1.05f), f.Face);
                        b.Item(f.P(R(-0.3f, 0.3f), 0.45f, 0.765f));
                    }
                    break;
                case RoomType.CopyRoom:
                    if (roll < 0.5)
                    {
                        b.Add(PropKind.Desk, f.P(0, 0.4f), f.Face);
                        b.Add(PropKind.Desk, f.P(0, -0.4f), f.Back);
                        b.Add(PropKind.PaperStack, f.P(1.3f, 0), f.Face);
                        b.Item(f.P(R(-0.5f, 0.5f), 0.3f, 0.77f));
                    }
                    else
                    {   // the binding table, the mail cart beside it
                        b.Add(PropKind.WorkTable, f.P(0, 0), f.Face);
                        b.Add(PropKind.MailCart, f.P(1.35f, 0), f.Face + 90f);
                        b.Item(f.P(R(-0.5f, 0.5f), R(-0.2f, 0.2f), 0.905f));
                    }
                    break;
                case RoomType.ControlRoom:
                    if (roll < 0.5)
                    {
                        b.Add(PropKind.ConsoleDesk, f.P(0, 0), f.Face);
                        b.Add(PropKind.OperatorChair, f.P(-0.6f, 0.85f), f.Back);
                        b.Add(PropKind.OperatorChair, f.P(0.6f, 0.85f), f.Back);
                        b.Item(f.P(R(-0.9f, 0.9f), 0.2f, 0.77f));
                    }
                    else
                    {   // the supervisor's desk: screens at its back, two operators at its front
                        b.Add(PropKind.SupervisorDesk, f.P(0, 0), f.Face);
                        b.Add(PropKind.OperatorChair, f.P(-0.45f, 0.85f), f.Back);
                        b.Add(PropKind.OperatorChair, f.P(0.45f, 0.85f), f.Back);
                        b.Item(f.P(R(-0.6f, 0.6f), 0.2f, 1.105f));
                    }
                    break;
                case RoomType.Lab:
                    if (roll < 0.45)
                    {   // two benches back to back
                        b.Add(PropKind.LabBench, f.P(0, 0.4f), f.Face);
                        b.Add(PropKind.LabBench, f.P(0, -0.4f), f.Back);
                        b.Add(PropKind.Chair, f.P(R(-0.4f, 0.4f), 1.2f), f.Back);
                        b.Add(PropKind.Chair, f.P(R(-0.4f, 0.4f), -1.2f), f.Face);
                        b.Item(f.P(R(-0.6f, 0.6f), Chance(0.5) ? 0.5f : -0.5f, 0.91f));
                    }
                    else if (roll < 0.62) { b.Add(PropKind.RobotArm, f.P(0, 0), f.Face + m_Rng.Next(4) * 90f); b.Item(f.P(0.7f, 0.4f)); }
                    else
                    {   // the robot on its table, a stool each side and the scope cart
                        b.Add(PropKind.RobotTable, f.P(0, 0), f.Face);
                        b.Add(PropKind.Chair, f.P(-0.5f, 0.85f), f.Back);
                        b.Add(PropKind.ScopeCart, f.P(1.3f, 0), f.Face - 90f);
                        b.Item(f.P(0.35f, 0.1f, 0.905f));
                    }
                    break;
                case RoomType.NapRoom:
                    if (roll < 0.32) b.Add(PropKind.Hammock, f.P(0, 0), f.Face);
                    else if (roll < 0.6)
                    {
                        b.Add(PropKind.CoffeeTable, f.P(0, 0), f.Face);
                        b.Add(PropKind.BeanBag, f.P(-1.0f, 0.2f), f.Face + 60f);
                        b.Add(PropKind.BeanBag, f.P(1.0f, 0.2f), f.Face - 60f);
                        b.Item(f.P(0, 0, 0.42f));
                    }
                    else if (roll < 0.8)
                    {   // a daybed screened off by its curtain, the night table at its head
                        b.Add(PropKind.Daybed, f.P(0, 0), f.Face);
                        b.Add(PropKind.CurtainDivider, f.P(0, -0.62f), f.Face);
                        b.Add(PropKind.NightTable, f.P(1.3f, -0.1f), f.Face);
                        b.Item(f.P(1.3f, -0.1f, 0.405f));
                    }
                    else { b.Add(PropKind.FloorCushions, f.P(0, 0), f.Face); b.Add(PropKind.FloorLamp, f.P(0.95f, -0.4f), f.Face); }
                    break;
                case RoomType.Studio:
                    if (roll < 0.4) { b.Add(PropKind.DrumKit, f.P(0, 0), f.Face); }
                    else if (roll < 0.7)
                    {   // two voices at their mics, facing each other
                        b.Add(PropKind.MicStand, f.P(0, 0.45f), f.Back);
                        b.Add(PropKind.MicStand, f.P(0, -0.45f), f.Face);
                        b.Add(PropKind.BarStool, f.P(0, 1.0f), f.Back);
                        b.Add(PropKind.BarStool, f.P(0, -1.0f), f.Face);
                    }
                    else
                    {   // the keyboard player: synth, stool, an amp beside
                        b.Add(PropKind.SynthStand, f.P(0, 0), f.Face);
                        b.Add(PropKind.BarStool, f.P(0, 0.75f), f.Back);
                        b.Add(PropKind.AmpCabinet, f.P(1.1f, 0), f.Face);
                    }
                    break;
                case RoomType.LockerRoom:
                    if (roll < 0.5) { b.Add(PropKind.Bench, f.P(0, 0), f.Face); if (Chance(0.5)) b.Item(f.P(R(-0.5f, 0.5f), 0, 0.46f)); }
                    else { b.Add(PropKind.BenchIsland, f.P(0, 0), f.Face); b.Item(f.P(R(-0.6f, 0.6f), Chance(0.5) ? 0.25f : -0.25f, 0.455f)); }
                    break;
                case RoomType.Infirmary:
                    if (roll < 0.5)
                    {
                        b.Add(PropKind.HospitalBed, f.P(0, 0), f.Face);
                        b.Add(PropKind.PrivacyScreen, f.P(1.25f, 0), f.Face + 90f);
                        b.Add(PropKind.IvStand, f.P(-0.8f, -0.6f), f.Face);
                        b.Item(f.P(0, 0.6f, 0.66f));
                    }
                    else
                    {   // the examination couch, the drip by it
                        b.Add(PropKind.ExamTable, f.P(0, 0), f.Face);
                        b.Add(PropKind.IvStand, f.P(0.65f, -0.55f), f.Face);
                        b.Item(f.P(0, 0.3f, 0.725f));
                    }
                    break;
                case RoomType.GameRoom:
                    int game = m_Rng.Next(5);
                    if (game == 0) { b.Add(PropKind.PoolTable, f.P(0, 0), f.Face); b.Item(f.P(R(-0.8f, 0.8f), R(-0.3f, 0.3f), 0.81f)); }
                    else if (game == 1) { b.Add(PropKind.Foosball, f.P(0, 0), f.Face); b.Item(f.P(R(-0.4f, 0.4f), 0, 0.77f)); }
                    else if (game == 2) { b.Add(PropKind.PingPongTable, f.P(0, 0), f.Face); b.Item(f.P(0.6f, 0, 0.78f)); }
                    else if (game == 3) { b.Add(PropKind.AirHockey, f.P(0, 0), f.Face); b.Item(f.P(R(-0.6f, 0.6f), 0, 0.85f)); }
                    else { b.Add(PropKind.PokerTable, f.P(0, 0), f.Face); Chairs(f, b, 4); b.Item(f.P(R(-0.3f, 0.3f), R(-0.3f, 0.3f), 0.785f)); }
                    break;
                case RoomType.Server: // a cold aisle between two rows of racks
                    if (roll < 0.75)
                    {
                        for (int i = -1; i <= 1; i++)
                        {
                            b.Add(PropKind.ServerRack, f.P(i * 0.72f, 1.25f), f.Back);
                            b.Add(PropKind.ServerRack, f.P(i * 0.72f, -1.25f), f.Face);
                        }
                        b.Item(f.P(R(-0.8f, 0.8f), 0));
                    }
                    else
                    {   // the tape library back to back with the power: a block in the middle of the floor
                        b.Add(PropKind.TapeLibrary, f.P(-0.3f, 0.42f), f.Face);
                        b.Add(PropKind.UpsUnit, f.P(0.6f, -0.42f), f.Back + 90f);
                        b.Add(PropKind.NetworkCabinet, f.P(-0.55f, -0.33f), f.Back);
                    }
                    break;
                case RoomType.Workshop: // loose things on the floor between the bays and the benches
                    if (roll < 0.3) b.Add(PropKind.TireStack, f.P(0, 0), 0);
                    else if (roll < 0.55) { b.Add(PropKind.EngineStand, f.P(0, 0), f.Face); b.Item(f.P(-0.2f, 0, 1.14f)); }
                    else if (roll < 0.75) b.Add(PropKind.OilDrain, f.P(0, 0), 0);
                    else if (roll < 0.9) b.Add(PropKind.FloorJack, f.P(0, 0), f.Face);
                    else { b.Add(PropKind.TireStack, f.P(-0.45f, 0), 0); b.Add(PropKind.TireStack, f.P(0.45f, 0), 0); }
                    break;
                case RoomType.NurseStation: // the counter facing the corridor's way, the nurses' chairs behind, a cart beside
                    b.Add(PropKind.NurseStationDesk, f.P(0, 0), f.Face);
                    b.Add(PropKind.Chair, f.P(-0.7f, -0.95f), f.Face);
                    b.Add(PropKind.Chair, f.P(0.7f, -0.95f), f.Face);
                    b.Item(f.P(R(-1f, 1f), 0.1f, 0.78f));
                    break;
                case RoomType.Operating: // the table under its lamps, the anaesthesia machine at the head, the instruments at the side
                    b.Add(PropKind.SurgicalTable, f.P(0, 0), f.AlongU);
                    b.Add(PropKind.AnesthesiaMachine, f.P(-1.75f, 0), f.AlongU);
                    b.Add(PropKind.InstrumentTable, f.P(0.3f, 1.5f), f.Back);
                    b.Add(PropKind.VitalsMonitor, f.P(-1.6f, 1.0f), f.Back);
                    break;
                case RoomType.Imaging: // a CT scanner where it fits, else the X-ray room's table and tube stand
                    if (room.Area >= 30f && roll < 0.6) b.Add(PropKind.CtScanner, f.P(0, 0), f.AlongU);
                    else b.Add(PropKind.XrayMachine, f.P(0, 0), f.AlongU);
                    break;
                case RoomType.PaintBooth: // the car being painted, lengthwise in the booth
                    b.Add(PropKind.CarParked, f.P(0, 0), f.AlongU);
                    break;
                case RoomType.Storage: // a wrapped pallet and a stack of boxes by the pile
                    b.Add(PropKind.Pallet, f.P(0, 0), m_Rng.Next(2) * 90f);
                    b.Add(PropKind.Boxes, f.P(0.2f, 1.2f), f.Face);
                    break;
                case RoomType.Lounge:
                    if (roll < 0.22) { b.Add(PropKind.PingPongTable, f.P(0, 0), f.Face); b.Item(f.P(0.6f, 0, 0.78f)); }
                    else if (roll < 0.72)
                    {   // the living corner on a rug: coffee table, sofa, armchairs at its ends
                        b.Add(PropKind.AreaRug, f.P(0, -0.3f), f.Face, solid: false);
                        b.Add(PropKind.CoffeeTable, f.P(0, 0), f.Face);
                        b.Add(PropKind.Sofa, f.P(0, -1.15f), f.Face);
                        b.Add(PropKind.Armchair, f.P(-1.35f, 0.2f), f.Face + 90f);
                        if (Chance(0.6)) b.Add(PropKind.Armchair, f.P(1.35f, 0.2f), f.Face - 90f);
                        b.Item(f.P(0, 0, 0.42f));
                    }
                    else
                    {   // the big sectional round a coffee table
                        b.Add(PropKind.SectionalSofa, f.P(0, -0.55f), f.Face);
                        b.Add(PropKind.AreaRug, f.P(0, 0.6f), f.Face, solid: false);
                        b.Add(PropKind.CoffeeTable, f.P(0, 0.85f), f.Face);
                        b.Item(f.P(0, 0.85f, 0.42f));
                    }
                    break;
                case RoomType.Lobby when m_L.Kind == HouseKind.Hospital:
                    // the admissions desk, rows of waiting seats back to back
                    if (roll < 0.45)
                    {
                        b.Add(PropKind.ReceptionDesk, f.P(0, 0), f.Face);
                        b.Add(PropKind.Chair, f.P(0, -0.85f), f.Face);
                        b.Item(f.P(-0.4f, 0.33f, 1.14f));
                    }
                    else
                    {
                        b.Add(PropKind.WaitingSeats, f.P(0, 0.35f), f.Back);
                        b.Add(PropKind.WaitingSeats, f.P(0, -0.35f), f.Face);
                    }
                    break;
                case RoomType.Lobby when m_L.Kind == HouseKind.Garage:
                    // the shop's front desk: the service counter, a row of waiting chairs facing it, a stack of tyres on show
                    if (roll < 0.45)
                    {
                        b.Add(PropKind.ReceptionDesk, f.P(0, 0), f.Face);
                        b.Add(PropKind.Chair, f.P(0, -0.85f), f.Face);
                        b.Item(f.P(-0.4f, 0.33f, 1.14f));
                    }
                    else if (roll < 0.8)
                        for (int i = -1; i <= 1; i++) b.Add(PropKind.GuestChair, f.P(i * 0.65f, 0), f.Face);
                    else { b.Add(PropKind.TireStack, f.P(-0.45f, 0), 0f); b.Add(PropKind.TireStack, f.P(0.45f, 0), 0f); }
                    break;
                case RoomType.Lobby:
                    if (roll < 0.32)
                    {   // the reception, the queue lines in front of it
                        b.Add(PropKind.ReceptionDesk, f.P(0, 0), f.Face);
                        b.Add(PropKind.Chair, f.P(0, -0.85f), f.Face);
                        b.Add(PropKind.QueueBarrier, f.P(-0.85f, 1.55f), f.Face);
                        b.Add(PropKind.QueueBarrier, f.P(0.85f, 1.55f), f.Face);
                        b.Item(f.P(-0.4f, 0.33f, 1.14f));
                    }
                    else if (roll < 0.55) b.Add(PropKind.RoundBenchPlanter, f.P(0, 0), 0f);
                    else if (roll < 0.7)
                    {   // the security line: turnstiles side by side, a kiosk at the end
                        for (int i = -1; i <= 1; i++) b.Add(PropKind.SecurityTurnstile, f.P(i * 1.22f, 0), f.Face);
                        b.Add(PropKind.InfoKiosk, f.P(2.2f, 0), f.Face);
                    }
                    else
                    {
                        b.Add(PropKind.CoffeeTable, f.P(0, 0), f.Face);
                        b.Add(PropKind.Armchair, f.P(-1.1f, 0), f.Face + 90f);
                        b.Add(PropKind.Armchair, f.P(1.1f, 0), f.Face - 90f);
                        b.Item(f.P(0, 0, 0.42f));
                    }
                    break;
                case RoomType.Office:
                    if (roll < 0.5)
                    {   // the boss's desk in the middle: the chair behind it, two guests in front
                        b.Add(PropKind.ExecutiveDesk, f.P(0, 0), f.Face);
                        b.Add(PropKind.Chair, f.P(0, -0.8f), f.Face);
                        b.Add(PropKind.GuestChair, f.P(-0.45f, 0.85f), f.Back);
                        b.Add(PropKind.GuestChair, f.P(0.45f, 0.85f), f.Back);
                        b.Item(f.P(R(-0.5f, 0.5f), 0.1f, 0.765f));
                    }
                    else
                    {
                        b.Add(PropKind.CoffeeTable, f.P(0, 0), f.Face);
                        b.Add(PropKind.Armchair, f.P(0, 0.85f), f.Back);
                        b.Add(PropKind.Armchair, f.P(0, -0.85f), f.Face);
                        b.Item(f.P(0, 0, 0.42f));
                    }
                    break;
                case RoomType.Hall:
                    if (roll < 0.4) b.Add(PropKind.Sculpture, f.P(0, 0), f.Face);
                    else if (roll < 0.7) b.Add(PropKind.RoundBenchPlanter, f.P(0, 0), 0f);
                    else
                    {
                        b.Add(PropKind.CoffeeTable, f.P(0, 0), f.Face);
                        b.Add(PropKind.Armchair, f.P(0, 0.85f), f.Back);
                        b.Add(PropKind.Armchair, f.P(0, -0.85f), f.Face);
                        b.Item(f.P(0, 0, 0.42f));
                    }
                    break;
            }
        }

        /// <summary>The metal shelving's decks within reach (Tools/Blender/rooms2_tech.py).</summary>
        static readonly float[] StorageDecks = { 0.61f, 1.09f, 1.57f };

        /// <summary>Back-to-back cluster of four desks with privacy screens, a planter closing one end sometimes; loot on some of them.</summary>
        void DeskCluster(Frame f, Batch b)
        {
            foreach (float u in new[] { -0.8f, 0.8f })
            {
                b.Add(PropKind.Desk, f.P(u, 0.4f), f.Face);
                b.Add(PropKind.Chair, f.P(u + R(-0.1f, 0.1f), 1.15f), f.Back + R(-12f, 12f));
                b.Add(PropKind.Desk, f.P(u, -0.4f), f.Back);
                b.Add(PropKind.Chair, f.P(u + R(-0.1f, 0.1f), -1.15f), f.Face + R(-12f, 12f));
                b.Add(PropKind.Partition, f.P(u, 0f), f.Face, solid: false);
                if (Chance(0.6)) b.Item(f.P(u + (Chance(0.5) ? -0.5f : 0.5f), Chance(0.5) ? 0.2f : -0.2f, DeskTop + 0.005f));
                if (Chance(0.35)) b.Add(PropKind.DeskLamp, f.P(u + 0.55f, 0.55f, DeskTop), f.Back, solid: false);
            }
            if (Chance(0.5)) b.Add(PropKind.PlanterDivider, f.P(Chance(0.5) ? 1.82f : -1.82f, 0), f.Face + 90f);
        }

        /// <summary>
        /// A kitchen whose middle had no room for the table with chairs all round (its middle stays free for the robots'
        /// patrol stop): a round table with a chair on each of two sides and a narrower walkway, or else the table against a
        /// wall with chairs on its three open sides.
        /// </summary>
        void SmallTable(PlanRoom room)
        {
            if (FreeTable(room)) return;
            float y = LevelLayout.FloorY(room.Floor);
            foreach (var (w, n) in FacesOf(room))
            {
                var u = new Vector3(w.Dir.x, 0, w.Dir.y);
                for (float t = 1.4f; t < w.Length - 1.4f; t += 0.25f)
                {
                    var face = new Vector3(w.A.x, y, w.A.y) + u * t + n * (LevelMetrics.WallThickness / 2f);
                    var f = new Frame(face, u, n);
                    var batch = new Batch();
                    var table = f.P(0, 0.5f + WallGap);
                    batch.Add(PropKind.RoundTable, table, 0);
                    foreach (var d in new[] { f.U, -f.U, f.N }) batch.Add(PropKind.Chair, table + d * 1.0f, Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg);
                    batch.Item(table + Vector3.up * 0.76f);
                    if (TryCommit(room, batch, 0.05f, front: n)) return;
                }
            }
        }

        bool FreeTable(PlanRoom room)
        {
            var b = room.Bounds;
            float y = LevelLayout.FloorY(room.Floor);
            var spots = new List<Vector3>();
            for (float x = b.xMin + 1.1f; x < b.xMax - 1.0f; x += 0.25f)
                for (float z = b.yMin + 1.1f; z < b.yMax - 1.0f; z += 0.25f)
                    if (InRoom(room.Floor, room.Id, x, z)) spots.Add(new Vector3(x, y, z));
            var c = room.Center;
            spots.Sort((p, q) => (p - c).sqrMagnitude.CompareTo((q - c).sqrMagnitude));
            // the chairs across the room: the robots' stop is along its long side (LevelGenerator.PatrolCentre)
            bool wide = b.width >= b.height;
            // two chairs, else one (a doorway's free floor on one side)
            foreach (int chairs in new[] { 2, 1 })
                foreach (var s in spots)
                    foreach (float side in chairs == 2 ? new[] { 1f } : new[] { 1f, -1f })
                    {
                        var f = new Frame(s, wide ? Vector3.forward : Vector3.right, wide ? Vector3.right : Vector3.forward);
                        var batch = new Batch();
                        batch.Add(PropKind.RoundTable, f.P(0, 0), 0);
                        foreach (var d in chairs == 2 ? new[] { f.U, -f.U } : new[] { f.U * side })
                            batch.Add(PropKind.Chair, f.O + d * 1.0f, Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg);
                        batch.Item(f.P(0, 0, 0.76f));
                        // a walkway round it, clear of other furniture (it may cross a doorway's or the patrol stop's free
                        // floor: those are walkways too)
                        var all = FootAt(PropKind.RoundTable, f.O, 0);
                        foreach (var p in batch.Pieces) all = Union(all, FootAt(p.Kind, p.Pos, p.Yaw));
                        if (m_Taken[room.Floor].Overlaps(Grow(all, 0.7f)) || !OnFloor(room.Floor, room.Id, all, 0.5f)) continue;
                        if (TryCommit(room, batch, 0.05f)) return true;
                    }
            return false;
        }

        void Chairs(Frame f, Batch b, int n)
        {
            // around a round table: on its four sides, facing it
            var sides = new[] { -f.N, f.U, f.N, -f.U };
            for (int i = 0; i < Mathf.Min(n, 4); i++)
            {
                var d = sides[i];
                b.Add(PropKind.Chair, f.O + d * 1.0f, Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg);
            }
        }

        // ------------------------------------------------------------------ walls

        void Walls(PlanRoom room)
        {
            var list = FacesOf(room); // longest walls first: the big pieces get the long walls
            float y = LevelLayout.FloorY(room.Floor);
            foreach (var (w, n) in list)
            {
                var u = new Vector3(w.Dir.x, 0, w.Dir.y);
                float t = R(0.15f, 0.9f);
                // a corridor is a walkway (1.7 m stays free in front of its things), a room keeps an aisle
                float walk = room.Type == RoomType.Corridor ? 1.4f : Aisle + 0.1f;
                while (t < w.Length - 0.3f)
                {
                    var face = new Vector3(w.A.x, y, w.A.y) + u * t + n * (LevelMetrics.WallThickness / 2f);
                    // what does not fit here (too deep for the room, tall under a window, one too many) gives way to
                    // another group of the room's set before the spot is given up
                    float placed = 0f;
                    for (int attempt = 0; attempt < 3 && placed <= 0f; attempt++)
                    {
                        var batch = new Batch();
                        float width = WallGroup(room, batch, face, u, n);
                        if (width <= 0f || t + width > w.Length - 0.1f) continue;
                        // the group spans [t, t + width] along the wall: shift its pieces to the group's middle
                        var shift = u * (width / 2f);
                        for (int i = 0; i < batch.Pieces.Count; i++)
                        {
                            var p = batch.Pieces[i];
                            p.Pos += shift;
                            batch.Pieces[i] = p;
                        }
                        for (int i = 0; i < batch.Items.Count; i++) batch.Items[i] += shift;
                        for (int i = 0; i < batch.Drops.Count; i++) { var d = batch.Drops[i]; d.Spot += shift; batch.Drops[i] = d; }
                        if (Deep(room, face + shift, n, batch, walk) && TryCommit(room, batch, front: n)) placed = width;
                    }
                    if (placed > 0f) t += placed + (room.Type == RoomType.Corridor ? R(4f, 10f) : R(0.15f, 1.2f));
                    else t += 0.5f;
                }
            }
        }

        /// <summary>
        /// A walkway stays in front of a group along a wall: <paramref name="walk"/> of the room's floor, and no other
        /// furniture within <see cref="Facing"/> more — the walkability check wants more room between two pieces than
        /// between a piece and a bare wall (<see cref="Walk"/>: the navmesh erodes more around furniture).
        /// </summary>
        bool Deep(PlanRoom room, Vector3 face, Vector3 n, Batch b, float walk = Aisle + 0.1f)
        {
            // the whole footprint, pushed walk further into the room, must still be the room's floor
            var u = Vector3.Cross(Vector3.up, n);
            foreach (var p in b.Pieces)
            {
                var r = FootAt(p.Kind, p.Pos, p.Yaw);
                foreach (var c in new[] { new Vector3(r.xMin, 0, r.yMin), new Vector3(r.xMax, 0, r.yMin), new Vector3(r.xMin, 0, r.yMax), new Vector3(r.xMax, 0, r.yMax) })
                    for (float d = 0f; d <= walk + Facing + 0.01f; d += 0.25f)
                    {
                        var q = c + n * d;
                        if (d <= walk + 0.01f && !InRoom(room.Floor, room.Id, q.x, q.z)) return false;
                        if (d > 0.2f && m_Taken[room.Floor].Contains(new Vector2(q.x, q.z))) return false;
                    }
            }
            return true;
        }

        /// <summary>One group along a wall, laid out from the wall's face (u along it from 0 to the returned width, n into the room).</summary>
        float WallGroup(PlanRoom room, Batch b, Vector3 face, Vector3 u, Vector3 n)
        {
            var f = new Frame(face, u, n);
            float Single(PropKind k, float w)
            {
                b.Add(k, f.P(0, BackOf(k)), f.Face);
                return w;
            }
            // a thing on the wall (a clock, a board, a cabinet) — nothing on the floor to walk into
            float Hung(PropKind k, float w)
            {
                b.Add(k, f.P(0, BackOf(k)), f.Face, solid: false);
                return w;
            }
            // lying along the wall (its side to it): beds, a rowing machine
            float Along(PropKind k, float du = 0f)
            {
                var r = FootprintOf(k);
                b.Add(k, f.P(du, r.width / 2f + WallGap), f.Face + 90f);
                return r.height + 0.05f;
            }
            float Clock(float w)
            {
                if (Chance(0.3)) b.Add(PropKind.WallClock, f.P(0, BackOf(PropKind.WallClock)), f.Face, solid: false); // over something low
                return w;
            }
            int roll = m_Rng.Next(100);
            switch (room.Type)
            {
                case RoomType.Bedroom:
                    if (roll < 40) { Single(PropKind.Wardrobe, 1.25f); return 1.25f; }
                    if (roll < 65) { Single(PropKind.Dresser, 1.05f); b.Item(f.P(R(-0.3f, 0.3f), BackOf(PropKind.Dresser), 0.86f)); return 1.05f; }
                    if (roll < 78) return Single(PropKind.Armchair, 0.9f);
                    if (roll < 90) return Single(PropKind.FloorLamp, 0.5f);
                    return Single(PropKind.Plant, 0.75f);
                case RoomType.Living:
                    if (roll < 28) { Single(PropKind.TvStand, 1.45f); return 1.45f; }
                    if (roll < 50) { Single(PropKind.Bookshelf, 1.25f); return 1.25f; }
                    if (roll < 64) return Single(PropKind.Armchair, 0.9f);
                    if (roll < 76) { Single(PropKind.Dresser, 1.05f); b.Item(f.P(R(-0.3f, 0.3f), BackOf(PropKind.Dresser), 0.86f)); return 1.05f; }
                    if (roll < 88) return Single(PropKind.FloorLamp, 0.5f);
                    return Single(PropKind.Plant, 0.75f);
                case RoomType.FlatHall:
                    if (roll < 45) return Single(PropKind.Wardrobe, 1.25f);
                    if (roll < 70) return Single(PropKind.UmbrellaStand, 0.35f);
                    return Single(PropKind.CoatRack, 0.6f);
                case RoomType.Bathroom:
                    if (roll < 60) return Single(PropKind.WashingMachine, 0.65f);
                    return Single(PropKind.SanitaryBin, 0.35f);
                case RoomType.Corridor when m_L.Kind == HouseKind.Apartments:
                    // the landing: the lift beside the stairs, a notice board, the fire hose cabinet
                    if (roll < 40) return Single(PropKind.ElevatorDoors, 1.35f); // flat against the wall, from the floor
                    if (roll < 65) return Hung(PropKind.NoticeBoard, 1.25f);
                    if (roll < 85) return Hung(PropKind.FireCabinet, 0.45f);
                    return Single(PropKind.Plant, 0.75f);
                case RoomType.Lobby when m_L.Kind == HouseKind.Apartments:
                    // the entrance hall: the mailboxes, the notice board, a bench and a plant
                    if (roll < 45) return Hung(PropKind.Mailboxes, 1.25f);
                    if (roll < 65) return Hung(PropKind.NoticeBoard, 1.25f);
                    if (roll < 85) return Single(PropKind.CorridorBench, 1.45f);
                    return Single(PropKind.Plant, 0.75f);
                case RoomType.Corridor when m_L.Kind == HouseKind.Hospital:
                    // a ward corridor: trolleys and chairs along the walls, far apart (the beds pass in the middle)
                    if (roll < 22) return Along(PropKind.Gurney);
                    if (roll < 40) return Single(PropKind.WaitingSeats, 2.5f);
                    if (roll < 55) return Single(PropKind.Wheelchair, 0.75f);
                    if (roll < 68) return Single(PropKind.LinenCart, 0.75f);
                    if (roll < 80) return Single(PropKind.MedicationCart, 1.05f);
                    if (roll < 90) return Hung(PropKind.HandSanitizer, 0.3f);
                    return Hung(PropKind.FireCabinet, 0.45f);
                case RoomType.Corridor:
                    // corridors are walkways: only slim things, far apart (Walls keeps 1.7 m free in front of them)
                    if (roll < 28) return Hung(PropKind.FireCabinet, 0.45f);
                    if (roll < 52) return Hung(PropKind.NoticeBoard, 1.25f);
                    if (roll < 70) return Single(PropKind.BinPair, 0.75f);
                    if (roll < 84) return Single(PropKind.UmbrellaStand, 0.35f);
                    return Single(PropKind.CorridorBench, 1.45f);
                case RoomType.Hall:
                    if (roll < 26) return Single(PropKind.Plant, 0.75f); // stair halls stay clear (no vending machines: they look like ATMs)
                    if (roll < 34) return Single(PropKind.WaterCooler, 0.4f);
                    if (roll < 40) return Single(PropKind.CoatRack, 0.6f);
                    if (roll < 46) return Single(PropKind.Armchair, 0.9f);
                    if (roll < 58) { Single(PropKind.DisplayCase, 1.05f); return Clock(1.05f); }
                    if (roll < 72) { Single(PropKind.HallBench, 1.85f); if (Chance(0.4)) b.Item(f.P(R(-0.6f, 0.6f), BackOf(PropKind.HallBench), 0.455f)); return 1.85f; }
                    if (roll < 82) return Single(PropKind.DirectoryBoard, 0.85f);
                    if (roll < 92) return Single(PropKind.PlanterLarge, 1.05f);
                    { b.Add(PropKind.Sculpture, f.P(0, 0.45f), f.Face); return 0.7f; }
                case RoomType.Lobby when m_L.Kind == HouseKind.Hospital:
                    // the admissions' waiting area: rows of seats, water, a wheelchair or two by the door
                    if (roll < 40) return Single(PropKind.WaitingSeats, 2.5f);
                    if (roll < 52) return Single(PropKind.Wheelchair, 0.75f);
                    if (roll < 62) return Single(PropKind.WaterCooler, 0.5f);
                    if (roll < 72) return Single(PropKind.VendingMachine, 1.0f);
                    if (roll < 82) return Hung(PropKind.HandSanitizer, 0.3f);
                    if (roll < 92) return Single(PropKind.Plant, 0.75f);
                    return Single(PropKind.MagazineRack, 0.6f);
                case RoomType.Lobby when m_L.Kind == HouseKind.Garage:
                    // the waiting area: chairs along the wall, the tyre display, coffee and water, a rack of brochures
                    if (roll < 30) return Row(f, b, PropKind.GuestChair, 3, 0.65f);
                    if (roll < 48) return Single(PropKind.TireRack, 2.1f);
                    if (roll < 58) return Single(PropKind.VendingMachine, 1.0f);
                    if (roll < 66) return Single(PropKind.WaterCooler, 0.5f);
                    if (roll < 76) { b.Add(PropKind.Sofa, f.P(0, BackOf(PropKind.Sofa)), f.Face); b.Add(PropKind.CoffeeTable, f.P(0, 1.35f), f.Face); b.Item(f.P(0, 1.35f, 0.42f)); return 2.25f; }
                    if (roll < 84) return Single(PropKind.MagazineRack, 0.6f);
                    if (roll < 92) return Single(PropKind.Plant, 0.75f);
                    return Single(PropKind.TireStack, 0.85f);
                case RoomType.Lobby:
                    if (roll < 18) { b.Add(PropKind.Sofa, f.P(0, BackOf(PropKind.Sofa)), f.Face); b.Add(PropKind.CoffeeTable, f.P(0, 1.35f), f.Face); b.Item(f.P(0, 1.35f, 0.42f)); return 2.25f; }
                    if (roll < 30) { b.Add(PropKind.Lockers, f.P(-0.525f, BackOf(PropKind.Lockers)), f.Face); b.Add(PropKind.Lockers, f.P(0.525f, BackOf(PropKind.Lockers)), f.Face); return 2.1f; }
                    if (roll < 40) return Single(PropKind.Plant, 0.75f);                  // no vending machines
                    if (roll < 48) return Single(PropKind.Lockers, 1.05f);                 // lockers to open instead of a plant
                    if (roll < 54) return Single(PropKind.CoatRack, 0.6f);
                    if (roll < 60) return Single(PropKind.FloorLamp, 0.45f);
                    if (roll < 70) return Single(PropKind.LogoTotem, 0.85f);
                    if (roll < 80) return Row(f, b, PropKind.InfoKiosk, 2, 0.7f);
                    if (roll < 90) return Single(PropKind.PlanterLarge, 1.05f);
                    return Single(PropKind.DirectoryBoard, 0.85f);
                case RoomType.Office:
                    if (roll < 20)
                    {   // a work desk against the wall
                        b.Add(PropKind.Desk, f.P(0, 0.4f + WallGap), f.Face);
                        b.Add(PropKind.Chair, f.P(R(-0.1f, 0.1f), 1.2f), f.Back + R(-15f, 15f));
                        if (Chance(0.6)) b.Add(PropKind.DeskLamp, f.P(0.55f, 0.3f, DeskTop), f.Face, solid: false);
                        b.Item(f.P(R(-0.5f, 0.5f), 0.5f, DeskTop + 0.005f));
                        if (!Chance(0.5)) return 1.65f;
                        b.Add(PropKind.FilePedestal, f.P(1.05f, BackOf(PropKind.FilePedestal)), f.Face); // its drawers beside it
                        return 2.55f;
                    }
                    if (roll < 32)
                    {   // the manager's desk: bookcase behind, the desk facing the room, a guest chair
                        b.Add(PropKind.Bookshelf, f.P(0, BackOf(PropKind.Bookshelf)), f.Face);
                        b.Add(PropKind.Chair, f.P(0, 0.85f), f.Face);
                        b.Add(PropKind.Desk, f.P(0, 1.6f), f.Back);
                        b.Add(PropKind.GuestChair, f.P(0, 2.4f), f.Back);
                        b.Item(f.P(R(-0.5f, 0.5f), 1.6f, DeskTop + 0.005f));
                        return 1.65f;
                    }
                    if (roll < 42)
                    {   // the boss's suite: credenza at the wall, the big desk in front of it facing the door, two guest chairs
                        b.Add(PropKind.Credenza, f.P(0, BackOf(PropKind.Credenza)), f.Face);
                        b.Add(PropKind.Chair, f.P(0, 0.9f), f.Face);
                        b.Add(PropKind.ExecutiveDesk, f.P(0, 1.72f), f.Face);
                        b.Add(PropKind.GuestChair, f.P(-0.45f, 2.55f), f.Back);
                        b.Add(PropKind.GuestChair, f.P(0.45f, 2.55f), f.Back);
                        b.Item(f.P(R(-0.5f, 0.5f), 1.7f, 0.765f));
                        b.Item(f.P(R(-0.5f, 0.5f), BackOf(PropKind.Credenza), 0.755f));
                        return Clock(1.85f);
                    }
                    if (roll < 50) { Single(PropKind.Safe, 0.85f); b.Item(f.P(0, BackOf(PropKind.Safe), 1.02f)); return 0.85f; }
                    if (roll < 58) { b.Add(PropKind.Bookshelf, f.P(-0.51f, BackOf(PropKind.Bookshelf)), f.Face); b.Add(PropKind.Bookshelf, f.P(0.51f, BackOf(PropKind.Bookshelf)), f.Face); return 2.05f; }
                    if (roll < 66) { Row(f, b, PropKind.FilingCabinet, 2, 0.5f); b.Item(f.P(R(-0.2f, 0.2f), BackOf(PropKind.FilingCabinet), 1.305f)); return Clock(1.05f); }
                    if (roll < 73) { Single(PropKind.Credenza, 1.65f); b.Item(f.P(R(-0.5f, 0.5f), BackOf(PropKind.Credenza), 0.755f)); return Clock(1.65f); }
                    if (roll < 80) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return Clock(0.95f); }
                    if (roll < 86 && !m_Printer) { m_Printer = true; return Single(PropKind.Printer, 0.65f); }
                    if (roll < 93) return Row(f, b, PropKind.GuestChair, 2, 0.65f);
                    return Single(PropKind.Lockers, 1.05f);
                case RoomType.OpenSpace:
                    if (roll < 8 && !m_Printer) { m_Printer = true; return Single(PropKind.Printer, 0.65f); } // one per room, rarely
                    if (roll < 30) { b.Add(PropKind.Cabinet, f.P(-0.475f, BackOf(PropKind.Cabinet)), f.Face); b.Add(PropKind.Cabinet, f.P(0.475f, BackOf(PropKind.Cabinet)), f.Face); b.Item(f.P(0.4f, BackOf(PropKind.Cabinet), CabinetTop)); return Clock(1.9f); }
                    if (roll < 40) { Single(PropKind.StandingDesk, 1.45f); b.Item(f.P(R(-0.4f, 0.4f), BackOf(PropKind.StandingDesk), 1.105f)); return 1.45f; }
                    if (roll < 48) return Row(f, b, PropKind.FilePedestal, 2, 0.45f);
                    if (roll < 56) return Single(PropKind.MobileWhiteboard, 1.45f);
                    if (roll < 64) return Single(PropKind.SofaPod, 1.85f);
                    if (roll < 72) return Single(PropKind.WaterCooler, 0.4f);
                    if (roll < 82) { b.Add(PropKind.Desk, f.P(0, 0.4f + WallGap), f.Face); b.Add(PropKind.Chair, f.P(0, 1.2f), f.Back); b.Item(f.P(0.3f, 0.5f, DeskTop + 0.005f)); return 1.65f; }
                    if (roll < 92) return Single(PropKind.PlanterDivider, 1.65f);
                    return Single(PropKind.TrashBin, 0.45f);
                case RoomType.Meeting:
                    if (roll < 20) { Single(PropKind.TvStand, 1.45f); return 1.45f; }
                    if (roll < 34) { b.Add(PropKind.Whiteboard, f.P(0, 0.14f), f.Face); return 1.95f; }
                    if (roll < 50)
                    {   // the projector screen over the AV cabinet
                        b.Add(PropKind.AvCredenza, f.P(0, BackOf(PropKind.AvCredenza)), f.Face);
                        b.Add(PropKind.ProjectorScreen, f.P(0, BackOf(PropKind.ProjectorScreen)), f.Face, solid: false);
                        b.Item(f.P(R(-0.6f, 0.1f), BackOf(PropKind.AvCredenza), 0.755f));
                        return 2.05f;
                    }
                    if (roll < 60) return Single(PropKind.Flipchart, 0.75f);
                    if (roll < 70) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return Clock(0.95f); }
                    if (roll < 78) return Single(PropKind.WaterCooler, 0.4f);
                    if (roll < 90) return Row(f, b, PropKind.MeetingChair, 2, 0.62f);
                    { Single(PropKind.Credenza, 1.65f); b.Item(f.P(R(-0.5f, 0.5f), BackOf(PropKind.Credenza), 0.755f)); return Clock(1.65f); }
                case RoomType.Kitchen:
                    if (!m_Fridge) return Single(PropKind.Fridge, 0.75f); // every kitchen has its fridge
                    if (!m_Table)
                    {   // and its table: by the wall, a chair on each side of it
                        var table = f.P(0, 0.5f + WallGap);
                        b.Add(PropKind.RoundTable, table, 0);
                        foreach (var d in new[] { f.U, -f.U }) b.Add(PropKind.Chair, table + d * 0.95f, Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg);
                        b.Item(table + Vector3.up * 0.76f);
                        return 2.6f;
                    }
                    if (roll < 26)
                    {   // the kitchen run: sink, cooker (or the dishwasher), worktop with the microwave and the coffee machine, cupboards over it
                        b.Add(PropKind.KitchenSink, f.P(-1.52f, BackOf(PropKind.KitchenSink)), f.Face);
                        var mid = Chance(0.5) ? PropKind.Stove : PropKind.Dishwasher;
                        b.Add(mid, f.P(-0.62f, BackOf(mid)), f.Face);
                        b.Add(PropKind.KitchenCounter, f.P(0.92f, BackOf(PropKind.KitchenCounter)), f.Face);
                        // the counter has its microwave at its local −X: the coffee machine on the other end
                        var cx = Quaternion.Euler(0, f.Face, 0) * Vector3.right;
                        b.Add(PropKind.CoffeeMachine, f.P(0.92f, BackOf(PropKind.KitchenCounter) + 0.05f, 0.93f) + cx * 0.75f, f.Face, solid: false);
                        b.Add(PropKind.WallCabinet, f.P(-1.52f, BackOf(PropKind.WallCabinet)), f.Face, solid: false);
                        b.Add(PropKind.WallCabinet, f.P(0.92f, BackOf(PropKind.WallCabinet)), f.Face, solid: false);
                        b.Item(f.P(0.9f, BackOf(PropKind.KitchenCounter), 0.93f));
                        return 4.3f;
                    }
                    if (roll < 40)
                    {   // a shorter run: the sink and a worktop
                        b.Add(PropKind.KitchenSink, f.P(-1.22f, BackOf(PropKind.KitchenSink)), f.Face);
                        b.Add(PropKind.KitchenCounter, f.P(0.62f, BackOf(PropKind.KitchenCounter)), f.Face);
                        b.Add(PropKind.WallCabinet, f.P(-1.22f, BackOf(PropKind.WallCabinet)), f.Face, solid: false);
                        b.Item(f.P(0.3f, BackOf(PropKind.KitchenCounter), 0.93f));
                        return 3.7f;
                    }
                    // the fridge is there already (one a kitchen): drawers, a pantry, the water, the recycling, the bin
                    if (roll < 50) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return 0.95f; }
                    if (roll < 58) { Single(PropKind.PantryCabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.PantryCabinet), 0.915f)); return 0.95f; }
                    if (roll < 66) return Single(PropKind.RecyclingBins, 1.25f);
                    if (roll < 72) return Single(PropKind.WaterCooler, 0.4f);
                    if (roll < 84)
                    {   // a table pushed against the wall, two chairs at it
                        b.Add(PropKind.DiningTable, f.P(0, 0.45f + WallGap), f.Face);
                        b.Add(PropKind.Chair, f.P(-0.42f, 1.25f), f.Back);
                        b.Add(PropKind.Chair, f.P(0.42f, 1.25f), f.Back);
                        b.Item(f.P(R(-0.5f, 0.5f), 0.4f, 0.755f));
                        return Clock(1.65f);
                    }
                    if (roll < 92) return Single(PropKind.TrashBin, 0.45f);
                    { Single(PropKind.PantryCabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.PantryCabinet), 1.315f)); return 0.95f; }
                case RoomType.Server:
                    if (roll < 45)
                    {
                        for (int i = -1; i <= 1; i++) b.Add(PropKind.ServerRack, f.P(i * 0.72f, BackOf(PropKind.ServerRack)), f.Face);
                        return 2.2f;
                    }
                    if (roll < 56) return Row(f, b, PropKind.NetworkCabinet, 2, 0.62f);
                    if (roll < 66) return Single(PropKind.CracUnit, 1.05f);
                    if (roll < 75) return Single(PropKind.TapeLibrary, 1.25f);
                    if (roll < 84) { Row(f, b, PropKind.UpsUnit, 2, 0.55f); b.Item(f.P(R(-0.2f, 0.2f), BackOf(PropKind.UpsUnit), 0.985f)); return 1.15f; }
                    if (roll < 92) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return 0.95f; }
                    { Single(PropKind.CrashCart, 0.65f); b.Item(f.P(0, BackOf(PropKind.CrashCart), 0.815f)); return 0.65f; }
                case RoomType.Workshop:
                    // the benches and machines along the walls behind the bays: a real shop's equipment list
                    if (roll < 22) { Single(PropKind.ToolChest, 1.2f); b.Item(f.P(R(-0.3f, 0.3f), BackOf(PropKind.ToolChest), 1.06f)); return 1.2f; }
                    if (roll < 34) return Single(PropKind.TireRack, 2.1f);
                    if (roll < 42) return Single(PropKind.TireChanger, 1.2f);
                    if (roll < 50) return Single(PropKind.WheelBalancer, 1.2f);
                    if (roll < 56) return Single(PropKind.AirCompressor, 1.3f);
                    if (roll < 64) { Single(PropKind.OilDrums, 1.25f); return 1.25f; }
                    if (roll < 70) return Single(PropKind.WeldingCart, 0.7f);
                    if (roll < 76) return Single(PropKind.EngineHoist, 1.0f);
                    if (roll < 86) { Single(PropKind.WorkTable, 1.7f); b.Item(f.P(R(-0.5f, 0.5f), BackOf(PropKind.WorkTable), 0.92f)); return 1.7f; }
                    if (roll < 93) return Hung(PropKind.HoseReel, 0.6f);
                    { Single(PropKind.MetalShelving, 1.85f); if (Chance(0.7)) b.Item(f.P(R(-0.6f, 0.6f), BackOf(PropKind.MetalShelving), StorageDecks[m_Rng.Next(3)])); return 1.85f; }
                case RoomType.Ward:
                    if (roll < 25) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return 0.95f; }
                    if (roll < 45) return Single(PropKind.GuestChair, 0.7f);
                    if (roll < 60) return Single(PropKind.VitalsMonitor, 0.7f);
                    if (roll < 72) return Hung(PropKind.HandSanitizer, 0.3f);
                    if (roll < 86) return Single(PropKind.Wheelchair, 0.75f);
                    return Single(PropKind.IvStand, 0.5f);
                case RoomType.NurseStation:
                    if (roll < 30) { Single(PropKind.MedicationCart, 1.05f); b.Item(f.P(0, BackOf(PropKind.MedicationCart), 1.06f)); return 1.05f; }
                    if (roll < 50) return Single(PropKind.LinenCart, 0.75f);
                    if (roll < 70) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return 0.95f; }
                    if (roll < 85) return Single(PropKind.VitalsMonitor, 0.7f);
                    return Hung(PropKind.HandSanitizer, 0.3f);
                case RoomType.Operating:
                    if (roll < 30) return Single(PropKind.ScrubSink, 1.6f);
                    if (roll < 55) { Single(PropKind.MedicationCart, 1.05f); b.Item(f.P(0, BackOf(PropKind.MedicationCart), 1.06f)); return 1.05f; }
                    if (roll < 75) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return 0.95f; }
                    if (roll < 88) return Single(PropKind.LinenCart, 0.75f);
                    return Hung(PropKind.HandSanitizer, 0.3f);
                case RoomType.Imaging:
                    if (roll < 35) { Single(PropKind.Desk, 1.45f); b.Item(f.P(R(-0.4f, 0.4f), 0.4f + WallGap, DeskTop + 0.005f)); return 1.45f; }
                    if (roll < 60) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return 0.95f; }
                    if (roll < 80) return Single(PropKind.Wheelchair, 0.75f);
                    return Hung(PropKind.HandSanitizer, 0.3f);
                case RoomType.PaintBooth:
                    if (roll < 50) { Single(PropKind.PaintRack, 1.45f); b.Item(f.P(-0.3f, BackOf(PropKind.PaintRack), 0.95f)); return 1.45f; }
                    if (roll < 70) return Single(PropKind.ToolChest, 1.2f);
                    if (roll < 85) return Hung(PropKind.HoseReel, 0.6f);
                    return Single(PropKind.TireStack, 0.85f);
                case RoomType.Storage:
                    if (m_L.Kind == HouseKind.Garage && roll < 30) return Single(PropKind.TireRack, 2.1f); // the parts store keeps tyres too
                    if (roll < 86)
                    {
                        // empty pallet racks all round (no boxes on them: the decks are for loot): now and then a thing
                        // on an upper deck from the start, the bottom deck for the workers — three places along it
                        b.Add(PropKind.StorageRack, f.P(0, BackOf(PropKind.StorageRack)), f.Face);
                        if (Chance(0.5)) b.Item(f.P(R(-0.6f, 0.6f), BackOf(PropKind.StorageRack), ShelfLevels[1 + m_Rng.Next(2)]));
                        foreach (float along in new[] { -0.62f, 0f, 0.62f })
                            b.Drops.Add(new StorageDrop { Spot = f.P(along, BackOf(PropKind.StorageRack), Enemies.CargoMotion.ShelfTop + 0.02f), Facing = -f.N, Shelf = true });
                        return 2.05f;
                    }
                    // the rest of the walls: boxes, drums, crates
                    if (roll < 91) { b.Add(PropKind.Boxes, f.P(-0.2f, 0.45f), f.Face + m_Rng.Next(4) * 90f); return 1.2f; }
                    if (roll < 95) return Single(PropKind.DrumPair, 0.95f);
                    if (roll < 98) { Single(PropKind.CrateStack, 1.25f); b.Item(f.P(R(-0.3f, 0.3f), BackOf(PropKind.CrateStack), 1.385f)); return 1.25f; }
                    return Single(PropKind.RollingCage, 0.95f);
                case RoomType.Restroom:
                    if (roll < 38)
                    {
                        for (int i = -1; i <= 1; i++) b.Add(PropKind.ToiletStall, f.P(i * 1.06f, BackOf(PropKind.ToiletStall)), f.Face);
                        b.Item(f.P(0, 2.1f));
                        return 3.2f;
                    }
                    if (roll < 52)
                    {   // basins with mirrors, the hand dryer beside them
                        b.Add(PropKind.MirrorSink, f.P(-0.62f, BackOf(PropKind.MirrorSink)), f.Face);
                        b.Add(PropKind.MirrorSink, f.P(0.18f, BackOf(PropKind.MirrorSink)), f.Face);
                        b.Add(PropKind.HandDryer, f.P(0.82f, BackOf(PropKind.HandDryer)), f.Face, solid: false);
                        if (Chance(0.4)) b.Item(f.P(-0.62f, BackOf(PropKind.MirrorSink), 0.86f));
                        return 2.0f;
                    }
                    if (roll < 64)
                    {   // the double vanity, the towels beside it
                        b.Add(PropKind.DoubleSink, f.P(-0.15f, BackOf(PropKind.DoubleSink)), f.Face);
                        b.Add(PropKind.TowelDispenser, f.P(0.82f, BackOf(PropKind.TowelDispenser)), f.Face, solid: false);
                        if (Chance(0.4)) b.Item(f.P(-0.15f, BackOf(PropKind.DoubleSink) + 0.05f, 0.855f));
                        return 2.0f;
                    }
                    if (roll < 74) return Row(f, b, PropKind.Urinal, 3, 0.7f);
                    if (roll < 82) return Single(PropKind.JanitorCart, 0.95f);
                    if (roll < 88) return Hung(PropKind.ChangingTable, 0.95f);
                    if (roll < 94) return Single(PropKind.SanitaryBin, 0.35f);
                    return Single(PropKind.TrashBin, 0.45f);
                case RoomType.Lounge:
                    if (roll < 16)
                    {
                        b.Add(PropKind.TvStand, f.P(0, BackOf(PropKind.TvStand)), f.Face);
                        b.Add(PropKind.Armchair, f.P(-0.6f, 2.0f), f.Back);
                        b.Add(PropKind.Armchair, f.P(0.6f, 2.0f), f.Back);
                        return 1.45f;
                    }
                    if (roll < 30)
                    {
                        b.Add(PropKind.Sofa, f.P(0, BackOf(PropKind.Sofa)), f.Face);
                        b.Add(PropKind.CoffeeTable, f.P(0, 1.35f), f.Face);
                        b.Item(f.P(0, BackOf(PropKind.Sofa), 0.46f));
                        b.Item(f.P(0, 1.35f, 0.42f));
                        return Clock(2.25f);
                    }
                    if (roll < 40)
                    {   // the sectional against the wall, a lamp table at its end
                        b.Add(PropKind.SectionalSofa, f.P(-0.3f, BackOf(PropKind.SectionalSofa)), f.Face);
                        b.Add(PropKind.SideTable, f.P(1.2f, BackOf(PropKind.SideTable)), f.Face);
                        b.Item(f.P(1.2f, BackOf(PropKind.SideTable) + 0.1f, 0.555f));
                        return 3.0f;
                    }
                    if (roll < 47) return Single(PropKind.ArcadeMachine, 0.8f);
                    if (roll < 53) return Single(PropKind.Plant, 0.75f);                  // no vending machines
                    if (roll < 58) return Single(PropKind.WaterCooler, 0.4f);
                    if (roll < 65) { b.Add(PropKind.BeanBag, f.P(0, 0.6f), f.Face); return 0.95f; }
                    if (roll < 76) { b.Add(PropKind.EggChair, f.P(-0.3f, BackOf(PropKind.EggChair)), f.Face); b.Add(PropKind.SideTable, f.P(0.45f, BackOf(PropKind.SideTable)), f.Face); return 1.45f; }
                    if (roll < 84) { Single(PropKind.MagazineRack, 0.65f); b.Item(f.P(0, BackOf(PropKind.MagazineRack), 0.41f)); return 0.65f; }
                    if (roll < 92) return Single(PropKind.FloorLamp, 0.45f);
                    return Single(PropKind.Plant, 0.75f);
                case RoomType.Gym:
                    if (roll < 18) return Row(f, b, PropKind.Treadmill, 2, 0.95f);
                    if (roll < 30) return Row(f, b, PropKind.ExerciseBike, 2, 0.65f);
                    if (roll < 40) { Single(PropKind.DumbbellRack, 1.5f); return 1.5f; }
                    if (roll < 47) { b.Add(PropKind.PunchingBag, f.P(0, 0.75f), f.Face); return 0.8f; }
                    if (roll < 52) return Single(PropKind.WaterCooler, 0.4f);
                    if (roll < 58) return Single(PropKind.Bench, 1.7f);
                    if (roll < 67) return Single(PropKind.KettlebellRack, 1.05f);
                    if (roll < 75) return Single(PropKind.MatRack, 0.85f);
                    if (roll < 85) return Single(PropKind.CableMachine, 1.25f);
                    if (roll < 94) return Along(PropKind.RowingMachine);
                    return Single(PropKind.PowerCage, 1.45f);
                case RoomType.Library:
                    if (roll < 36)
                    {
                        Row(f, b, PropKind.TallBookcase, 2, 1.02f);
                        if (Chance(0.6)) b.Item(f.P(R(-0.8f, 0.8f), BackOf(PropKind.TallBookcase), BookcaseLevels[m_Rng.Next(3)]));
                        return 2.05f;
                    }
                    if (roll < 48)
                    {   // a reading corner: the wingback under a floor lamp
                        b.Add(PropKind.WingbackChair, f.P(-0.3f, BackOf(PropKind.WingbackChair) + 0.1f), f.Face);
                        b.Add(PropKind.FloorLamp, f.P(0.45f, 0.3f), f.Face);
                        return 1.4f;
                    }
                    if (roll < 54) { b.Add(PropKind.Globe, f.P(0, 0.4f), f.Face + R(-40f, 40f)); return 0.7f; }
                    if (roll < 60) return Row(f, b, PropKind.Bookshelf, 2, 1.02f);
                    if (roll < 70) { Single(PropKind.CardCatalog, 0.85f); b.Item(f.P(0, BackOf(PropKind.CardCatalog), 1.305f)); return 0.85f; }
                    if (roll < 82)
                    {   // a study carrel with its chair
                        b.Add(PropKind.StudyCarrel, f.P(0, BackOf(PropKind.StudyCarrel)), f.Face);
                        b.Add(PropKind.Chair, f.P(0, 1.12f), f.Back);
                        b.Item(f.P(R(-0.3f, 0.3f), BackOf(PropKind.StudyCarrel) + 0.1f, 0.765f));
                        return 1.05f;
                    }
                    if (roll < 92) return Single(PropKind.BookCart, 0.8f);
                    { b.Add(PropKind.Lectern, f.P(0, 0.45f), f.Face); return 0.55f; }
                case RoomType.CopyRoom:
                    if (roll < 22) { Single(PropKind.Copier, 1.0f); b.Item(f.P(0.2f, BackOf(PropKind.Copier), 1.05f)); return 1.0f; }
                    if (roll < 36) { Single(PropKind.MailShelf, 1.25f); b.Item(f.P(R(-0.4f, 0.4f), BackOf(PropKind.MailShelf), 0.87f)); return 1.25f; }
                    if (roll < 44) return Single(PropKind.PaperStack, 0.85f);
                    if (roll < 50) return Single(PropKind.Shredder, 0.5f);
                    if (roll < 56) return Single(PropKind.Printer, 0.65f);
                    if (roll < 62) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return Clock(0.95f); }
                    if (roll < 72) return Single(PropKind.Plotter, 1.55f);
                    if (roll < 82) { Single(PropKind.SupplyShelf, 1.05f); b.Item(f.P(R(-0.3f, 0.3f), BackOf(PropKind.SupplyShelf), 0.915f)); return 1.05f; }
                    if (roll < 91) return Single(PropKind.PaperBin, 0.65f);
                    return Single(PropKind.MailCart, 0.95f);
                case RoomType.ControlRoom:
                    if (roll < 30)
                    {   // another robots' control console and its operator (LevelGenerator.PlaceTerminals; spare ones are desks)
                        b.Add(PropKind.Terminal, f.P(0, BackOf(PropKind.Terminal)), f.Face);
                        b.Add(PropKind.OperatorChair, f.P(0, BackOf(PropKind.Terminal) + 0.85f), f.Back);
                        return 1.7f;
                    }
                    if (roll < 50)
                    {   // the video wall, the operators' console in front of it facing it
                        b.Add(PropKind.MonitorWall, f.P(0, BackOf(PropKind.MonitorWall)), f.Face);
                        b.Add(PropKind.ConsoleDesk, f.P(0, 1.55f), f.Face); // its screens face the operators, who face the wall
                        b.Add(PropKind.OperatorChair, f.P(-0.6f, 2.35f), f.Back);
                        b.Add(PropKind.OperatorChair, f.P(0.6f, 2.35f), f.Back);
                        b.Item(f.P(R(-0.9f, 0.9f), 1.45f, 0.77f));
                        return 2.45f;
                    }
                    if (roll < 44) return Row(f, b, PropKind.ServerRack, 2, 0.72f);
                    if (roll < 52) { Single(PropKind.Cabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.Cabinet), CabinetTop)); return 0.95f; }
                    if (roll < 56) return Single(PropKind.WaterCooler, 0.4f);
                    if (roll < 66) return Row(f, b, PropKind.RadioRack, 2, 0.65f);
                    if (roll < 78)
                    {   // the supervisor at the wall: the desk, its chair in front
                        b.Add(PropKind.SupervisorDesk, f.P(0, BackOf(PropKind.SupervisorDesk)), f.Face);
                        b.Add(PropKind.OperatorChair, f.P(0, 1.3f), f.Back);
                        b.Item(f.P(R(-0.6f, 0.6f), BackOf(PropKind.SupervisorDesk) + 0.2f, 1.105f));
                        return 1.85f;
                    }
                    if (roll < 90) return Hung(PropKind.MapBoard, 2.05f);
                    return Hung(PropKind.AlarmPanel, 0.85f);
                case RoomType.Lab:
                    if (roll < 26)
                    {
                        b.Add(PropKind.LabBench, f.P(0, BackOf(PropKind.LabBench)), f.Face);
                        b.Add(PropKind.Chair, f.P(R(-0.3f, 0.3f), 1.2f), f.Back);
                        b.Item(f.P(R(-0.6f, 0.6f), BackOf(PropKind.LabBench) + 0.1f, 0.91f));
                        return 1.85f;
                    }
                    if (roll < 36) { Single(PropKind.PartsCabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.PartsCabinet), 1.41f)); return 0.95f; }
                    if (roll < 42) { b.Add(PropKind.RobotArm, f.P(0, BackOf(PropKind.RobotArm)), f.Face); return 0.7f; }
                    if (roll < 48) { Single(PropKind.Shelf, 2.05f); if (Chance(0.6)) b.Item(f.P(R(-0.6f, 0.6f), BackOf(PropKind.Shelf), ShelfLevels[m_Rng.Next(3)])); return 2.05f; }
                    if (roll < 52) return Single(PropKind.TrashBin, 0.45f);
                    if (roll < 63) { Single(PropKind.FumeHood, 1.55f); b.Item(f.P(R(-0.4f, 0.4f), BackOf(PropKind.FumeHood) + 0.05f, 0.935f)); return 1.55f; }
                    if (roll < 73) return Single(PropKind.ComponentShelf, 1.25f);
                    if (roll < 82) return Single(PropKind.Printer3d, 0.78f);
                    if (roll < 90) { Single(PropKind.ScopeCart, 0.65f); b.Item(f.P(0.1f, BackOf(PropKind.ScopeCart), 0.955f)); return 0.65f; }
                    {
                        b.Add(PropKind.RobotTable, f.P(0, BackOf(PropKind.RobotTable)), f.Face);
                        b.Add(PropKind.Chair, f.P(0.35f, 1.3f), f.Back);
                        b.Item(f.P(0.35f, BackOf(PropKind.RobotTable), 0.905f));
                        return 1.85f;
                    }
                case RoomType.NapRoom:
                    if (roll < 26) { Row(f, b, PropKind.SleepPod, 2, 1.15f); b.Item(f.P(R(-0.6f, 0.6f), 2.35f)); return 2.3f; }
                    if (roll < 36) { b.Add(PropKind.Hammock, f.P(0, 0.55f), f.Face); return 2.65f; }
                    if (roll < 42) { b.Add(PropKind.BeanBag, f.P(0, 0.6f), f.Face); return 0.95f; }
                    if (roll < 46) return Single(PropKind.FloorLamp, 0.45f);
                    if (roll < 60)
                    {   // the bunk bed lengthwise along the wall, the night table at its head
                        float w = Along(PropKind.BunkBed, 0.3f);
                        b.Add(PropKind.NightTable, f.P(0.3f - w / 2f - 0.3f, BackOf(PropKind.NightTable)), f.Face);
                        b.Item(f.P(0.3f, 0.5f, 0.505f));
                        return w + 0.6f;
                    }
                    if (roll < 74)
                    {
                        b.Add(PropKind.Daybed, f.P(-0.3f, BackOf(PropKind.Daybed)), f.Face);
                        b.Add(PropKind.NightTable, f.P(1.0f, BackOf(PropKind.NightTable)), f.Face);
                        b.Item(f.P(1.0f, BackOf(PropKind.NightTable), 0.405f));
                        return 2.6f;
                    }
                    if (roll < 84) { b.Add(PropKind.FloorCushions, f.P(0, 0.7f), f.Face); return 1.2f; }
                    if (roll < 92) return Single(PropKind.NightTable, 0.55f);
                    return Single(PropKind.Plant, 0.75f);
                case RoomType.Studio:
                    if (roll < 20)
                    {
                        b.Add(PropKind.MixingDesk, f.P(0, BackOf(PropKind.MixingDesk)), f.Face);
                        b.Add(PropKind.Chair, f.P(0, 1.2f), f.Back);
                        b.Item(f.P(R(-0.6f, 0.6f), BackOf(PropKind.MixingDesk) + 0.25f, 0.76f));
                        return 1.65f;
                    }
                    if (roll < 34) { b.Add(PropKind.AcousticPanel, f.P(-0.62f, BackOf(PropKind.AcousticPanel)), f.Face, solid: false); b.Add(PropKind.AcousticPanel, f.P(0.62f, BackOf(PropKind.AcousticPanel)), f.Face, solid: false); return 2.45f; }
                    if (roll < 42) { b.Add(PropKind.DrumKit, f.P(0, 0.75f), f.Face); return 1.65f; }
                    if (roll < 48) { b.Add(PropKind.MicStand, f.P(0, 0.5f), f.Face); return 0.55f; }
                    if (roll < 56) { Single(PropKind.StudioCouch, 2.05f); b.Item(f.P(R(-0.6f, 0.6f), BackOf(PropKind.StudioCouch), 0.465f)); return 2.05f; }
                    if (roll < 66) { b.Add(PropKind.SynthStand, f.P(0, BackOf(PropKind.SynthStand)), f.Face); b.Add(PropKind.BarStool, f.P(0, 0.95f), f.Back); return 1.35f; }
                    if (roll < 76) { b.Add(PropKind.AmpCabinet, f.P(-0.25f, BackOf(PropKind.AmpCabinet)), f.Face); b.Add(PropKind.GuitarStand, f.P(0.4f, BackOf(PropKind.GuitarStand) + 0.05f), f.Face); b.Item(f.P(-0.25f, BackOf(PropKind.AmpCabinet), 0.675f)); return 1.2f; }
                    if (roll < 84) return Row(f, b, PropKind.GuitarStand, 2, 0.5f);
                    if (roll < 93) return Single(PropKind.VocalBooth, 1.25f);
                    return Single(PropKind.AmpCabinet, 0.75f);
                case RoomType.LockerRoom:
                    if (roll < 26) return Row(f, b, PropKind.Lockers, 3, 1.05f);
                    if (roll < 38) return Row(f, b, PropKind.ShowerStall, 2, 0.98f);
                    if (roll < 47)
                    {
                        b.Add(PropKind.MirrorSink, f.P(-0.25f, BackOf(PropKind.MirrorSink)), f.Face);
                        b.Add(PropKind.HandDryer, f.P(0.45f, BackOf(PropKind.HandDryer)), f.Face, solid: false);
                        return 1.2f;
                    }
                    if (roll < 56) { b.Add(PropKind.Bench, f.P(0, 0.35f), f.Face); if (Chance(0.4)) b.Item(f.P(R(-0.5f, 0.5f), 0.35f, 0.46f)); return 1.65f; }
                    if (roll < 60) return Single(PropKind.TrashBin, 0.45f);
                    if (roll < 70) { Single(PropKind.TowelShelf, 0.95f); b.Item(f.P(R(-0.3f, 0.3f), BackOf(PropKind.TowelShelf), 0.865f)); return 0.95f; }
                    if (roll < 80) { Single(PropKind.ShoeRack, 1.05f); b.Item(f.P(R(-0.3f, 0.3f), BackOf(PropKind.ShoeRack), 0.605f)); return 1.05f; }
                    if (roll < 88) return Single(PropKind.WeighingScale, 0.45f);
                    return Single(PropKind.LaundryCart, 0.85f);
                case RoomType.Infirmary:
                    if (roll < 22) { Row(f, b, PropKind.HospitalBed, 2, 1.15f); b.Item(f.P(R(-0.5f, 0.5f), 1.2f, 0.66f)); return 2.3f; }
                    if (roll < 34) { Single(PropKind.MedicineCabinet, 0.95f); b.Item(f.P(0, BackOf(PropKind.MedicineCabinet), 1.65f)); return 0.95f; }
                    if (roll < 41) { b.Add(PropKind.PrivacyScreen, f.P(0, 0.35f), f.Face); return 1.65f; }
                    if (roll < 47) { Single(PropKind.MirrorSink, 0.75f); return 0.75f; }
                    if (roll < 52) return Single(PropKind.Cabinet, 0.95f);
                    if (roll < 64)
                    {   // the doctor's desk at the wall, the doctor's chair in front of it
                        b.Add(PropKind.DoctorDesk, f.P(0, BackOf(PropKind.DoctorDesk)), f.Face);
                        b.Add(PropKind.Chair, f.P(-0.3f, 1.1f), f.Back);
                        b.Item(f.P(0.35f, BackOf(PropKind.DoctorDesk) + 0.1f, 0.765f));
                        return Clock(1.45f);
                    }
                    if (roll < 74) { float w = Along(PropKind.ExamTable); b.Item(f.P(0, 0.35f + WallGap, 0.725f)); return w; }
                    if (roll < 80) return Hung(PropKind.DefibCabinet, 0.45f);
                    if (roll < 90) { b.Add(PropKind.Wheelchair, f.P(0, BackOf(PropKind.Wheelchair)), f.Face); return 0.7f; }
                    return Single(PropKind.IvStand, 0.5f);
                case RoomType.GameRoom:
                    if (roll < 12) { b.Add(PropKind.DartBoard, f.P(0, BackOf(PropKind.DartBoard)), f.Face, solid: false); return 0.65f; }
                    if (roll < 24) return Row(f, b, PropKind.ArcadeMachine, 2, 0.8f);
                    if (roll < 36)
                    {   // the bar with stools in front of it
                        b.Add(PropKind.BarCounter, f.P(0, BackOf(PropKind.BarCounter)), f.Face);
                        b.Add(PropKind.BarStool, f.P(-0.55f, 0.95f), f.Back);
                        b.Add(PropKind.BarStool, f.P(0.55f, 0.95f), f.Back);
                        b.Item(f.P(R(-0.7f, 0.7f), BackOf(PropKind.BarCounter), 1.06f));
                        return 2.05f;
                    }
                    if (roll < 46)
                    {
                        b.Add(PropKind.Sofa, f.P(0, BackOf(PropKind.Sofa)), f.Face);
                        b.Add(PropKind.CoffeeTable, f.P(0, 1.35f), f.Face);
                        b.Item(f.P(0, 1.35f, 0.42f));
                        return 2.25f;
                    }
                    if (roll < 52) return Single(PropKind.VendingMachine, 0.95f);
                    if (roll < 56) { b.Add(PropKind.BeanBag, f.P(0, 0.6f), f.Face); return 0.95f; }
                    if (roll < 66) return Single(PropKind.Jukebox, 0.85f);
                    if (roll < 78) return Row(f, b, PropKind.Pinball, 2, 0.8f);
                    if (roll < 90)
                    {   // the console corner: the TV on its stand, two bean bags in front of it
                        b.Add(PropKind.GamingCorner, f.P(0, BackOf(PropKind.GamingCorner)), f.Face);
                        // turned by 15° a 0.9 m bean bag takes 1.1 m: 1.16 m apart, or the two sink into each other
                        b.Add(PropKind.BeanBag, f.P(-0.58f, 1.45f), f.Back + 15f);
                        b.Add(PropKind.BeanBag, f.P(0.58f, 1.45f), f.Back - 15f);
                        b.Item(f.P(0.65f, BackOf(PropKind.GamingCorner), 0.38f));
                        return 1.65f;
                    }
                    return Single(PropKind.Pinball, 0.8f);
            }
            return 0f;
        }

        /// <summary><paramref name="n"/> pieces side by side along a wall, <paramref name="step"/> apart, backs to it; returns the width.</summary>
        float Row(Frame f, Batch b, PropKind k, int n, float step)
        {
            for (int i = 0; i < n; i++) b.Add(k, f.P((i - (n - 1) / 2f) * step, BackOf(k)), f.Face);
            return n * step + 0.05f;
        }

        /// <summary>
        /// A room the rules above left almost bare (small, doors on every wall, windows): its own wall groups once more,
        /// at every spot of every wall, more tries at each, until it has a few pieces for its size.
        /// </summary>
        void Sparse(PlanRoom room)
        {
            if (room.Type is RoomType.Corridor or RoomType.Hall) return;
            int want = Mathf.Clamp(Mathf.RoundToInt(room.Area / 5f), 3, 8);
            int Have() { int n = 0; foreach (var kv in m_InRoom) n += kv.Value; return n; }
            if (Have() >= want) return;
            float y = LevelLayout.FloorY(room.Floor);
            foreach (var (w, n) in FacesOf(room))
            {
                var u = new Vector3(w.Dir.x, 0, w.Dir.y);
                for (float t = 0.15f; t < w.Length - 0.3f;)
                {
                    var face = new Vector3(w.A.x, y, w.A.y) + u * t + n * (LevelMetrics.WallThickness / 2f);
                    float placed = 0f;
                    for (int attempt = 0; attempt < 4 && placed <= 0f; attempt++)
                    {
                        var batch = new Batch();
                        float width = WallGroup(room, batch, face, u, n);
                        if (width <= 0f || t + width > w.Length - 0.1f) continue;
                        var shift = u * (width / 2f);
                        for (int i = 0; i < batch.Pieces.Count; i++) { var p = batch.Pieces[i]; p.Pos += shift; batch.Pieces[i] = p; }
                        for (int i = 0; i < batch.Items.Count; i++) batch.Items[i] += shift;
                        if (Deep(room, face + shift, n, batch, Aisle) && TryCommit(room, batch, front: n)) placed = width;
                    }
                    if (placed > 0f)
                    {
                        if (Have() >= want) return;
                        t += placed + 0.15f;
                    }
                    else t += 0.25f;
                }
            }
        }

        // ------------------------------------------------------------------ corners

        /// <summary>
        /// A piece the room must have and the walls had no run for (a kitchen's fridge): against any bit of wall, its back to
        /// it, wherever it fits.
        /// </summary>
        void AgainstAnyWall(PlanRoom room, PropKind kind, bool walkway = false)
        {
            var b = room.Bounds;
            float y = LevelLayout.FloorY(room.Floor);
            var dirs = new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            for (float x = b.xMin + 0.25f; x < b.xMax; x += 0.25f)
                for (float z = b.yMin + 0.25f; z < b.yMax; z += 0.25f)
                {
                    if (!InRoom(room.Floor, room.Id, x, z)) continue;
                    foreach (var d in dirs)
                    {
                        // the wall right behind it (a model's back sits on BackOf from the wall's face)
                        var behind = new Vector3(x, 0f, z) + d * (BackOf(kind) + 0.15f);
                        if (InRoom(room.Floor, room.Id, behind.x, behind.z)) continue;
                        var batch = new Batch();
                        batch.Add(kind, new Vector3(x, y, z), Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg);
                        if (TryCommit(room, batch, 0.05f, front: walkway ? -d : null)) return;
                    }
                }
        }

        /// <summary>Plants, lamps, bins, stands and the room's own odds and ends in the free inner corners of a room.</summary>
        void Corners(PlanRoom room)
        {
            if (room.Type == RoomType.Kitchen && !m_Fridge) AgainstAnyWall(room, PropKind.Fridge);
            if (room.Type is RoomType.Corridor or RoomType.Server) return;
            var b = room.Bounds;
            float y = LevelLayout.FloorY(room.Floor);
            int placed = 0, max = Mathf.Clamp(Mathf.RoundToInt(room.Area / 16f), 1, 7);
            for (float x = b.xMin + 0.4f; x < b.xMax && placed < max; x += 0.5f)
                for (float z = b.yMin + 0.4f; z < b.yMax && placed < max; z += 0.5f)
                {
                    if (!InRoom(room.Floor, room.Id, x, z)) continue;
                    // a corner: room cells on two sides, a wall within 0.6 m on two perpendicular sides
                    bool wx = !InRoom(room.Floor, room.Id, x - 0.6f, z) || !InRoom(room.Floor, room.Id, x + 0.6f, z);
                    bool wz = !InRoom(room.Floor, room.Id, x, z - 0.6f) || !InRoom(room.Floor, room.Id, x, z + 0.6f);
                    if (!wx || !wz || !Chance(0.45)) continue;
                    PropKind Pick(params PropKind[] kinds) => kinds[m_Rng.Next(kinds.Length)];
                    var kind = room.Type switch
                    {
                        RoomType.Lounge => Pick(PropKind.FloorLamp, PropKind.Plant, PropKind.PlanterLarge),
                        RoomType.Lobby => Pick(PropKind.FloorLamp, PropKind.PlanterLarge, PropKind.UmbrellaStand),
                        RoomType.Hall => Pick(PropKind.PlanterLarge, PropKind.UmbrellaStand, PropKind.Plant),
                        RoomType.Office => Pick(PropKind.Plant, PropKind.CoatRack, PropKind.FilingCabinet),
                        RoomType.OpenSpace => Pick(PropKind.Plant, PropKind.TrashBin, PropKind.FilePedestal),
                        RoomType.Meeting => Pick(PropKind.Plant, PropKind.Flipchart),
                        RoomType.Kitchen => Pick(PropKind.TrashBin, PropKind.Plant),
                        RoomType.Restroom => Pick(PropKind.SanitaryBin, PropKind.TrashBin),
                        RoomType.Storage => Pick(PropKind.Boxes, PropKind.HandTruck, PropKind.DrumPair),
                        // the themed rooms' own odds and ends
                        RoomType.Gym => Pick(PropKind.PunchingBag, PropKind.WaterCooler),
                        RoomType.Library => Pick(PropKind.Globe, PropKind.FloorLamp, PropKind.Lectern),
                        RoomType.CopyRoom => Pick(PropKind.Shredder, PropKind.PaperStack, PropKind.PaperBin),
                        RoomType.ControlRoom => Pick(PropKind.Plant, PropKind.WaterCooler),
                        RoomType.Lab => Pick(PropKind.RobotArm, PropKind.TrashBin, PropKind.ScopeCart),
                        RoomType.NapRoom => Pick(PropKind.BeanBag, PropKind.FloorLamp, PropKind.NightTable),
                        RoomType.Studio => Pick(PropKind.MicStand, PropKind.FloorLamp, PropKind.GuitarStand, PropKind.AmpCabinet),
                        RoomType.Infirmary => Pick(PropKind.PrivacyScreen, PropKind.Plant, PropKind.IvStand, PropKind.Wheelchair),
                        RoomType.GameRoom => Pick(PropKind.BarStool, PropKind.BeanBag),
                        RoomType.LockerRoom => Pick(PropKind.TrashBin, PropKind.LaundryCart, PropKind.WeighingScale),
                        _ => Chance(0.7) ? PropKind.Plant : PropKind.TrashBin,
                    };
                    var p = new Vector3(x, y, z);
                    var batch = new Batch();
                    batch.Add(kind, p, m_Rng.Next(4) * 90f);
                    if (TryCommit(room, batch, 0.05f)) placed++;
                }
        }
    }
}
