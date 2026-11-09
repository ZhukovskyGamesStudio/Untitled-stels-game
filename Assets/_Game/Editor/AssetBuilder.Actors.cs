using System;
using System.Linq;
using Stealth.Enemies;
using Stealth.Items;
using Stealth.Level;
using Stealth.Player;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Stealth.EditorTools
{
    public static partial class AssetBuilder
    {
        const string RobotPrefabs = PrefabsDir + "/Robots";
        const string SensorPrefabs = PrefabsDir + "/Sensors";

        struct Actors
        {
            public GameObject Player, Item, Laser;
            /// <summary>Indexed by RobotKind.</summary>
            public GameObject[] Robots;
        }

        static Actors BuildActors()
        {
            var ripple = Prefab(PrefabsDir + "/FX/RadioRipple.prefab", BuildRadioRipple).GetComponent<RadioRipple>();
            var visuals = DataAsset<ItemVisuals>(DataDir + "/ItemVisuals.asset", v =>
            {
                v.Meshes = ItemCatalog.Shapes.Select(s => AssetDatabase.LoadAllAssetsAtPath(ModelPath($"item_{s.Id}")).OfType<Mesh>().First()).ToArray();
                v.ColorMaterials = Enumerable.Range(0, ItemCatalog.Colors.Length).Select(i => M($"Item_{i}")).ToArray();
            });

            var a = new Actors
            {
                Item = Prefab(PrefabsDir + "/Items/WorldItem.prefab", () => BuildWorldItem(visuals)),
                Player = Prefab(PrefabsDir + "/Player/Player.prefab", BuildPlayer),
                Laser = Prefab(SensorPrefabs + "/LaserGate.prefab", BuildLaser),
                Robots = new GameObject[RobotRoster.KindCount],
            };

            void Robot(RobotKind kind, string path, Func<GameObject> build) => a.Robots[(int)kind] = Prefab(path, () =>
            {
                var go = build();
                go.GetComponent<Detector>().Kind = kind;
                return go;
            });

            Robot(RobotKind.Guard, RobotPrefabs + "/RobotGuard.prefab", () => MobileRobot("RobotGuard", "robot_guard", "RobotBody", 0.4f, new Vector3(0, 1.84f, 0.3f), ripple, (s, t, anim) =>
            {
                anim.Bob = 0.06f;
                s.ViewDistance = 13f; s.ViewAngle = 100f; s.HearingMultiplier = 0.8f;
                t.PatrolSpeed = 1.8f; t.InvestigateSpeed = 2.8f; t.ChaseSpeed = 4.4f;
            }));
            Robot(RobotKind.Listener, RobotPrefabs + "/RobotListener.prefab", () => MobileRobot("RobotListener", "robot_listener", "ListenerBody", 0.55f, new Vector3(0, 1.2f, 0f), ripple, (s, t, anim) =>
            {
                s.HasEyes = false; s.HearingMultiplier = 1.6f; s.ChaseHearingShare = 0.6f; s.ContactDistance = 1.4f;
                t.CanUseStairs = false; // wheels
                t.PatrolSpeed = 1.5f; t.InvestigateSpeed = 3.2f; t.ChaseSpeed = 5f; t.LoseTargetAfter = 3f;
            }));
            Robot(RobotKind.Drone, RobotPrefabs + "/RobotDrone.prefab", () => MobileRobot("RobotDrone", "robot_drone", "DroneBody", 0.45f, new Vector3(0, -0.1f, 0.25f), ripple, (s, t, anim) =>
            {
                anim.Lean = 14f;
                s.ViewDistance = 11f; s.ViewAngle = 120f; s.EyePitch = 35f; s.HearingMultiplier = 0.5f; // looks down-forward
                t.HoverHeight = 2.1f; t.AlarmOnDetect = true;
                t.PatrolSpeed = 2.6f; t.InvestigateSpeed = 3.6f; t.ChaseSpeed = 5.2f; t.AttackRange = 1.4f;
            }));
            Robot(RobotKind.Thumper, RobotPrefabs + "/RobotThumper.prefab", () => MobileRobot("RobotThumper", "robot_thumper", "ThumperBody", 0.7f, new Vector3(0, 0.3f, 0.8f), ripple, (s, t, anim) =>
            {
                s.HasEyes = false; s.HearingMultiplier = 0f; // deaf; it listens with its feet
                s.SeismicRange = 10f; s.SeismicSpeed = 1.1f; s.ContactDistance = 1.6f;
                t.CanUseStairs = false; // tracks
                t.PatrolSpeed = 1.2f; t.InvestigateSpeed = 2.2f; t.ChaseSpeed = 3.3f; t.AttackRange = 1.9f;
            }));
            Robot(RobotKind.Hound, RobotPrefabs + "/RobotHound.prefab", () => MobileRobot("RobotHound", "robot_hound", "HoundBody", 0.4f, new Vector3(0, 0.9f, 0.9f), ripple, (s, t, anim) =>
            {
                s.HasEyes = false; s.HearingMultiplier = 0.6f; s.SmellRange = 3f; s.SmellSpeed = 1.6f;
                t.PatrolSpeed = 2.2f; t.InvestigateSpeed = 3.4f; t.ChaseSpeed = 5.4f; t.LoseTargetAfter = 6f;
            }));
            Robot(RobotKind.Sorter, RobotPrefabs + "/RobotSorter.prefab", () => MobileRobot("RobotSorter", "robot_sorter", "SorterBody", 0.35f, new Vector3(0, 1.25f, 0.3f), ripple, (s, t, anim) =>
            {
                s.ColorSorter = true; s.ViewDistance = 16f; s.ViewAngle = 160f; s.DetectionSpeed = 2.2f; s.HearingMultiplier = 0.3f;
                t.PatrolSpeed = 1.7f; t.InvestigateSpeed = 2.6f; t.ChaseSpeed = 4.6f;
            }));
            Robot(RobotKind.Worker, RobotPrefabs + "/RobotWorker.prefab", () => MobileRobot("RobotWorker", "robot_worker", "WorkerBody", 0.45f, new Vector3(0, 0.5f, 0.4f), ripple, (s, t, anim) =>
            {
                s.ViewDistance = 10f; s.ViewAngle = 90f; s.DetectionSpeed = 1.2f; s.HearingMultiplier = 0.5f; s.ContactDistance = 0.9f;
                t.CanAttack = false;    // harmless, but a snitch
                t.CanUseStairs = false; // wheels
                t.PatrolSpeed = 1.6f; t.InvestigateSpeed = 1.8f; t.ChaseSpeed = 1.8f;
            }));
            Robot(RobotKind.Hunter, RobotPrefabs + "/RobotHunter.prefab", () => MobileRobot("RobotHunter", "robot_hunter", "HunterBody", 0.45f, new Vector3(0, 1.9f, 0.3f), ripple, (s, t, anim) =>
            {
                s.ViewDistance = 16f; s.ViewAngle = 120f; s.DetectionSpeed = 2f; // everything at once
                s.HearingMultiplier = 1.2f; s.ChaseHearingShare = 0.4f; s.SeismicRange = 6f; s.SmellRange = 3f;
                t.PatrolSpeed = 2.8f; t.InvestigateSpeed = 4.5f; t.ChaseSpeed = 5.8f; t.LoseTargetAfter = 7f; t.AttackCooldown = 1.3f;
            }));

            Robot(RobotKind.Turret, RobotPrefabs + "/RobotTurret.prefab", () => Device<SentryTurret>("RobotTurret", "robot_turret", "TurretBody", false, false, (root, t) =>
            {
                t.Sight = Beam(root, "Sight", 0.025f);
                t.Head = Find(root, "AccentHead" + RobotAnimator.PivotSuffix);
                t.Point.localPosition = new Vector3(0, 1.35f, 0);
            }));
            Robot(RobotKind.Spider, RobotPrefabs + "/RobotSpider.prefab", () => Device<SpiderBot>("RobotSpider", "robot_spider", "SpiderBody", true, true, (root, s) => { }));
            Robot(RobotKind.Vacuum, RobotPrefabs + "/RobotVacuum.prefab", () => Device<CeilingVacuum>("RobotVacuum", "robot_vacuum", "VacuumBody", true, true, (root, v) =>
            {
                v.Line = Beam(root, "ScanLine", 0.03f);
            }));
            Robot(RobotKind.Lidar, SensorPrefabs + "/SensorLidar.prefab", () => Device<LidarScanner>("SensorLidar", "sensor_lidar", "TurretBody", false, false, (root, l) =>
            {
                l.Line = Beam(root, "LidarBeam", 0.02f);
                l.Head = Find(root, "AccentHead" + RobotAnimator.PivotSuffix);
            }));
            Robot(RobotKind.MotionSensor, SensorPrefabs + "/SensorMotion.prefab", () => Device<MotionSensor>("SensorMotion", "sensor_pir", "RobotBody", false, false, (root, m) =>
            {
                Object.DestroyImmediate(root.GetComponent<BoxCollider>()); // on the wall, nothing to bump into
            }));
            Robot(RobotKind.PressurePlate, SensorPrefabs + "/SensorPlate.prefab", () => Device<PressurePlate>("SensorPlate", "sensor_plate", "Floor", false, false, (root, p) =>
            {
                Object.DestroyImmediate(root.GetComponent<BoxCollider>()); // flush with the floor
                p.Status.Light.range = 1.2f;
                p.Status.Light.intensity = 0.6f;
            }));
            Robot(RobotKind.Camera, SensorPrefabs + "/SecurityCamera.prefab", BuildSecurityCamera);
            return a;
        }

        // ------------------------------------------------------------------ player and items

        static GameObject BuildWorldItem(ItemVisuals visuals)
        {
            var root = new GameObject("WorldItem");
            root.AddComponent<NetworkObject>();
            var mf = root.AddComponent<MeshFilter>();
            var mr = root.AddComponent<MeshRenderer>();
            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            mf.sharedMesh = visuals.Meshes[0];
            mr.sharedMaterial = visuals.ColorMaterials[0];
            var item = root.AddComponent<WorldItem>();
            item.Visuals = visuals;
            item.MeshFilter = mf;
            item.Renderer = mr;
            item.Collider = col;
            return root;
        }

        static GameObject BuildPlayer()
        {
            var root = new GameObject("Player");
            var cc = root.AddComponent<CharacterController>();
            cc.height = PlayerController.StandHeight;
            cc.radius = 0.35f;
            cc.center = new Vector3(0, PlayerController.StandHeight / 2f, 0);
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.04f;
            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;

            var model = Model("player");
            model.transform.SetParent(root.transform, false);
            Paint(model, "PlayerBody", "PlayerAccent", "PlayerGlasses");

            // Head pivot at the neck: teammates see where this player is looking.
            var headPivot = Child(model.transform, "HeadPivot", new Vector3(0, 1.3f, 0));
            foreach (var part in model.GetComponentsInChildren<Transform>(true).Where(t => t.name == "AccentHead" || t.name == "EyeGlasses").ToArray())
                part.SetParent(headPivot.transform, true);

            var head = Child(root.transform, "Head", new Vector3(0, 1.6f, 0));
            var cam = head.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 75f;
            cam.enabled = false;
            var listener = head.AddComponent<AudioListener>();
            listener.enabled = false;

            var motor = root.AddComponent<PlayerMotor>();
            root.AddComponent<PlayerInventory>();
            var pc = root.AddComponent<PlayerController>();
            pc.Motor = motor;
            pc.Head = head.transform;
            pc.Camera = cam;
            pc.Listener = listener;
            pc.Model = model.transform;
            pc.HeadPivot = headPivot.transform;
            pc.BodyRenderers = model.GetComponentsInChildren<Renderer>(true);
            return root;
        }

        // ------------------------------------------------------------------ robots

        static GameObject NetworkedRoot(string name, bool moves, bool fullRotation)
        {
            var root = new GameObject(name);
            root.AddComponent<NetworkObject>();
            if (!moves) return root;
            var nt = root.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Server;
            nt.SyncRotAngleX = fullRotation;
            nt.SyncRotAngleZ = fullRotation;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            return root;
        }

        static Light StatusLight(Transform parent, Vector3 position, float range, float intensity)
        {
            var light = Child(parent, "StatusLight", position).AddComponent<Light>();
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        /// <summary>Navmesh robot: NetworkObject + server NetworkTransform + NavMeshAgent + RobotController + animator.</summary>
        static GameObject MobileRobot(string name, string file, string bodyMat, float radius, Vector3 eyePos, RadioRipple ripple,
            Action<RobotSenses, RobotTraits, RobotAnimator> config)
        {
            var root = NetworkedRoot(name, true, false);
            var model = Model(file);
            model.transform.SetParent(root.transform, false);
            Paint(model, bodyMat, "RobotAccent", "SensorGlow");
            var b = LocalBounds(root);
            RobotAnimator.BakePivots(model.transform);

            var col = root.AddComponent<CapsuleCollider>();
            col.height = Mathf.Max(b.size.y, radius * 2f);
            col.radius = radius;
            col.center = new Vector3(0, b.center.y, 0);

            var agent = root.AddComponent<NavMeshAgent>();
            agent.enabled = false;
            agent.radius = radius;
            agent.height = Mathf.Max(0.5f, b.size.y);
            agent.angularSpeed = 360f;
            agent.acceleration = 14f;
            agent.stoppingDistance = 0.4f;
            agent.autoBraking = true;

            var eye = Child(root.transform, "EyePoint", eyePos);
            var anim = root.AddComponent<RobotAnimator>();
            anim.Model = model.transform;
            var rc = root.AddComponent<RobotController>();
            rc.Point = eye.transform;
            rc.Eye = eye.transform;
            rc.Status.Renderers = Parts(model, "Eye");
            rc.Status.Light = StatusLight(root.transform, eyePos + new Vector3(0, 0.05f, 0.15f), 3.5f, 2.5f);
            rc.RadioRipplePrefab = ripple;
            config(rc.Senses, rc.Traits, anim);
            return root;
        }

        /// <summary>Robot / device with its own movement script (<see cref="SecurityDevice"/>).</summary>
        static GameObject Device<T>(string name, string file, string bodyMat, bool moves, bool fullRotation, Action<GameObject, T> config) where T : SecurityDevice
        {
            var root = NetworkedRoot(name, moves, fullRotation);
            var model = Model(file);
            model.transform.SetParent(root.transform, false);
            Paint(model, bodyMat, "RobotAccent", "SensorGlow");
            var b = LocalBounds(root);
            RobotAnimator.BakePivots(model.transform);
            var col = root.AddComponent<BoxCollider>();
            col.center = b.center;
            col.size = b.size;

            var eye = Child(root.transform, "EyePoint", b.center);
            root.AddComponent<RobotAnimator>().Model = model.transform;
            var device = root.AddComponent<T>();
            device.Point = eye.transform;
            device.Status.Renderers = Parts(model, "Eye");
            device.Status.Light = StatusLight(root.transform, b.center + Vector3.up * (b.extents.y + 0.1f), 3f, 2f);
            config(root, device);
            return root;
        }

        static LineRenderer Beam(GameObject root, string name, float width)
        {
            var lr = Child(root.transform, name).AddComponent<LineRenderer>();
            lr.sharedMaterial = M("Laser");
            lr.widthMultiplier = width;
            lr.positionCount = 2;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.enabled = false;
            return lr;
        }

        static GameObject BuildSecurityCamera()
        {
            var root = NetworkedRoot("SecurityCamera", false, false);
            var pivot = Child(root.transform, "Pivot");
            var model = Model("security_camera");
            model.transform.SetParent(pivot.transform, false);
            Paint(model, "RobotAccent", "RobotBody", "SensorGlow");

            var spotGo = Child(pivot.transform, "Spot", new Vector3(0, -0.02f, 0.25f));
            var spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.shadows = LightShadows.None;

            var sc = root.AddComponent<SecurityCamera>();
            sc.Point = spotGo.transform;
            sc.Pivot = pivot.transform;
            sc.Spot = spot;
            sc.AlarmRadius = 35f;
            sc.Status.Renderers = Parts(model, "Eye");
            sc.Status.Emission = 3f;
            return root;
        }

        static GameObject BuildLaser()
        {
            var root = NetworkedRoot("LaserGate", false, false);
            Transform Emitter(string n, float yaw)
            {
                var holder = Child(root.transform, n);
                var model = Model("laser_emitter");
                model.transform.SetParent(holder.transform, false);
                model.transform.localRotation = Quaternion.Euler(0, yaw, 0);
                Paint(model, "LaserPost", "LaserPost", "Laser");
                return holder.transform;
            }
            var gate = root.AddComponent<LaserGate>();
            gate.EmitterA = Emitter("EmitterA", 90f);
            gate.EmitterB = Emitter("EmitterB", -90f);
            gate.Beams = Enumerable.Range(0, 3)
                .Select(i => Cube(root.transform, $"Beam{i}", new Vector3(0, 1f, 0), new Vector3(1.8f, LaserGate.BeamThickness, LaserGate.BeamThickness), M("Laser"), false).transform)
                .ToArray();
            return root;
        }

        static GameObject BuildRadioRipple()
        {
            var go = new GameObject("RadioRipple");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = M("Radio");
            lr.loop = true;
            lr.positionCount = 32;
            lr.widthMultiplier = 0.04f;
            lr.useWorldSpace = false;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<RadioRipple>();
            return go;
        }
    }
}
