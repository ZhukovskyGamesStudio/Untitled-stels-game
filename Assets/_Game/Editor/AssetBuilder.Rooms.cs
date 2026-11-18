using System.Collections.Generic;
using System.Linq;
using Stealth.Configs;
using Stealth.Level;
using UnityEditor;
using UnityEngine;

namespace Stealth.EditorTools
{
    public static partial class AssetBuilder
    {
        /// <summary>
        /// The furniture of the themed rooms (Tools/Blender/rooms_*.py → Art/Models/Props): kind, model file and the
        /// materials of its parts by role — Body, Accent, Trim (defaults to Accent), Eye (glows). Placed by the Furnisher.
        /// </summary>
        static readonly (PropKind kind, string file, string body, string accent, string trim, string eye)[] RoomPropTable =
        {
            (PropKind.KitchenSink, "kitchen_sink", "PropBody", "Metal", "Wood", "Metal"),
            (PropKind.Stove, "stove", "Ceiling", "Metal", "Metal", "PropScreen"),
            (PropKind.Microwave, "microwave", "Ceiling", "Metal", "Metal", "PropScreen"),
            (PropKind.WallCabinet, "wall_cabinet", "PropBody", "Metal", "Metal", "Metal"),
            (PropKind.Urinal, "urinal", "Ceiling", "Metal", "Metal", "Metal"),
            (PropKind.MirrorSink, "mirror_sink", "Ceiling", "Metal", "Metal", "Mirror"),
            (PropKind.HandDryer, "hand_dryer", "Ceiling", "Metal", "Metal", "LampBulb"),
            (PropKind.Bench, "bench", "Wood", "Metal", "Metal", "Metal"),
            (PropKind.ShowerStall, "shower_stall", "Ceiling", "Metal", "Metal", "StairGlass"),
            (PropKind.Treadmill, "treadmill", "Rack", "Rubber", "Metal", "PropScreen"),
            (PropKind.ExerciseBike, "exercise_bike", "Rack", "Rubber", "Metal", "PropScreen"),
            (PropKind.WeightBench, "weight_bench", "Rack", "Rubber", "Metal", "PropScreen"),
            (PropKind.DumbbellRack, "dumbbell_rack", "Rack", "Rubber", "Metal", "PropScreen"),
            (PropKind.PunchingBag, "punching_bag", "Rubber", "Vending", "Metal", "Metal"),
            (PropKind.HospitalBed, "hospital_bed", "Ceiling", "Fabric", "Metal", "Metal"),
            (PropKind.MedicineCabinet, "medicine_cabinet", "Ceiling", "Metal", "PropAccent", "StairGlass"),
            (PropKind.PrivacyScreen, "privacy_screen", "Metal", "Fabric", "Fabric", "Metal"),
            (PropKind.SleepPod, "sleep_pod", "Ceiling", "Fabric", "Fabric", "LampBulb"),
            (PropKind.Hammock, "hammock", "Metal", "Fabric", "Fabric", "Metal"),
            (PropKind.TallBookcase, "tall_bookcase", "Wood", "Folders", "PropAccent", "Folders"),
            (PropKind.ReadingTable, "reading_table", "Wood", "Metal", "Folders", "LampBulb"),
            (PropKind.Globe, "globe", "Wood", "Metal", "PropAccent", "Metal"),
            (PropKind.Copier, "copier", "Ceiling", "Rack", "Rack", "PropScreen"),
            (PropKind.PaperStack, "paper_stack", "Cardboard", "Ceiling", "Wood", "Ceiling"),
            (PropKind.MailShelf, "mail_shelf", "Wood", "Ceiling", "Cardboard", "Ceiling"),
            (PropKind.Shredder, "shredder", "Rack", "Metal", "Metal", "LampBulb"),
            (PropKind.MonitorWall, "monitor_wall", "Rack", "Rack", "Rack", "PropScreen"),
            (PropKind.ConsoleDesk, "console_desk", "PropBody", "Rack", "Rack", "PropScreen"),
            (PropKind.MixingDesk, "mixing_desk", "Wood", "Rack", "Metal", "PropScreen"),
            (PropKind.MicStand, "mic_stand", "Rack", "Metal", "Fabric", "Metal"),
            (PropKind.AcousticPanel, "acoustic_panel", "Fabric", "Rack", "Rack", "Rack"),
            (PropKind.DrumKit, "drum_kit", "Ceiling", "Metal", "Metal", "Metal"),
            (PropKind.PoolTable, "pool_table", "Wood", "Felt", "PropAccent", "Felt"),
            (PropKind.Foosball, "foosball", "Wood", "Metal", "PropAccent", "Metal"),
            (PropKind.DartBoard, "dart_board", "Wood", "PropAccent", "Rack", "PropAccent"),
            (PropKind.BarCounter, "bar_counter", "Wood", "Metal", "Rack", "Metal"),
            (PropKind.BarStool, "bar_stool", "Metal", "Fabric", "Fabric", "Metal"),
            (PropKind.LabBench, "lab_bench", "PropBody", "Rack", "Metal", "PropScreen"),
            (PropKind.PartsCabinet, "parts_cabinet", "Metal", "PropAccent", "Rack", "PropAccent"),
            (PropKind.RobotArm, "robot_arm", "BeanBag", "Rack", "Rack", "LampBulb"), // the arm in safety orange
            // round 2 (Tools/Blender/rooms2_*.py): five more pieces for every room type, and the vault
            (PropKind.StandingDesk, "standing_desk", "Wood", "PropAccent", "PropAccent", "PropScreen"),
            (PropKind.PlanterDivider, "planter_divider", "PlantPot", "Plant", "PropAccent", "PropScreen"),
            (PropKind.MobileWhiteboard, "mobile_whiteboard", "PropAccent", "Metal", "Ceiling", "PropScreen"),
            (PropKind.FilePedestal, "file_pedestal", "PropBody", "PropAccent", "PropAccent", "PropScreen"),
            (PropKind.SofaPod, "sofa_pod", "Fabric", "Rubber", "PropAccent", "PropScreen"),
            (PropKind.ExecutiveDesk, "executive_desk", "Wood", "Metal", "Fabric", "LampBulb"),
            (PropKind.FilingCabinet, "filing_cabinet", "PropBody", "Metal", "Metal", "PropScreen"),
            (PropKind.Credenza, "credenza", "PropBody", "Metal", "Wood", "PropScreen"),
            (PropKind.GuestChair, "guest_chair", "Fabric", "Wood", "Wood", "PropScreen"),
            (PropKind.WallClock, "wall_clock", "Ceiling", "PropAccent", "PropAccent", "PropScreen"),
            (PropKind.ConferenceTable, "conference_table", "Wood", "Metal", "Metal", "PropScreen"),
            (PropKind.ProjectorScreen, "projector_screen", "PropBody", "PropAccent", "Ceiling", "PropScreen"),
            (PropKind.Flipchart, "flipchart", "PropAccent", "Metal", "Ceiling", "PropScreen"),
            (PropKind.AvCredenza, "av_credenza", "PropBody", "Rack", "Wood", "PropScreen"),
            (PropKind.MeetingChair, "meeting_chair", "Fabric", "PropAccent", "Rubber", "PropScreen"),
            (PropKind.InfoKiosk, "info_kiosk", "PropBody", "PropAccent", "PropAccent", "PropScreen"),
            (PropKind.SecurityTurnstile, "security_turnstile", "PropBody", "PropAccent", "StairGlass", "PropScreen"),
            (PropKind.RoundBenchPlanter, "round_bench_planter", "Fabric", "Plant", "PropAccent", "PropScreen"),
            (PropKind.LogoTotem, "logo_totem", "PropBody", "PropAccent", "PropAccent", "PropScreen"),
            (PropKind.QueueBarrier, "queue_barrier", "Metal", "PropAccent", "Vending", "PropScreen"),
            (PropKind.DiningTable, "dining_table", "Wood", "PropAccent", "Wood", "PropScreen"),
            (PropKind.KitchenIsland, "kitchen_island", "PropBody", "Metal", "PropAccent", "PropScreen"),
            (PropKind.Dishwasher, "dishwasher", "PropBody", "Metal", "Wood", "PropScreen"),
            (PropKind.RecyclingBins, "recycling_bins", "Folders", "Plant", "Vending", "PropScreen"),
            (PropKind.PantryCabinet, "pantry_cabinet", "PropBody", "PropAccent", "PropAccent", "PropScreen"),
            (PropKind.DoubleSink, "double_sink", "PropBody", "Metal", "PropAccent", "Mirror"),
            (PropKind.JanitorCart, "janitor_cart", "PropBody", "Rubber", "BeanBag", "PropScreen"),
            (PropKind.ChangingTable, "changing_table", "PropBody", "Metal", "Fabric", "PropScreen"),
            (PropKind.TowelDispenser, "towel_dispenser", "Ceiling", "PropAccent", "Cardboard", "PropScreen"),
            (PropKind.SanitaryBin, "sanitary_bin", "Metal", "PropAccent", "PropAccent", "PropScreen"),
            (PropKind.SectionalSofa, "sectional_sofa", "Fabric", "PropAccent", "BeanBag", "PropScreen"),
            (PropKind.EggChair, "egg_chair", "PropBody", "Metal", "Fabric", "PropScreen"),
            (PropKind.SideTable, "side_table", "Wood", "Metal", "Folders", "LampBulb"),
            (PropKind.MagazineRack, "magazine_rack", "Wood", "Metal", "Folders", "PropScreen"),
            (PropKind.AreaRug, "area_rug", "Felt", "Fabric", "Fabric", "PropScreen"),
            (PropKind.CorridorBench, "corridor_bench", "Fabric", "Metal", "Wood", "PropScreen"),
            (PropKind.FireCabinet, "fire_cabinet", "PropBody", "Vending", "StairGlass", "PropScreen"),
            (PropKind.NoticeBoard, "notice_board", "Cardboard", "Ceiling", "Wood", "PropScreen"),
            (PropKind.BinPair, "bin_pair", "PropBody", "PropAccent", "Folders", "PropScreen"),
            (PropKind.UmbrellaStand, "umbrella_stand", "Metal", "PropAccent", "Fabric", "PropScreen"),
            (PropKind.NetworkCabinet, "network_cabinet", "Rack", "PropAccent", "StairGlass", "PropScreen"),
            (PropKind.UpsUnit, "ups_unit", "PropAccent", "Rack", "PropBody", "PropScreen"),
            (PropKind.CrashCart, "crash_cart", "PropBody", "PropAccent", "Rubber", "PropScreen"),
            (PropKind.CracUnit, "crac_unit", "PropBody", "PropAccent", "PropAccent", "PropScreen"),
            (PropKind.TapeLibrary, "tape_library", "Rack", "Folders", "StairGlass", "PropScreen"),
            (PropKind.MetalShelving, "metal_shelving", "Metal", "Folders", "Cardboard", "PropScreen"),
            (PropKind.DrumPair, "drum_pair", "Folders", "PropAccent", "Metal", "PropScreen"),
            (PropKind.HandTruck, "hand_truck", "Vending", "Rubber", "Cardboard", "PropScreen"),
            (PropKind.CrateStack, "crate_stack", "Wood", "PropAccent", "PropAccent", "PropScreen"),
            (PropKind.RollingCage, "rolling_cage", "Metal", "PropAccent", "Cardboard", "PropScreen"),
            (PropKind.Plotter, "plotter", "PropBody", "PropAccent", "Ceiling", "PropScreen"),
            (PropKind.WorkTable, "work_table", "PropBody", "PropAccent", "Ceiling", "PropScreen"),
            (PropKind.SupplyShelf, "supply_shelf", "PropBody", "Folders", "Ceiling", "PropScreen"),
            (PropKind.MailCart, "mail_cart", "Wood", "PropAccent", "Ceiling", "PropScreen"),
            (PropKind.PaperBin, "paper_bin", "PropAccent", "Rack", "Metal", "PropScreen"),
            (PropKind.MapBoard, "map_board", "Cardboard", "PropAccent", "Ceiling", "LockLamp"),
            (PropKind.RadioRack, "radio_rack", "Rack", "PropAccent", "Metal", "PropScreen"),
            (PropKind.OperatorChair, "operator_chair", "Fabric", "PropAccent", "Metal", "PropScreen"),
            (PropKind.SupervisorDesk, "supervisor_desk", "PropBody", "PropAccent", "PropAccent", "PropScreen"),
            (PropKind.AlarmPanel, "alarm_panel", "PropBody", "Vending", "Metal", "LockLamp"),
            (PropKind.DisplayCase, "display_case", "Wood", "Metal", "Metal", "StairGlass"),
            (PropKind.PlanterLarge, "planter_large", "PlantPot", "Plant", "Wood", "PropScreen"),
            (PropKind.HallBench, "hall_bench", "Wood", "Metal", "Metal", "PropScreen"),
            (PropKind.Sculpture, "sculpture", "Ceiling", "Metal", "Mirror", "PropScreen"),
            (PropKind.DirectoryBoard, "directory_board", "PropBody", "PropAccent", "Ceiling", "PropScreen"),
            (PropKind.RowingMachine, "rowing_machine", "PropBody", "PropAccent", "Rubber", "PropScreen"),
            (PropKind.PowerCage, "power_cage", "Rack", "Rubber", "Metal", "PropScreen"),
            (PropKind.KettlebellRack, "kettlebell_rack", "Rack", "PropAccent", "Metal", "PropScreen"),
            (PropKind.MatRack, "mat_rack", "Wood", "Fabric", "Rubber", "PropScreen"),
            (PropKind.CableMachine, "cable_machine", "PropBody", "PropAccent", "Metal", "PropScreen"),
            (PropKind.TowelShelf, "towel_shelf", "Wood", "Ceiling", "Fabric", "PropScreen"),
            (PropKind.LaundryCart, "laundry_cart", "Metal", "Fabric", "Rubber", "PropScreen"),
            (PropKind.BenchIsland, "bench_island", "Metal", "Wood", "Metal", "PropScreen"),
            (PropKind.WeighingScale, "weighing_scale", "PropBody", "Metal", "Rubber", "PropScreen"),
            (PropKind.ShoeRack, "shoe_rack", "Wood", "Fabric", "Rubber", "PropScreen"),
            (PropKind.ExamTable, "exam_table", "PropBody", "PropAccent", "Ceiling", "PropScreen"),
            (PropKind.Wheelchair, "wheelchair", "PropBody", "PropAccent", "Metal", "PropScreen"),
            (PropKind.IvStand, "iv_stand", "Metal", "Rubber", "StairGlass", "PropScreen"),
            (PropKind.DoctorDesk, "doctor_desk", "Wood", "PropAccent", "Metal", "PropScreen"),
            (PropKind.DefibCabinet, "defib_cabinet", "Plant", "Vending", "StairGlass", "LampBulb"),
            (PropKind.BunkBed, "bunk_bed", "Metal", "Fabric", "Ceiling", "PropScreen"),
            (PropKind.Daybed, "daybed", "Wood", "Fabric", "BeanBag", "PropScreen"),
            (PropKind.CurtainDivider, "curtain_divider", "Metal", "Fabric", "Rubber", "PropScreen"),
            (PropKind.FloorCushions, "floor_cushions", "Fabric", "BeanBag", "Felt", "PropScreen"),
            (PropKind.NightTable, "night_table", "Wood", "Metal", "Metal", "LampBulb"),
            (PropKind.CardCatalog, "card_catalog", "Wood", "Metal", "Metal", "PropScreen"),
            (PropKind.StudyCarrel, "study_carrel", "Fabric", "PropAccent", "Wood", "LampBulb"),
            (PropKind.Lectern, "lectern", "Wood", "Folders", "Ceiling", "PropScreen"),
            (PropKind.WingbackChair, "wingback_chair", "Fabric", "Wood", "Wood", "PropScreen"),
            (PropKind.BookCart, "book_cart", "PropBody", "Rubber", "Folders", "PropScreen"),
            (PropKind.FumeHood, "fume_hood", "PropBody", "Rack", "Metal", "StairGlass"),
            (PropKind.Printer3d, "printer_3d", "PropBody", "Rack", "BeanBag", "PropScreen"),
            (PropKind.ComponentShelf, "component_shelf", "Metal", "Folders", "Vending", "PropScreen"),
            (PropKind.RobotTable, "robot_table", "PropBody", "PropAccent", "BeanBag", "PropScreen"),
            (PropKind.ScopeCart, "scope_cart", "Metal", "PropAccent", "Rubber", "PropScreen"),
            (PropKind.GuitarStand, "guitar_stand", "Vending", "Rack", "Wood", "PropScreen"),
            (PropKind.SynthStand, "synth_stand", "Vending", "Rack", "Ceiling", "PropScreen"),
            (PropKind.VocalBooth, "vocal_booth", "PropBody", "PropAccent", "Fabric", "StairGlass"),
            (PropKind.AmpCabinet, "amp_cabinet", "Rack", "Metal", "Fabric", "LampBulb"),
            (PropKind.StudioCouch, "studio_couch", "PropAccent", "Metal", "Metal", "PropScreen"),
            (PropKind.AirHockey, "air_hockey", "Folders", "Rack", "Ceiling", "PropScreen"),
            (PropKind.PokerTable, "poker_table", "Felt", "Rack", "Vending", "PropScreen"),
            (PropKind.Jukebox, "jukebox", "Wood", "Metal", "StairGlass", "LampBulb"),
            (PropKind.Pinball, "pinball", "Vending", "Metal", "Folders", "PropScreen"),
            (PropKind.GamingCorner, "gaming_corner", "Wood", "Rack", "Folders", "PropScreen"),
            (PropKind.DepositWall, "deposit_wall", "Metal", "PropAccent", "PropAccent", "PropScreen"),
            (PropKind.GoldPallet, "gold_pallet", "Gold", "Wood", "Wood", "PropScreen"),
            (PropKind.MoneyCart, "money_cart", "PropBody", "Rubber", "Felt", "PropScreen"),
            (PropKind.CashShelf, "cash_shelf", "Metal", "Felt", "Cardboard", "PropScreen"),
            (PropKind.StorageRack, "storage_rack", "ShopSteel", "ShopYellow", "Metal", "PropScreen"), // Tools/Blender/storage.py
            // the auto repair shop (Tools/Blender/garage.py)
            (PropKind.CarParked, "car_parked", "CarRed", "Rubber", "CarGlass", "LampBulb"),
            (PropKind.CarHoodOpen, "car_hood_open", "CarBlue", "Rubber", "CarGlass", "LampBulb"),
            (PropKind.LiftCar, "lift_car", "CarSilver", "Rubber", "CarGlass", "LampBulb"),
            (PropKind.LiftEmpty, "lift_empty", "ShopYellow", "Rubber", "Metal", "LampBulb"),
            (PropKind.CarOnStands, "car_on_stands", "Ceiling", "Rubber", "CarGlass", "LampBulb"),
            (PropKind.ToolChest, "tool_chest", "ToolRed", "Rubber", "Metal", "LampBulb"),
            (PropKind.TireChanger, "tire_changer", "ToolRed", "Rubber", "Metal", "PropScreen"),
            (PropKind.WheelBalancer, "wheel_balancer", "ToolRed", "Rubber", "Metal", "PropScreen"),
            (PropKind.TireRack, "tire_rack", "Rack", "Rubber", "Metal", "PropScreen"),
            (PropKind.TireStack, "tire_stack", "Rubber", "Rubber", "Metal", "PropScreen"),
            (PropKind.EngineStand, "engine_stand", "ToolRed", "PropAccent", "Metal", "PropScreen"),
            (PropKind.EngineHoist, "engine_hoist", "ShopYellow", "Rubber", "Metal", "PropScreen"),
            (PropKind.AirCompressor, "air_compressor", "ToolRed", "PropAccent", "Metal", "PropScreen"),
            (PropKind.OilDrain, "oil_drain", "ShopYellow", "Rubber", "Metal", "PropScreen"),
            (PropKind.HoseReel, "hose_reel", "ShopYellow", "Rubber", "Metal", "PropScreen"),
            (PropKind.FloorJack, "floor_jack", "ToolRed", "Rubber", "Metal", "PropScreen"),
            (PropKind.OilDrums, "oil_drums", "Lockers", "Wood", "Metal", "PropScreen"),
            (PropKind.WeldingCart, "welding_cart", "Lockers", "Rubber", "Metal", "PropScreen"),
            (PropKind.BayLines, "bay_lines", "ShopYellow", "ShopYellow", "ShopYellow", "PropScreen"),
            (PropKind.GarageDoor, "garage_door", "Ceiling", "PropAccent", "CarGlass", "PropScreen"),
            (PropKind.PaintRack, "paint_rack", "Vending", "Rack", "Metal", "PropScreen"),
            // the hospital (Tools/Blender/hospital.py)
            (PropKind.BedHeadwall, "bed_headwall", "Metal", "PropAccent", "Metal", "LampBulb"),
            (PropKind.BedCurtain, "bed_curtain", "Curtain", "Metal", "Metal", "PropScreen"),
            (PropKind.NurseStationDesk, "nurse_station", "Ceiling", "MedicalBlue", "Metal", "PropScreen"),
            (PropKind.Gurney, "gurney", "Ceiling", "MedicalBlue", "Metal", "PropScreen"),
            (PropKind.MedicationCart, "medication_cart", "MedicalBlue", "PropAccent", "Metal", "PropScreen"),
            (PropKind.LinenCart, "linen_cart", "MedicalBlue", "Rubber", "Metal", "PropScreen"),
            (PropKind.SurgicalTable, "surgical_table", "Ceiling", "Rubber", "Metal", "PropScreen"),
            (PropKind.SurgicalLights, "surgical_lights", "Ceiling", "Metal", "Metal", "LampBulb"),
            (PropKind.AnesthesiaMachine, "anesthesia_machine", "Ceiling", "PropAccent", "Metal", "PropScreen"),
            (PropKind.InstrumentTable, "instrument_table", "Metal", "SurgicalGreen", "Metal", "PropScreen"),
            (PropKind.ScrubSink, "scrub_sink", "Metal", "PropAccent", "Metal", "PropScreen"),
            (PropKind.XrayMachine, "xray_machine", "Ceiling", "PropAccent", "Metal", "PropScreen"),
            (PropKind.CtScanner, "ct_scanner", "Ceiling", "PropAccent", "Metal", "PropScreen"),
            (PropKind.WaitingSeats, "waiting_seats", "MedicalBlue", "Rubber", "Metal", "PropScreen"),
            (PropKind.VitalsMonitor, "vitals_monitor", "Ceiling", "PropAccent", "Metal", "PropScreen"),
            (PropKind.HandSanitizer, "hand_sanitizer", "Ceiling", "PropAccent", "Metal", "PropScreen"),
            // the apartment block (Tools/Blender/apartments.py)
            (PropKind.DoubleBed, "double_bed", "Wood", "Fabric", "Metal", "PropScreen"),
            (PropKind.Wardrobe, "wardrobe", "Wood", "Metal", "Metal", "PropScreen"),
            (PropKind.Dresser, "dresser", "Wood", "Fabric", "Metal", "PropScreen"),
            (PropKind.Bathtub, "bathtub", "Ceiling", "Ceiling", "Metal", "PropScreen"),
            (PropKind.HomeToilet, "home_toilet", "Ceiling", "Ceiling", "Metal", "PropScreen"),
            (PropKind.BathVanity, "bath_vanity", "Ceiling", "PropAccent", "Metal", "PropScreen"),
            (PropKind.WashingMachine, "washing_machine", "Ceiling", "PropAccent", "Metal", "PropScreen"),
            (PropKind.ShoeCabinet, "shoe_cabinet", "Wood", "Fabric", "Metal", "PropScreen"),
            (PropKind.HallHooks, "hall_hooks_bare", "Wood", "Metal", "Metal", "PropScreen"), // Tools/Blender/variants.py: garments on its hooks (PropVariants)
            (PropKind.Mailboxes, "mailboxes", "PropBody", "PropAccent", "Metal", "PropScreen"),
            (PropKind.ElevatorDoors, "elevator_doors", "Metal", "PropBody", "Metal", "LampBulb"),
        };

