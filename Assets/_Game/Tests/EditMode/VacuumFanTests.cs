using System.Reflection;
using NUnit.Framework;
using Stealth.Enemies;
using UnityEditor;
using UnityEngine;

namespace Stealth.Tests
{
    /// <summary>The ceiling vacuum's sheet of light, built from the real prefab: each half stops at the wall on its own side.</summary>
    public class VacuumFanTests
    {
        GameObject m_Robot, m_Wall;

        [TearDown]
        public void TearDown()
        {
            if (m_Robot != null) Object.DestroyImmediate(m_Robot);
            if (m_Wall != null) Object.DestroyImmediate(m_Wall);
        }

        static float FarthestAlong(MeshFilter mf, Vector3 origin, Vector3 dir)
        {
            float best = float.MinValue;
            foreach (var v in mf.sharedMesh.vertices) best = Mathf.Max(best, Vector3.Dot(mf.transform.TransformPoint(v) - origin, dir));
            return best;
        }

        readonly System.Collections.Generic.List<GameObject> m_Clutter = new();

        [TearDown]
        public void Clear()
        {
            foreach (var go in m_Clutter) if (go != null) Object.DestroyImmediate(go);
            m_Clutter.Clear();
        }

        [Test]
        public void EachHalfIsCutByTheWallOnItsSide([Values(0f, 37f, 90f, 200f)] float yaw, [Values(false, true)] bool clutterBehind)
        {
            if (clutterBehind)
            {
                // furniture behind the wall (a chair is a dozen colliders): the ray meets more than a small buffer holds
                var d = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                for (int i = 0; i < 16; i++)
                {
                    var c = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    c.transform.position = d * (1.4f + i * 0.1f) + Vector3.up * (1.9f - i * 0.11f);
                    c.transform.localScale = new Vector3(2f, 0.05f, 0.05f);
                    c.transform.rotation = Quaternion.LookRotation(d);
                    m_Clutter.Add(c);
                }
            }
            var dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            m_Robot = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Robots/RobotVacuum.prefab"));
            m_Robot.transform.SetPositionAndRotation(new Vector3(0f, 2.95f, 0f), Quaternion.Euler(180f, 25f, 0f)); // upside down on the ceiling
            var vacuum = m_Robot.GetComponent<CeilingVacuum>();
            typeof(CeilingVacuum).GetField("m_FloorY", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(vacuum, 0f);
            // a wall 1 m out along +dir (the A half); nothing on the −dir side
            m_Wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            m_Wall.transform.position = dir * 1.1f + Vector3.up * 1.5f;
            m_Wall.transform.rotation = Quaternion.LookRotation(dir);
            m_Wall.transform.localScale = new Vector3(6f, 3f, 0.2f);
            Physics.SyncTransforms();
            vacuum.Sweep(dir);
            vacuum.Drift(0.02f); // the server's step after the rays: the body moves and spins under its light
            var apex = vacuum.FanA.position;
            float a = FarthestAlong(vacuum.FanA.GetComponent<MeshFilter>(), apex, dir);
            float b = FarthestAlong(vacuum.FanB.GetComponent<MeshFilter>(), apex, -dir);
            Assert.That(a, Is.LessThanOrEqualTo(1.0f + 0.02f), $"yaw {yaw}: the A half passes the wall on its side ({a:0.00} m)");
            Assert.That(b, Is.GreaterThan(vacuum.Profile.HalfLength - 0.05f), $"yaw {yaw}: the B half is cut though nothing is there ({b:0.00} m)");
        }
    }
}
