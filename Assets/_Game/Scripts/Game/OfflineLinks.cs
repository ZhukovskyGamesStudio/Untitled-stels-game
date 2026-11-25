using Stealth.Enemies;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;

namespace Stealth.Game
{
    /// <summary>
    /// The links of a peer that is its own server (no network twin: tests, a game with no session): every request is
    /// handled right away by the server side of the same component — the code that sends it does not tell the two apart.
    /// </summary>
    public sealed class OfflinePlayerLink : IPlayerLink
    {
        readonly PlayerController m_P;
        public OfflinePlayerLink(PlayerController player) => m_P = player;

        static GameManager Game => GameManager.Instance;

        public void SendTerminal(int terminal, Detector steered, float yaw) => m_P.TerminalServer(terminal, steered, yaw);
        public void SendUse(UseKind kind, int index) => Game?.UseServer(m_P, kind, index);
        public void SendGadget() => Game?.GadgetServer(m_P);
        public void SendBot(byte command) => Game?.BotCommandServer(m_P, command);
        public void SendCheat(Cheat cheat, int arg) => m_P.CheatServer(cheat, arg);
        public void Teleport(Vector3 position, Quaternion rotation) { } // nothing to smooth: the transform is already there
        public void PlaceOwner(Vector3 position, float yaw) => m_P.PlaceFromServer(position, yaw);
        public void SendNoise(Vector3 position, float radius) => m_P.OnNoiseRequest(position, radius);
        public void SendGasKnockout() => m_P.OnGasKnockoutRequest();
        public void SendRevive(PlayerController target) => m_P.OnReviveRequest(target);
        public void SendGetUp() => m_P.OnGetUpRequest();
        public void SendHack(Detector target) => m_P.OnHackRequest(target);
        public void SendRemoteHack(Detector target, int terminal) => Game?.RemoteHackServer(m_P, terminal, target);
        public void SendPing(Vector3 position) => m_P.OnPingRequest(position);
        public void SendMark(Detector target) => m_P.OnMarkRequest(target);
        public void SendMarkThing(Vector3 point) => m_P.OnMarkThingRequest(point);
        public void SendHatch(bool hide) => m_P.OnHatchRequest(hide);
        public void SendUseHand() => m_P.UseHandServer();
        public void SendHitFeedback(Vector3 awayFromSource) => m_P.OnHitFeedback(awayFromSource);
        public void SendFallHurt() => m_P.OnFallHurtRequest();
        public void SendGear(GearAction action, Detector target, Vector3 point) => m_P.OnGearRequest(action, target, point);
        public void SendLoadout(byte cls, byte item, bool ready) => m_P.OnLoadoutRequest(cls, item, ready);
    }

    /// <summary>The backpack's requests on a peer that is its own server (<see cref="OfflinePlayerLink"/>).</summary>
    public sealed class OfflineInventoryLink : IInventoryLink
    {
        readonly PlayerInventory m_I;
        public OfflineInventoryLink(PlayerInventory inventory) => m_I = inventory;

        public void RequestPickup(WorldItem item, byte x, byte y, byte rot) => m_I.PickupServer(item, x, y, rot);
        public void RequestMove(int index, byte x, byte y, byte rot) => m_I.MoveServer(index, x, y, rot);
        public void RequestSwapHand(int index) => m_I.SwapHandServer(index);
        public void RequestDrop(int index, Vector3 position, float yaw) => m_I.DropServer(index, position, yaw);
        public void RequestDeposit(int index) => m_I.DepositServer(index);
        public void RequestStash(int index, byte x, byte y, byte rot) => m_I.StashServer(index, x, y, rot);
        public void RequestUnstash(InventoryEntry from, byte x, byte y, byte rot) => m_I.UnstashServer(from, x, y, rot);
        public void RequestStashMove(InventoryEntry from, byte x, byte y, byte rot) => m_I.StashMoveServer(from, x, y, rot);
        public void RequestSellStashed(InventoryEntry from) => m_I.SellStashedServer(from);
        public void RequestStashWorld(WorldItem item, byte x, byte y, byte rot) => m_I.StashWorldServer(item, x, y, rot);
        public void RequestSellWorld(WorldItem item) => m_I.SellWorldServer(item);
        public void RequestStashAll() => m_I.StashAllServer();
        public void RequestTakeCargo(ulong cargo, byte x, byte y, byte rot) => m_I.TakeCargoServer(Vehicles.CargoItem.Unpack(cargo), x, y, rot);
        public void RequestRecharge(int battery, int flashlight) => m_I.RechargeServer(battery, flashlight);
        public void RequestRechargeWorld(WorldItem battery, int flashlight) => m_I.RechargeWorldServer(battery, flashlight);
        public void RequestDropStashed(InventoryEntry from, Vector3 position, float yaw) => m_I.DropStashedServer(from, position, yaw);
        public void RequestBench(int index, byte x, byte y, byte rot) => m_I.BenchServer(index, x, y, rot);
        public void RequestUnbench(InventoryEntry from, byte x, byte y, byte rot) => m_I.UnbenchServer(from, x, y, rot);
        public void RequestBenchMove(InventoryEntry from, byte x, byte y, byte rot) => m_I.BenchMoveServer(from, x, y, rot);
        public void RequestCombine() => m_I.CombineServer();
        public void RequestCarUpgrade(byte upgrade) => m_I.CarUpgradeServer(upgrade);
    }

    /// <summary>The run's requests and broadcasts on a peer that is its own server (<see cref="OfflinePlayerLink"/>).</summary>
    public sealed class OfflineGameLink : IGameLink
    {
        readonly GameManager m_G;
        public OfflineGameLink(GameManager game) => m_G = game;

        public void RequestRestart() => m_G.RestartRunServer();
        public void RequestStyle(int index) => m_G.SetStyleServer(index);
        public void BroadcastBanner(string text) => m_G.ShowBanner(text);
        public void BroadcastPing(ulong owner, PingKind kind, Vector3 position, ulong target) => m_G.ReceivePing(owner, kind, position, target);
        public void BroadcastHouseStats(string packed) => m_G.ReceiveHouseStats(packed);
        public void BroadcastSound(Stealth.Audio.GameSound sound, Vector3 at) => m_G.ReceiveSound(sound, at);
    }
}
