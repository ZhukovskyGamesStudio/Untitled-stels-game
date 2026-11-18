using System.Collections;
using System.Linq;
using Stealth.Enemies;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Stealth.Game
{
    public partial class DevAutomation
    {
        /// <summary>
        /// The house's main job, done with real keys: the vault — E at each lock panel, the switches and the sequence
        /// solved with the number keys, the dial tried digit by digit, its door open and the treasure inside; or the
        /// hostage — the guard with the key on his back, hacked (he drops it), the key picked up, E at the cage, the reward.
        /// </summary>
        IEnumerator ObjectiveScenario()
        {
            DevFlags.FreezeRobots = true;
            DevFlags.Peaceful = true;
            Me.CheatServer(Cheat.Invulnerable, true);
            var gm = GameManager.Instance;
            var l = ActiveLevel.Layout;
            Log($"objective: {l.Objective} (house seed {l.Seed}, house {l.LevelIndex})");
            if (l.Objective == ObjectiveKind.Vault) yield return VaultRun(gm, l);
            else if (l.Objective == ObjectiveKind.Hostage) yield return HostageRun(gm, l);
            else Log("FAIL: no main job in this house");
        }

        IEnumerator VaultRun(GameManager gm, LevelLayout l)
        {
            var door = AutoDoor.All.FirstOrDefault(d => d.Index == l.VaultDoor);
            if (door == null) { Log("FAIL: no vault door"); yield break; }
            var across = door.transform.forward;
            var outside = l.Plan.RoomAt(door.transform.position + across * 1.2f + Vector3.up * 0.3f)?.Id == l.VaultRoom ? -across : across;
            LookFrom(door.transform.position + outside * 3f + Vector3.up * 0.05f, door.transform.position + Vector3.up * 2f);
            yield return new WaitForSeconds(0.6f);
            Log($"vault door: locked {door.Locked}, vault {door.Vault}, solved {gm.VaultSolved}");
            yield return Shot("vault_door_shut");

            var consoles = Object.FindObjectsByType<VaultConsole>(FindObjectsSortMode.None).OrderBy(c => c.Index).ToList();
            foreach (var c in consoles)
            {
                var stand = c.transform.position + c.transform.forward * 1.1f;
                stand.y = LevelLayout.FloorY(LevelLayout.FloorOf(c.transform.position.y)) + 0.05f;
                LookFrom(stand, c.Point);
                yield return new WaitForSeconds(0.5f);
                Log($"at panel {c.Index} ({c.Game}): target {(Me.UseTarget != null ? Me.UseTarget.name : "none")}, prompt '{Me.UsePrompt}'");
                yield return Shot($"vault_panel_{c.Index}");
                yield return PressKey(Key.E);
                yield return new WaitForSeconds(0.3f);
                switch (c.Game)
                {
                    case VaultGame.Switches:
                        if (Me.Switches == null) { Log("FAIL: E at the switch panel opened nothing"); break; }
                        yield return Shot("vault_switches");
                        // the same lock from the same seed: find the presses that light every lamp, then press them for real
                        int seed = LevelRandom.Mix(l.Seed, 0x0C05, c.Index);
                        int count = Me.Switches.Count, solution = -1;
                        for (int mask = 0; mask < 1 << count && solution < 0; mask++)
                        {
                            var trial = new SwitchPuzzle(seed, Configs.Tuning.Items.Objectives);
                            for (int i = 0; i < count; i++) if ((mask & (1 << i)) != 0) trial.Press(i);
                            if (trial.Solved) solution = mask;
                        }
                        for (int i = 0; i < count && solution >= 0; i++)
                            if ((solution & (1 << i)) != 0) yield return PressKey(Key.Digit1 + i);
                        break;
                    case VaultGame.Sequence:
                        if (Me.Sequence == null) { Log("FAIL: E at the sequence panel opened nothing"); break; }
                        var sequence = Me.Sequence;
                        yield return new WaitForSeconds(1.0f);
                        yield return Shot("vault_sequence_playing");
                        while (sequence.Playing(Time.time)) yield return null;
                        for (int i = 0; i < sequence.Length; i++) yield return PressKey(Key.Digit1 + sequence.At(i));
                        break;
                    default:
                        if (Me.Cracking == null || Me.CrackingConsole != c) { Log("FAIL: E at the dial panel opened nothing"); break; }
                        yield return Shot("vault_dial");
                        while (Me.Cracking != null)
                        {
                            Me.Cracking.SetDial(Me.Cracking.CodeAt(Me.Cracking.Found));
                            Me.TryDial();
                            yield return new WaitForSeconds(0.2f);
                        }
                        break;
                }
                yield return new WaitForSeconds(0.6f);
                Log($"panel {c.Index}: solved {c.Solved}, vault {gm.VaultSolved}/{l.VaultConsoles.Count}");
            }
            // unlocked, still shut: E at the wheel spins it, then the door swings in
            LookFrom(door.transform.position + outside * 1.6f + Vector3.up * 0.05f, door.transform.position + Vector3.up * 1.15f);
            yield return new WaitForSeconds(0.6f);
            Log($"vault: unlocked {!door.Locked}, still shut {door.OpenAmount < 0.05f}, prompt '{Me.UsePrompt}'");
            yield return PressKey(Key.E);
            yield return new WaitForSeconds(0.9f);
            yield return Shot("vault_wheel_turning");
            yield return new WaitForSeconds(AutoDoor.VaultSpinTime + AutoDoor.VaultSwingTime);
            LookFrom(door.transform.position + outside * 3f + Vector3.up * 0.05f, door.transform.position + Vector3.up * 2f);
            yield return new WaitForSeconds(0.8f);
            Log($"vault: door locked {door.Locked}, open {door.OpenAmount:0.00} -> {(!door.Locked && door.OpenAmount > 0.95f ? "OPENED" : "FAIL")}");
            yield return Shot("vault_door_open");
            var room = l.Plan.Rooms[l.VaultRoom];
            LookFrom(door.transform.position - outside * 0.9f + Vector3.up * 0.05f, room.Center + Vector3.up * 0.2f);
            yield return new WaitForSeconds(0.6f);
            int treasure = WorldItem.All.Count(i => l.Plan.RoomAt(i.transform.position + Vector3.up * 0.3f)?.Id == l.VaultRoom);
            Log($"vault: {treasure} items inside");
            yield return Shot("vault_inside");
        }

        IEnumerator HostageRun(GameManager gm, LevelLayout l)
        {
            var cage = Object.FindObjectsByType<Cage>(FindObjectsSortMode.None).FirstOrDefault();
            if (cage == null) { Log("FAIL: no cage"); yield break; }
            var front = cage.transform.position + cage.transform.forward * 2.6f + Vector3.up * 0.05f;
            LookFrom(front, cage.transform.position + Vector3.up * 1.1f);
            yield return new WaitForSeconds(0.6f);
            Log($"at the cage: target {(Me.UseTarget != null ? Me.UseTarget.name : "none")}, prompt '{Me.UsePrompt}', freed {cage.Freed}");
            yield return Shot("cage");

            var carrier = RobotController.Robots.FirstOrDefault(r => r.Cargo >= 0 && Detector.DecodeCargo(r.Cargo).shape == ItemCatalog.CageKey);
            WorldItem key = null;
            if (carrier != null)
            {
                var back = carrier.transform.position - carrier.transform.forward * 2.2f + Vector3.up * 0.05f;
                LookFrom(back, carrier.transform.position + Vector3.up * 1.2f);
                yield return new WaitForSeconds(0.6f);
                Log($"key carrier: {carrier.name} at {carrier.transform.position}");
                yield return Shot("key_on_guard");
                carrier.HackServer(30f); // hacked: it drops what it carries
                yield return new WaitForSeconds(0.5f);
            }
            else Log("no robot carries the key: it lies at " + l.CageKeySpot);
            key = WorldItem.All.FirstOrDefault(i => i.Shape == ItemCatalog.CageKey);
            if (key == null) { Log("FAIL: the key is nowhere"); yield break; }
            Me.TeleportOwner(key.transform.position + Vector3.right * 0.8f, Quaternion.identity);
            yield return new WaitForSeconds(0.3f);
            var inv = Me.GetComponent<PlayerInventory>();
            bool placed = false;
            for (int y = 0; y < 8 && !placed; y++)
                for (int x = 0; x < 8 && !placed; x++)
                    if (inv.CanPlace(key.Shape, 0, x, y)) { inv.PickupServer(key, (byte)x, (byte)y, 0); placed = true; }
            yield return new WaitForSeconds(0.3f);
            Log($"key picked up: {Cage.HasKey(Me)}");
            LookFrom(front, cage.transform.position + Vector3.up * 1.1f);
            yield return new WaitForSeconds(0.5f);
            Log($"at the cage with the key: target {(Me.UseTarget != null ? Me.UseTarget.name : "none")}, prompt '{Me.UsePrompt}'");
            yield return PressKey(Key.E);
            yield return new WaitForSeconds(0.8f);
            bool reward = WorldItem.All.Any(i => i.Shape == ItemCatalog.Diamond && (i.transform.position - cage.transform.position).magnitude < 3f);
            Log($"hostage: freed {cage.Freed}, job done {gm.ObjectiveDone}, reward {reward} -> {(cage.Freed && reward ? "FREED" : "FAIL")}");
            yield return Shot("cage_open");
        }
    }
}
