using System.Collections.Generic;
using UnityEngine;

namespace Stealth.Level
{
    /// <summary>What a screen with nothing to show looks like (the house is empty at night). In the order of <see cref="LevelArt.Screens"/>: append only.</summary>
    public enum ScreenLook { Off, Static, Cracked, NoSignal, Boot }

    /// <summary>Whose screen it is: a television, a computer's monitor, a display of some other machine.</summary>
    public enum ScreenKind { Appliance, Tv, Computer }

    /// <summary>
    /// The look of the furniture's screens (pure): a house's TVs and monitors are not all one glowing blue panel — many are
    /// switched off (dark glass), a TV is left on with nothing but static, a computer waits at its boot menu or its monitor
    /// says it has no signal, some are broken (cracked glass). Chosen per piece from the house's seed, so every peer sees
    /// the same (LevelBuilder swaps the screen's material: <see cref="LevelArt.Screens"/>). Screens that are used —
    /// terminals, consoles — have their own pictures.
    /// </summary>
    public static class ScreenLooks
    {
        /// <summary>A glowing part smaller than this (its longest side, metres) is a display on an appliance, not a screen: it keeps its dim glow.</summary>
        public const float SmallScreen = 0.2f;

        static readonly HashSet<PropKind> Tvs = new()
        {
            PropKind.TvStand, PropKind.GamingCorner, PropKind.AvCredenza, PropKind.ArcadeMachine, PropKind.Pinball,
        };

        static readonly HashSet<PropKind> Computers = new()
        {
            PropKind.Desk, PropKind.ReceptionDesk, PropKind.StandingDesk, PropKind.SupervisorDesk, PropKind.ExecutiveDesk, PropKind.DoctorDesk,
            PropKind.NurseStationDesk, PropKind.ConsoleDesk, PropKind.MixingDesk, PropKind.LabBench, PropKind.MonitorWall,
        };

        /// <summary>Pictures of small displays on their texture, columns and rows (Tools/Blender/machine_atlas.py DISPLAYS, in this order).</summary>
        public const int DisplayCols = 4, DisplayRows = 6;
        static readonly string[] Displays =
        {
            "synth", "clock", "program", "printer", "treadmill", "score", "vitals", "wave", "server", "power", "temp", "plain", "scan", "turnstile", "welder",
            "balance", "kiosk", "game", "rower", "bars", "paint", "plot", "print3d", "ok",
        };

        static readonly Dictionary<PropKind, string> Shows = new()
        {
            [PropKind.SynthStand] = "synth", [PropKind.Microwave] = "clock", [PropKind.Stove] = "clock", [PropKind.Dishwasher] = "program", [PropKind.WashingMachine] = "program",
            [PropKind.Copier] = "printer", [PropKind.Printer] = "printer", [PropKind.Treadmill] = "treadmill", [PropKind.ExerciseBike] = "treadmill", [PropKind.AirHockey] = "score",
            [PropKind.AnesthesiaMachine] = "vitals", [PropKind.CrashCart] = "vitals", [PropKind.ScopeCart] = "wave", [PropKind.RadioRack] = "wave",
            [PropKind.NetworkCabinet] = "server", [PropKind.TapeLibrary] = "server", [PropKind.UpsUnit] = "power", [PropKind.CracUnit] = "temp", [PropKind.CtScanner] = "scan",
            [PropKind.XrayMachine] = "scan", [PropKind.WeldingCart] = "welder", [PropKind.WheelBalancer] = "balance",
            [PropKind.InfoKiosk] = "kiosk", [PropKind.RowingMachine] = "rower", [PropKind.PaintRack] = "paint", [PropKind.Plotter] = "plot",
            [PropKind.Printer3d] = "print3d", [PropKind.SurgicalTable] = "ok", [PropKind.RobotTable] = "bars", [PropKind.MedTable] = "vitals",
        };

        /// <summary>The plain glow on the displays' texture: lamps, strips of light, rings — what is no display.</summary>
        public static int PlainDisplay => System.Array.IndexOf(Displays, "plain");

