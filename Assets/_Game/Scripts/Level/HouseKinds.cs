using UnityEngine;

namespace Stealth.Level
{
    /// <summary>
    /// What kind of building a house is: it changes the program of rooms and the shapes, not the rules. Office, Business,
    /// DataCenter, Warehouse and Headquarters are offices of different character (location <see cref="Location.Office"/>);
    /// Hospital, Garage, Apartments and Abandoned (a research station left to decay) are locations of their own
    /// (<see cref="HouseFeatures.LocationOf"/>). TestRooms is the rooms test: a small quiet house of one floor with every
    /// room type, built by its own level settings (Data/Config/RoomsTest/Level.asset) — never rolled on a route, only
    /// started from the cheat panel (<see cref="HouseKinds.IsTestRooms"/>). Append only: the byte travels and seeds pick by it.
    /// </summary>
    public enum HouseKind : byte { Office, Business, DataCenter, Warehouse, Headquarters, Hospital, Garage, Apartments, Abandoned, TestRooms }

    /// <summary>
    /// House kinds (pure): which one a house is (from its seed: every peer and the route panel agree without sending
    /// anything) and what it does to the building settings — room mix, room sizes, corridors, atriums, rotundas,
    /// cut corners, darkness. Any house may be any kind, the first too (its size and its guards come from its number).
    /// </summary>
    public static class HouseKinds
    {
        public static HouseKind Of(int seed, int house)
        {
            if (IsTestRooms(seed)) return HouseKind.TestRooms;
            var rng = new System.Random(LevelRandom.Mix(seed, Salt.HouseKind, 0));
            int roll = rng.Next(100);
            // a quarter each: a hospital, a garage, an apartment block; the last quarter an office (of some character) or,
            // about half as often, the abandoned research station
            if (roll >= 75) return HouseKind.Apartments;
            if (roll >= 50) return HouseKind.Garage;
            if (roll >= 25) return HouseKind.Hospital;
            if (roll < 9) return HouseKind.Abandoned;
            int office = rng.Next(100);
            return office < 34 ? HouseKind.Office : office < 52 ? HouseKind.Business : office < 68 ? HouseKind.DataCenter
                 : office < 84 ? HouseKind.Warehouse : HouseKind.Headquarters;
        }

        /// <summary>
        /// The seed of a test rooms house: the sign bit. Every seed a run makes is non-negative (the route's, LevelRandom.LevelSeed,
        /// <see cref="SeedOf"/> for the other kinds), so the kind travels with the seed like every kind does — every peer,
        /// the house's info on the wire and a reconnect agree without anything more sent.
        /// </summary>
        public static bool IsTestRooms(int seed) => seed < 0;

        /// <summary>The test rooms house of seed <paramref name="seed"/> (its sign bit set; never 0).</summary>
        public static int TestRoomsSeed(int seed) => seed | int.MinValue;

        /// <summary>The house plays its music (the test rooms are quiet: rooms and generation are tuned in them).</summary>
        public static bool HasMusic(HouseKind kind) => kind != HouseKind.TestRooms;

        /// <summary>A location with a plan of its own (the hospital, the repair shop, the apartment block): not the office generator's rooms and zones.</summary>
        public static bool OwnPlan(HouseKind kind) => HouseRules.For(kind).OwnPlan;

        /// <summary>
        /// A house seed whose house number <paramref name="house"/> is of <paramref name="kind"/> (pure; the n-th one from
        /// <paramref name="from"/> on: a cheat starting "a house of this kind" gets a different one each time).
        /// </summary>
        public static int SeedOf(HouseKind kind, int house, int from)
        {
            if (kind == HouseKind.TestRooms) return TestRoomsSeed(LevelRandom.Mix(from, Salt.HouseKindSeed, 0));
            for (int k = 0; k < 100000; k++)
            {
                int seed = LevelRandom.Mix(from, Salt.HouseKindSeed, k) & 0x7fffffff;
                if (seed != 0 && Of(seed, house) == kind) return seed;
            }
            return 0;
        }

