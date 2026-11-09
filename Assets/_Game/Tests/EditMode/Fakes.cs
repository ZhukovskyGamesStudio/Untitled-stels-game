using System.Collections.Generic;
using Stealth.Enemies;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>A player as robots see it, fully controlled by the test.</summary>
    public class FakeIntruder : IIntruder
    {
        public ulong ClientId { get; set; }
        public Vector3 Position { get; set; }
        public float Height { get; set; } = 1.8f;
        public bool IsCrouching { get; set; }
        public bool IsDown { get; set; }
        public float GroundSpeed { get; set; }
        public int CarriedColor = -1;
        public int Hits;

        public bool Carries(int color) => color == CarriedColor;
        public void HitServer(Vector3 source) => Hits++;
    }

    /// <summary>Records what a <see cref="RobotBrain"/> asks its body to do.</summary>
    public class FakeBody : IRobotBody
    {
        public Vector3 Position { get; set; }
        public bool Arrived { get; set; }
        public Vector3? Destination;
        public float Speed;
        public bool Stopped;
        public float Turned;
        public int PatrolRequests;
        public int BackupCalls;
        public readonly List<(Vector3 at, float radius)> Alarms = new();
        public readonly List<IIntruder> Attacks = new();

        public void MoveTo(Vector3 destination, float speed)
        {
            Destination = destination;
            Speed = speed;
            Stopped = false;
        }

        public void Move(float speed)
        {
            Speed = speed;
            Stopped = false;
        }

        public void Stop() => Stopped = true;
        public void Face(Vector3 point, float dt) { }
        public void Turn(float degrees) => Turned += degrees;
        public void PatrolToNextPoint() => PatrolRequests++;
        public void Attack(IIntruder target) => Attacks.Add(target);
        public void RaiseAlarm(Vector3 at, float radius) => Alarms.Add((at, radius));
        public void CallForBackup(Vector3 at) => BackupCalls++;
    }
}