        /// <summary>Which picture this machine's display shows (its place on the texture), −1: it has no display of its own.</summary>
        public static int DisplayOf(PropKind kind) => Shows.TryGetValue(kind, out var name) ? System.Array.IndexOf(Displays, name) : -1;

        /// <summary>The display is off, by the piece's stream of the house's seed.</summary>
        public static bool DisplayDark(int seed) => (uint)seed % 100u < 30u;

        public static ScreenKind KindOf(PropKind kind) => Tvs.Contains(kind) ? ScreenKind.Tv : Computers.Contains(kind) ? ScreenKind.Computer : ScreenKind.Appliance;

        /// <summary>The piece's screens are used by the players: never switched off or broken; a display of its own still shows its picture.</summary>
        public static bool InUse(PropKind kind) => kind is PropKind.Terminal or PropKind.Programmer or PropKind.MedTable;

        /// <summary>The look of a piece's screens, from its own stream of the house's seed.</summary>
        public static ScreenLook Pick(int seed, ScreenKind kind)
        {
            int roll = (int)((uint)seed % 100u);
            return kind switch
            {
                ScreenKind.Tv => roll < 45 ? ScreenLook.Off : roll < 85 ? ScreenLook.Static : ScreenLook.Cracked,
                ScreenKind.Computer => roll < 35 ? ScreenLook.Off : roll < 62 ? ScreenLook.NoSignal : roll < 88 ? ScreenLook.Boot : ScreenLook.Cracked,
                _ => roll < 80 ? ScreenLook.Off : ScreenLook.Cracked,
            };
        }

        /// <summary>Where a screen's static is on its texture at a step (pure): it jumps about, never drifting one way.</summary>
        public static Vector2 Jump(int step)
        {
            float x = step * 0.61803398f, y = step * 0.75487766f;
            return new Vector2(x - Mathf.Floor(x), y - Mathf.Floor(y));
        }

        /// <summary>
        /// How far a monitor's "no signal" box stands off the screen's middle at a step, in screens (pure): it moves about, as
        /// monitors do so it does not burn in, its whole box (<see cref="BoxHalf"/>) staying on the screen.
        /// </summary>
        public static Vector2 Drift(int step)
        {
            var j = Jump(step);
            return new Vector2((j.x - 0.5f) * 2f * (0.5f - BoxHalf.x), (j.y - 0.5f) * 2f * (0.5f - BoxHalf.y));
        }

        /// <summary>Half the "no signal" box on its picture, in screens (Tools/Art/generate_screens.py screen_nosignal).</summary>
        public static readonly Vector2 BoxHalf = new(0.24f, 0.17f);
    }

    /// <summary>
    /// The screens of the built house: their pictures fitted to each screen's face (<see cref="ScreenFaces"/>, again whenever
    /// a style swaps the furniture's models), the static jumping a dozen times a second and the "no signal" boxes moving
    /// every few seconds (a property block per screen — which also keeps them out of the merged furniture — only on the
    /// ones in view). Picture only, on every peer.
    /// </summary>
    public static class LiveScreens
    {
        /// <summary>How many times the noise texture repeats over a screen, how often it jumps and how often a "no signal" box moves, seconds.</summary>
        public const float Tiling = 3f, Interval = 0.08f, DriftEvery = 4f;

        static readonly List<(Renderer r, ScreenLook look)> s_Screens = new();
        static readonly int StId = Shader.PropertyToID("_BaseMap_ST");
        static MaterialPropertyBlock s_Block;
        static float s_Next, s_NextDrift;
        static int s_Step, s_DriftStep;

        static LiveScreens() => VisualStyles.GeometryChanged += Fit;

        /// <summary>Screens that move: static and "no signal".</summary>
        public static int Count
        {
            get
            {
                int n = 0;
                foreach (var (_, look) in s_Screens) if (Moves(look)) n++;
                return n;
            }
        }

        static bool Moves(ScreenLook look) => look is ScreenLook.Static or ScreenLook.NoSignal;

