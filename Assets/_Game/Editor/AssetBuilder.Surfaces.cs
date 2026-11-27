using System.Collections.Generic;
using System.IO;
using System.Linq;
using Stealth.Configs;
using UnityEditor;
using UnityEngine;

namespace Stealth.EditorTools
{
    /// <summary>
    /// What things are made of: the PBR surfaces of Art/Textures/Surfaces (Tools/Art/generate_surfaces.py) on the furniture
    /// and fittings of a house (<see cref="SurfaceSet"/>, worn by the houses whose <see cref="HouseLook.Surfaces"/> names it)
    /// and on the items. A surface is neutral: a thing keeps its colour, the surface gives it grain, sheen and wear.
    /// </summary>
    public static partial class AssetBuilder
    {
        const string SurfaceTextures = Root + "/Art/Textures/Surfaces", CoverTextures = Root + "/Art/Textures/Covers", SurfacesDir = StylesDir + "/Surfaces";

        /// <summary>A surface: how many metres one tile covers (the models' UVs are metres), how light its texture is.</summary>
        static readonly Dictionary<string, SurfaceLook> Surfaces = new()
        {
            ["powdercoat"] = new(0.6f) { Relief = 1.0f, Clouds = 0.035f, Under = 0.66f, UnderMetal = 0.4f, UnderSmooth = 0.5f, Smudges = 0.5f, Scratches = 0.45f, Edges = 0.45f },
            ["gloss"] = new(1f) { Relief = 0.35f, Clouds = 0.02f, Under = 0.64f, UnderMetal = 0.4f, UnderSmooth = 0.5f, Smudges = 0.75f, Scratches = 0.5f, Edges = 0.35f },
            ["plastic"] = new(0.9f) { Relief = 0.6f, Clouds = 0.03f, Lighter = 1.25f, UnderSmooth = 0.6f, Smudges = 0.5f, Scratches = 0.4f, Edges = 0.35f },
            ["plastic_rough"] = new(0.9f) { Relief = 0.6f, Clouds = 0.05f, Lighter = 1.2f, UnderSmooth = 0.55f, Smudges = 0.6f, Scratches = 0.6f, Edges = 0.5f },
            ["laminate"] = new(0.8f) { Relief = 0.35f, Clouds = 0.025f, Lighter = 0.62f, UnderSmooth = 0.2f, Smudges = 0.55f, Scratches = 0.5f, Edges = 0.4f },
            ["fabric"] = new(0.35f) { Relief = 1.6f, Clouds = 0.06f, Lighter = 1.25f, UnderSmooth = 0.08f, Edges = 0.3f, Stains = 0.25f },
            ["wool"] = new(0.4f) { Relief = 1.6f, Clouds = 0.05f, Lighter = 1.2f, UnderSmooth = 0.06f, Edges = 0.25f, Stains = 0.2f },
            ["cloth"] = new(0.22f) { Relief = 0.7f, Clouds = 0.05f, Lighter = 1.15f, UnderSmooth = 0.1f, Edges = 0.15f, Stains = 0.15f },
            ["felt"] = new(0.5f) { Relief = 0.5f, Clouds = 0.06f, Lighter = 1.2f, UnderSmooth = 0.08f, Edges = 0.3f, Stains = 0.2f },
            ["leather"] = new(0.45f) { Relief = 1.8f, Clouds = 0.06f, Lighter = 1.28f, UnderSmooth = 0.55f, Smudges = 0.45f, Scratches = 0.25f, Edges = 0.5f },
            ["rubber"] = new(0.25f) { Relief = 0.8f, Clouds = 0.05f, Lighter = 1.2f, UnderSmooth = 0.3f, Smudges = 0.2f, Scratches = 0.25f, Edges = 0.2f, Stains = 0.25f },
            ["steel"] = new(0.6f) { Relief = 0.8f, Clouds = 0.03f, Lighter = 1.2f, UnderMetal = 0.8f, UnderSmooth = 0.7f, Smudges = 0.6f, Scratches = 0.5f, Edges = 0.3f },
            ["ceramic"] = new(0.9f) { Relief = 0.5f, Clouds = 0.01f, Lighter = 1f, UnderSmooth = 0.8f, Smudges = 0.35f, Scratches = 0.1f, Edges = 0.05f, Runs = 0.25f },
            ["cardboard"] = new(0.6f) { Relief = 1.4f, Clouds = 0.05f, Lighter = 1.2f, UnderSmooth = 0.15f, Edges = 0.5f, Stains = 0.3f, Scratches = 0.2f },
            ["paper"] = new(0.8f) { Relief = 0.8f, Clouds = 0.03f, Lighter = 1f, UnderSmooth = 0.2f, Edges = 0.15f, Stains = 0.1f },
            ["wood"] = new(0.9f) { Relief = 0.3f, Clouds = 0.05f, Lighter = 1.35f, UnderSmooth = 0.25f, Smudges = 0.5f, Scratches = 0.45f, Edges = 0.5f },
            ["oak"] = new(1.1f) { Relief = 0.3f, Clouds = 0.05f, Lighter = 1.25f, UnderSmooth = 0.25f, Smudges = 0.5f, Scratches = 0.4f, Edges = 0.45f },
            ["timber"] = new(1.1f) { Relief = 0.6f, Clouds = 0.06f, Lighter = 1.2f, UnderSmooth = 0.15f, Scratches = 0.4f, Edges = 0.55f, Stains = 0.35f },
            ["leaf"] = new(0.25f) { Relief = 0.0f, Clouds = 0.05f, Lighter = 1.1f, UnderSmooth = 0.4f, Edges = 0f },
            ["wallpaint"] = new(2.5f) { Lighter = 1.1f, UnderSmooth = 0.2f, Smudges = 0.22f, Scratches = 0.25f, Stains = 0.02f, Edges = 0f, Relief = 0.5f, Clouds = 0.022f },
            ["stone"] = new(0.3f) { Relief = 0.6f, Clouds = 0.06f, Lighter = 1.2f, UnderSmooth = 0.3f, Edges = 0.4f, Stains = 0.3f, Runs = 0.3f, Smudges = 0.1f },
        };

