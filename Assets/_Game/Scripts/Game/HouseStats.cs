using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>What the server counts of every player in a house, for the panel of the drive to the next one.</summary>
    public enum StatKind : byte
    {
        Fuel, Downs, Revives, Distance, Noise, Talk, Hacks, Pickups, Locks, Crouch, Spotted, Falls,
        /// <summary>No title of their own: did nothing anyone else did not do more of.</summary>
        None = 255,
    }

    /// <summary>One player's title for the house: the thing they did more of than the others, and how much.</summary>
    public struct HouseTitle
    {
        public ulong Player;
        public StatKind Kind;
        public float Value;
        /// <summary>What they did, for the row under the title: fuel, falls, teammates raised, metres walked.</summary>
        public int Fuel, Downs, Revives, Metres;
    }

    /// <summary>
    /// The house's statistics (pure, unit tested): the server adds what each player did (<see cref="Add"/>); when the team
    /// drives off, <see cref="Titles"/> gives every player the thing they stand out in — the most fuel, the most falls, the
    /// most talk… — each title to one player while there are enough, and the result goes to every peer as one string
    /// (<see cref="Pack"/> / <see cref="Unpack"/>).
    /// </summary>
    public sealed class HouseStats
    {
        public static readonly int Kinds = 12;

        /// <summary>How much of a thing is "a lot" for one player in one house: weighs a title when nobody else did any.</summary>
        static readonly float[] Typical = { 60f, 1f, 1f, 300f, 40f, 30f, 1f, 6f, 1f, 60f, 1f, 1f };

        readonly Dictionary<ulong, float[]> m_By = new();

        public void Clear() => m_By.Clear();

        public void Add(ulong player, StatKind kind, float amount = 1f)
        {
            if (kind == StatKind.None || amount == 0f) return;
            if (!m_By.TryGetValue(player, out var v)) m_By[player] = v = new float[Kinds];
            v[(int)kind] += amount;
        }

        public float Get(ulong player, StatKind kind) =>
            kind != StatKind.None && m_By.TryGetValue(player, out var v) ? v[(int)kind] : 0f;

        /// <summary>
        /// A title for each of <paramref name="players"/>: of all (player, thing) pairs the one where a player stands out most
        /// is given first — their share of the team's total, then how much it is for one house — then the next among the
        /// players and things left. A thing goes to one player only while every player can still get one of their own.
        /// </summary>
        public List<HouseTitle> Titles(IReadOnlyList<ulong> players)
        {
            var result = new List<HouseTitle>();
            var candidates = new List<(float score, int p, int k)>();
            var sums = new float[Kinds];
            foreach (var id in players)
                for (int k = 0; k < Kinds; k++) sums[k] += Get(id, (StatKind)k);
            for (int p = 0; p < players.Count; p++)
                for (int k = 0; k < Kinds; k++)
                {
                    float v = Get(players[p], (StatKind)k);
                    if (v <= 0f) continue;
                    float share = v / sums[k];
                    candidates.Add((share + 0.15f * Mathf.Min(v / Typical[k], 5f), p, k));
                }
            candidates.Sort((a, b) => b.score.CompareTo(a.score));
            var given = new bool[players.Count];
            var taken = new bool[Kinds];
            var titles = new HouseTitle?[players.Count];
            for (int pass = 0; pass < 2; pass++)
                foreach (var (_, p, k) in candidates)
                {
                    if (given[p] || (pass == 0 && taken[k])) continue;
                    given[p] = true;
                    taken[k] = true;
                    titles[p] = Title(players[p], (StatKind)k);
                }
            for (int p = 0; p < players.Count; p++) result.Add(titles[p] ?? Title(players[p], StatKind.None));
            return result;
        }

        HouseTitle Title(ulong player, StatKind kind) => new()
        {
            Player = player, Kind = kind, Value = Get(player, kind),
            Fuel = Mathf.RoundToInt(Get(player, StatKind.Fuel)), Downs = Mathf.RoundToInt(Get(player, StatKind.Downs)),
            Revives = Mathf.RoundToInt(Get(player, StatKind.Revives)), Metres = Mathf.RoundToInt(Get(player, StatKind.Distance)),
        };

        /// <summary>The house's number and the titles as one string for the wire.</summary>
        public static string Pack(int house, IReadOnlyList<HouseTitle> titles)
        {
            var sb = new StringBuilder();
            sb.Append(house.ToString(CultureInfo.InvariantCulture));
            foreach (var t in titles)
                sb.Append('|').Append(t.Player.ToString(CultureInfo.InvariantCulture)).Append(',').Append((int)t.Kind)
                  .Append(',').Append(t.Value.ToString("0.#", CultureInfo.InvariantCulture))
                  .Append(',').Append(t.Fuel).Append(',').Append(t.Downs).Append(',').Append(t.Revives).Append(',').Append(t.Metres);
            return sb.ToString();
        }

        /// <summary>What <see cref="Pack"/> made (false if it is not that).</summary>
        public static bool Unpack(string packed, out int house, List<HouseTitle> titles)
        {
            titles.Clear();
            house = 0;
            if (string.IsNullOrEmpty(packed)) return false;
            var parts = packed.Split('|');
            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out house)) return false;
            for (int i = 1; i < parts.Length; i++)
            {
                var f = parts[i].Split(',');
                if (f.Length < 7 || !ulong.TryParse(f[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)) continue;
                int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int kind);
                float.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float value);
                int.TryParse(f[3], out int fuel);
                int.TryParse(f[4], out int downs);
                int.TryParse(f[5], out int revives);
                int.TryParse(f[6], out int metres);
                titles.Add(new HouseTitle { Player = id, Kind = (StatKind)kind, Value = value, Fuel = fuel, Downs = downs, Revives = revives, Metres = metres });
            }
            return true;
        }

        /// <summary>The title's name (<c>stats.title.&lt;kind&gt;</c>).</summary>
        public static string TitleKey(StatKind kind) => "stats.title." + KindKey(kind);

        /// <summary>What they did, with the number (<c>stats.did.&lt;kind&gt;</c>, a plural form).</summary>
        public static string DidKey(StatKind kind) => "stats.did." + KindKey(kind);

        static string KindKey(StatKind kind) => kind == StatKind.None ? "none" : kind.ToString().ToLowerInvariant();
    }
}
