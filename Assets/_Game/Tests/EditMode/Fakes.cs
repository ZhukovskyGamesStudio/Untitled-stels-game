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
        public Stealth.Player.DownCause LastCause;
        /// <summary>A hit puts it down (one hit and you lie), as in the game.</summary>
        public bool FallsWhenHit;
        public bool CanBeCarried { get; set; }
        public void HitServer(Vector3 source, Stealth.Player.DownCause cause = Stealth.Player.DownCause.Servant)
        {
            Hits++;
            LastCause = cause;
            if (FallsWhenHit) { IsDown = true; CanBeCarried = true; }
        }
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
        public readonly List<Vector3> Faced = new();
        public void Face(Vector3 point, float dt) => Faced.Add(point);
        public void Turn(float degrees) => Turned += degrees;
        public void PatrolToNextPoint() => PatrolRequests++;
        public Vector3 Forward { get; set; } = Vector3.forward;
        public Vector3 NextPoint = new(5f, 0f, 0f);
        public int Plans;
        public Vector3 PlanNextPatrolPoint() { Plans++; return NextPoint; }
        public void Attack(IIntruder target) => Attacks.Add(target);
        public void RaiseAlarm(Vector3 at, float radius) => Alarms.Add((at, radius));
        public void CallForBackup(Vector3 at) => BackupCalls++;
    }

    /// <summary>Stands in for the player's network twin: records what would be sent.</summary>
    public class FakePlayerLink : IPlayerLink
    {
        public readonly List<float> Noises = new();
        public readonly List<Vector3> HitFeedback = new();
        public readonly List<Vector3> MarkedThings = new();
        public void SendMarkThing(Vector3 point) => MarkedThings.Add(point);
        public readonly List<PlayerController> Revives = new();
        public readonly List<Detector> Hacks = new();
        public int GasKnockouts, Teleports;

        public void Teleport(Vector3 position, Quaternion rotation) => Teleports++;
        public void PlaceOwner(Vector3 position, float yaw) => Teleports++;
        public void SendNoise(Vector3 position, float radius) => Noises.Add(radius);
        public void SendGasKnockout() => GasKnockouts++;
        public void SendRevive(PlayerController target) => Revives.Add(target);
        public int GetUps;
        public void SendGetUp() => GetUps++;
        public void SendStruggle() { }
        public void SendHack(Detector target) => Hacks.Add(target);
        public readonly List<(Detector target, int terminal)> RemoteHacks = new();
        public void SendRemoteHack(Detector target, int terminal) => RemoteHacks.Add((target, terminal));
        public void SendHitFeedback(Vector3 awayFromSource) => HitFeedback.Add(awayFromSource);
        public readonly List<Vector3> Pings = new();
        public readonly List<Detector> Marks = new();
        public void SendPing(Vector3 position) => Pings.Add(position);
        public void SendMark(Detector target) => Marks.Add(target);
        public readonly List<bool> Hatch = new();
        public void SendHatch(bool hide) => Hatch.Add(hide);
        public int HandUses;
        public void SendUseHand() => HandUses++;
        public int FallHurts;
        public void SendFallHurt() => FallHurts++;
        public readonly List<(GearAction action, Detector target, Vector3 point)> Gear = new();
        public void SendGear(GearAction action, Detector target, Vector3 point) => Gear.Add((action, target, point));
        public readonly List<(byte cls, byte item, bool ready)> Loadouts = new();
        public void SendLoadout(byte cls, byte item, bool ready) => Loadouts.Add((cls, item, ready));
        public readonly List<(Cheat cheat, int arg)> Cheats = new();
        public void SendCheat(Cheat cheat, int arg) => Cheats.Add((cheat, arg));
        public readonly List<(Stealth.Level.UseKind kind, int index)> Uses = new();
        public void SendUse(Stealth.Level.UseKind kind, int index) => Uses.Add((kind, index));
        public int Gadgets;
        public void SendGadget() => Gadgets++;
        public readonly System.Collections.Generic.List<byte> BotCommands = new();
        public void SendBot(byte command) => BotCommands.Add(command);
        public readonly List<int> Terminals = new();
        public void SendTerminal(int terminal, Detector steered, float yaw) => Terminals.Add(terminal);
    }

    /// <summary>Stands in for the inventory's network twin.</summary>
    public class FakeInventoryLink : IInventoryLink
    {
        public readonly List<string> Requests = new();

        public void RequestPickup(WorldItem item, byte x, byte y, byte rot) => Requests.Add($"pickup {x},{y},{rot}");
        public void RequestMove(int index, byte x, byte y, byte rot) => Requests.Add($"move {index} {x},{y},{rot}");
        public void RequestSwapHand(int index) => Requests.Add($"swap-hand {index}");
        public void RequestDrop(int index, Vector3 position, float yaw) => Requests.Add($"drop {index}");
        public void RequestDeposit(int index) => Requests.Add($"deposit {index}");
        public void RequestCassette(int index) => Requests.Add($"cassette {index}");
        public void RequestStash(int index, byte x, byte y, byte rot) => Requests.Add($"stash {index} {x},{y},{rot}");
        public void RequestUnstash(InventoryEntry from, byte x, byte y, byte rot) => Requests.Add($"unstash {from.X},{from.Y} {x},{y},{rot}");
        public void RequestStashMove(InventoryEntry from, byte x, byte y, byte rot) => Requests.Add($"stash-move {from.X},{from.Y} {x},{y},{rot}");
        public void RequestSellStashed(InventoryEntry from) => Requests.Add($"sell-stashed {from.X},{from.Y}");
        public void RequestStashWorld(WorldItem item, byte x, byte y, byte rot) => Requests.Add($"stash-world {x},{y},{rot}");
        public void RequestSellWorld(WorldItem item) => Requests.Add("sell-world");
        public void RequestStashAll() => Requests.Add("stash-all");
        public void RequestTakeCargo(ulong cargo, byte x, byte y, byte rot) => Requests.Add($"take-cargo {x},{y},{rot}");
        public void RequestRecharge(int battery, int flashlight) => Requests.Add($"recharge {battery} {flashlight}");
        public void RequestRechargeWorld(WorldItem battery, int flashlight) => Requests.Add($"recharge-world {flashlight}");
        public void RequestDropStashed(InventoryEntry from, Vector3 position, float yaw) => Requests.Add($"drop-stashed {from.X},{from.Y}");
        public void RequestBench(int index, byte x, byte y, byte rot) => Requests.Add($"bench {index} {x},{y},{rot}");
        public void RequestUnbench(InventoryEntry from, byte x, byte y, byte rot) => Requests.Add($"unbench {from.X},{from.Y} {x},{y},{rot}");
        public void RequestBenchMove(InventoryEntry from, byte x, byte y, byte rot) => Requests.Add($"bench-move {from.X},{from.Y} {x},{y},{rot}");
        public void RequestCombine() => Requests.Add("combine");
        public void RequestShelf(byte shelf, int index, byte x, byte y, byte rot) => Requests.Add($"shelf {shelf} {index} {x},{y},{rot}");
        public void RequestUnshelf(byte shelf, InventoryEntry from, byte x, byte y, byte rot) => Requests.Add($"unshelf {shelf} {from.X},{from.Y} {x},{y},{rot}");
        public void RequestShelfMove(byte shelf, InventoryEntry from, byte x, byte y, byte rot) => Requests.Add($"shelf-move {shelf} {from.X},{from.Y} {x},{y},{rot}");
        public void RequestCarUpgrade(byte upgrade) => Requests.Add($"upgrade {upgrade}");
    }

    /// <summary>Stands in for a detector's network twin.</summary>
    public class FakeDetectorLink : IDetectorLink
    {
        public readonly List<byte> Pulses = new();
        public void BroadcastPulse(byte effect) => Pulses.Add(effect);
    }
}
