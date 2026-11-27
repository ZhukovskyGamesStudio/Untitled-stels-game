using System.Collections;
using System.Collections.Generic;
using Stealth.Level;
using Unity.AI.Navigation;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// The breaches a driller knocks in walls (<see cref="WallBreaks"/>): the server decides and keeps a key per breach
    /// with the opened things; every peer cuts the wall when the key arrives and puts the style on again; the server
    /// bakes the navmesh anew so that robots walk through.
    /// </summary>
    public partial class GameManager
    {
        readonly HashSet<int> m_WallsCut = new();
        GameObject m_WallsRoot;
        int m_WallsSeen = -1;
        IEnumerator<bool> m_WallRestyle;
        IEnumerator<bool> m_NavRebake;

        /// <summary>The breaches made in this house: the wall and where on it.</summary>
        IEnumerable<(int wall, float at)> WallBreaksMade()
        {
            foreach (int key in m_Opened)
                if (OpenKeys.KindOf(key) == OpenKind.Wall) yield return OpenKeys.WallBreakOf(key);
        }

        /// <summary>Server: a driller breaks through a wall here, if the rule lets it (<see cref="WallBreaks.CanBreak"/>). True if it did.</summary>
        public bool BreakWallServer(int wall, float at)
        {
            var layout = ActiveLevel.Layout;
            if (!m_IsServer || Phase != RunPhase.Playing || layout == null || !ActiveLevel.Ready) return false;
            at = WallBreaks.Snap(at);
            if (!WallBreaks.CanBreak(layout, wall, at, WallBreaksMade())) return false;
            if (!m_Opened.Add(OpenKeys.WallBreak(wall, at))) return false;
            OpenedVersion++;
            var w = layout.Plan.Walls[wall];
            var p = w.At(at);
            NoiseSystem.EmitServer(new Vector3(p.x, LevelLayout.FloorY(w.Floor), p.y), 16f);
            return true;
        }

        /// <summary>Every peer, every frame: the breaches whose keys have arrived are cut into the house (in the same order everywhere).</summary>
        void UpdateWallBreaks()
        {
            if (m_WallRestyle != null && !m_WallRestyle.MoveNext()) m_WallRestyle = null;
            if (m_NavRebake != null && !m_NavRebake.MoveNext()) m_NavRebake = null;
            if (ActiveLevel.Root != m_WallsRoot) { m_WallsRoot = ActiveLevel.Root; m_WallsCut.Clear(); m_WallsSeen = -1; m_NavRebake = null; }
            var layout = ActiveLevel.Layout;
            if (!ActiveLevel.Ready || m_WallsRoot == null || layout == null || layout.Seed != Level.Seed || OpenedVersion == m_WallsSeen) return;
            m_WallsSeen = OpenedVersion;
            var fresh = new List<int>();
            foreach (int key in m_Opened)
                if (OpenKeys.KindOf(key) == OpenKind.Wall && !m_WallsCut.Contains(key)) fresh.Add(key);
            if (fresh.Count == 0) return;
            fresh.Sort();
            foreach (int key in fresh)
            {
                m_WallsCut.Add(key);
                var (wall, at) = OpenKeys.WallBreakOf(key);
                if (wall < 0 || wall >= layout.Plan.Walls.Count || WallBreaks.OpenAt(layout.Plan, wall, at)) continue;
                WallBreaks.Cut(layout, wall, at, LevelRandom.Mix(layout.Seed, Salt.WallBreaks, key));
            }
            m_WallRestyle = VisualStyles.ReapplySteps().GetEnumerator();
            if (m_IsServer && m_WallsRoot.TryGetComponent<NavMeshSurface>(out var surface)) m_NavRebake = Rebake(surface);
        }

        /// <summary>Server: the navmesh baked again after a wall changed (a frame for the destroyed blocks to go, then on worker threads).</summary>
        static IEnumerator<bool> Rebake(NavMeshSurface surface)
        {
            yield return true;
            AsyncOperation bake = null;
            foreach (var _ in NavBake.Steps(surface, op => bake = op)) yield return true;
            while (bake != null && !bake.isDone) yield return true;
        }
    }
}