        /// <summary>Kinds hung on a wall or standing on a counter: no collider (nothing walks into them, nothing stands on them).</summary>
        static readonly HashSet<PropKind> NoCollider = new() { PropKind.Microwave, PropKind.HandDryer, PropKind.AcousticPanel, PropKind.DartBoard, PropKind.AreaRug,
            PropKind.BayLines, PropKind.HoseReel, PropKind.BedHeadwall, PropKind.BedCurtain, PropKind.SurgicalLights, PropKind.HandSanitizer,
            PropKind.HallHooks, PropKind.Mailboxes };

        /// <summary>Cupboards whose doors open (Items.Storage, models with BodyDoor{n} parts): loot may hide behind them.</summary>
        static readonly HashSet<PropKind> Opens = new() { PropKind.WallCabinet, PropKind.MedicineCabinet, PropKind.PartsCabinet, PropKind.MailShelf, PropKind.PantryCabinet,
            PropKind.Wardrobe, PropKind.Dresser, PropKind.ShoeCabinet,
            // every little door opens: the sink and vanity cupboards, the dishwasher, sideboards, drawers of desks and night tables
            PropKind.KitchenSink, PropKind.BathVanity, PropKind.Dishwasher, PropKind.DoubleSink, PropKind.Credenza, PropKind.AvCredenza,
            PropKind.FilingCabinet, PropKind.FilePedestal, PropKind.ExecutiveDesk, PropKind.NightTable };