        /// <summary>
        /// A surface: the metres its tile covers (the models' UVs are metres), what shows where it is rubbed through (a grey of
        /// its own, or its colour so much lighter), and how it wears in a kept house.
        /// </summary>
        sealed class SurfaceLook
        {
            public readonly float Tile;
            public float Under, Lighter = 1.2f, UnderMetal, UnderSmooth = 0.5f, Smudges, Scratches, Stains = 0.06f, Runs, Edges = 0.3f;
            /// <summary>Slow waves and dents (1: about two degrees), how uneven its colour and sheen are from across the room.</summary>
            public float Relief, Clouds;
            public SurfaceLook(float tile) => Tile = tile;
        }

        /// <summary>How light the plain surfaces' textures are (generate_surfaces.BASE): the tint makes up for it.</summary>
        const float SurfaceLight = 0.86f;

        /// <summary>
        /// The surface of every material of the furniture and the fittings, and another colour where the plain one was
        /// made for the toy look (nothing: its own). "Ceiling" is not here: it is also the slabs (their stretched cubes take
        /// no texture), so the props painted with it are named part by part.
        /// </summary>
        static readonly Dictionary<string, (string surface, string color)> MaterialSurfaces = new()
        {
            ["PropBody"] = ("powdercoat", null), ["PropAccent"] = ("plastic", null), ["Metal"] = ("steel", null), ["Rack"] = ("powdercoat", null),
            ["Wood"] = ("oak", "9A744C"), ["Rubber"] = ("rubber", null), ["Fabric"] = ("wool", "4A5566"), ["Folders"] = ("plastic", "3E5672"),
            ["Vending"] = ("gloss", "9C1F1C"), ["Cardboard"] = ("cardboard", "A5825A"), ["BeanBag"] = ("leather", "8A4A2A"), ["Felt"] = ("felt", null),
            ["Lockers"] = ("powdercoat", "5A7183"), ["Stall"] = ("laminate", null), ["ToolRed"] = ("powdercoat", null), ["ShopYellow"] = ("powdercoat", null),
            ["MedicalBlue"] = ("leather", null), ["SurgicalGreen"] = ("cloth", null), ["Curtain"] = ("cloth", null), ["PlantPot"] = ("stone", "8C6A55"),
            ["Plant"] = ("leaf", "35592F"), ["SafeMetal"] = ("powdercoat", null), ["ShopSteel"] = ("powdercoat", null), ["LiftSteel"] = ("steel", null),
            ["DoorFrameAlu"] = ("steel", null), ["ClinicDoor"] = ("laminate", null), ["ClinicFrame"] = ("powdercoat", null), ["FlatDoor"] = ("leather", null),
            ["FlatDoorTrim"] = ("steel", null), ["WoodDoor"] = ("oak", "94693F"), ["EntranceSteel"] = ("powdercoat", null), ["DoorFrame"] = ("wood", "5E4632"),
            ["DoorSeal"] = ("rubber", null), ["LampHousing"] = ("powdercoat", null), ["Blinds"] = ("fabric", null), ["Arcade"] = ("gloss", null),
            ["PingPong"] = ("laminate", null), ["CarRed"] = ("gloss", null), ["CarBlue"] = ("gloss", null), ["CarSilver"] = ("gloss", null), ["Gold"] = ("steel", null),
            ["Rope"] = ("cloth", null), ["CageWood"] = ("wood", null), ["CageBrass"] = ("steel", null), ["LiftFloor"] = ("rubber", null),
            ["ClothNavy"] = ("cloth", null), ["ClothGrey"] = ("cloth", null), ["ClothBeige"] = ("cloth", null), ["ClothBlack"] = ("cloth", null),
            // the house's services (UtilityPlan)
            ["UtilDuct"] = ("steel", "8A8D90"), ["UtilGrille"] = ("powdercoat", null), ["UtilPipe"] = ("gloss", "A9A8A2"), ["UtilCopper"] = ("steel", null),
            ["UtilConduit"] = ("powdercoat", "7C7E80"), ["UtilPlate"] = ("plastic", "C9C8C2"), ["UtilDark"] = ("rubber", null), ["UtilPanel"] = ("powdercoat", "8E9290"),
            ["UtilWarning"] = ("gloss", "C9961C"),
            ["VaseGreen"] = ("ceramic", null), ["VaseCream"] = ("ceramic", null), ["VaseBlue"] = ("ceramic", null), ["Wicker"] = ("timber", null),
            ["ClothRed"] = ("cloth", null), ["ClothOlive"] = ("cloth", null), ["ClothBrown"] = ("cloth", null), ["ClothDenim"] = ("cloth", null),
        };

