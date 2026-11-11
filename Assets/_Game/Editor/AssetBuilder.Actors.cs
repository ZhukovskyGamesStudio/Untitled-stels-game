using System;
using System.Linq;
using Stealth.Enemies;
using Stealth.Items;
using Stealth.Level;
using Stealth.Net;
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
            s_StompRipple = Prefab(PrefabsDir + "/FX/StompRipple.prefab", BuildStompRipple).GetComponent<RadioRipple>();
            var visuals = DataAsset<ItemVisuals>(DataDir + "/ItemVisuals.asset", v =>
            {
                v.Meshes = ItemCatalog.Shapes.Select(s => AssetDatabase.LoadAllAssetsAtPath(ModelPath($"item_{s.Id}")).OfType<Mesh>().First()).ToArray();
            });
            // One material per configured colour (ItemsConfig can grow).
            visuals.ColorMaterials = Enumerable.Range(0, ItemCatalog.Colors.Length).Select(i => M($"Item_{i}")).ToArray();
            EditorUtility.SetDirty(visuals);

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
                s.ViewDistance = 13f; s.ViewAngle = 100f; // deaf: the first houses are full of guards
                t.PatrolSpeed = 1.8f; t.InvestigateSpeed = 2.8f; t.ChaseSpeed = 4.4f;
            }));
            Robot(RobotKind.Listener, RobotPrefabs + "/RobotListener.prefab", () => MobileRobot("RobotListener", "robot_listener", "ListenerBody", 0.55f, new Vector3(0, 1.2f, 0f), ripple, (s, t, anim) =>
            {
                s.HasEyes = false; s.HearingMultiplier = 1.6f; s.ContactDistance = 1.4f;
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
                s.SeismicRange = 10f; s.SeismicSpeed = 0.8f; s.ContactDistance = 1.6f;
                t.CanUseStairs = false; // tracks
                t.PatrolSpeed = 1.2f; t.InvestigateSpeed = 2.2f; t.ChaseSpeed = 3.3f; t.AttackRange = 1.9f;
                t.SuspicionDecay = 0.12f; // waves come seconds apart: it keeps what it felt between them
            }));
            Robot(RobotKind.Hound, RobotPrefabs + "/RobotHound.prefab", () => MobileRobot("RobotHound", "robot_hound", "HoundBody", 0.4f, new Vector3(0, 0.9f, 0.9f), ripple, (s, t, anim) =>
            {
                s.HasEyes = false; s.HearingMultiplier = 0.6f; s.SmellRange = 3f; s.SmellSpeed = 1.6f;
                t.PatrolSpeed = 2.2f; t.InvestigateSpeed = 3.4f; t.ChaseSpeed = 5.4f; t.LoseTargetAfter = 6f;
            }));
            Robot(RobotKind.Sorter, RobotPrefabs + "/RobotSorter.prefab", () => MobileRobot("RobotSorter", "robot_sorter", "SorterBody", 0.35f, new Vector3(0, 1.25f, 0.3f), ripple, (s, t, anim) =>
            {
                s.ColorSorter = true; s.ViewDistance = 16f; s.ViewAngle = 160f; s.DetectionSpeed = 0.9f; s.HearingMultiplier = 0.3f;
                t.PatrolSpeed = 1.7f; t.InvestigateSpeed = 2.6f; t.ChaseSpeed = 4.6f;
            }));
            Robot(RobotKind.Worker, RobotPrefabs + "/RobotWorker.prefab", () =>
            {
                var go = MobileRobot("RobotWorker", "robot_worker", "WorkerBody", 0.45f, new Vector3(0, 0.5f, 0.4f), ripple, (s, t, anim) =>
                {
                    s.ViewDistance = 10f; s.ViewAngle = 90f; s.DetectionSpeed = 0.5f; s.HearingMultiplier = 0.5f; s.ContactDistance = 0.9f;
                    t.CanAttack = false;    // harmless, but a snitch
                    t.AlarmRadius = 40f;
                    t.CanUseStairs = false; // wheels
                    t.PatrolSpeed = 1.6f; t.InvestigateSpeed = 1.8f; t.ChaseSpeed = 1.8f;
                    t.CargoChance = 0.6f;   // carries loot on its tray: hack it and it drops it
                });
                // The tray on top of the box it carries (model: box at 1.15 m, 0.4 high).
                var rc = go.GetComponent<RobotController>();
                var cargo = Child(go.transform, "Cargo", new Vector3(0f, 1.36f, 0.15f));
                rc.CargoMesh = cargo.AddComponent<MeshFilter>();
                rc.CargoRenderer = cargo.AddComponent<MeshRenderer>();
                rc.CargoRenderer.enabled = false;
                rc.CargoVisuals = visuals;
                return go;
            });
            Robot(RobotKind.Sweeper, RobotPrefabs + "/RobotSweeper.prefab", () =>
            {
                var go = MobileRobot("RobotSweeper", "robot_sweeper", "SweeperBody", 0.4f, new Vector3(0, 0.5f, 0.35f), ripple, (s, t, anim) =>
                {
                    s.HasEyes = false; s.HearingMultiplier = 0f; s.ContactDistance = 0.9f; // blind and deaf: only the floor lidar
                    s.LidarRange = 6f; s.LidarHeight = 0.22f; s.LidarTurnSpeed = 140f; s.LidarRate = 15f;
                    t.CanUseStairs = false; // wheels
                    t.PatrolSpeed = 1.4f; t.InvestigateSpeed = 2.2f; t.ChaseSpeed = 3.4f; t.AttackRange = 1.3f;
                });
                var rc = go.GetComponent<RobotController>();
                var lidar = go.AddComponent<RobotLidar>();
                lidar.Robot = rc;
                lidar.Head = Find(go, "AccentLidar");
                lidar.Line = Beam(go, "LidarBeam", 0.025f);
                rc.Lidar = lidar;
                return go;
            });
            Robot(RobotKind.Hunter, RobotPrefabs + "/RobotHunter.prefab", () => MobileRobot("RobotHunter", "robot_hunter", "HunterBody", 0.45f, new Vector3(0, 1.9f, 0.3f), ripple, (s, t, anim) =>
            {
                s.ViewDistance = 16f; s.ViewAngle = 120f; s.DetectionSpeed = 1f; // everything at once
                s.HearingMultiplier = 1.2f; s.SeismicRange = 6f; s.SmellRange = 3f;
                t.PatrolSpeed = 2.8f; t.InvestigateSpeed = 4.5f; t.ChaseSpeed = 5.8f; t.LoseTargetAfter = 7f; t.AttackCooldown = 1.3f; t.WarningTime = 0.9f;
            }));

            Robot(RobotKind.Turret, RobotPrefabs + "/RobotTurret.prefab", () => Device<SentryTurret>("RobotTurret", "robot_turret", "TurretBody", false, false, (root, t) =>
            {
                t.Profile = Profile<TurretProfile>("RobotTurret", p => p.ForgetRate = 0.6f);
                t.Sight = Beam(root, "Sight", 0.025f);
                t.Head = Find(root, "AccentHead" + RobotAnimator.PivotSuffix);
                t.Point.localPosition = new Vector3(0, 1.35f, 0);
            }));
            Robot(RobotKind.Spider, RobotPrefabs + "/RobotSpider.prefab", () => Device<SpiderBot>("RobotSpider", "robot_spider", "SpiderBody", true, true, (root, s) =>
            {
                s.Profile = Profile<SpiderProfile>("RobotSpider");
            }));
            Robot(RobotKind.Vacuum, RobotPrefabs + "/RobotVacuum.prefab", () => Device<CeilingVacuum>("RobotVacuum", "robot_vacuum", "VacuumBody", true, true, (root, v) =>
            {
                v.Profile = Profile<VacuumProfile>("RobotVacuum", p => p.ForgetRate = 0.6f);
                v.Line = Beam(root, "ScanLine", 0.03f);
            }));
            Robot(RobotKind.Lidar, SensorPrefabs + "/SensorLidar.prefab", () => Device<LidarScanner>("SensorLidar", "sensor_lidar", "TurretBody", false, false, (root, l) =>
            {
                l.Profile = Profile<LidarProfile>("SensorLidar", p => p.ForgetRate = 0.6f);
                l.Line = Beam(root, "LidarBeam", 0.02f);
                l.Head = Find(root, "AccentHead" + RobotAnimator.PivotSuffix);
            }));
            Robot(RobotKind.MotionSensor, SensorPrefabs + "/SensorMotion.prefab", () => Device<MotionSensor>("SensorMotion", "sensor_pir", "RobotBody", false, false, (root, m) =>
            {
                m.Profile = Profile<MotionSensorProfile>("SensorMotion");
                Object.DestroyImmediate(root.GetComponent<BoxCollider>()); // on the wall, nothing to bump into
            }));
            Robot(RobotKind.PressurePlate, SensorPrefabs + "/SensorPlate.prefab", () => Device<PressurePlate>("SensorPlate", "sensor_plate", "PlateSeam", false, false, (root, p) =>
            {
                p.Profile = Profile<PressurePlateProfile>("SensorPlate", c => { c.ForgetRate = 0.4f; c.HalfSize = 0.55f; });
                Object.DestroyImmediate(root.GetComponent<BoxCollider>()); // flush with the floor
                // Only a faint dot: the indicator barely glows, nothing lights the floor around it.
                p.Status.Light.enabled = false; // no light pooling on the floor around it
                p.Status.Emission = 0.45f; // below 1: no bloom halo, just a dim dot
                foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
            root.AddComponent<WorldItemNet>();
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
            ConfigureCamera(cam);
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 75f;
            cam.enabled = false;
            var listener = head.AddComponent<AudioListener>();
            listener.enabled = false;

            var motor = root.AddComponent<PlayerMotor>();
            root.AddComponent<PlayerInventory>();
            var pc = root.AddComponent<PlayerController>();
            root.AddComponent<PlayerNet>();
            root.AddComponent<InventoryNet>();
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

        /// <summary>
        /// Status light of a robot / device: a small spot with shadows (pitched <paramref name="pitch"/>° down from the robot's
        /// forward) — walls and slabs keep it in the room, it never glows through a floor.
        /// </summary>
        static Light StatusLight(Transform parent, Vector3 position, float range, float intensity, float pitch)
        {
            var go = Child(parent, "StatusLight", position);
            go.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 130f;
            light.innerSpotAngle = 60f;
            light.range = range;
            light.intensity = intensity;
            ShadowedLight(light, UnityEngine.Rendering.Universal.UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierLow);
            return light;
        }

        /// <summary>Legs cut at the knee (AccentLeg{n}Upper / Lower) walk by inverse kinematics.</summary>
        static void WalkingLegs(GameObject root, GameObject model)
        {
            var legs = root.AddComponent<ProceduralLegs>();
            if (!ProceduralLegs.Bake(root.transform, model.transform, legs)) Object.DestroyImmediate(legs);
        }

        /// <summary>Navmesh robot: NetworkObject + server NetworkTransform + NavMeshAgent + RobotController + animator.</summary>
        static GameObject MobileRobot(string name, string file, string bodyMat, float radius, Vector3 eyePos, RadioRipple ripple,
            Action<RobotSenses, RobotTraits, AnimationSettings> config)
        {
            var root = NetworkedRoot(name, true, false);
            var model = Model(file);
            model.transform.SetParent(root.transform, false);
            Paint(model, bodyMat, "RobotAccent", "SensorGlow");
            var b = LocalBounds(root);
            RobotAnimator.BakePivots(model.transform);
            WalkingLegs(root, model);

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
            root.AddComponent<DetectorNet>();
            rc.Point = eye.transform;
            rc.Eye = eye.transform;
            rc.Status.Renderers = Parts(model, "Eye");
            rc.Status.Light = StatusLight(root.transform, eyePos + new Vector3(0, 0.05f, 0.15f), 3.5f, 2.5f, 35f);
            rc.RadioRipplePrefab = ripple;
            rc.StompRipplePrefab = s_StompRipple;
            // All the numbers live in a profile next to the prefab (tuned by hand, never overwritten).
            rc.Profile = Profile<RobotProfile>(name, p => config(p.Senses, p.Traits, p.Animation));
            BuildSensorRig(root, model, rc, eyePos, radius);
            AddLoop(root, rc.Profile.Traits.HoverHeight > 0f ? s_Bank.DroneRotor : s_Bank.RobotMotor);
            rc.Status.Renderers = Parts(model, "Eye"); // the sensor lenses glow in the status colour too
            return root;
        }

        /// <summary>
        /// Every kind of sensor mount on the model (camera eye, two microphones, two ground probes, a snout);
        /// <see cref="SensorRig"/> keeps only those the robot's senses use.
        /// </summary>
        static void BuildSensorRig(GameObject root, GameObject model, RobotController rc, Vector3 eye, float radius)
        {
            var rig = root.AddComponent<SensorRig>();
            rig.Robot = rc;
            float scale = Mathf.Clamp(radius / 0.45f, 0.85f, 1.4f), side = Mathf.Max(0.2f, radius * 0.75f);
            GameObject Mount(string file, string name, Vector3 position, Quaternion rotation)
            {
                var go = Model(file);
                go.name = name;
                go.transform.SetParent(model.transform, false);
                go.transform.localPosition = position;
                go.transform.localRotation = rotation;
                go.transform.localScale = Vector3.one * scale;
                Paint(go, "RobotAccent", "RobotAccent", "SensorGlow");
                return go;
            }
            rig.Eyes = new[] { Mount("attach_eye", "SensorEye", eye + new Vector3(0, 0, 0.08f), Quaternion.Euler(rc.Profile.Senses.EyePitch, 0, 0)) };
            rig.Microphones = new[]
            {
                Mount("attach_mic", "SensorMicL", eye + new Vector3(-side, 0.05f, -0.1f), Quaternion.Euler(0, 0, 15f)),
                Mount("attach_mic", "SensorMicR", eye + new Vector3(side, 0.05f, -0.1f), Quaternion.Euler(0, 0, -15f)),
            };
            rig.GroundProbes = new[]
            {
                Mount("attach_seismic", "SensorGroundL", new Vector3(-side, 0, radius + 0.1f), Quaternion.identity),
                Mount("attach_seismic", "SensorGroundR", new Vector3(side, 0, radius + 0.1f), Quaternion.identity),
            };
            rig.Noses = new[] { Mount("attach_nose", "SensorNose", eye + new Vector3(0, -0.15f, 0.12f), Quaternion.identity) };
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
            WalkingLegs(root, model);
            var col = root.AddComponent<BoxCollider>();
            col.center = b.center;
            col.size = b.size;

            var eye = Child(root.transform, "EyePoint", b.center);
            root.AddComponent<RobotAnimator>().Model = model.transform;
            var device = root.AddComponent<T>();
            root.AddComponent<DetectorNet>();
            device.Point = eye.transform;
            device.Status.Renderers = Parts(model, "Eye");
            device.Status.Light = StatusLight(root.transform, b.center + Vector3.up * (b.extents.y + 0.1f), 3f, 2f, 90f);
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
            var model = Model("security_camera");
            model.transform.SetParent(root.transform, false);
            Paint(model, "RobotAccent", "RobotBody", "SensorGlow");
            // Only the head turns: the wall plate stays, the arm and housing swivel at the bracket.
            var pivot = Child(root.transform, "Pivot", new Vector3(0f, 0f, -0.24f));
            foreach (var part in model.GetComponentsInChildren<MeshRenderer>(true).Select(r => r.transform).ToArray())
                if (part.name.StartsWith("Accent") || part.name.StartsWith("Eye")) part.SetParent(pivot.transform, true);

            var spotGo = Child(pivot.transform, "Spot", new Vector3(0, -0.02f, 0.49f));
            var spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            ShadowedLight(spot, UnityEngine.Rendering.Universal.UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium);

            var sc = root.AddComponent<SecurityCamera>();
            root.AddComponent<DetectorNet>();
            sc.Point = spotGo.transform;
            sc.Pivot = pivot.transform;
            sc.Spot = spot;
            sc.Profile = Profile<CameraProfile>("SecurityCamera", p => p.AlarmRadius = 35f);
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
            gate.Profile = Profile<LaserGateProfile>("LaserGate");
            root.AddComponent<LaserGateNet>();
            AddLoop(root, s_Bank.LaserHum);
            gate.EmitterA = Emitter("EmitterA", 90f);
            gate.EmitterB = Emitter("EmitterB", -90f);
            gate.Beams = Enumerable.Range(0, 3)
                .Select(i => Cube(root.transform, $"Beam{i}", new Vector3(0, 1f, 0), new Vector3(1.8f, LaserGate.BeamThickness, LaserGate.BeamThickness), M("Laser"), false).transform)
                .ToArray();
            return root;
        }

        static RadioRipple s_StompRipple;

        /// <summary>The floor wave of a seismic stomp: a wide ring running out along the floor (radius set per robot).</summary>
        static GameObject BuildStompRipple()
        {
            var go = BuildRadioRipple();
            go.name = "StompRipple";
            var lr = go.GetComponent<LineRenderer>();
            lr.sharedMaterial = M("Stomp");
            lr.positionCount = 64;
            var r = go.GetComponent<RadioRipple>();
            r.Duration = 1.1f;
            r.StartRadius = 0.6f;
            r.EndRadius = 10f;
            r.Height = 0.04f;
            r.Width = 0.14f;
            return go;
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
