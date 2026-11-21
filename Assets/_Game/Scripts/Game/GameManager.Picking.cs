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
            int ready = 0, count = 0;
            foreach (var p in PlayerController.All)
            {
                if (!p.IsSpawned) continue;
                count++;
                if (p.LoadoutReady) ready++;
                if (car == null || m_PlacedInRow.Contains(p)) continue;
                m_PlacedInRow.Add(p);
                var (at, yaw) = PickingRules.Spot(car.transform.position, car.transform.rotation, PlayerLooks.SlotOf(p.ClientId));
                p.PlaceServer(at, yaw);
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
        /// <summary>Metres behind the car the row stands, between two figures, and from the row to the camera.</summary>
        public const float Behind = 4.2f, Gap = 1.25f, CameraBack = 4.6f, CameraHeight = 1.45f;

        /// <summary>The spot of a slot (0..3, left to right as the camera sees it) and the yaw it faces: the camera.</summary>
        public static (Vector3 at, float yaw) Spot(Vector3 car, Quaternion carRotation, int slot)
        {
            var back = carRotation * Vector3.back;
            var right = carRotation * Vector3.right; // the camera looks forward: the car's right is its right
            float x = (Mathf.Clamp(slot, 0, 3) - 1.5f) * Gap;
            var at = car + back * Behind + right * x + Vector3.up * 0.05f;
            float yaw = Quaternion.LookRotation(back).eulerAngles.y;
            return (at, yaw);
        }

        /// <summary>The camera: behind the row, looking at it (the car behind the team).</summary>
        public static (Vector3 at, Quaternion rotation) Camera(Vector3 car, Quaternion carRotation)
        {
            var back = carRotation * Vector3.back;
            var at = car + back * (Behind + CameraBack) + Vector3.up * CameraHeight;
            var target = car + back * Behind + Vector3.up * 1.05f;
            return (at, Quaternion.LookRotation(target - at));
        }
    }
}
