using UnityEngine;

namespace Stealth.Enemies
{
    /// <summary>
    /// A player as security sees it. Implemented by PlayerController; tests use fakes.
    /// Keeps robot logic free of networking and MonoBehaviour details.
    /// </summary>
    public interface IIntruder
    {
        ulong ClientId { get; }
        Vector3 Position { get; }
        /// <summary>Current capsule height (crouching players are lower).</summary>
        float Height { get; }
        bool IsCrouching { get; }
        /// <summary>Downed, disconnected or otherwise not a valid target.</summary>
        bool IsDown { get; }
        /// <summary>Horizontal speed measured on the server (vibration / motion sensors).</summary>
        float GroundSpeed { get; }
        /// <summary>Carries loot of this ItemCatalog colour (colour sorters).</summary>
        bool Carries(int color);
        /// <summary>Server: deal one hit coming from <paramref name="source"/>.</summary>
        void HitServer(Vector3 source);
    }

    /// <summary>Something a player can switch off for a while (hold E).</summary>
    public interface IHackable
    {
        bool IsHacked { get; }
        /// <summary>Can a player standing at <paramref name="player"/> hack it right now?</summary>
        bool CanBeHackedFrom(Vector3 player);
        /// <summary>Server: disable for <paramref name="seconds"/>.</summary>
        void HackServer(float seconds);
    }

    /// <summary>
    /// What a <see cref="RobotBrain"/> can do with its body. RobotController implements it with a NavMeshAgent;
    /// tests use a fake that records calls.
    /// </summary>
    public interface IRobotBody
    {
        Vector3 Position { get; }
        /// <summary>Reached the current destination (or has none).</summary>
        bool Arrived { get; }
        /// <summary>Walk to a point at the given speed.</summary>
        void MoveTo(Vector3 destination, float speed);
        /// <summary>Keep walking to the current destination at the given speed.</summary>
        void Move(float speed);
        void Stop();
        /// <summary>Turn towards a point (limited turn rate).</summary>
        void Face(Vector3 point, float dt);
        /// <summary>Turn on the spot by <paramref name="degrees"/> (look around).</summary>
        void Turn(float degrees);
        /// <summary>Choose the next patrol point and walk there.</summary>
        void PatrolToNextPoint();
        void Attack(IIntruder target);
        void RaiseAlarm(Vector3 at, float radius);
        /// <summary>Radio nearby robots to come and check <paramref name="at"/>.</summary>
        void CallForBackup(Vector3 at);
    }
}
