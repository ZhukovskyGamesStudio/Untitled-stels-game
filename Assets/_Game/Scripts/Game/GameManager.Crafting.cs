using System.Collections.Generic;
using System.Linq;
using Stealth.Configs;
using Stealth.Enemies;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// The car's workbench and what it makes (server-decided, replicated by the network twin): the bench's 4 x 4 grid
    /// (<see cref="Bench"/>), «combine» (<see cref="CombineServer"/>, <see cref="BenchRules"/>) and the recipes the team has
    /// made this run (<see cref="KnownRecipes"/>); the blueprints and chess pieces a house gets; the double espresso's ram,
    /// the robots and devices the upgraded screwdriver took apart, the hammer's holes, the robot sensors lying on the floor.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>The workbench in the car's bed: what lies on it (it rides with the car the whole run).</summary>
        public TeamStash Bench { get; } = new(CraftBook.BenchSize, CraftBook.BenchSize);

        /// <summary>The recipes the team has made this run (a bit per recipe of <see cref="Book"/>): the bench shows their result.</summary>
        public int KnownRecipes { get; private set; }

        /// <summary>
        /// The last press of «combine», for every peer's bench: its count in the high bits, what came of it in the low byte
        /// (<see cref="BenchOutcome"/>) and the shape that was made or broke in the next.
        /// </summary>
        public int LastCombine { get; private set; }

        /// <summary>What a press of «combine» did.</summary>
        public enum BenchOutcome : byte { None, Made, Failed, Broke }

        /// <summary>This run's recipes (every peer makes the same from the run's seed).</summary>
        public CraftBook Book => CraftBook.Use(Level.RunSeed, Tuning.Items.Crafting);

        public void ApplyCrafting(int known, int lastCombine)
        {
            KnownRecipes = known;
            LastCombine = lastCombine;
        }

        /// <summary>Whether a player at <paramref name="position"/> can work at the car's bench.</summary>
        public bool AtBench(Vector3 position)
        {
            var bench = Vehicles.Car.Active != null ? Vehicles.Car.Active.Bench : null;
            return bench != null && ServerReach.WithinFlat(position, bench.Point, bench.Reach + 0.6f)
                   && Mathf.Abs(position.y - bench.Point.y) < 2.2f;
        }

        /// <summary>Server: «combine» — the bench's things become what their recipe makes, or (no recipe) one of them may break.</summary>
        public void CombineServer(PlayerController player)
        {
            if (player == null || player.Downed || !AtBench(player.transform.position) || !BenchRules.CanCombine(Bench.Items.Count)) return;
            var book = Book;
            var rng = new System.Random(LevelRandom.Mix(Level.RunSeed, Salt.CraftBreak, ++m_Combines));
            var o = BenchRules.Combine(Bench.Items, book, rng, Tuning.Items.Crafting.BreakChance);
            int count = (LastCombine >> 16) + 1;
            if (o.Recipe >= 0)
            {
                byte made = book.Get(o.Recipe).Result;
                Bench.Clear();
                if (Bench.FindSpot(made, out var x, out var y, out var rot)) Bench.Add(made, ItemCatalog.FreshColor(made, 0), x, y, rot);
                KnownRecipes |= 1 << o.Recipe;
                LastCombine = count << 16 | made << 8 | (int)BenchOutcome.Made;
                NoteServer(player, StatKind.Pickups);
                return;
            }
            if (o.Broken >= 0)
            {
                var broken = Bench.Items[o.Broken];
                Bench.Take(broken);
                LastCombine = count << 16 | broken.Shape << 8 | (int)BenchOutcome.Broke;
                return;
            }
            LastCombine = count << 16 | (int)BenchOutcome.Failed;
        }

        int m_Combines;

        /// <summary>Server: a new run — the bench is empty, nothing is known yet.</summary>
        public void ClearCraftingServer()
        {
            Bench.Clear();
            KnownRecipes = 0;
            LastCombine = 0;
        }

        // ------------------------------------------------------------------ the things the bench makes

        /// <summary>The team's hack slots now: one more while anyone carries an upgraded keyboard (every peer: backpacks are replicated).</summary>
        int CraftedHackSlots()
        {
            bool upgraded = false;
            foreach (var p in PlayerController.All)
                if (p.TryGetComponent<PlayerInventory>(out var inv) && inv.CarriesExactly(ItemCatalog.ProKeyboard)) { upgraded = true; break; }
            return CraftedRules.HackSlots(Tuning.Player.Interaction.MaxHacked, upgraded, Tuning.Items.Crafted);
        }

        readonly HashSet<Detector> m_Unscrewed = new();

        /// <summary>Server: a player under a double espresso ran into a robot — it is out for a while (no hack slot taken).</summary>
        public void RamServer(PlayerController player, Detector robot, float seconds)
        {
            robot.HackServer(seconds);
            NoiseSystem.EmitServer(robot.transform.position, 8f);
        }

        /// <summary>
        /// Server: the upgraded screwdriver took a robot or a device apart (not a hack) — it is off for good (it never wakes, it takes no slot) and
        /// its innards fall out (a circuit board, wires, a chip: fuel).
        /// </summary>
        public bool UnscrewServer(PlayerController player, Detector device)
        {
            if (device == null || m_Unscrewed.Contains(device)) return false;
            m_Unscrewed.Add(device);
            m_Hacked.Remove(device);
            device.HackServer(0f); // for good
            SyncHackSlots();
            var rng = new System.Random(LevelRandom.Mix(Level.Seed, Salt.Unscrewed, m_Unscrewed.Count));
            var at = device.Position;
            foreach (var shape in CraftedRules.UnscrewDrops(rng, Tuning.Items.Crafted))
            {
                var p = at + new Vector3((float)rng.NextDouble() - 0.5f, -0.1f, (float)rng.NextDouble() - 0.5f) * 0.4f;
                SpawnItemServer(shape, ItemCatalog.FreshColor(shape, 0), p, rng.Next(360));
            }
            NoteHackServer(player);
            return true;
        }

        /// <summary>Server: the hammer hit the floor at <paramref name="point"/> — a hole there if one may be knocked (false if not).</summary>
        public bool BreakFloorServer(PlayerController player, Vector3 point)
        {
            var layout = ActiveLevel.Layout;
            if (Phase != RunPhase.Playing || layout == null || !ActiveLevel.Ready) return false;
            int floor = FloorHoles.FloorAt(point.y);
            var cells = FloorHoles.CellsAt(point, Tuning.Items.Crafted.HoleSize);
            if (!FloorHoles.CanBreak(layout, floor, cells, HolesMade())) return false;
            // nothing standing over it (a desk, a shelf: the hole is in an open bit of floor)
            var r = FloorHoles.Metres(cells);
            var centre = new Vector3(r.center.x, LevelLayout.FloorY(floor) + 0.6f, r.center.y);
            foreach (var c in Physics.OverlapBox(centre, new Vector3(r.width / 2f - 0.05f, 0.5f, r.height / 2f - 0.05f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                if (c.GetComponentInParent<PlayerController>() == null && c.GetComponentInParent<WorldItem>() == null && c.attachedRigidbody == null)
                    return false;
            m_Opened.Add(OpenKeys.Hole(floor, cells.x, cells.y));
            OpenedVersion++;
            NoiseSystem.EmitServer(point, Tuning.Items.Crafted.HoleNoise);
            return true;
        }

        /// <summary>The holes knocked in this house so far (every peer: keys of <see cref="Opened"/>).</summary>
        IEnumerable<(int floor, RectInt cells)> HolesMade()
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(Tuning.Items.Crafted.HoleSize / HousePlan.Res));
            foreach (int key in m_Opened)
            {
                if (OpenKeys.KindOf(key) != OpenKind.Hole) continue;
                var (f, x, z) = OpenKeys.HoleOf(key);
                yield return (f, new RectInt(x, z, n, n));
            }
        }

        readonly HashSet<int> m_HolesCut = new();
        GameObject m_HolesRoot;
        int m_HolesSeen = -1;
        IEnumerator<bool> m_Restyle;

        /// <summary>Every peer, every frame: a new hole key cuts its hole into the built house, then the style is put on again over a few frames.</summary>
        void UpdateHoles()
        {
            if (m_Restyle != null && !m_Restyle.MoveNext()) m_Restyle = null;
            if (ActiveLevel.Root != m_HolesRoot) { m_HolesRoot = ActiveLevel.Root; m_HolesCut.Clear(); m_HolesSeen = -1; }
            var layout = ActiveLevel.Layout;
            // (the keys are this house's only once it is the house being played: on the drive the old house's are still there)
            if (!ActiveLevel.Ready || m_HolesRoot == null || layout == null || layout.Seed != Level.Seed || OpenedVersion == m_HolesSeen) return;
            m_HolesSeen = OpenedVersion;
            bool cut = false;
            foreach (var (f, cells) in HolesMade())
            {
                if (!m_HolesCut.Add(OpenKeys.Hole(f, cells.x, cells.y)) || !FloorHoles.CanBreak(layout, f, cells)) continue;
                FloorHoles.Cut(f, cells);
                cut = true;
            }
            if (cut) m_Restyle = VisualStyles.ReapplySteps().GetEnumerator();
        }

        float m_SensorsAt;
        readonly Dictionary<Detector, Vector3> m_SeenAt = new();

        /// <summary>
        /// Server, a few times a second: every robot sensor lying on a floor marks the moving robots it sees — as a press of
        /// the mark key does (seen through walls by the whole team).
        /// </summary>
        void UpdateRoboSensorsServer()
        {
            if (Time.time < m_SensorsAt) return;
            float dt = Time.time - m_SensorsAt + 0.25f;
            m_SensorsAt = Time.time + 0.25f;
            var s = Tuning.Items.Crafted;
            var team = Tuning.Player.Team;
            var robots = new List<(Detector d, float speed)>();
            if (m_SeenAt.Count > Detector.All.Count + 8)
                foreach (var gone in m_SeenAt.Keys.Where(k => k == null).ToList()) m_SeenAt.Remove(gone);
            foreach (var d in Detector.All)
            {
                if (d is not RobotController || d.IsHacked) continue;
                var at = d.transform.position;
                float speed = m_SeenAt.TryGetValue(d, out var was) ? (at - was).magnitude / Mathf.Max(0.05f, dt) : 0f;
                m_SeenAt[d] = at;
                robots.Add((d, speed));
            }
            const int mask = ~(1 << LevelMetrics.CeilingLayer);
            foreach (var item in WorldItem.All)
            {
                if (item.Shape != ItemCatalog.RoboSensor || item.Taking || (item.Body != null && !item.Body.IsSleeping() && item.Body.linearVelocity.sqrMagnitude > 0.01f)) continue;
                var eye = item.transform.position + Vector3.up * 0.15f;
                foreach (var (d, speed) in robots)
                {
                    var target = d.Position;
                    if (d.IsMarked || !CraftedRules.SensorSees(target - eye, item.transform.forward, speed, s)) continue;
                    if (Physics.Linecast(eye, target, out var hit, mask, QueryTriggerInteraction.Ignore)
                        && hit.collider.GetComponentInParent<Detector>() != d && hit.collider.GetComponentInParent<WorldItem>() != item) continue;
                    d.MarkServer(Session.ServerTimeF + team.MarkDuration);
                    Link.BroadcastPing(ulong.MaxValue, PingKind.Enemy, target, d.NetId);
                }
            }
        }

        // ------------------------------------------------------------------ what a house gets

        /// <summary>
        /// Server, a new house: its blueprints (a recipe each) and, for every chess detector the team has (on a player, in
        /// the bed, on the bench), its chess pieces — on spots the loot left free.
        /// </summary>
        int SpawnCraftingLoot(LevelLayout layout, int seed, List<LootPlanner.Loot> loot)
        {
            var s = Tuning.Items.Crafting;
            var rng = new System.Random(LevelRandom.Mix(seed, Salt.Blueprints, 0));
            var things = new List<(byte shape, byte color)>();
            int recipes = Book.Recipes.Count;
            for (int i = 0; i < s.BlueprintsPerHouse && recipes > 0; i++) things.Add((ItemCatalog.Blueprint, (byte)rng.Next(recipes)));
            var chessRng = new System.Random(LevelRandom.Mix(seed, Salt.ChessPieces, 0));
            int detectors = ChessDetectors();
            for (int i = 0; i < detectors * s.ChessPerDetector; i++)
            {
                byte piece = CraftedRules.ChessPiece(chessRng, s.ChessWeights);
                things.Add((piece, ItemCatalog.FreshColor(piece, 0)));
            }
            int total = 0;
            var spots = LootPlanner.Extras(layout, rng, things.Count, loot, Tuning.Level.Loot.MinSpacing);
            for (int i = 0; i < things.Count && i < spots.Count; i++)
                total += SpawnItemServer(things[i].shape, things[i].color, spots[i], rng.Next(360)).Value;
            // with the car's radio: cassettes, each a random song (a stream of their own: the rest keeps its places)
            if (Upgrades.Level(Vehicles.CarUpgrade.Radio) > 0)
            {
                var tapes = new System.Random(LevelRandom.Mix(seed, Salt.Cassettes, 0));
                int n = Mathf.Max(0, UpgradeSettings.CassettesPerHouse);
                var at = LootPlanner.Extras(layout, tapes, n, loot, Tuning.Level.Loot.MinSpacing);
                for (int i = 0; i < n && i < at.Count; i++)
                    total += SpawnItemServer(ItemCatalog.Cassette, (byte)tapes.Next(ItemCatalog.CassetteSongs), at[i], tapes.Next(360)).Value;
            }
            return total;
        }

        /// <summary>How many chess detectors the team has: on the players, in the car's bed, on the bench and in the stash.</summary>
        int ChessDetectors()
        {
            int n = 0;
            foreach (var p in PlayerController.All)
                if (p.TryGetComponent<PlayerInventory>(out var inv)) n += inv.Items.Count(e => e.Shape == ItemCatalog.ChessDetector);
            n += Cargo.Items.Count(e => e.Shape == ItemCatalog.ChessDetector);
            n += Bench.Items.Count(e => e.Shape == ItemCatalog.ChessDetector);
            n += Stash.Items.Count(e => e.Shape == ItemCatalog.ChessDetector);
            return n;
        }
    }
}