        /// <summary>The kind's name for the player (table <c>house.kind.&lt;kind&gt;</c>).</summary>
        public static string Name(HouseKind kind) => Loc.T(NameKey(kind));

        public static string NameKey(HouseKind kind) => "house.kind." + kind.ToString().ToLowerInvariant();

        /// <summary>A list from the table: "a;b;c" (the lists have the same length in every language — the same company for everyone).</summary>
        static string[] List(string key) => Loc.T(key).Split(';');

        /// <summary>
        /// The name on the HUD: the kind and a made-up company from the house's seed, e.g. «Офис ООО «Рога и Копыта»»
        /// (every peer and every visit to the same house agree). The first name start is the joke one (table <c>house.horns</c>).
        /// </summary>
        public static string Title(int seed, HouseKind kind)
        {
            var rng = new System.Random(LevelRandom.Mix(seed, Salt.HouseTitle, 1));
            string[] forms = List("house.forms"), starts = List("house.name_starts"), ends = List("house.name_ends");
            string form = forms[rng.Next(forms.Length)];
            int start = rng.Next(starts.Length);
            string end = ends[rng.Next(ends.Length)];
            string company = start == 0 ? Loc.T("house.horns") : Loc.F("house.company", starts[start], end);
            return Loc.F("house.title", Name(kind), form, company);
        }