        public static void Clear()
        {
            s_Screens.Clear();
            s_Displays.Clear();
        }

        /// <summary>What this screen shows, if it is one of the house's.</summary>
        public static bool TryGet(Renderer r, out ScreenLook look)
        {
            foreach (var s in s_Screens)
                if (s.r == r) { look = s.look; return true; }
            look = default;
            return false;
        }

        public static void Add(Renderer r, ScreenLook look)
        {
            if (r == null) return;
            s_Screens.Add((r, look));
            Fit(r);
            if (look == ScreenLook.Static) Set(r, look, ScreenLooks.Jump(s_Screens.Count));
            else if (look == ScreenLook.NoSignal) Set(r, look, ScreenLooks.Drift(s_Screens.Count));
        }

        static readonly List<(Renderer r, PictureSheet picture)> s_Displays = new();

        /// <summary>A small display showing its picture (<see cref="ScreenLooks.DisplayOf"/>).</summary>
        public static void AddDisplay(Renderer r, int picture) => AddPicture(r, new PictureSheet
        {
            Cols = ScreenLooks.DisplayCols, Rows = ScreenLooks.DisplayRows, Cell = picture, Rest = ScreenLooks.PlainDisplay, MinSide = ScreenFaces.DisplaySide, Lying = true,
        });

        /// <summary>A part showing its own picture of a sheet (<see cref="PropPictures"/>): fitted again whenever the models change.</summary>
        public static void AddPicture(Renderer r, PictureSheet picture)
        {
            if (r == null) return;
            s_Displays.Add((r, picture));
            Fit(r, picture);
        }

        /// <summary>The screens' models were swapped: their pictures are fitted to the new faces.</summary>
        static void Fit()
        {
            foreach (var (r, _) in s_Screens) Fit(r);
            s_Displays.RemoveAll(d => d.r == null);
            foreach (var (r, picture) in s_Displays) Fit(r, picture);
        }

        static void Fit(Renderer r, PictureSheet picture)
        {
            if (r == null || !r.TryGetComponent<MeshFilter>(out var mf) || mf.sharedMesh == null) return;
            var fitted = ScreenFaces.Of(mf.sharedMesh, picture);
            if (fitted != mf.sharedMesh) mf.sharedMesh = fitted;
        }

        static void Fit(Renderer r)
        {
            if (r == null || !r.TryGetComponent<MeshFilter>(out var mf) || mf.sharedMesh == null) return;
            var fitted = ScreenFaces.Of(mf.sharedMesh);
            if (fitted != mf.sharedMesh) mf.sharedMesh = fitted;
        }

        /// <summary>One frame (called by the level's light budget, which runs wherever a house stands).</summary>
        public static void Tick(float time)
        {
            if (s_Screens.Count == 0) return;
            bool snow = !(time >= s_Next - Interval && time < s_Next), drift = !(time >= s_NextDrift - DriftEvery && time < s_NextDrift);
            if (!snow && !drift) return;
            if (snow) { s_Next = time + Interval; s_Step++; }
            if (drift) { s_NextDrift = time + DriftEvery; s_DriftStep++; }
            for (int i = s_Screens.Count - 1; i >= 0; i--)
            {
                var (r, look) = s_Screens[i];
                if (r == null) { s_Screens.RemoveAt(i); continue; }
                if (snow && look == ScreenLook.Static && r.isVisible) Set(r, look, ScreenLooks.Jump(s_Step * 7 + i));
                else if (drift && look == ScreenLook.NoSignal) Set(r, look, ScreenLooks.Drift(s_DriftStep * 5 + i));
            }
        }

        static void Set(Renderer r, ScreenLook look, Vector2 at)
        {
            s_Block ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(s_Block);
            float tiling = look == ScreenLook.Static ? Tiling : 1f;
            s_Block.SetVector(StId, new Vector4(tiling, tiling, at.x, at.y));
            r.SetPropertyBlock(s_Block);
        }
    }
}