        /// <summary>"Prefab/Part" (a part's name begins with it) → surface, colour: where the material's own rule says the wrong thing.</summary>
        static readonly Dictionary<string, (string surface, string color)> PartSurfaces = new()
        {
            // desks and tables: laminate tops and carcasses, the accents are their steel and plastic
            ["Desk/Body"] = ("laminate", "8F8A80"), ["Desk/Accent"] = ("powdercoat", null), ["MeetingTable/Body"] = ("wood", "7A5638"), ["MeetingTable/Accent"] = ("powdercoat", null),
            ["WorkTable/Body"] = ("laminate", null), ["WorkTable/Accent"] = ("powdercoat", null), ["WorkTable/Trim"] = ("paper", null),
            ["SupervisorDesk/Body"] = ("laminate", null), ["ConsoleDesk/Body"] = ("laminate", null), ["AvCredenza/Body"] = ("laminate", null), ["Credenza/Body"] = ("laminate", null),
            ["LabBench/Body"] = ("laminate", "A9ABA8"), ["StandingDesk/Accent"] = ("powdercoat", null), ["ExecutiveDesk/Body"] = ("wood", "5A3A24"),
            ["ExecutiveDesk/Trim"] = ("leather", "26302A"), ["ConferenceTable/Body"] = ("wood", "6E4A2E"), ["ReceptionDesk/Accent"] = ("laminate", null),
            ["RobotTable/Body"] = ("laminate", null), ["StudyCarrel/Trim"] = ("oak", null), ["DoctorDesk/Body"] = ("laminate", "A08A6C"), ["Terminal/Body"] = ("powdercoat", null),
            // seats
            ["Chair/Accent"] = ("fabric", "2E3238"), ["Chair/Body"] = ("plastic", "3A3C40"), ["OperatorChair/Accent"] = ("plastic", null), ["StudioCouch/Body"] = ("leather", null),
            ["EggChair/Body"] = ("plastic", null), ["Wheelchair/Accent"] = ("leather", null), ["ExamTable/Body"] = ("powdercoat", null), ["ExamTable/Accent"] = ("leather", null),
            ["SofaPod/Trim"] = ("plastic", null), ["WeightBench/Accent"] = ("leather", null), ["BarStool/Accent"] = ("leather", "5A2A20"), ["PunchingBag/Body"] = ("leather", "2A2422"),
            ["RowingMachine/Trim"] = ("rubber", null),
            // storage
            ["Bookshelf/Body"] = ("wood", "6B482C"), ["TallBookcase/Body"] = ("wood", "4E3320"), ["CardCatalog/Body"] = ("wood", "6E4A2C"), ["Wardrobe/Body"] = ("wood", null),
            ["Crate/Body"] = ("timber", "97764E"), ["CrateStack/Body"] = ("timber", "97764E"), ["Pallet/Accent"] = ("timber", "97764E"), ["GoldPallet/Accent"] = ("timber", "97764E"),
            ["OilDrums/Accent"] = ("timber", "8A6E4A"), ["Bench/Body"] = ("timber", "9A7448"), ["BenchIsland/Accent"] = ("timber", "9A7448"), ["HallBench/Body"] = ("timber", "8A6440"),
            ["Workbench/Body"] = ("timber", "8A6A44"), ["DrumPair/Body"] = ("powdercoat", "2F4E74"), ["Boxes/"] = ("cardboard", "A5825A"), ["TapeLibrary/Accent"] = ("plastic", null),
            ["BookCart/Trim"] = ("cardboard", "6C4A3A"), ["MailCart/Trim"] = ("paper", null), ["MailShelf/Accent"] = ("paper", null), ["SupplyShelf/Trim"] = ("paper", null),
            ["PaperStack/Accent"] = ("paper", null), ["PaperBin/Trim"] = ("paper", "D8D4C8"), ["Lectern/Trim"] = ("paper", null), ["FilePedestal/Accent"] = ("plastic", null),
            // paper, boards and signs
            ["Flipchart/Trim"] = ("paper", null), ["NoticeBoard/Body"] = ("cardboard", "8C6A42"), ["MapBoard/Body"] = ("cardboard", "8C6A42"),
            ["MapBoard/Trim"] = ("paper", null), ["Whiteboard/Body"] = ("gloss", "D8DAD8"), ["MobileWhiteboard/Trim"] = ("gloss", "D8DAD8"), ["ProjectorScreen/Trim"] = ("cloth", "DCDCD8"),
            ["DirectoryBoard/Trim"] = ("gloss", null), ["WallClock/Body"] = ("plastic", null), ["DartBoard/Accent"] = ("felt", null),
            // glazed ware and enamel
            ["WashBasin/Body"] = ("ceramic", "DDDDD8"), ["Urinal/Body"] = ("ceramic", "DDDDD8"), ["HomeToilet/"] = ("ceramic", "DDDDD8"), ["Bathtub/"] = ("ceramic", "DDDDD8"),
            ["MirrorSink/Body"] = ("ceramic", "DDDDD8"), ["MirrorSink/BodyDoor"] = ("laminate", "D2D2CC"), ["ToiletStall/Accent"] = ("ceramic", "DDDDD8"), ["BathVanity/Body"] = ("laminate", "D2D2CC"),
            ["ShowerStall/Body"] = ("ceramic", "D8D8D4"), ["DoubleSink/Body"] = ("laminate", "BFBDB6"), ["KitchenSink/Body"] = ("laminate", "BFBDB6"), ["KitchenCounter/Body"] = ("laminate", "BFBDB6"),
            ["KitchenCounter/Accent"] = ("stone", "34353A"), ["KitchenIsland/Body"] = ("laminate", "BFBDB6"), ["KitchenIsland/Trim"] = ("stone", "34353A"), ["PantryCabinet/Body"] = ("laminate", "BFBDB6"),
            ["WallCabinet/Body"] = ("laminate", "BFBDB6"), ["Fridge/Body"] = ("gloss", "CFD0CE"), ["Stove/Body"] = ("gloss", "CFD0CE"), ["Microwave/Body"] = ("gloss", "CFD0CE"),
            ["Dishwasher/Body"] = ("gloss", "CFD0CE"), ["WashingMachine/Body"] = ("gloss", "D6D6D4"), ["HandDryer/Body"] = ("gloss", "D6D6D4"), ["MedTable/Body"] = ("gloss", "CFD2D2"),
            ["DefibCabinet/Body"] = ("gloss", null), ["FireCabinet/Body"] = ("gloss", "8E1B18"), ["GuitarStand/Body"] = ("gloss", null), ["SynthStand/Body"] = ("gloss", null),
            ["HandTruck/Body"] = ("powdercoat", "9C2A1E"), ["PaintRack/Body"] = ("powdercoat", null), ["PokerTable/Trim"] = ("leather", null),
            ["TrashBin/Body"] = ("powdercoat", "34373A"), ["TrashBin/Accent"] = ("steel", null),
            // moulded plastic
            ["Copier/Body"] = ("plastic", "CACAC4"), ["Printer/Body"] = ("plastic", "CACAC4"), ["Plotter/Body"] = ("plastic", "B8B8B2"), ["Printer3d/Body"] = ("plastic", null),
            ["WaterCooler/Body"] = ("plastic", "D0D0CA"), ["InfoKiosk/Body"] = ("gloss", "B4B6B6"), ["LogoTotem/Body"] = ("gloss", "B4B6B6"), ["BinPair/Body"] = ("plastic", null),
            ["RecyclingBins/"] = ("plastic", null), ["JanitorCart/Body"] = ("plastic", "A08A34"), ["CrashCart/Body"] = ("plastic", null), ["RobotArm/Body"] = ("gloss", null),
            ["SleepPod/Body"] = ("plastic", "CFCFC8"), ["VitalsMonitor/Body"] = ("plastic", "CFCFC8"), ["AirHockey/Body"] = ("gloss", null), ["PingPongTable/Accent"] = ("plastic", null),
            ["VocalBooth/Body"] = ("laminate", null), ["FumeHood/Body"] = ("powdercoat", "B4B6B2"), ["AmpCabinet/Body"] = ("leather", "1D1D1F"),
            // cloth among the plastics
            ["TowelShelf/Accent"] = ("cloth", "D8D6D0"), ["ShowerStall/Accent"] = ("steel", null), ["HospitalBed/Body"] = ("powdercoat", "CFD0CE"), ["Gurney/Body"] = ("cloth", "D8D8D4"),
            ["BunkBed/Trim"] = ("cloth", "D4D2CC"), ["SurgicalTable/Body"] = ("powdercoat", null), ["CashShelf/Accent"] = ("paper", "5F7A5A"), ["MoneyCart/Trim"] = ("paper", "5F7A5A"),
            ["DrumKit/Body"] = ("gloss", "C8C6C0"), ["Sculpture/Body"] = ("stone", "C6C4BC"), ["CarOnStands/Body"] = ("gloss", "C4C6C8"), ["GarageDoor/Body"] = ("powdercoat", "B6B8B8"),
            ["Programmer/Body"] = ("powdercoat", null), ["Cage/"] = (null, null),
            // the hospital's scrub trough is stainless steel (it was the dark grey of plastic)
            ["ScrubSink/Body"] = ("steel", "B6BABC"), ["ClinicBench/Body"] = ("laminate", "D2D4D0"), ["MedFridge/Body"] = ("powdercoat", "D6D8D6"), ["MedWorkstation/Body"] = ("powdercoat", "D6D8D6"),
        };

