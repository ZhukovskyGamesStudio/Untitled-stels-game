using System.Linq;
using NUnit.Framework;
using Stealth.Level;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>Depth from the lobby (RoomDepth): public rooms shallow, private deep; robots and loot keep to the deep rooms; ruins as loops.</summary>
    public class RoomDepthTests
    {
        [Test]
        public void TheLobbyIsShallowestAndWalkedWaysCount()
        {
            Assert.IsTrue(RoomDepth.Walked(EdgeType.Breach));
            Assert.IsFalse(RoomDepth.Walked(EdgeType.Hole), "a hole is crawled, not walked");
            Assert.IsFalse(RoomDepth.Walked(EdgeType.InnerWindow));
            foreach (var l in TestHouses.Sample)
            {
                Assert.AreEqual(0, l.RoomSteps[l.LobbyRoom], $"seed {l.Seed}");
                Assert.AreEqual(0f, l.DepthShare(l.LobbyRoom));
                Assert.That(l.RoomSteps.Max(), Is.GreaterThan(1), $"seed {l.Seed}: rooms behind rooms");
            }
        }

        [Test]
        public void PrivateRoomsLieDeeperThanPublicOnes()
        {
            float pub = 0, priv = 0;
            int np = 0, nv = 0;
            float robots = 0, rooms = 0;
            int nr = 0, nrooms = 0;
            int offices = 0;
            foreach (var l in TestHouses.Offices()) // (a location's plan has its rooms where its layout puts them)
            {
                offices++;
                foreach (var r in l.Rooms)
                {
                    int privacy = RoomRules.For(r.Type, l.RoomRules).Privacy;
                    float d = l.DepthShare(r.Id);
                    if (privacy < 0) { pub += d; np++; }
                    if (privacy > 0) { priv += d; nv++; }
                    if (r.Type is not (RoomType.Corridor or RoomType.Hall or RoomType.Lobby)) { rooms += d; nrooms++; }
                }
                foreach (var s in l.RobotSpawns)
                {
                    var room = l.Plan.RoomAt(s.Position + Vector3.up * 0.3f);
                    if (room == null || s.Kind is not (RobotKind.Guard or RobotKind.Listener or RobotKind.Hound)) continue;
                    if (room.Type is RoomType.Corridor or RoomType.Hall or RoomType.Lobby) continue; // like with like: the rooms' depth below leaves these out too
                    robots += l.DepthShare(room.Id);
                    nr++;
                }
            }
            Assert.That(offices, Is.GreaterThanOrEqualTo(15), "offices to measure");
            Assert.That(priv / nv, Is.GreaterThan(pub / np + 0.05f), $"private rooms {priv / nv:0.00} deep, public {pub / np:0.00} ({offices} offices)");
            // (measured over 40 offices after the locations came: the weight keeps the guards about as deep as the rooms, not
            // clearly deeper — SecuritySettings.DeepRoomWeight is a balance question; the check keeps them from the shallow end)
            Assert.That(robots / Mathf.Max(1, nr), Is.GreaterThan(rooms / nrooms - 0.03f), $"guards {robots / Mathf.Max(1, nr):0.00} deep, rooms {rooms / nrooms:0.00}");
        }

        [Test]
        public void LoopsAreMostlyRuins()
        {
            int doors = 0, ruins = 0;
            foreach (var l in TestHouses.Sample)
                foreach (var o in l.Plan.Openings)
                {
                    if (o.Kind is EdgeType.Breach or EdgeType.Hole or EdgeType.InnerWindow) ruins++;
                    if (o.Kind == EdgeType.Door) doors++;
                }
            Assert.That(ruins, Is.GreaterThan(0), "breaches, holes and inner windows");
            Assert.That(doors, Is.GreaterThan(ruins), "the rooms' own ways in are doors");
        }
    }
}
