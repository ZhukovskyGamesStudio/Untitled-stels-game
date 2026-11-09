using System.Linq;
using NUnit.Framework;
using Stealth.Enemies;
using Stealth.Game;
using Stealth.Items;
using Stealth.Level;
using Stealth.Net;
using Stealth.Player;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>
    /// The game assembly knows nothing about Netcode or Steam; every networked game component has a network twin
    /// in Stealth.Net; and the game components work through their link interfaces without any network at all.
    /// </summary>
    public class NetworkBoundaryTests
    {
        const string Prefabs = "Assets/_Game/Prefabs";

        GameObject m_Player, m_Other, m_Plate, m_Robot;

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in new[] { m_Player, m_Other, m_Plate, m_Robot })
            {
                if (go == null) continue;
                foreach (var p in go.GetComponents<PlayerController>()) p.Unbind();
                foreach (var d in go.GetComponents<Detector>()) d.Despawned();
                Object.DestroyImmediate(go);
            }
        }

        static GameObject Spawn(string path, Vector3 position)
        {
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/{path}"));
            go.transform.position = position;
            return go;
        }

        // ------------------------------------------------------------------ assemblies and prefabs

        [Test]
        public void GameAssemblyDoesNotReferenceNetworking()
        {
            var refs = typeof(GameManager).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            foreach (var forbidden in new[] { "Unity.Netcode.Runtime", "Unity.Networking.Transport", "com.rlabrecque.steamworks.net" })
                CollectionAssert.DoesNotContain(refs, forbidden, $"Stealth.Game must not use {forbidden}: put that code into Stealth.Net");
            Assert.IsFalse(typeof(GameManager).Assembly.GetTypes().Any(t => typeof(NetworkBehaviour).IsAssignableFrom(t)));
        }

        [Test]
        public void EveryNetworkedGameComponentHasItsTwin()
        {
            int checkedPrefabs = 0;
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { Prefabs }).Select(AssetDatabase.GUIDToAssetPath))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                void Requires<TGame, TNet>() where TGame : Component where TNet : NetworkBehaviour
                {
                    if (go.GetComponent<TGame>() == null) return;
                    checkedPrefabs++;
                    Assert.IsNotNull(go.GetComponent<TNet>(), $"{path}: {typeof(TGame).Name} needs {typeof(TNet).Name}");
                    Assert.IsNotNull(go.GetComponent<NetworkObject>(), $"{path}: a NetworkObject on the root");
                }
                Requires<PlayerController, PlayerNet>();
                Requires<PlayerInventory, InventoryNet>();
                Requires<Detector, DetectorNet>();
                Requires<WorldItem, WorldItemNet>();
                Requires<LaserGate, LaserGateNet>();
                Requires<GameManager, GameManagerNet>();
            }
            Assert.Greater(checkedPrefabs, 15);
        }

        // ------------------------------------------------------------------ game components offline

        [Test]
        public void WithoutASessionThisPeerIsTheAuthority()
        {
            Assert.IsFalse(Session.IsRunning);
            Assert.IsTrue(Session.IsServer);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/Items/WorldItem.prefab");
            var go = Session.Spawn(prefab, Vector3.one, Quaternion.identity, g => g.GetComponent<WorldItem>().SetLook(2, 3));
            var item = go.GetComponent<WorldItem>();
            Assert.AreEqual(2, item.Shape);
            Assert.AreEqual(3, item.Color);
            Session.Despawn(go);
            Assert.IsTrue(go == null);
        }

        [Test]
        public void PlayerHealthAndReviveWorkOnTheServerSide()
        {
            m_Player = Spawn("Player/Player.prefab", Vector3.zero);
            m_Other = Spawn("Player/Player.prefab", new Vector3(1f, 0, 0));
            var me = m_Player.GetComponent<PlayerController>();
            var mate = m_Other.GetComponent<PlayerController>();
            var link = new FakePlayerLink();
            me.Bind(link, 1, isOwner: false, isServer: true);
            mate.Bind(new FakePlayerLink(), 2, isOwner: false, isServer: true);
            Assert.AreSame(mate, PlayerController.Find(2));

            mate.OnGasKnockoutRequest();
            Assert.IsTrue(mate.Downed);
            Assert.IsTrue(((IIntruder)mate).IsDown);
            me.OnReviveRequest(mate);
            Assert.IsFalse(mate.Downed);
            Assert.AreEqual(1, mate.Hp);
        }

        [Test]
        public void ClientsShowWhatTheServerAndTheOwnerSay()
        {
            m_Player = Spawn("Player/Player.prefab", Vector3.zero);
            var p = m_Player.GetComponent<PlayerController>();
            p.Bind(new FakePlayerLink(), 4, isOwner: false, isServer: false);
            p.ApplyServerState(1, false);
            p.ApplyOwnerState(true, -20, "Саша");
            Assert.AreEqual(1, p.Hp);
            Assert.IsTrue(p.IsCrouching);
            Assert.AreEqual(PlayerController.CrouchHeight, p.Height);
            Assert.AreEqual("Саша", p.Nick);
            p.ApplyOwnerState(false, 0, "");
            Assert.AreEqual("Игрок 5", p.Nick, "a default name without Steam");
        }

        [Test]
        public void HackingADeviceIsDecidedOnTheServer()
        {
            m_Player = Spawn("Player/Player.prefab", Vector3.zero);
            m_Plate = Spawn("Sensors/SensorPlate.prefab", new Vector3(1f, 0, 0));
            var me = m_Player.GetComponent<PlayerController>();
            var plate = m_Plate.GetComponent<Detector>();
            me.Bind(new FakePlayerLink(), 1, false, true);
            plate.Spawned(new FakeDetectorLink(), 9, isServer: true);
            me.OnHackRequest(plate);
            Assert.IsTrue(plate.IsHacked);
            Assert.AreEqual(SecurityPalette.Hacked, plate.Status.Current, "goes dark");
        }

        [Test]
        public void ReplicatedDetectorStateDrivesTheLook()
        {
            m_Plate = Spawn("Sensors/SensorPlate.prefab", Vector3.zero);
            var plate = (SecurityDevice)m_Plate.GetComponent<Detector>();
            plate.Spawned(new FakeDetectorLink(), 3, isServer: false);
            var s = DetectorSnapshot.Initial;
            s.Mode = 2;
            s.Awareness = 255;
            s.Focus = 7;
            plate.ApplyReplicated(s);
            Assert.AreEqual(2, plate.Alert);
            Assert.AreEqual(1f, plate.AwarenessOf(7));
            Assert.AreEqual(0f, plate.AwarenessOf(8), "only the focused player sees the indicator");
            Assert.AreEqual(SecurityPalette.Alarm, plate.Status.Current);
            s.Hacked = true;
            plate.ApplyReplicated(s);
            Assert.AreEqual(SecurityPalette.Hacked, plate.Status.Current);
        }

        [Test]
        public void RadioCallsGoThroughTheLink()
        {
            m_Robot = Spawn("Robots/RobotGuard.prefab", Vector3.zero);
            var robot = m_Robot.GetComponent<RobotController>();
            var link = new FakeDetectorLink();
            robot.Spawned(link, 5, isServer: false);
            robot.CallForBackup(Vector3.forward);
            Assert.AreEqual(1, link.Pulses.Count, "every peer is told to show the ripple");
        }

        [Test]
        public void InventoryRequestsGoThroughTheLinkOrApplyDirectly()
        {
            m_Player = Spawn("Player/Player.prefab", Vector3.zero);
            var inv = m_Player.GetComponent<PlayerInventory>();
            Assert.IsTrue(inv.GiveServer(0, 1));
            int version = inv.Version;

            var link = new FakeInventoryLink();
            inv.Bind(link);
            inv.RequestMove(0, 3, 0, 0);
            CollectionAssert.AreEqual(new[] { "move 0 3,0,0" }, link.Requests);
            Assert.AreEqual(version, inv.Version, "nothing changes until the server says so");

            inv.Bind(null);
            inv.RequestMove(0, 3, 0, 0);
            Assert.AreEqual(3, inv.Items[0].X, "offline the request applies at once");
            Assert.Greater(inv.Version, version);

            inv.ApplyReplicated(new[] { new InventoryEntry { Shape = 1, Color = 2 }, new InventoryEntry { Shape = 0, X = 4 } });
            Assert.AreEqual(2, inv.Items.Count);
            Assert.IsTrue(inv.Carries(2));
        }
    }
}