        /// <summary>The surface in a house long left (nothing: the house's own paint, with its own wear, stays).</summary>
        static string Worn(string surface) => surface switch
        {
            "powdercoat" or "wood" or "oak" or "timber" => null, // the facility's painted steel, every piece worn its own way (WearVariants)
            "plastic" => "plastic_rough",
            _ => surface,
        };

        /// <summary>The surface on a material: its textures, its own colour, the wear of a kept thing — or, worn, of one left for years.</summary>
        static void SetSurface(Material m, string surface, Color color, float scale = 1f, float wear = 0f, bool thing = false)
        {
            // wear: 0 kept, 0.5 in daily hard use, 1 years without care
            bool worn = wear >= 0.99f;
            float W(float kept, float left) => Mathf.Lerp(kept, left, wear);
            var s = Surfaces[surface];
            m.shader = Shader.Find("Stealth/Surface");
            float lit = Mathf.GammaToLinearSpace(SurfaceLight);
            var linear = color.linear;
            Color Tint(float k) => new(Mathf.LinearToGammaSpace(linear.r / lit * k), Mathf.LinearToGammaSpace(linear.g / lit * k), Mathf.LinearToGammaSpace(linear.b / lit * k), color.a);
            SetPbr(m, $"{SurfaceTextures}/{surface}", Tint(1f), 1f, 1f / (s.Tile * scale));
            foreach (var map in new[] { "_BumpMap", "_MetallicGlossMap", "_OcclusionMap" }) m.SetTextureScale(map, m.GetTextureScale("_BaseMap"));
            m.SetTexture("_WearMap", Tex($"{SurfaceTextures}/wear.png"));
            m.SetTexture("_ReliefMap", Tex($"{SurfaceTextures}/relief.png"));
            m.SetFloat("_ReliefTile", thing ? 0.35f : 1.6f);
            m.SetFloat("_Relief", s.Relief * (thing ? 0.6f : W(1f, 1.5f)));
            m.SetFloat("_Clouds", s.Clouds * W(1f, 1.6f));
            m.SetFloat("_WallBand", 0f);
            m.SetFloat("_Contact", 0f);
            m.SetFloat("_RoomHeight", Stealth.Level.LevelMetrics.WallHeight);
            m.SetFloat("_WearTile", thing ? 0.38f : 1.15f);
            float k = W(1f, 1.9f);
            m.SetFloat("_Smudges", Mathf.Clamp01(s.Smudges * (worn || thing ? 1.2f : 1f)));
            m.SetFloat("_Scratches", Mathf.Clamp01(s.Scratches * k));
            m.SetFloat("_Stains", Mathf.Clamp01(W(0f, 0.55f) + s.Stains));
            m.SetFloat("_Streaks", Mathf.Clamp01(W(0f, 0.6f) + s.Runs));
            m.SetFloat("_EdgeWear", Mathf.Clamp01(s.Edges * (thing ? 1.4f : W(1.1f, 1.9f))));
            var under = s.Under > 0f ? new Color(s.Under, s.Under, s.Under) : s.Lighter < 1f ? color * s.Lighter : Color.Lerp(color, new Color(0.86f, 0.84f, 0.8f), (s.Lighter - 1f) * 1.1f); // scuffed paler
            m.SetColor("_WearColor", under);
            m.SetFloat("_WearMetal", s.UnderMetal);
            m.SetFloat("_WearSmooth", s.UnderSmooth);
            m.SetFloat("_Corners", W(0.5f, 0.9f));
            m.SetFloat("_FloorDirt", thing ? 0f : W(0.45f, 0.95f));
            m.SetFloat("_FloorHeight", Stealth.Level.LevelMetrics.FloorHeight);
            m.SetColor("_DirtColor", Color.Lerp(new Color(0.4f, 0.36f, 0.31f), new Color(0.27f, 0.24f, 0.2f), wear));
        }

