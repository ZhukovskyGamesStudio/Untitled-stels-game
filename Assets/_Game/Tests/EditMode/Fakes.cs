using System.Collections.Generic;
using Stealth.Enemies;
using Stealth.Game;
using Stealth.Items;
using Stealth.Player;
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

    /// <summary>Stands in for the player's network twin: records what would be sent.</summary>
    public class FakePlayerLink : IPlayerLink
    {
        public readonly List<float> Noises = new();
        public readonly List<Vector3> HitFeedback = new();
        public readonly List<PlayerController> Revives = new();
        public readonly List<Detector> Hacks = new();
        public int GasKnockouts, Teleports;

        public void Teleport(Vector3 position, Quaternion rotation) => Teleports++;
        public void SendNoise(Vector3 position, float radius) => Noises.Add(radius);
        public void SendGasKnockout() => GasKnockouts++;
        public void SendRevive(PlayerController target) => Revives.Add(target);
        public int GetUps;
        public void SendGetUp() => GetUps++;
        public void SendHack(Detector target) => Hacks.Add(target);
        public readonly List<(Detector target, int terminal)> RemoteHacks = new();
        public void SendRemoteHack(Detector target, int terminal) => RemoteHacks.Add((target, terminal));
        public void SendHitFeedback(Vector3 awayFromSource) => HitFeedback.Add(awayFromSource);
        public readonly List<Vector3> Pings = new();
        public readonly List<Detector> Marks = new();
        public void SendPing(Vector3 position) => Pings.Add(position);
        public void SendMark(Detector target) => Marks.Add(target);
        public readonly List<bool> Hatch = new();
        public readonly List<int> Doors = new(), Safes = new();
        public void SendHatch(bool hide) => Hatch.Add(hide);
        public void SendUnlockDoor(int door) => Doors.Add(door);
        public void SendOpenSafe(int safe) => Safes.Add(safe);
    }

    /// <summary>Stands in for the inventory's network twin.</summary>
    public class FakeInventoryLink : IInventoryLink
    {
        public readonly List<string> Requests = new();

        public void RequestPickup(WorldItem item, byte x, byte y, byte rot) => Requests.Add($"pickup {x},{y},{rot}");
        public void RequestMove(int index, byte x, byte y, byte rot) => Requests.Add($"move {index} {x},{y},{rot}");
        public void RequestDrop(int index, Vector3 position, float yaw) => Requests.Add($"drop {index}");
        public void RequestDeposit(int index) => Requests.Add($"deposit {index}");
        public void RequestStash(int index, byte x, byte y, byte rot) => Requests.Add($"stash {index} {x},{y},{rot}");
        public void RequestUnstash(InventoryEntry from, byte x, byte y, byte rot) => Requests.Add($"unstash {from.X},{from.Y} {x},{y},{rot}");
        public void RequestStashMove(InventoryEntry from, byte x, byte y, byte rot) => Requests.Add($"stash-move {from.X},{from.Y} {x},{y},{rot}");
        public void RequestSellStashed(InventoryEntry from) => Requests.Add($"sell-stashed {from.X},{from.Y}");
        public void RequestStashWorld(WorldItem item, byte x, byte y, byte rot) => Requests.Add($"stash-world {x},{y},{rot}");
        public void RequestSellWorld(WorldItem item) => Requests.Add("sell-world");
    }

    /// <summary>Stands in for a detector's network twin.</summary>
    public class FakeDetectorLink : IDetectorLink
    {
        public readonly List<byte> Pulses = new();
        public void BroadcastPulse(byte effect) => Pulses.Add(effect);
    }
}