        /// <summary>Prefabs of the themed rooms' furniture whose models exist, into <see cref="LevelArt.RoomProps"/>.</summary>
        static void BuildRoomProps(LevelArt art)
        {
            string p = PropPrefabs + "/";
            var list = new List<PropPrefab>();
            foreach (var (kind, file, body, accent, trim, eye) in RoomPropTable)
            {
                if (!HasModel(file)) { Debug.LogWarning($"[Builder] no model {file} yet: {kind} is left out"); continue; }
                var prefab = Prefab(p + kind + ".prefab", () =>
                {
                    var go = PropObject(kind.ToString(), file, body, accent, eye, collider: !NoCollider.Contains(kind));
                    PaintTrim(go, trim);
                    if (kind == PropKind.HandDryer) DressDryer(go);
                    if (Opens.Contains(kind)) go.AddComponent<Stealth.Items.Storage>();
                    if (kind == PropKind.TallBookcase) ShelfBooks(go); // its books differ every time
                    if (kind == PropKind.HallHooks) HookGarments(go, "hall_hooks_bare");
                    return go;
                });
                list.Add(new PropPrefab { Kind = kind, Prefab = prefab });
            }
            if (art.RoomProps != null && art.RoomProps.Length == list.Count && art.RoomProps.Zip(list, (a, b) => a.Kind == b.Kind && a.Prefab == b.Prefab).All(x => x)) return;
            art.RoomProps = list.ToArray();
            EditorUtility.SetDirty(art);
        }