        /// <summary>
        /// A painted wall of the style's kit (a ceiling: clean, nothing reaches it): plaster under roller paint, uneven from
        /// across the room, marked where hands, chairs and feet reach, a little darker where it meets the floor and the ceiling.
        /// </summary>
        static void WallPaint(string name, string hex, float sheen, bool ceiling = false)
        {
            var m = LitAsset($"{KitMaterialsDir}/{name}.mat");
            SetSurface(m, "wallpaint", Hex(hex));
            m.SetFloat("_Smoothness", sheen);
            m.SetFloat("_WearTile", 1.7f);
            m.SetFloat("_WallBand", 1f);
            m.SetFloat("_Contact", ceiling ? 0f : 0.16f);
            m.SetFloat("_Corners", ceiling ? 0.15f : 0.3f);
            if (ceiling)
                foreach (var p in new[] { "_Smudges", "_Scratches", "_FloorDirt", "_Streaks" }) m.SetFloat(p, 0f);
            EditorUtility.SetDirty(m);
            s_Kit[name] = m;
        }

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;

        /// <summary>The material of a surface in a set, one for each material it stands for (a style change must find the way back).</summary>
        /// <summary>What a broken wall is made of (brick, mortar, plaster shards): one material of a surface and a colour, the same in every house.</summary>
        static Material RuinMaterial(string name, string surface, string hex)
        {
            var m = LitAsset($"{KitMaterialsDir}/{name}.mat");
            SetSurface(m, surface, Hex(hex), 1f, 1f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material SurfaceMaterial(string set, Material from, string surface, string hex)
        {
            if (!AssetDatabase.IsValidFolder($"{SurfaceTextures}/{surface}")) return null;
            var m = LitAsset($"{SurfacesDir}/{set}/{from.name}_{surface}{(hex != null ? "_" + hex : "")}.mat");
            m.CopyPropertiesFromMaterial(from);
            m.shaderKeywords = from.shaderKeywords;
            var color = hex != null ? Hex(hex) : from.GetColor("_BaseColor");
            if (set == "Worn") color = Saturate(color, 0.55f, 0.8f); // faded
            SetSurface(m, surface, color, 1f, set == "Worn" ? 1f : set == "Used" ? 0.5f : 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>The three sets: clean (kept things: a little handled), used (worked with every day: scuffed, grimy at the floor, its colours whole)
        /// and worn (years without care; faded, scuffs and dirt, no rust).</summary>
        static Dictionary<string, SurfaceSet> BuildSurfaceSets()
        {
            var sets = new Dictionary<string, SurfaceSet>();
            if (!AssetDatabase.IsValidFolder(SurfaceTextures)) return sets;
            foreach (var name in new[] { "Clean", "Used", "Worn" })
            {
                EnsureFolder($"{SurfacesDir}/{name}");
                string Of(string surface) => surface == null ? null : name == "Worn" ? Worn(surface) : surface;
                var swaps = new List<MaterialSwap>();
                foreach (var kv in MaterialSurfaces)
                {
                    var from = new[] { "Props", "Environment", "City" }.Select(d => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/{d}/{kv.Key}.mat")).FirstOrDefault(m => m != null);
                    // (the services have no paint of the facility's own to keep)
                    string surface = kv.Key.StartsWith("Util") ? kv.Value.surface : Of(kv.Value.surface);
                    if (from == null || surface == null) continue;
                    var to = SurfaceMaterial(name, from, surface, kv.Value.color);
                    if (to != null) swaps.Add(new MaterialSwap { From = from, To = to });
                }
                var parts = new List<PartSurface>();
                var known = new HashSet<(Mesh, Material)>();
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { PropPrefabs }))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    foreach (var r in prefab.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        var from = r.sharedMaterial;
                        var mesh = r.TryGetComponent<MeshFilter>(out var mf) ? mf.sharedMesh : null;
                        if (from == null || mesh == null || r.name.StartsWith("Eye") || from.shader.name != "Universal Render Pipeline/Lit" || from.IsKeywordEnabled("_EMISSION")) continue;
                        bool own = PartRule(prefab.name, r.name, out var rule);
                        if (!own && from.name == "Ceiling") { rule = ("plastic", "CCCCC6"); own = true; }
                        if (!own || Of(rule.surface) == null || !known.Add((mesh, from))) continue;
                        var to = SurfaceMaterial(name, from, Of(rule.surface), rule.color);
                        if (to != null) parts.Add(new PartSurface { Mesh = mesh, From = from, To = to });
                    }
                }
                var path = $"{SurfacesDir}/{name}.asset";
                var set = AssetDatabase.LoadAssetAtPath<SurfaceSet>(path);
                if (set == null) { set = ScriptableObject.CreateInstance<SurfaceSet>(); AssetDatabase.CreateAsset(set, path); }
                set.Materials = swaps.ToArray();
                set.Parts = parts.ToArray();
                set.Forget();
                EditorUtility.SetDirty(set);
                sets[name] = set;
            }
            AssetDatabase.SaveAssets();
            return sets;
        }

        /// <summary>The rule of a prefab's part: the longest one its name begins with.</summary>
        static bool PartRule(string prefab, string part, out (string surface, string color) rule)
        {
            rule = default;
            int best = -1;
            foreach (var kv in PartSurfaces)
            {
                int cut = kv.Key.IndexOf('/');
                string p = kv.Key.Substring(cut + 1);
                if (kv.Key.Substring(0, cut) != prefab || !part.StartsWith(p) || p.Length <= best) continue;
                best = p.Length;
                rule = kv.Value;
            }
            return best >= 0;
        }

        /// <summary>The set every kind of house wears (a house that names its set by hand keeps it): kept places — clean, the repair shop and the warehouse — used, the abandoned facility — worn.</summary>
        static SurfaceSet SurfacesOf(Stealth.Level.HouseKind kind)
        {
            string name = kind switch
            {
                Stealth.Level.HouseKind.Abandoned => "Worn",
                Stealth.Level.HouseKind.Garage or Stealth.Level.HouseKind.Warehouse => "Used",
                _ => "Clean",
            };
            return AssetDatabase.LoadAssetAtPath<SurfaceSet>($"{SurfacesDir}/{name}.asset");
        }

        // ------------------------------------------------------------------ items

        /// <summary>The surface of every item's material (Art/Materials/Items), and how small it is drawn (share of its tile).</summary>
        static readonly Dictionary<string, (string surface, float scale)> ItemSurfaces = new()
        {
            ["Gear_radio_Body"] = ("plastic", 0.5f), ["Gear_radio_Accent"] = ("rubber", 0.5f), ["Gear_keyboard_Body"] = ("plastic", 0.5f), ["Gear_keyboard_Accent"] = ("plastic", 0.4f),
            ["Gear_tablet_Body"] = ("rubber", 0.5f), ["Gear_tablet_Accent"] = ("plastic", 0.5f), ["Gear_medkit_Body"] = ("plastic", 0.7f), ["Gear_medkit_Accent"] = ("steel", 0.4f),
            ["Gear_medkit_Trim"] = ("plastic", 0.7f), ["Gear_coffee_Body"] = ("ceramic", 0.4f), ["Gear_coffee_Accent"] = ("steel", 0.3f),             ["Gear_finder_Body"] = ("powdercoat", 0.4f), ["Gear_finder_Accent"] = ("steel", 0.3f), ["Gear_boots_Body"] = ("leather", 0.5f), ["Gear_boots_Accent"] = ("rubber", 0.5f),
            ["Gear_boots_Trim"] = ("cloth", 0.5f), ["Gear_tomatoes_Accent"] = ("steel", 0.3f), ["Gear_chess_Body"] = ("wood", 0.4f),
            ["Gear_chess_Accent"] = ("wood", 0.3f), ["Gear_magazine_Accent"] = ("paper", 0.5f), ["Gear_pills_Accent"] = ("steel", 0.2f),
            ["Gear_toothpaste_Body"] = ("plastic", 0.5f), ["Gear_toothpaste_Accent"] = ("plastic", 0.4f), ["Gear_tape_Body"] = ("cloth", 0.5f), ["Gear_tape_Accent"] = ("cardboard", 0.4f),
            ["Gear_matches_Accent"] = ("timber", 0.15f), ["Gear_matches_Trim"] = ("rubber", 0.3f), ["Gear_toiletpaper_Body"] = ("paper", 0.4f),
            ["Gear_toiletpaper_Accent"] = ("cardboard", 0.4f), ["Gear_applecore_Body"] = ("leaf", 0.3f), ["Gear_applecore_Accent"] = ("leaf", 0.4f), ["Gear_applecore_Trim"] = ("timber", 0.1f),
            ["Gear_soap_Body"] = ("ceramic", 0.3f), ["Gear_soap_Accent"] = ("plastic", 0.4f), ["Gear_cassette_Body"] = ("plastic", 0.4f), ["Gear_cassette_Trim"] = ("paper", 0.4f), ["Gear_cassette_AccentStripe"] = ("paper", 0.4f),
            ["Gear_cassette_AccentReel"] = ("plastic", 0.3f), ["Gear_cassette_Accent"] = ("plastic", 0.2f), ["Gear_ssd_Body"] = ("powdercoat", 0.3f), ["Gear_ssd_Accent"] = ("steel", 0.2f),
            ["Gear_ssd_Trim"] = ("paper", 0.4f), ["Gear_gamepad_Body"] = ("plastic", 0.4f), ["Gear_gamepad_Accent"] = ("rubber", 0.4f), ["Gear_gamepad_Trim"] = ("plastic", 0.4f),
            ["Gear_gamekeyboard_Body"] = ("powdercoat", 0.5f), ["Gear_gamekeyboard_Accent"] = ("plastic", 0.4f), ["Gear_gamekeyboard_Trim"] = ("plastic", 0.4f),
            ["Gear_canister_Body"] = ("powdercoat", 0.7f), ["Gear_canister_Accent"] = ("rubber", 0.5f), ["Gear_canister_Trim"] = ("plastic", 0.5f), ["Gear_bandage_Body"] = ("cloth", 0.4f),
            ["Gear_bandage_Accent"] = ("cloth", 0.4f), ["Gear_gloves_Body"] = ("leather", 0.4f), ["Gear_gloves_Accent"] = ("cloth", 0.5f), ["Gear_necklace_Body"] = ("steel", 0.2f),
            ["Gear_necklace_Accent"] = ("ceramic", 0.3f), ["Gear_mop_Body"] = ("gloss", 0.5f), ["Gear_mop_Accent"] = ("cloth", 0.6f), ["Gear_plush_Body"] = ("felt", 0.5f),
            ["Gear_plush_Accent"] = ("felt", 0.5f), ["Gear_plush_Trim"] = ("plastic", 0.3f), ["Gear_socks_Body"] = ("fabric", 0.3f), ["Gear_socks_Accent"] = ("fabric", 0.3f),
            ["Gear_stun_Body"] = ("powdercoat", 0.6f), ["Gear_stun_Accent"] = ("rubber", 0.6f), ["Gear_stun_AccentSteel"] = ("steel", 0.4f), ["Gear_stun_AccentCoil"] = ("steel", 0.3f),
            ["Gear_stun_Trim"] = ("powdercoat", 0.4f),
            // the car bench's crafted things (Tools/Blender/items_more.py, «upgraded tools, gadgets, parts»)
            ["Gear_proscrewdriver_Body"] = ("plastic", 0.4f), ["Gear_proscrewdriver_Accent"] = ("rubber", 0.4f), ["Gear_proscrewdriver_Trim"] = ("steel", 0.2f),
            ["Gear_proscrewdriver_TrimSteel"] = ("steel", 0.2f), ["Gear_prokeyboard_Body"] = ("powdercoat", 0.5f), ["Gear_prokeyboard_Accent"] = ("plastic", 0.4f),
            ["Gear_protablet_Body"] = ("rubber", 0.5f), ["Gear_protablet_Accent"] = ("plastic", 0.5f), ["Gear_espresso_Body"] = ("paper", 0.4f),
            ["Gear_espresso_Trim"] = ("cardboard", 0.4f), ["Gear_espresso_TrimBand"] = ("cardboard", 0.4f), ["Gear_espresso_Accent"] = ("plastic", 0.3f),
            ["Gear_jumpboots_Body"] = ("leather", 0.5f), ["Gear_jumpboots_Accent"] = ("rubber", 0.5f), ["Gear_jumpboots_Trim"] = ("cloth", 0.5f),
            ["Gear_jumpboots_TrimSpring"] = ("steel", 0.3f), ["Gear_gasmask_Body"] = ("rubber", 0.5f), ["Gear_gasmask_Accent"] = ("plastic", 0.3f),
            ["Gear_gasmask_Trim"] = ("plastic", 0.4f), ["Gear_gasmask_AccentFilter"] = ("powdercoat", 0.4f), ["Gear_hammer_Body"] = ("timber", 0.5f),
            ["Gear_hammer_Accent"] = ("steel", 0.4f), ["Gear_hammer_Trim"] = ("rubber", 0.4f), ["Gear_robosensor_Body"] = ("plastic", 0.4f),
            ["Gear_robosensor_Accent"] = ("plastic", 0.3f), ["Gear_robosensor_Trim"] = ("rubber", 0.3f), ["Gear_chessdetector_Body"] = ("plastic", 0.4f),
            ["Gear_chessdetector_Accent"] = ("rubber", 0.4f), ["Gear_chessdetector_Trim"] = ("steel", 0.2f), ["Gear_profinder_Body"] = ("plastic", 0.4f),
            ["Gear_profinder_Accent"] = ("steel", 0.3f), ["Gear_profinder_Trim"] = ("plastic", 0.3f), ["Gear_profinder_TrimLcd"] = ("gloss", 0.3f),
            ["Gear_blueprint_Body"] = ("paper", 0.6f), ["Gear_blueprint_Accent"] = ("paper", 0.6f), ["Gear_circuit_Body"] = ("plastic", 0.3f),
            ["Gear_circuit_Accent"] = ("plastic", 0.2f), ["Gear_circuit_Trim"] = ("steel", 0.2f), ["Gear_circuit_TrimGold"] = ("steel", 0.15f),
            ["Gear_wires_Body"] = ("rubber", 0.3f), ["Gear_wires_Accent"] = ("rubber", 0.3f), ["Gear_wires_AccentYellow"] = ("rubber", 0.3f),
            ["Gear_wires_Trim"] = ("plastic", 0.3f), ["Gear_wires_TrimCopper"] = ("steel", 0.15f), ["Gear_chip_Body"] = ("plastic", 0.2f),
            ["Gear_chip_Accent"] = ("steel", 0.15f), ["Gear_chip_Trim"] = ("steel", 0.15f), ["Gear_magazine_Trim"] = ("paper", 0.5f),
            // the collectible chess pieces: turned wood on a felt base
            ["Gear_pawn_w_Body"] = ("wood", 0.3f), ["Gear_pawn_b_Body"] = ("wood", 0.3f), ["Gear_rook_w_Body"] = ("wood", 0.3f), ["Gear_rook_b_Body"] = ("wood", 0.3f),
            ["Gear_knight_w_Body"] = ("wood", 0.3f), ["Gear_knight_b_Body"] = ("wood", 0.3f), ["Gear_bishop_w_Body"] = ("wood", 0.3f), ["Gear_bishop_b_Body"] = ("wood", 0.3f),
            ["Gear_queen_w_Body"] = ("wood", 0.3f), ["Gear_queen_b_Body"] = ("wood", 0.3f), ["Gear_king_w_Body"] = ("wood", 0.3f), ["Gear_king_b_Body"] = ("wood", 0.3f),
            ["Gear_pawn_w_Accent"] = ("felt", 0.3f), ["Gear_pawn_b_Accent"] = ("felt", 0.3f), ["Gear_rook_w_Accent"] = ("felt", 0.3f), ["Gear_rook_b_Accent"] = ("felt", 0.3f),
            ["Gear_knight_w_Accent"] = ("felt", 0.3f), ["Gear_knight_b_Accent"] = ("felt", 0.3f), ["Gear_bishop_w_Accent"] = ("felt", 0.3f), ["Gear_bishop_b_Accent"] = ("felt", 0.3f),
            ["Gear_queen_w_Accent"] = ("felt", 0.3f), ["Gear_queen_b_Accent"] = ("felt", 0.3f), ["Gear_king_w_Accent"] = ("felt", 0.3f), ["Gear_king_b_Accent"] = ("felt", 0.3f),
            ["BagFabric"] = ("cloth", 0.6f), ["BagStrap"] = ("cloth", 0.5f), ["BatteryLabel"] = ("plastic", 0.4f), ["BatterySteel"] = ("steel", 0.3f), ["CageIron"] = ("powdercoat", 0.5f),
            ["CanLabel"] = ("paper", 0.5f), ["CanTin"] = ("steel", 0.4f), ["FlashlightBody"] = ("rubber", 0.5f), ["FlashlightMetal"] = ("steel", 0.3f), ["KeyBrass"] = ("steel", 0.25f),
            ["KeyTag"] = ("plastic", 0.4f), ["MapPaper"] = ("paper", 1f), ["MapString"] = ("cloth", 0.3f), ["ScrewHandle"] = ("plastic", 0.3f), ["ShovelSteel"] = ("steel", 0.6f),
            ["ShovelWood"] = ("timber", 0.4f),
        };

        /// <summary>The printed covers and the page edges: on the materials themselves, in every style.</summary>
        static void PrintCovers()
        {
            void Print(string material, string texture, bool tinted = false, float tiling = 1f)
            {
                s_Mats.TryGetValue(material, out var m); // (the items' own come after the styles)
                var tex = Tex($"{CoverTextures}/{texture}.png");
                if (m == null || tex == null || (m.GetTexture("_BaseMap") == tex && m.GetTextureScale("_BaseMap").x == tiling)) return;
                m.SetTexture("_BaseMap", tex);
                m.SetTextureScale("_BaseMap", Vector2.one * tiling);
                if (!tinted) m.SetColor("_BaseColor", Color.white);
                EditorUtility.SetDirty(m);
            }
            foreach (var c in BookColours.Where(s_Mats.ContainsKey)) Print(c, "spines_" + c.Substring("Book".Length).ToLowerInvariant());
            Print("Printed", "printed");
            Print("Gear_magazine_Body", "printed");
            // labels of the small things (labels.png, Tools/Art/generate_labels.py): they keep their print in every look
            foreach (var label in new[] { "Gear_coffee_Trim", "Gear_tomatoes_Body", "Gear_pills_Body", "Gear_matches_Body" }) Print(label, "labels");
            Print("BookCover", "printed");
            Print("BookPagesFlat", "pages_flat", false, 8f);
            Print("Sheets", "sheets");
            Print("KeyCaps", "keyboard");
            Print("BoardSheet", "boards");
            Print("ThingPrint", "wall_things");
            Print("MirrorGlass", "mirror");
            Print("ProjectorCloth", "projector");
            Print("GlobeMap", "globe");
            Print("MachinePrint", "machines");
            // what shines through its own picture
            foreach (var (material, texture) in new[] { ("MachineLit", "machines"), ("MachineGlow", "machine_glow"), ("Displays", "displays") })
            {
                Print(material, texture, true);
                var tex = Tex($"{CoverTextures}/{texture}.png");
                if (!s_Mats.TryGetValue(material, out var m) || tex == null || m.GetTexture("_EmissionMap") == tex) continue;
                m.SetTexture("_EmissionMap", tex);
                EditorUtility.SetDirty(m);
            }
            Print("BookPages", "pages", false, 8f);
        }
    }
}
