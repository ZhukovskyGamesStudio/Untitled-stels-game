using System.Collections.Generic;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>What kind of building a stop on the route is. Only offices so far.</summary>
    public enum HouseKind : byte { Office }

    /// <summary>A house on the route: its column (how deep into the run) and row, and the seed it is built from.</summary>
    public readonly struct RouteNode
    {
        public readonly int Depth, Lane, Seed;
        public readonly HouseKind Kind;

        public RouteNode(int depth, int lane, int seed, HouseKind kind)
        {
            Depth = depth;
            Lane = lane;
            Seed = seed;
            Kind = kind;
        }

        public static string KindName(HouseKind kind) => kind switch
        {
            _ => "Офис",
        };
    }

    /// <summary>
    /// The run's route through the sewers (pure, unit tested), like a Slay the Spire map: columns of houses, each next
    /// column 2–4 houses, every house leads to 1–3 houses of the next column, every house can be reached. Everything
    /// follows from the run seed, column by column on demand, so every peer has the same map without sending it.
    /// </summary>
    public sealed class RunMap
    {
        public const int MinLanes = 2, MaxLanes = 4;
        const int RouteSalt = 0x5E3E7, EdgeSalt = 0x5E3E8;

        readonly int m_RunSeed;
        readonly Dictionary<int, int> m_Lanes = new();
        readonly Dictionary<int, List<int>[]> m_Edges = new();

        public RunMap(int runSeed) => m_RunSeed = runSeed;

        public int RunSeed => m_RunSeed;

        /// <summary>Houses in a column: the run starts at a single house.</summary>
        public int LanesAt(int depth)
        {
            if (depth <= 0) return 1;
            if (m_Lanes.TryGetValue(depth, out int n)) return n;
            var rng = new System.Random(LevelRandom.Mix(m_RunSeed, RouteSalt, depth));
            n = rng.Next(MinLanes, MaxLanes + 1);
            m_Lanes[depth] = n;
            return n;
        }

        /// <summary>The house at a column and row. Row 0 keeps the classic seed of that depth (tests, -level).</summary>
        public RouteNode Node(int depth, int lane)
        {
            int seed = LevelRandom.LevelSeed(m_RunSeed, depth);
            if (lane > 0) seed = (LevelRandom.Mix(seed, RouteSalt, lane) & int.MaxValue) | 1;
            return new RouteNode(depth, lane, seed, HouseKind.Office);
        }

        /// <summary>Rows of the next column this house leads to (1–3, sorted).</summary>
        public IReadOnlyList<int> Next(int depth, int lane) => EdgesFrom(depth)[Mathf.Clamp(lane, 0, LanesAt(depth) - 1)];

        /// <summary>Position across the column, 0..1 (for drawing and for linking nearby houses).</summary>
        public float Across(int depth, int lane) => (lane + 0.5f) / LanesAt(depth);

        List<int>[] EdgesFrom(int depth)
        {
            if (m_Edges.TryGetValue(depth, out var edges)) return edges;
            int n = LanesAt(depth), m = LanesAt(depth + 1);
            var rng = new System.Random(LevelRandom.Mix(m_RunSeed, EdgeSalt, depth));
            edges = new List<int>[n];
            for (int i = 0; i < n; i++)
            {
                // The nearest house of the next column, sometimes also its neighbours: 1 to 3 ways on.
                int near = Nearest(Across(depth, i), m);
                var list = new List<int> { near };
                if (near > 0 && rng.NextDouble() < 0.45) list.Add(near - 1);
                if (near < m - 1 && rng.NextDouble() < 0.45) list.Add(near + 1);
                edges[i] = list;
            }
            // Nobody is unreachable: a house without a way in gets one from the nearest house before it.
            for (int j = 0; j < m; j++)
            {
                bool reached = false;
                foreach (var l in edges) reached |= l.Contains(j);
                if (reached) continue;
                int from = Nearest(Across(depth + 1, j), n);
                edges[from].Add(j);
            }
            foreach (var l in edges) l.Sort();
            m_Edges[depth] = edges;
            return edges;
        }

        static int Nearest(float across, int lanes) => Mathf.Clamp(Mathf.RoundToInt(across * lanes - 0.5f), 0, lanes - 1);
    }

    /// <summary>
    /// The team's vote for the next house (pure, unit tested): one vote per player, the house with most votes wins,
    /// a tie is decided by chance.
    /// </summary>
    public sealed class RouteVotes
    {
        readonly Dictionary<ulong, int> m_Votes = new();

        public IReadOnlyDictionary<ulong, int> All => m_Votes;
        public int Version { get; private set; }

        public void Cast(ulong player, int lane)
        {
            if (m_Votes.TryGetValue(player, out int old) && old == lane) return;
            m_Votes[player] = lane;
            Version++;
        }

        public void Clear()
        {
            if (m_Votes.Count == 0) return;
            m_Votes.Clear();
            Version++;
        }

        /// <summary>Client: the server's votes.</summary>
        public void ApplyReplicated(IEnumerable<KeyValuePair<ulong, int>> votes)
        {
            m_Votes.Clear();
            foreach (var v in votes) m_Votes[v.Key] = v.Value;
            Version++;
        }

        public int Count(int lane)
        {
            int n = 0;
            foreach (var v in m_Votes.Values) if (v == lane) n++;
            return n;
        }

        /// <summary>Every player in the team has voted for one of the options.</summary>
        public bool Everyone(IEnumerable<ulong> players, IReadOnlyList<int> options)
        {
            bool any = false;
            foreach (var p in players)
            {
                any = true;
                if (!m_Votes.TryGetValue(p, out int lane) || !Contains(options, lane)) return false;
            }
            return any;
        }

        /// <summary>The option with most votes; a tie between the leaders is decided by <paramref name="rng"/>.</summary>
        public int Winner(IReadOnlyList<int> options, System.Random rng)
        {
            int best = -1;
            var tied = new List<int>();
            foreach (var o in options)
            {
                int c = Count(o);
                if (c > best) { best = c; tied.Clear(); }
                if (c == best) tied.Add(o);
            }
            return tied.Count == 1 ? tied[0] : tied[rng.Next(tied.Count)];
        }

        static bool Contains(IReadOnlyList<int> list, int value)
        {
            foreach (var v in list) if (v == value) return true;
            return false;
        }
    }
}
