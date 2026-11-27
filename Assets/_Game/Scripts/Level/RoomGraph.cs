using System.Collections.Generic;

namespace Stealth.Level
{
    /// <summary>Who goes through which openings, for <see cref="RoomGraph"/>.</summary>
    public enum Passage : byte
    {
        /// <summary>A player: through every opening (doors, passages, ruins, crawl ways, squeezes, the illusion).</summary>
        Players,
        /// <summary>A robot: doors, open passages and breaches only.</summary>
        Robots,
        /// <summary>Walking, the way privacy is measured (<see cref="RoomDepth"/>): a robot's ways and the illusion walls.</summary>
        Walkers,
    }

    /// <summary>
    /// The rooms of a plan as a graph (pure): linked by the openings someone passes (<see cref="Passage"/>) and by the
    /// stairs. One place for every "which rooms can be reached", "how many steps deep" and "which rooms lie behind
    /// this door" the generator asks.
    /// </summary>
    public sealed class RoomGraph
    {
        readonly List<(int room, int opening)>[] m_Links;

        public int Rooms => m_Links.Length;

        public RoomGraph(HousePlan plan, IReadOnlyList<Stair> stairs, Passage passage)
        {
            m_Links = new List<(int, int)>[plan.Rooms.Count];
            for (int i = 0; i < plan.Openings.Count; i++)
            {
                var o = plan.Openings[i];
                if (!Passes(passage, o.Kind)) continue;
                var w = plan.Walls[o.Wall];
                Link(w.RoomA, w.RoomB, i);
            }
            if (stairs != null) foreach (var s in stairs) Link(s.Room, s.RoomAbove, -1);
        }

        public static bool Passes(Passage passage, EdgeType kind) => passage switch
        {
            Passage.Players => kind != EdgeType.Wall,
            Passage.Robots => kind is EdgeType.Door or EdgeType.Open or EdgeType.Breach,
            _ => kind is EdgeType.Door or EdgeType.Open or EdgeType.Breach or EdgeType.Illusion,
        };

        void Link(int a, int b, int opening)
        {
            if (a < 0 || b < 0 || a >= m_Links.Length || b >= m_Links.Length) return;
            (m_Links[a] ??= new List<(int, int)>()).Add((b, opening));
            (m_Links[b] ??= new List<(int, int)>()).Add((a, opening));
        }

        /// <summary>The rooms one step from <paramref name="room"/> (a room linked twice is listed twice).</summary>
        public IEnumerable<int> Neighbours(int room)
        {
            if (room < 0 || room >= m_Links.Length || m_Links[room] == null) yield break;
            foreach (var (n, _) in m_Links[room]) yield return n;
        }

        /// <summary>Steps from <paramref name="start"/> per room (−1: not reached), not through <paramref name="skip"/> nor into <paramref name="closed"/>.</summary>
        public int[] Steps(int start, int skip = -1, ICollection<int> closed = null)
        {
            var steps = new int[m_Links.Length];
            for (int i = 0; i < steps.Length; i++) steps[i] = -1;
            if (start < 0 || start >= steps.Length) return steps;
            var q = new Queue<int>();
            steps[start] = 0;
            q.Enqueue(start);
            while (q.Count > 0)
            {
                int r = q.Dequeue();
                if (m_Links[r] == null) continue;
                foreach (var (n, opening) in m_Links[r])
                {
                    if (steps[n] >= 0 || (skip >= 0 && opening == skip) || (closed != null && closed.Contains(n))) continue;
                    steps[n] = steps[r] + 1;
                    q.Enqueue(n);
                }
            }
            return steps;
        }

        /// <summary>The rooms reached from <paramref name="start"/> (with it), as <see cref="Steps"/>.</summary>
        public HashSet<int> Reach(int start, int skip = -1, ICollection<int> closed = null)
        {
            var set = new HashSet<int>();
            var steps = Steps(start, skip, closed);
            for (int i = 0; i < steps.Length; i++) if (steps[i] >= 0) set.Add(i);
            if (start < 0 || start >= steps.Length) set.Add(start); // (a start the plan does not know: itself only)
            return set;
        }

        /// <summary>The rooms reached from <paramref name="start"/> nearest first (with it), not into <paramref name="closed"/>.</summary>
        public List<int> ByDistance(int start, ICollection<int> closed = null)
        {
            var list = new List<int>();
            if (start < 0 || start >= m_Links.Length) return list;
            var seen = new HashSet<int> { start };
            list.Add(start);
            for (int k = 0; k < list.Count; k++) // the list is the queue: rooms in the order they are reached
            {
                var links = m_Links[list[k]];
                if (links == null) continue;
                foreach (var (n, _) in links)
                    if ((closed == null || !closed.Contains(n)) && seen.Add(n)) list.Add(n);
            }
            return list;
        }

        /// <summary>The rooms only the opening <paramref name="opening"/> leads to, from <paramref name="start"/>: reached with it, not without.</summary>
        public HashSet<int> Behind(int start, int opening)
        {
            var behind = Reach(start);
            behind.ExceptWith(Reach(start, opening));
            return behind;
        }
    }
}
