using System.Collections.Generic;
using Stealth.Level;
using Stealth.Player;
using Stealth.Vehicles;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// The run's start screen: the team (up to four) stands in a row behind its car in the first sewer, each picks a class
    /// (the tool they start with) and an extra item under their figure; when everyone is ready the run begins — the picks
    /// go into the backpacks (<see cref="PlayerController.GiveLoadoutServer"/>). Rules of the row: <see cref="PickingRules"/>.
    /// </summary>
    public partial class GameManager
    {
        /// <summary>The start screen is up (server-decided, replicated): nobody walks, the camera looks at the row.</summary>
        public bool Picking { get; private set; }

        readonly HashSet<PlayerController> m_PlacedInRow = new();
        readonly List<PlayerController> m_Row = new();

        /// <summary>Client: the server's word on the start screen.</summary>
        public void ApplyPicking(bool picking) => Picking = picking;

        /// <summary>Server: a new run shows the start screen (not for the autopilot's runs).</summary>
        void BeginPickingServer()
        {
            m_PlacedInRow.Clear();
            Picking = !DevFlags.SkipPicking;
            foreach (var p in PlayerController.All) p.ClearReadyServer();
        }

        /// <summary>Server, every frame while the start screen is up: everyone in the row; all ready — the run begins.</summary>
        void UpdatePickingServer()
        {
            if (!Picking) return;
            var car = Car.Active;
            int ready = 0;
            m_Row.Clear();
            foreach (var p in PlayerController.All)
            {
                if (!p.IsSpawned) continue;
                m_Row.Add(p);
                if (p.LoadoutReady) ready++;
            }
            int count = m_Row.Count;
            // the row stands centred before the camera, in the slots' order; someone joined or left — everyone moves up
            if (count != m_PlacedInRow.Count || m_Row.Exists(p => !m_PlacedInRow.Contains(p))) m_PlacedInRow.Clear();
            if (car != null && m_PlacedInRow.Count == 0)
            {
                m_Row.Sort((a, b) => PlayerLooks.SlotOf(a.ClientId).CompareTo(PlayerLooks.SlotOf(b.ClientId)));
                for (int i = 0; i < count; i++)
                {
                    var (at, yaw) = PickingRules.Spot(car.transform.position, car.transform.rotation, i, count);
                    m_Row[i].PlaceServer(at, yaw);
                    m_PlacedInRow.Add(m_Row[i]);
                }
            }
            if (count == 0 || ready < count) return;
            Picking = false;
            foreach (var p in PlayerController.All)
            {
                if (!p.IsSpawned) continue;
                p.GiveLoadoutServer();
                p.ClearReadyServer();
            }
            Debug.Log($"[Game] The start screen is over: {count} players go");
        }

        /// <summary>Every peer: where the start screen's camera stands, before the row (false: no car yet).</summary>
        public bool PickingCamera(out Vector3 position, out Quaternion rotation)
        {
            var car = Car.Active;
            position = default;
            rotation = Quaternion.identity;
            if (car == null) return false;
            (position, rotation) = PickingRules.Camera(car.transform.position, car.transform.rotation);
            return true;
        }
    }

    /// <summary>Where the team stands on the run's start screen and where it is seen from (pure, unit tested).</summary>
    public static class PickingRules
    {
        /// <summary>
        /// Metres behind the car the row stands, between two figures, from the row to the camera, the camera's height and
        /// the height on the row it looks at (low: the figures stand in the upper half, their columns under their feet).
        /// </summary>
        public const float Behind = 4.2f, Gap = 1.15f, CameraBack = 3.6f, CameraHeight = 1.35f, LookHeight = 0.5f;
        /// <summary>The camera's vertical field of view on the start screen (fixed: the player's own setting would reframe it).</summary>
        public const float Fov = 50f;

        /// <summary>
        /// The spot of the i-th of <paramref name="count"/> in the row (left to right as the camera sees it, centred before
        /// it) and the yaw it faces: the camera.
        /// </summary>
        public static (Vector3 at, float yaw) Spot(Vector3 car, Quaternion carRotation, int index, int count)
        {
            var back = carRotation * Vector3.back;
            var right = carRotation * Vector3.right; // the camera looks forward: the car's right is its right
            count = Mathf.Clamp(count, 1, 4);
            float x = (Mathf.Clamp(index, 0, count - 1) - (count - 1) / 2f) * Gap;
            var at = car + back * Behind + right * x + Vector3.up * 0.05f;
            float yaw = Quaternion.LookRotation(back).eulerAngles.y;
            return (at, yaw);
        }

        /// <summary>The camera: behind the row, looking at it (the car behind the team).</summary>
        public static (Vector3 at, Quaternion rotation) Camera(Vector3 car, Quaternion carRotation)
        {
            var back = carRotation * Vector3.back;
            var at = car + back * (Behind + CameraBack) + Vector3.up * CameraHeight;
            var target = car + back * Behind + Vector3.up * LookHeight;
            return (at, Quaternion.LookRotation(target - at));
        }
    }
}