        /// <summary>A copy of the settings with this kind's character (the office keeps them as they are).</summary>
        public static BuildingSettings Apply(HouseKind kind, BuildingSettings b)
        {
            if (kind is HouseKind.Office or HouseKind.TestRooms) return b; // (the test rooms: their own settings as they are)
            var s = (BuildingSettings)b.Clone();
            float Mul(RoomType t) => kind switch
            {
                HouseKind.Business => t switch { RoomType.OpenSpace => 2f, RoomType.Meeting => 1.6f, RoomType.Lounge => 1.6f, RoomType.Storage => 0.5f, RoomType.Server => 0.6f,
                    RoomType.Gym => 1.6f, RoomType.GameRoom => 1.6f, RoomType.NapRoom => 1.4f, _ => 1f },
                HouseKind.DataCenter => t switch { RoomType.Server => 4f, RoomType.Storage => 1.5f, RoomType.OpenSpace => 0.5f, RoomType.Lounge => 0.3f, RoomType.Kitchen => 0.6f,
                    RoomType.ControlRoom => 3f, RoomType.Lab => 1.6f, RoomType.Library => 0.3f, RoomType.Studio => 0.3f, _ => 1f },
                HouseKind.Warehouse => t switch { RoomType.Storage => 5f, RoomType.OpenSpace => 0.6f, RoomType.Meeting => 0.4f, RoomType.Lounge => 0.5f,
                    RoomType.CopyRoom => 2f, RoomType.LockerRoom => 2f, RoomType.Library => 0.3f, _ => 1f },
                HouseKind.Headquarters => t switch { RoomType.Office => 2.2f, RoomType.Meeting => 2f, RoomType.Lounge => 1.3f, RoomType.Storage => 0.5f,
                    RoomType.Library => 2f, RoomType.ControlRoom => 1.5f, RoomType.Studio => 1.5f, _ => 1f },
                // wards and surgeries off long corridors, waiting rooms, toilets, stores of supplies
                // the hospital's own plan lays out the wards, the station, the theatres and imaging (PlanGenerator.Blocks); what
                // is left over: examination rooms, doctors' offices, toilets, stores, the lab, the staff's rooms
                HouseKind.Hospital => t switch { RoomType.Office => 1.4f, RoomType.Restroom => 1f, RoomType.Infirmary => 2f, RoomType.Storage => 1f,
                    RoomType.LockerRoom => 1f, RoomType.Lab => 1f, RoomType.Kitchen => 1f, RoomType.Lounge => 0.8f, RoomType.ControlRoom => 1f, _ => 0f },
                // the shop's own plan lays out the hall, the booth and the block (PlanGenerator.Garage); what is left over is
                // the service office, the toilets, the mechanics' locker room and break room, a waiting lounge, the robots' room
                HouseKind.Garage => t switch { RoomType.Office => 1.4f, RoomType.Restroom => 1f, RoomType.Kitchen => 1f, RoomType.LockerRoom => 1.4f,
                    RoomType.Lounge => 0.8f, RoomType.ControlRoom => 1f, RoomType.Storage => 1f, _ => 0f },
                // the block's own plan lays out every flat (PlanGenerator.Apartments): the program only says whether it has a store
                HouseKind.Apartments => t switch { RoomType.Office => 1f, RoomType.Storage => 1f, _ => 0f },
                // the station's own plan lays out its labs, servers, control and quarters (PlanGenerator.Blocks): what is
                // left over is offices, stores and the robots' room
                HouseKind.Abandoned => t switch { RoomType.Office => 1f, RoomType.Storage => 1f, RoomType.Lab => 1f, RoomType.ControlRoom => 1f, _ => 0f },
                _ => 1f,
            };
            if (b.RoomTypeRules != null)
            {
                var rules = (RoomRule[])b.RoomTypeRules.Clone();
                for (int i = 0; i < rules.Length; i++)
                    if (Mul(rules[i].Room) >= 2f) { rules[i].PerFloor = 0; rules[i].PerHouse = 0; }
                s.RoomTypeRules = rules;
            }
            var weights = new RoomWeight[b.RoomWeights.Length];
            for (int i = 0; i < weights.Length; i++)
                weights[i] = new RoomWeight(b.RoomWeights[i].Room, Mathf.RoundToInt(b.RoomWeights[i].Weight * Mul(b.RoomWeights[i].Room)));
            s.RoomWeights = weights;
            switch (kind)
            {
                case HouseKind.Business:
                    s.Layout = OfficeLayout.Court;
                    s.LinkedChance = 0f; // (pavilions joined by corridors are the plain office's other plan)
                    s.AtriumChance = Mathf.Min(1f, b.AtriumChance * 2f);
                    s.RotundaChance = b.RotundaChance * 1.6f;
                    s.RoomSizeScale = b.RoomSizeScale * 1.3f;
                    break;
                case HouseKind.DataCenter:
                    s.Layout = OfficeLayout.Grid;
                    s.LinkedChance = 0f;
                    s.CorridorChance = 1f;
                    s.AtriumChance = b.AtriumChance * 0.3f;
                    s.RotundaChance = b.RotundaChance * 0.3f;
                    s.CornerCutChance = b.CornerCutChance * 0.4f;
                    s.DarkChance = b.DarkChance + 0.12f;
                    s.RoomSizeScale = b.RoomSizeScale * 0.85f;
                    break;
                case HouseKind.Warehouse:
                    s.Layout = OfficeLayout.Sheds;
                    s.LinkedChance = 0f;
                    s.CorridorChance = b.CorridorChance * 0.35f;
                    s.AtriumChance = Mathf.Min(1f, b.AtriumChance * 1.8f);
                    s.RotundaChance = 0f;
                    s.CornerCutChance = b.CornerCutChance * 0.3f;
                    s.RoomSizeScale = b.RoomSizeScale * 1.7f;
                    break;
                case HouseKind.Headquarters:
                    s.Layout = OfficeLayout.Axis;
                    s.LinkedChance = 0f;
                    s.RotundaChance = b.RotundaChance * 3f;
                    s.CornerCutChance = Mathf.Min(1f, b.CornerCutChance * 1.6f);
                    s.RoomSizeScale = b.RoomSizeScale * 0.9f;
                    break;
            }
            HouseRules.For(kind).Shape(s, b); // a location's own plan
            return s;
        }
    }
}