        static bool HasModel(string file)
        {
            try { return AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(file)) != null; }
            catch (System.IO.FileNotFoundException) { return false; }
        }

        /// <summary>Parts named Trim* take their own material (PropObject paints them as the body).</summary>
        static void PaintTrim(GameObject go, string trim)
        {
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
                if (r.gameObject.name.StartsWith("Trim")) r.sharedMaterial = M(trim);
        }

        /// <summary>The dryer: E switches it on (HandDryer), its roar is a 3D sound heard down the corridor, the LED lights.</summary>
        static void DressDryer(GameObject go)
        {
            var dryer = go.AddComponent<HandDryer>();
            var sound = go.AddComponent<AudioSource>();
            sound.playOnAwake = false;
            sound.loop = false;
            sound.spatialBlend = 1f;
            sound.rolloffMode = AudioRolloffMode.Linear;
            sound.minDistance = 2f;
            sound.maxDistance = 22f;
            sound.volume = 0.9f;
            dryer.Sound = sound;
            dryer.Led = go.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name.StartsWith("Eye"));
            if (dryer.Led != null) dryer.Led.enabled = false;
        }

        /// <summary>
        /// Walls and ceilings of the themed rooms in the storybook style (Blue Prince): tiles to head height where water is,
        /// padding and bare concrete in the gym, white paint and acoustic tiles where people work with machines and medicine,
        /// brick in the game room, damask in the library; plain rooms have no panels and chair rail.
        /// </summary>
        static RoomLook[] StorybookRoomLooks()
        {
            RoomLook R(RoomType room, string wall, float wallTile, string lower, float lowerHeight, bool plain, string ceiling, float ceilingTile) => new()
            {
                Room = room, Wall = wall != null ? K(wall) : null, WallTile = wallTile, Lower = lower != null ? K(lower) : null, LowerTile = 1f,
                LowerHeight = lowerHeight, Plain = plain, Ceiling = ceiling != null ? K(ceiling) : null, CeilingTile = ceilingTile,
            };
            return new[]
            {
                R(RoomType.Restroom, "Wall_PaintWhite", 2f, "Wall_TilesWhite", 2.0f, true, "Ceiling_Acoustic", 1.2f),
                R(RoomType.LockerRoom, "Wall_PlasterPale", 2f, "Wall_TilesMint", 1.6f, true, "Ceiling_Acoustic", 1.2f),
                R(RoomType.Kitchen, "Wall_PlasterWarm", 2f, "Wall_TilesCream", 1.4f, true, "Ceiling_Plain", 2f),
                R(RoomType.Gym, "Wall_Concrete", 3f, "Wall_Padding", 1.2f, true, "Ceiling_Concrete", 3f),
                R(RoomType.Infirmary, "Wall_PaintWhite", 2f, "Wall_TilesWhite", 1.2f, true, "Ceiling_Acoustic", 1.2f),
                R(RoomType.Lab, "Wall_PaintWhite", 2f, null, 0f, true, "Ceiling_Acoustic", 1.2f),
                R(RoomType.Server, "Wall_Panels", 2f, null, 0f, true, "Ceiling_Acoustic", 1.2f),
                R(RoomType.Storage, "Wall_Concrete", 3f, null, 0f, true, "Ceiling_Concrete", 3f),
                R(RoomType.Studio, "Wall_Boards", 2f, null, 0f, true, null, 0f),
                R(RoomType.GameRoom, "Wall_Brick", 2f, null, 0f, true, null, 0f),
                R(RoomType.Library, "Wall_Damask", 1.5f, null, 0f, false, null, 0f),
            };
        }

        /// <summary>Floors of the themed rooms in the storybook style (Blue Prince): tiles where water is, rubber, carpet, lino.</summary>
        static RoomFloor[] StorybookRoomFloors()
        {
            RoomFloor F(RoomType room, string mat, float tile) => new() { Room = room, Material = K(mat), Tile = tile };
            return new[]
            {
                F(RoomType.Kitchen, "Floor_Checker", 1.2f),
                F(RoomType.Restroom, "Floor_SmallTiles", 1f),
                F(RoomType.LockerRoom, "Floor_SmallTiles", 1f),
                F(RoomType.Gym, "Floor_Rubber", 1f),
                F(RoomType.Library, "Floor_CarpetGreen", 1f),
                F(RoomType.NapRoom, "Floor_CarpetBlue", 1f),
                F(RoomType.Studio, "Floor_CarpetCharcoal", 1f),
                F(RoomType.GameRoom, "Floor_CarpetRed", 1f),
                F(RoomType.Infirmary, "Floor_Lino", 1.2f),
                F(RoomType.Lab, "Floor_Lino", 1.2f),
                F(RoomType.Server, "Floor_Lino", 1.2f),
                F(RoomType.CopyRoom, "Floor_CarpetCharcoal", 1f),
                F(RoomType.ControlRoom, "Floor_CarpetCharcoal", 1f),
            };
        }
    }
}
